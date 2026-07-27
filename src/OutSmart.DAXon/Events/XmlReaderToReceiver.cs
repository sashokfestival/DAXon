////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Jaxp.Transform;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace OutSmart.DAXon.Events
{
    /// <summary>
    /// Pumps a <see cref="System.Xml.XmlReader"/> (a .NET pull parser) directly into a Saxon
    /// <see cref="IReceiver"/>, building the tree without the intermediate SAX round-trip
    /// (DotNetXmlReader -&gt; ReceivingContentHandler). It reproduces the Receiver-event semantics of
    /// <see cref="ReceivingContentHandler"/>: immutable namespace-map maintenance, attribute-map
    /// construction (all attributes untyped, matching a non-validating parser), name caching, character
    /// buffering with <c>StringTool.Compress</c>, and the <see cref="ISourceLocator"/>/<c>levelInEntity</c>
    /// contract the tree builders (TinyBuilder / LinkedTreeBuilder) rely on for base-URI and entity tracking.
    /// Unlike the SAX path it carries no SAX types: its locator reads the XmlReader directly.
    /// </summary>
    public class XmlReaderToReceiver
    {

        // The XML declaration is ASCII-compatible in every XML-supported encoding, so peeking the first bytes
        // as Latin-1 (a lossless byte↔char map) safely finds version="1.1" regardless of the real encoding.
        private const int XmlDeclPeekBytes = 256;
        private readonly IReceiver receiver;
        private readonly PipelineConfiguration pipe;
        private readonly XmlReader reader;

        private XmlPullLocation localLocator;
        private readonly Stack<string> entityBaseStack = new Stack<string>();   // reader.BaseURI per open element
        private ILocation lastTextNodeLocator;
        private readonly bool lineNumbering;
        private readonly bool allowDisableOutputEscaping;
        private bool escapingDisabled;

        // DTD-declared attribute types: key "elementQName\tattrQName" -> ID/IDREF/IDREFS/NMTOKEN/NMTOKENS/
        // ENTITY/ENTITIES. .NET's XmlReader reports every attribute as CDATA even with DtdProcessing.Parse,
        // so ID typing (needed by fn:id/fn:idref) is recovered by parsing the DOCTYPE internal subset's
        // ATTLIST declarations. Null until a DOCTYPE with an internal subset is seen.
        private Dictionary<string, string> dtdAttTypes;

        // Buffer accumulating character data until the next markup event, mirroring ReceivingContentHandler.
        private char[] buffer = new char[512];
        private int charsUsed;

        // false once an end tag has been seen; controls whitespace-compression of the next text node.
        private bool afterStartTag = true;

        // Element-nesting depth (0 in the prolog/epilog). XmlReader reports boundary whitespace that a
        // Java SAX parser never would, so it is suppressed at depth 0.
        private int elementDepth;

        // Stack of in-scope namespace maps; the bottom entry is the empty document-level map.
        private readonly Stack<NamespaceMap> namespaceStack = new Stack<NamespaceMap>();

        // Name cache mirroring ReceivingContentHandler.GetNodeName: uri -> (lexical-QName -> INodeName).
        private readonly Dictionary<string, Dictionary<string, INodeName>> nameCache =
            new Dictionary<string, Dictionary<string, INodeName>>(10);
        private readonly Dictionary<string, INodeName> noNamespaceNameCache = new Dictionary<string, INodeName>(50);

        public XmlReaderToReceiver(XmlReader reader, IReceiver receiver)
        {
            this.reader = reader;
            this.receiver = receiver;
            this.pipe = receiver.GetPipelineConfiguration();
            Configuration config = pipe.GetConfiguration();
            this.lineNumbering = pipe.GetParseOptions().IsLineNumbering();
            this.allowDisableOutputEscaping = config.GetConfigurationProperty(Feature<bool>.USE_PI_DISABLE_OUTPUT_ESCAPING);
        }

        /// <summary>
        /// Parse the whole document, sending events to the receiver.
        /// </summary>
        public static void Send(XmlReader reader, IReceiver receiver)
        {
            new XmlReaderToReceiver(reader, receiver).Parse();
        }

        /// <summary>
        /// Build a <see cref="System.Xml.XmlReader"/> with the settings used by the (non-validating, no
        /// external-fetch) input pipeline — identical to DotNetXmlReader.CreateReader so parsing is byte-equivalent.
        /// </summary>
        public static XmlReader CreateXmlReader(TextReader charStream, Stream byteStream, string systemId)
        {
            return CreateXmlReader(charStream, byteStream, systemId, null);
        }

        /// <summary>
        /// As above, but with an explicit <see cref="System.Xml.XmlResolver"/> for external entities / DTD
        /// subsets (e.g. one backed by Saxon's ResourceResolver). Pass null for no external fetch.
        /// </summary>
        public static XmlReader CreateXmlReader(TextReader charStream, Stream byteStream, string systemId, XmlResolver resolver)
        {
            return CreateXmlReader(charStream, byteStream, systemId, resolver, false);
        }

        public static XmlReader CreateXmlReader(TextReader charStream, Stream byteStream, string systemId, XmlResolver resolver, bool dtdValidate, bool suppressValidationErrors = false)
        {
            var settings = new XmlReaderSettings
            {
                // File-relative DTD/external-entity fetch by default (Java SAX parity); an internal subset
                // is processed for entity expansion. When a resolver is supplied, external entities/DTD
                // resolve through it instead.
                DtdProcessing = DtdProcessing.Parse,
                XmlResolver = resolver ?? new FileOnlyXmlResolver(),
                ValidationType = dtdValidate ? ValidationType.DTD : ValidationType.None,
                IgnoreComments = false,
                IgnoreProcessingInstructions = false,
                IgnoreWhitespace = false,
                CheckCharacters = true,
                CloseInput = true,
                ConformanceLevel = ConformanceLevel.Document,
            };
            if (dtdValidate && suppressValidationErrors)
            {
                // ValidationType.DTD here is used only so .NET classifies element-content whitespace as
                // ignorable (XmlNodeType.Whitespace vs SignificantWhitespace — see Parse()); we do not want a
                // DTD-invalid but well-formed document to abort, so swallow validity events.
                settings.ValidationEventHandler += (sender, e) => { };
            }

            string baseUri = systemId ?? string.Empty;
            if (charStream != null)
            {
                return XmlReader.Create(charStream, settings, baseUri);
            }

            if (byteStream != null)
            {
                // XML 1.1: .NET's XmlReader rejects version="1.1". If the declaration announces 1.1, downgrade
                // it to 1.0 in a pass-through wrapper and relax character checking so the C0 controls that are
                // well-formed in 1.1 (but not 1.0) pass. A 1.0 / declaration-less document is untouched.
                byteStream = MaybeDowngradeXml11(byteStream, settings);
                return XmlReader.Create(byteStream, settings, baseUri);
            }

            if (!string.IsNullOrEmpty(systemId))
            {
                return XmlReader.Create(systemId, settings);
            }

            throw new XPathException("ActiveStreamSource supplies neither a stream nor a system identifier");
        }

        private static Stream MaybeDowngradeXml11(Stream input, XmlReaderSettings settings)
        {
            byte[] head = new byte[XmlDeclPeekBytes];
            int n = 0, r;
            while (n < XmlDeclPeekBytes && (r = input.Read(head, n, XmlDeclPeekBytes - n)) > 0)
            {
                n += r;
            }

            var latin1 = System.Text.Encoding.GetEncoding("ISO-8859-1");
            string decl = latin1.GetString(head, 0, n);
            if (IsXml11Declaration(decl))
            {
                settings.CheckCharacters = false;
                // '1.1' -> '1.0' is length-preserving, so the byte offsets the parser sees are unchanged.
                string patched = decl.Replace("version=\"1.1\"", "version=\"1.0\"").Replace("version='1.1'", "version='1.0'");
                byte[] patchedHead = latin1.GetBytes(patched);
                return new PrefixedStream(patchedHead, patchedHead.Length, input);
            }

            return new PrefixedStream(head, n, input);
        }

        private static bool IsXml11Declaration(string s)
        {
            int decl = s.IndexOf("<?xml", StringComparison.Ordinal);
            if (decl < 0 || decl > 3)   // must be the document's XML declaration (BOM may precede it)
            {
                return false;
            }

            int end = s.IndexOf("?>", decl, StringComparison.Ordinal);
            string declPart = end >= 0 ? s.Substring(decl, end - decl) : s;
            return declPart.Contains("version=\"1.1\"") || declPart.Contains("version='1.1'");
        }

        public void Parse()
        {
            localLocator = new XmlPullLocation(reader);
            lastTextNodeLocator = localLocator;
            StartDocument();
            bool hasDtd = false;
            // When the reader validates against a DTD, .NET reports whitespace in element-only content as
            // XmlNodeType.Whitespace and whitespace in mixed content as SignificantWhitespace — the signal
            // needed to drop ignorable whitespace (number-4501). Without validation every inter-element
            // whitespace is Whitespace, so this stays off and such nodes are preserved.
            bool dtdWhitespaceClassification = reader.Settings != null && reader.Settings.ValidationType == ValidationType.DTD;
            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        StartElement();

                        // An empty element (<x/>) opens and closes in one node and raises no separate
                        // EndElement, so synthesize the close and leave the nesting depth unchanged.
                        if (reader.IsEmptyElement)
                        {
                            EndElement();
                        }
                        else
                        {
                            elementDepth++;
                        }

                        break;
                    case XmlNodeType.EndElement:
                        EndElement();
                        elementDepth--;
                        break;
                    case XmlNodeType.SignificantWhitespace:
                        // Whitespace .NET knows to be significant (mixed content, or xml:space="preserve"):
                        // always retained. Prolog/epilog (depth 0) is still outside the data model.
                        if (elementDepth == 0)
                        {
                            break;
                        }

                        goto case XmlNodeType.Text;
                    case XmlNodeType.Whitespace:
                        // Prolog/epilog whitespace is not part of the XPath data model; a Java SAX parser
                        // never reports it. XmlReader does, at depth 0 -- suppress it.
                        if (elementDepth == 0)
                        {
                            break;
                        }

                        // Element-content (ignorable) whitespace: when validating against a DTD, .NET reports
                        // element-only-content whitespace as Whitespace (mixed content is SignificantWhitespace
                        // above). Drop it — Java's SAX ignorableWhitespace() likewise never enters the XSLT/XDM
                        // source tree (number-4501). Gated on the reader validating against a DTD, so a
                        // DTD-less document (every inter-element node is Whitespace) preserves it.
                        if (hasDtd && dtdWhitespaceClassification)
                        {
                            break;
                        }

                        goto case XmlNodeType.Text;
                    case XmlNodeType.Text:
                    case XmlNodeType.CDATA:
                        AppendChars(reader.Value);
                        break;
                    case XmlNodeType.ProcessingInstruction:
                        ProcessingInstruction(reader.Name, reader.Value);
                        break;
                    case XmlNodeType.Comment:
                        Comment(reader.Value);
                        break;
                    case XmlNodeType.DocumentType:
                        // Nothing to emit into the tree, but harvest ATTLIST declarations from the internal
                        // subset so ID/IDREF attributes can be typed below (fn:id / fn:idref), and NDATA
                        // entity declarations for fn:unparsed-entity-uri / -public-id.
                        hasDtd = true;
                        ParseDtdAttTypes(reader.Value);
                        ParseDtdUnparsedEntities(reader.Value);
                        // .NET surfaces only the internal subset as reader.Value. When the DOCTYPE names an
                        // external DTD (SYSTEM), fetch it and harvest its ATTLIST declarations too — the FOTS
                        // id/idref and xsl:number id() tests declare ID attributes in an external .dtd
                        // (number-4501, id-035). Best-effort: a missing/complex DTD leaves ID typing as-is.
                        ParseExternalDtd(reader.GetAttribute("SYSTEM"));
                        break;

                        // XmlDeclaration, expanded EntityReference: nothing to emit.
                }
            }

            EndDocument();
        }

        private void StartDocument()
        {
            charsUsed = 0;
            NamespaceMap empty = NamespaceMap.EmptyMap();
            namespaceStack.Push(empty);
            receiver.SetPipelineConfiguration(pipe);
            string systemId = localLocator.GetSystemId();
            if (systemId != null)
            {
                receiver.SetSystemId(systemId);
            }

            receiver.Open();
            receiver.StartDocument(ReceiverOption.NONE);
        }

        private void EndDocument()
        {
            Flush(true);
            receiver.EndDocument();
            receiver.Dispose();
        }

        // Harvest ID-family attribute types from a DOCTYPE internal subset's ATTLIST declarations. .NET's
        // XmlReader does not expose DTD attribute types, so fn:id/fn:idref would otherwise never see an ID.
        // Only the internal subset is available here (an external subset is parsed for entities but its text
        // is not surfaced); that covers the FOTS id/idref tests, which declare the DTD inline.
        private void ParseDtdAttTypes(string internalSubset)
        {
            if (string.IsNullOrEmpty(internalSubset))
            {
                return;
            }

            foreach (global::System.Text.RegularExpressions.Match decl in global::System.Text.RegularExpressions.Regex.Matches(internalSubset, @"<!ATTLIST\s+(\S+)\s+([\s\S]*?)>"))
            {
                string elem = decl.Groups[1].Value;
                // Each attribute def is `name type default`. Anchoring on the default token that follows the
                // type (#REQUIRED/#IMPLIED/#FIXED or a quoted value) avoids matching a type keyword that
                // appears inside an enumeration or a default value earlier in the same ATTLIST.
                foreach (global::System.Text.RegularExpressions.Match att in global::System.Text.RegularExpressions.Regex.Matches(decl.Groups[2].Value,
                    "([^\\s>]+)\\s+(ID|IDREF|IDREFS|NMTOKEN|NMTOKENS|ENTITY|ENTITIES)\\s+(?:#(?:REQUIRED|IMPLIED|FIXED)|\"|')"))
                {
                    if (dtdAttTypes == null)
                    {
                        dtdAttTypes = new Dictionary<string, string>();
                    }

                    dtdAttTypes[elem + "\t" + att.Groups[1].Value] = att.Groups[2].Value;
                }
            }
        }

        // Harvest <!ENTITY name SYSTEM|PUBLIC ... NDATA notation> declarations from the internal
        // subset and report them to the receiver (upstream gets these via SAX unparsedEntityDecl;
        // System.Xml.XmlReader has no unparsed-entity API). The system ID is absolutized against
        // the document URI — SAX parsers differ on this and the suite expects it resolved.
        private void ParseDtdUnparsedEntities(string internalSubset)
        {
            if (string.IsNullOrEmpty(internalSubset))
            {
                return;
            }

            foreach (global::System.Text.RegularExpressions.Match decl in global::System.Text.RegularExpressions.Regex.Matches(internalSubset, @"<!ENTITY\s+([^\s%>][^\s>]*)\s+([\s\S]*?)>"))
            {
                string name = decl.Groups[1].Value;
                string body = decl.Groups[2].Value;
                if (!global::System.Text.RegularExpressions.Regex.IsMatch(body, @"\bNDATA\s+\S+\s*$"))
                {
                    continue;   // parsed (general) entity — not reported
                }

                string publicId = null, systemId = null;
                var m = global::System.Text.RegularExpressions.Regex.Match(body, "^PUBLIC\\s+(?:\"([^\"]*)\"|'([^']*)')\\s+(?:\"([^\"]*)\"|'([^']*)')");
                if (m.Success)
                {
                    publicId = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
                    systemId = m.Groups[3].Success ? m.Groups[3].Value : m.Groups[4].Value;
                }
                else
                {
                    m = global::System.Text.RegularExpressions.Regex.Match(body, "^SYSTEM\\s+(?:\"([^\"]*)\"|'([^']*)')");
                    if (!m.Success)
                    {
                        continue;
                    }
                    systemId = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
                }

                string abs = systemId;
                try
                {
                    string baseUri = localLocator.GetSystemId();
                    if (!string.IsNullOrEmpty(baseUri))
                    {
                        abs = new Uri(new Uri(baseUri), systemId).AbsoluteUri;
                    }
                }
                catch { }
                receiver.SetUnparsedEntity(name, abs, publicId);
            }
        }

        // Fetch the external DTD subset (SYSTEM identifier) and harvest its ATTLIST declarations, so that
        // ID/IDREF attributes declared in an external .dtd are typed (fn:id/fn:idref, id() in patterns).
        // Best-effort and file-only: parameter-entity indirection and includes are not expanded (adequate
        // for the flat FOTS test DTDs); any failure silently leaves ID typing to the internal subset.
        private void ParseExternalDtd(string systemId)
        {
            if (string.IsNullOrEmpty(systemId))
            {
                return;
            }

            try
            {
                Uri abs;
                string baseUri = localLocator.GetSystemId();
                if (!string.IsNullOrEmpty(baseUri) && Uri.TryCreate(new Uri(baseUri), systemId, out abs))
                {
                    // nothing
                }
                else if (!Uri.TryCreate(systemId, UriKind.Absolute, out abs))
                {
                    return;
                }

                if (abs.IsFile && System.IO.File.Exists(abs.LocalPath))
                {
                    ParseDtdAttTypes(System.IO.File.ReadAllText(abs.LocalPath));
                    ParseDtdUnparsedEntities(System.IO.File.ReadAllText(abs.LocalPath));
                }
            }
            catch { }
        }

        private void StartElement()
        {
            Flush(true);
            int options = ReceiverOption.NAMESPACE_OK | ReceiverOption.ALL_NAMESPACES;

            string uri = reader.NamespaceURI ?? string.Empty;
            string localName = reader.LocalName;
            string qName = reader.Name;

            NamespaceMap nsMap = namespaceStack.Peek();
            IList<AttributeInfo> attributes = null;

            if (reader.MoveToFirstAttribute())
            {
                do
                {
                    string aName = reader.Name;
                    string aPrefix = reader.Prefix;
                    string aLocal = reader.LocalName;
                    if (aName == "xmlns" || aPrefix == "xmlns")
                    {
                        // Namespace declaration: fold into the namespace map, not the attribute list.
                        // The binding xmlns:xmlns is never legal and is ignored (matching ReceivingContentHandler).
                        string prefix = aName == "xmlns" ? string.Empty : aLocal;
                        if (!prefix.Equals("xmlns"))
                        {
                            nsMap = nsMap.Bind(prefix, NamespaceUri.Of(reader.Value));
                        }
                    }
                    else
                    {
                        if (attributes == null)
                        {
                            attributes = new List<AttributeInfo>();
                        }

                        INodeName attName = GetNodeName(reader.NamespaceURI ?? string.Empty, aLocal, aName);

                        // Non-validating parse: attributes are untyped, but recover DTD-declared ID/IDREF
                        // typing (from the ATTLIST harvest above) as IS_ID / IS_IDREF flags so fn:id and
                        // fn:idref work — the TinyTree honours these flags (HandleRootTinyDoc) exactly as it
                        // does for the SAX path. NMTOKEN(S)/ENTITY(IES) have no fn:id/idref effect on an
                        // untyped tree, so they stay plain untyped.
                        int attProps = ReceiverOption.NAMESPACE_OK;
                        if (dtdAttTypes != null && dtdAttTypes.TryGetValue(qName + "\t" + aName, out string dtdType))
                        {
                            if (dtdType == "ID")
                            {
                                attProps |= ReceiverOption.IS_ID;
                            }
                            else if (dtdType == "IDREF" || dtdType == "IDREFS")
                            {
                                attProps |= ReceiverOption.IS_IDREF;
                            }
                        }

                        attributes.Add(new AttributeInfo(attName, BuiltInAtomicType.UNTYPED_ATOMIC, reader.Value, localLocator, attProps));
                    }
                }
                while (reader.MoveToNextAttribute());
                reader.MoveToElement();
            }

            INodeName elementName = GetNodeName(uri, localName, qName);
            IAttributeMap attributeMap = attributes == null
                ? (IAttributeMap)EmptyAttributeMap.GetInstance()
                : SequenceTool.AttributeMapFromList(attributes);

            // SAX resets levelInEntity via startEntity/endEntity; XmlReader expands entities
            // transparently, so detect the boundary by a BaseURI change vs the enclosing element —
            // the builder marks LevelInEntity==0 elements topWithinEntity (xml:base inside an
            // external entity resolves against the ENTITY's URI, base-uri-051/052).
            string curBase = reader.BaseURI;
            localLocator.topOfEntity = entityBaseStack.Count > 0
                && !string.Equals(curBase, entityBaseStack.Peek(), StringComparison.Ordinal);
            receiver.StartElement(elementName, Untyped.INSTANCE, attributeMap, nsMap, localLocator, options);
            localLocator.topOfEntity = false;
            entityBaseStack.Push(curBase);
            localLocator.levelInEntity++;
            namespaceStack.Push(nsMap);
            afterStartTag = true;
        }

        private void EndElement()
        {
            // Don't attempt whitespace compression if this end tag directly follows a start tag.
            Flush(!afterStartTag);
            localLocator.levelInEntity--;
            entityBaseStack.Pop();
            receiver.EndElement();
            afterStartTag = false;
            namespaceStack.Pop();
        }

        private void AppendChars(string s)
        {
            int length = s.Length;
            if (length == 0)
            {
                return;
            }

            while (charsUsed + length > buffer.Length)
            {
                Array.Resize(ref buffer, buffer.Length * 2);
            }

            s.CopyTo(0, buffer, charsUsed, length);
            charsUsed += length;
            if (lineNumbering)
            {
                lastTextNodeLocator = localLocator.SaveLocation();
            }
        }

        private void Flush(bool compress)
        {
            if (charsUsed > 0)
            {
                UnicodeString content = StringTool.Compress(buffer, 0, charsUsed, compress);
                receiver.Characters(content, lastTextNodeLocator, escapingDisabled ? ReceiverOption.DISABLE_ESCAPING : ReceiverOption.WHOLE_TEXT_NODE);
                charsUsed = 0;
                escapingDisabled = false;
            }
        }

        private void ProcessingInstruction(string target, string data)
        {
            Flush(true);
            if (allowDisableOutputEscaping)
            {
                if (target.Equals(ResultConsts.PI_DISABLE_OUTPUT_ESCAPING))
                {
                    escapingDisabled = true;
                    return;
                }
                else if (target.Equals(ResultConsts.PI_ENABLE_OUTPUT_ESCAPING))
                {
                    escapingDisabled = false;
                    return;
                }
            }

            UnicodeString ud = string.IsNullOrEmpty(data)
                ? (UnicodeString)EmptyUnicodeString.GetInstance()
                : Whitespace.RemoveLeadingWhitespace(StringView.Tidy(data));
            receiver.ProcessingInstruction(target, ud, localLocator, ReceiverOption.NONE);
        }

        private void Comment(string text)
        {
            Flush(true);
            receiver.Comment(StringView.Of(text), localLocator, ReceiverOption.NONE);
        }

        private INodeName GetNodeName(string uri, string localname, string rawname)
        {
            Dictionary<string, INodeName> map2;
            if (uri.Length == 0)
            {
                map2 = noNamespaceNameCache;
            }
            else if (!nameCache.TryGetValue(uri, out map2))
            {
                map2 = new Dictionary<string, INodeName>(50);
                nameCache[uri] = map2;
            }

            if (map2.TryGetValue(rawname, out INodeName cached))
            {
                return cached;
            }

            // The INodeName is shared by all nodes with the same lexical QName; the namecode/fingerprint,
            // if needed, is allocated later by the tree builder. We key on the raw QName to retain the prefix.
            INodeName created;
            if (uri.Length == 0)
            {
                created = new NoNamespaceName(localname);
            }
            else
            {
                string prefix = NameChecker.GetPrefix(rawname);
                created = new FingerprintedQName(prefix, NamespaceUri.Of(uri), localname);
            }

            map2[rawname] = created;
            return created;
        }

        /// <summary>
        /// As above, but optionally validating against the document's DTD (<c>ValidationType.DTD</c>) — the
        /// native replacement for the old SAX DTD-STRICT path. DTD validation needs an XmlResolver to fetch an
        /// external DTD subset; the caller supplies one when validating against an external DTD.
        /// </summary>
        // Java's default SAX parser resolves file-relative external DTD subsets/entities; the old null
        // resolver made any <!DOCTYPE x SYSTEM "local.dtd"> fail to parse. Restrict to file: URIs so no
        // network fetch can happen implicitly.
        internal sealed class FileOnlyXmlResolver : XmlUrlResolver
        {
            public override object GetEntity(Uri absoluteUri, string role, System.Type ofObjectToReturn)
            {
                if (absoluteUri != null && absoluteUri.IsFile)
                {
                    return base.GetEntity(absoluteUri, role, ofObjectToReturn);
                }

                // IOException, not XmlException: an unfetchable URI is an I/O failure — callers map it
                // to SXXP0003/XTSE0165 (a raw XmlException would escape the compile path uncoded).
                throw new System.IO.IOException("External entity fetch blocked for non-file URI: " + absoluteUri);
            }
        }

        // Serves a buffered prefix, then the rest of the underlying stream — lets the XML declaration be
        // peeked (and optionally rewritten) without a seekable stream. Read-only, forward-only.
        private sealed class PrefixedStream : Stream
        {
            private readonly byte[] prefix;
            private readonly int prefixLen;
            private int pos;
            private readonly Stream rest;

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new System.NotSupportedException();
            public override long Position
            {
                get => throw new System.NotSupportedException();
                set => throw new System.NotSupportedException();
            }

            public PrefixedStream(byte[] prefix, int count, Stream rest)
            {
                this.prefix = prefix;
                this.prefixLen = count;
                this.rest = rest;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (pos < prefixLen)
                {
                    int take = System.Math.Min(count, prefixLen - pos);
                    System.Array.Copy(prefix, pos, buffer, offset, take);
                    pos += take;
                    return take;
                }

                return rest.Read(buffer, offset, count);
            }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new System.NotSupportedException();
            public override void SetLength(long value) => throw new System.NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new System.NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    rest.Dispose();
                }

                base.Dispose(disposing);
            }
        }

        // Native live location backed directly by the XmlReader's line/column information (no SAX Locator).
        // It carries levelInEntity so the tree builders (via ISourceLocator) can mark the top node of each
        // entity, exactly as ReceivingContentHandler.LocalLocator does on the SAX path.
        private sealed class XmlPullLocation : ISourceLocator
        {
            private readonly XmlReader reader;
            private readonly IXmlLineInfo lineInfo;
            public int levelInEntity;
            public bool topOfEntity;   // set for the StartElement call of an element whose BaseURI differs from its parent's

            public int LevelInEntity => topOfEntity ? 0 : levelInEntity;

            public XmlPullLocation(XmlReader reader)
            {
                this.reader = reader;
                this.lineInfo = reader as IXmlLineInfo;
            }
            public string GetPublicId() => null;
            public string GetSystemId() => reader.BaseURI;
            public int GetLineNumber() => lineInfo != null && lineInfo.HasLineInfo() ? lineInfo.LineNumber : -1;
            public int GetColumnNumber() => lineInfo != null && lineInfo.HasLineInfo() ? lineInfo.LinePosition : -1;
            public ILocation SaveLocation() => new Loc(GetSystemId(), GetLineNumber(), GetColumnNumber());
        }
    }
}
