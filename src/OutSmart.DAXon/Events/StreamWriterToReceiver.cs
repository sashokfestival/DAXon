////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Model.Pull;
using OutSmart.DAXon.Serialization.CharCodes;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
using OutSmart.DAXon.Internal.Jaxp.Namespace;
using OutSmart.DAXon.Internal.Jaxp.Stax;

using OutSmart.DAXon.Api;
using System.IO;
namespace OutSmart.DAXon.Events
{
    public class StreamWriterToReceiver : XMLStreamWriter
    {
        private static readonly bool DEBUG = false;

        private StartTag pendingTag = null;
        private readonly Stack<NamespaceMap> namespaceStack = new Stack<NamespaceMap>();
        /// <summary>
        /// The receiver to which events will be passed
        /// </summary>
        private readonly IReceiver receiver;
        /// <summary>
        /// The Checker used for testing valid characters
        /// </summary>
        private readonly IIntPredicateProxy charChecker;
        /// <summary>
        /// Flag to indicate whether names etc are to be checked for well-formedness
        /// </summary>
        private bool isChecking = false;
        /// <summary>
        /// Flag to indicate whether names etc are to be checked for well-formedness
        /// </summary>
        private int depth = -1;
        /// <summary>
        /// Flag indicating that an empty element has been requested.
        /// </summary>
        private bool isEmptyElement;
        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        private readonly NamespaceReducer inScopeNamespaces;
        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        private readonly Stack<IList<NamespaceBinding>> setPrefixes = new Stack<IList<NamespaceBinding>>();
        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        private NamespaceContext rootNamespaceContext = null;

        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        public virtual IReceiver Receiver => receiver;

        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        // no-op
        public virtual NamespaceContext NamespaceContext
        {
            get => new StreamWriterNamespaceContext(this); set
            {

                // Note, we do not enforce the rule that this can only be called once, because the spec is self-contradictory
                // on this point.
                if (depth > 0)
                {
                    throw new InvalidOperationException("setNamespaceContext may only be called at the start of the document");
                }


                // Unfortunately the JAXP NamespaceContext class does not allow us to discover all the namespaces
                // that were declared, nor to declare new ones. So we have to retain it separately
                rootNamespaceContext = value;
            }
        }
        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        public StreamWriterToReceiver(IReceiver receiver)
        {

            // Events are passed through a NamespaceReducer which maintains the namespace context
            // It also eliminates duplicate namespace declarations, and creates extra namespace declarations
            // where needed to support prefix-uri mappings used on elements and attributes
            PipelineConfiguration pipe = receiver.GetPipelineConfiguration();
            this.inScopeNamespaces = new NamespaceReducer(receiver);
            this.namespaceStack.Push(NamespaceMap.EmptyMap());
            this.receiver = inScopeNamespaces;
            this.charChecker = pipe.GetConfiguration().ValidCharacterChecker;
            this.setPrefixes.Push(new List<NamespaceBinding>());
            this.rootNamespaceContext = (NamespaceContext)(new NamespaceContextImpl(NamespaceMap.EmptyMap())); // See bug 2902; initialise rootNamespaceContext to an empty set of namespaces
        }

        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        public virtual void SetCheckValues(bool check)
        {
            this.isChecking = check;
        }

        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        public virtual bool IsCheckValues()
        {
            return this.isChecking;
        }

        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        private void FlushStartTag()
        {
            if (depth == -1)
            {
                WriteStartDocument();
            }

            if (pendingTag != null)
            {
                try
                {
                    CompleteTriple(pendingTag.elementName, false);
                    foreach (Triple t in pendingTag.attributes)
                    {
                        CompleteTriple(t, true);
                    }

                    INodeName elemName;
                    if (pendingTag.elementName.uri.IsEmpty())
                    {
                        elemName = new NoNamespaceName(pendingTag.elementName.local);
                    }
                    else
                    {
                        elemName = new FingerprintedQName(pendingTag.elementName.prefix, pendingTag.elementName.uri, pendingTag.elementName.local);
                    }

                    NamespaceMap nsMap = namespaceStack.Peek();
                    if (!pendingTag.elementName.uri.IsEmpty())
                    {
                        nsMap = nsMap.Put(pendingTag.elementName.prefix, pendingTag.elementName.uri);
                    }

                    foreach (Triple t in pendingTag.namespaces)
                    {
                        if (t.prefix == null)
                        {
                            t.prefix = "";
                        }

                        if (t.uri == null)
                        {
                            t.uri = NamespaceUri.NULL;
                        }

                        if (!t.uri.IsEmpty())
                        {
                            nsMap = nsMap.Put(t.prefix, t.uri);
                        }
                    }

                    IAttributeMap attributes = EmptyAttributeMap.GetInstance();
                    foreach (Triple t in pendingTag.attributes)
                    {
                        INodeName attName;
                        if (t.uri.IsEmpty())
                        {
                            attName = new NoNamespaceName(t.local);
                        }
                        else
                        {
                            attName = new FingerprintedQName(t.prefix, t.uri, t.local);
                            nsMap = nsMap.Put(t.prefix, t.uri);
                        }

                        attributes = attributes.Put(new AttributeInfo(attName, BuiltInAtomicType.UNTYPED_ATOMIC, t.value, Loc.NONE, ReceiverOption.NONE));
                    }

                    receiver.StartElement(elemName, Untyped.INSTANCE, attributes, nsMap, Loc.NONE, ReceiverOption.NONE);
                    pendingTag = null;
                    if (isEmptyElement)
                    {
                        isEmptyElement = false;
                        depth--;
                        setPrefixes.Pop();
                        receiver.EndElement();
                    }
                    else
                    {
                        namespaceStack.Push(nsMap);
                    }
                }
                catch (XPathException e)
                {
                    throw new XMLStreamException(e);
                }
            }
        }

        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        private void CompleteTriple(Triple t, bool isAttribute)
        {
            if (t.local == null)
            {
                throw new XMLStreamException("Local name of " + (isAttribute ? "Attribute" : "Element") + " is missing");
            }

            if (isChecking && !IsValidNCName(t.local))
            {
                throw new XMLStreamException("Local name of " + (isAttribute ? "Attribute" : "Element") + Err.Wrap(t.local) + " is invalid");
            }

            if (t.prefix == null)
            {
                t.prefix = "";
            }

            if (t.uri == null)
            {
                t.uri = NamespaceUri.NULL;
            }

            if (isChecking && !t.uri.IsEmpty() && IsInvalidURI(t.uri.ToString()))
            {
                throw new XMLStreamException("Namespace URI " + Err.Wrap(t.local) + " is invalid");
            }

            if ((t.prefix.Length == 0) && !t.uri.IsEmpty())
            {
                t.prefix = GetPrefixForUri(t.uri);
            }
        }

        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        private string GetPrefixForUri(NamespaceUri uri)
        {
            foreach (Triple t in pendingTag.namespaces)
            {
                if (uri.Equals(t.uri))
                {
                    return t.prefix == null ? "" : t.prefix;
                }
            }

            string setPrefix = GetPrefix(uri);
            if (setPrefix != null)
            {
                return setPrefix;
            }

            IEnumerator<string> prefixes = inScopeNamespaces.IteratePrefixes();
            while (prefixes.MoveNext())
            {
                string p = prefixes.Current;
                if (inScopeNamespaces.GetURIForPrefix(p, false).Equals(uri))
                {
                    return p;
                }
            }

            return "";
        }

        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        public virtual void WriteStartElement(string localName)
        {
            if (DEBUG)
            {
                Console.Error.WriteLine("StartElement " + localName);
            }

            CheckNonNull(localName);
            setPrefixes.Push(new List<NamespaceBinding>());
            FlushStartTag();
            depth++;
            pendingTag = new StartTag();
            pendingTag.elementName.local = localName;
        }

        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        public virtual void WriteStartElement(string namespaceURI, string localName)
        {
            if (DEBUG)
            {
                Console.Error.WriteLine("StartElement Q{" + namespaceURI + "}" + localName);
            }

            CheckNonNull(namespaceURI);
            CheckNonNull(localName);
            setPrefixes.Push(new List<NamespaceBinding>());
            FlushStartTag();
            depth++;
            pendingTag = new StartTag();
            pendingTag.elementName.local = localName;
            pendingTag.elementName.uri = NamespaceUri.Of(namespaceURI);
        }

        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        public virtual void WriteStartElement(string prefix, string localName, string namespaceURI)
        {
            if (DEBUG)
            {
                Console.Error.WriteLine("StartElement " + prefix + "=Q{" + namespaceURI + "}" + localName);
            }

            CheckNonNull(prefix);
            CheckNonNull(localName);
            CheckNonNull(namespaceURI);
            setPrefixes.Push(new List<NamespaceBinding>());
            FlushStartTag();
            depth++;
            pendingTag = new StartTag();
            pendingTag.elementName.local = localName;
            pendingTag.elementName.uri = NamespaceUri.Of(namespaceURI);
            pendingTag.elementName.prefix = prefix;
        }

        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        public virtual void WriteEmptyElement(string namespaceURI, string localName)
        {
            CheckNonNull(namespaceURI);
            CheckNonNull(localName);
            FlushStartTag();
            WriteStartElement(namespaceURI, localName);
            isEmptyElement = true;
        }

        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        public virtual void WriteEmptyElement(string prefix, string localName, string namespaceURI)
        {
            CheckNonNull(prefix);
            CheckNonNull(localName);
            CheckNonNull(namespaceURI);
            FlushStartTag();
            WriteStartElement(prefix, localName, namespaceURI);
            isEmptyElement = true;
        }

        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        public virtual void WriteEmptyElement(string localName)
        {
            CheckNonNull(localName);
            FlushStartTag();
            WriteStartElement(localName);
            isEmptyElement = true;
        }

        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        public virtual void WriteEndElement()
        {
            if (DEBUG)
            {
                Console.Error.WriteLine("EndElement" + depth);
            }

            if (depth <= 0)
            {
                throw new InvalidOperationException("writeEndElement with no matching writeStartElement");
            }


            //        if (isEmptyElement) {
            //            throw new global::System.InvalidOperationException("writeEndElement called for an empty element");
            //        }
            try
            {
                FlushStartTag();
                setPrefixes.Pop();
                namespaceStack.Pop();
                receiver.EndElement();
                depth--;
            }
            catch (XPathException err)
            {
                throw new XMLStreamException(err);
            }
        }

        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        public virtual void WriteEndDocument()
        {
            if (depth == -1)
            {
                throw new InvalidOperationException("writeEndDocument with no matching writeStartDocument");
            }

            try
            {
                FlushStartTag();
                while (depth > 0)
                {
                    WriteEndElement();
                }

                receiver.EndDocument();
                depth = -1;
            }
            catch (XPathException err)
            {
                throw new XMLStreamException(err);
            }
        }

        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        public virtual void Dispose()
        {
            if (depth >= 0)
            {
                WriteEndDocument();
            }

            try
            {
                receiver.Dispose();
            }
            catch (XPathException err)
            {
                throw new XMLStreamException(err);
            }
        }

        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        public virtual void Flush()
        {
        }

        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        public virtual void WriteAttribute(string localName, string value)
        {
            CheckNonNull(localName);
            CheckNonNull(value);
            if (pendingTag == null)
            {
                throw new InvalidOperationException("Cannot write attribute when not in a start tag");
            }

            Triple t = new Triple();
            t.local = localName;
            t.value = value;
            pendingTag.attributes.Add(t);
        }

        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        public virtual void WriteAttribute(string prefix, string namespaceURI, string localName, string value)
        {
            CheckNonNull(prefix);
            CheckNonNull(namespaceURI);
            CheckNonNull(localName);
            CheckNonNull(value);
            if (pendingTag == null)
            {
                throw new InvalidOperationException("Cannot write attribute when not in a start tag");
            }

            Triple t = new Triple();
            t.prefix = prefix;
            t.uri = NamespaceUri.Of(namespaceURI);
            t.local = localName;
            t.value = value;
            pendingTag.attributes.Add(t);
        }

        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        public virtual void WriteAttribute(string namespaceURI, string localName, string value)
        {
            CheckNonNull(namespaceURI);
            CheckNonNull(localName);
            CheckNonNull(value);
            Triple t = new Triple();
            t.uri = NamespaceUri.Of(namespaceURI);
            t.local = localName;
            t.value = value;
            pendingTag.attributes.Add(t);
        }

        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        public virtual void WriteNamespace(string prefix, string namespaceURI)
        {
            if (prefix == null || prefix.Equals("") || prefix.Equals("xmlns"))
            {
                WriteDefaultNamespace(namespaceURI);
            }
            else
            {
                CheckNonNull(namespaceURI);
                if (pendingTag == null)
                {
                    throw new InvalidOperationException("Cannot write namespace when not in a start tag");
                }

                Triple t = new Triple();
                t.uri = NamespaceUri.Of(namespaceURI);
                t.prefix = prefix;
                pendingTag.namespaces.Add(t);
            }
        }

        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        public virtual void WriteDefaultNamespace(string namespaceURI)
        {
            CheckNonNull(namespaceURI);
            if (pendingTag == null)
            {
                throw new InvalidOperationException("Cannot write namespace when not in a start tag");
            }

            Triple t = new Triple();
            t.uri = NamespaceUri.Of(namespaceURI);
            pendingTag.namespaces.Add(t);
        }

        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        public virtual void WriteComment(string data)
        {
            FlushStartTag();
            if (data == null)
            {
                data = "";
            }

            UnicodeString uData = StringView.Of(data);
            try
            {
                if (!IsValidChars(uData))
                {
                    throw new ArgumentException("Invalid XML character in comment: " + data);
                }

                if (isChecking && data.Contains("--"))
                {
                    throw new ArgumentException("Comment contains '--'");
                }

                receiver.Comment(uData, Loc.NONE, ReceiverOption.NONE);
            }
            catch (XPathException err)
            {
                throw new XMLStreamException(err);
            }
        }

        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        public virtual void WriteProcessingInstruction(string target)
        {
            WriteProcessingInstruction(target, "");
        }

        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        public virtual void WriteProcessingInstruction(string target, string data)
        {
            CheckNonNull(target);
            CheckNonNull(data);
            FlushStartTag();
            UnicodeString uData = StringView.Of(data);
            try
            {
                if (isChecking)
                {
                    if (!IsValidNCName(target) || "xml".EqualsIgnoreCase(target))
                    {
                        throw new ArgumentException("Invalid PITarget: " + target);
                    }

                    if (!IsValidChars(uData))
                    {
                        throw new ArgumentException("Invalid character in PI data: " + data);
                    }
                }

                receiver.ProcessingInstruction(target, uData, Loc.NONE, ReceiverOption.NONE);
            }
            catch (XPathException err)
            {
                throw new XMLStreamException(err);
            }
        }

        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        public virtual void WriteCData(string data)
        {
            CheckNonNull(data);
            FlushStartTag();
            WriteCharacters(data);
        }

        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        public virtual void WriteDTD(string dtd)
        {
        }

        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        // no-op
        public virtual void WriteEntityRef(string name)
        {
            throw new NotSupportedException("writeEntityRef");
        }

        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        public virtual void WriteStartDocument()
        {
            WriteStartDocument("utf-8", "1.0");
        }

        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        public virtual void WriteStartDocument(string version)
        {
            WriteStartDocument("utf-8", version);
        }

        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        public virtual void WriteStartDocument(string encoding, string version)
        {
            if (encoding == null)
            {
                encoding = "utf-8";
            }

            if (version == null)
            {
                version = "1.0";
            }

            if (depth != -1)
            {
                throw new InvalidOperationException("writeStartDocument must be the first call");
            }

            try
            {
                receiver.Open();
                receiver.StartDocument(ReceiverOption.NONE);
            }
            catch (XPathException err)
            {
                throw new XMLStreamException(err);
            }

            depth = 0;
        }

        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        public virtual void WriteCharacters(string text)
        {
            CheckNonNull(text);
            FlushStartTag();
            UnicodeString uData = StringView.Of(text);
            if (!IsValidChars(uData))
            {
                throw new ArgumentException("illegal XML character: " + text);
            }

            try
            {
                receiver.Characters(uData, Loc.NONE, ReceiverOption.NONE);
            }
            catch (XPathException err)
            {
                throw new XMLStreamException(err);
            }
        }

        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        public virtual void WriteCharacters(char[] text, int start, int len)
        {
            CheckNonNull(text);
            WriteCharacters(new string(text, start, len));
        }

        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        public virtual string GetPrefix(string uri)
        {
            return GetPrefix(NamespaceUri.Of(uri));
        }

        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        private string GetPrefix(NamespaceUri uri)
        {
            for (int i = setPrefixes.Count - 1; i >= 0; i--)
            {
                IList<NamespaceBinding> bindings = Enumerable.ElementAt(setPrefixes, setPrefixes.Count - 1 - i);
                for (int j = bindings.Count - 1; j >= 0; j--)
                {
                    NamespaceBinding binding = bindings[j];
                    if (binding.GetNamespaceUri().Equals(uri))
                    {
                        return binding.GetPrefix();
                    }
                }
            }

            if (rootNamespaceContext != null)
            {
                return rootNamespaceContext.GetPrefix(uri.ToString());
            }

            return null;
        }

        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        public virtual void SetPrefix(string prefix, string uri)
        {

            // See Saxon bug 2398: this should have stack-like effect
            CheckNonNull(prefix);
            if (uri == null)
            {
                uri = "";
            }

            if (IsInvalidURI(uri))
            {
                throw new ArgumentException("Invalid namespace URI: " + uri);
            }

            if (!"".Equals(prefix) && !IsValidNCName(prefix))
            {
                throw new ArgumentException("Invalid namespace prefix: " + prefix);
            }

            setPrefixes.Peek().Add(new NamespaceBinding(prefix, NamespaceUri.Of(uri)));
        }

        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        // no-op
        public virtual void SetDefaultNamespace(string uri)
        {
            SetPrefix("", uri);
        }

        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        public virtual object GetProperty(string name)
        {
            if (name.Equals("Javax.Xml.Stream.IsRepairingNamespaces"))
            {
                return receiver is NamespaceReducer;
            }
            else
            {
                throw new ArgumentException(name);
            }
        }

        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        private bool IsValidNCName(string name)
        {
            return !isChecking || NameChecker.IsValidNCName(name);
        }

        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        private bool IsValidChars(UnicodeString text)
        {
            return !isChecking || (UTF16CharacterSet.FirstInvalidChar(text.CodePoints(), charChecker) == -1);
        }

        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        private bool IsInvalidURI(string uri)
        {
            return isChecking && !StandardURIChecker.GetInstance().IsValidURI(uri);
        }

        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        private void CheckNonNull(object value)
        {
            if (value == null)
            {
                throw new NullReferenceException();
            }
        }
        public virtual void Close() => throw new NotImplementedException();
        private class Triple
        {
            public string prefix;
            public NamespaceUri uri;
            public string local;
            public string value;
        }

        private class StartTag
        {
            public Triple elementName;
            public IList<Triple> attributes;
            public IList<Triple> namespaces;
            public StartTag()
            {
                elementName = new Triple();
                attributes = new List<Triple>();
                namespaces = new List<Triple>();
            }
        }

        /// <summary>
        /// inScopeNamespaces represents namespaces that have been declared in the XML stream
        /// </summary>
        private class StreamWriterNamespaceContext : NamespaceContext
        {
            readonly NamespaceContext rootNamespaceContext;
            readonly Dictionary<string, NamespaceUri> bindings = new Dictionary<string, NamespaceUri>();
            public StreamWriterNamespaceContext(StreamWriterToReceiver streamWriter)
            {
                rootNamespaceContext = streamWriter.rootNamespaceContext;
                foreach (IList<NamespaceBinding> list in streamWriter.setPrefixes)
                {
                    foreach (NamespaceBinding binding in list)
                    {
                        bindings.Put(binding.GetPrefix(), binding.GetNamespaceUri());
                    }
                }
            }

            public virtual string GetNamespaceURI(string prefix)
            {
                NamespaceUri uri = bindings.Get(prefix);
                if (uri != null)
                {
                    return uri.ToString();
                }

                return rootNamespaceContext.GetNamespaceURI(prefix);
            }

            public virtual string GetPrefix(string namespaceURI)
            {
                foreach (KeyValuePair<string, NamespaceUri> entry in bindings.EntrySet())
                {
                    if (entry.Value.ToString().Equals(namespaceURI))
                    {
                        return entry.Key;
                    }
                }

                return rootNamespaceContext.GetPrefix(namespaceURI);
            }

            public virtual System.Collections.IEnumerator GetPrefixes(string namespaceURI)
            {
                IList<string> prefixes = new List<string>();
                foreach (KeyValuePair<string, NamespaceUri> entry in bindings.EntrySet())
                {
                    if (entry.Value.ToString().Equals(namespaceURI))
                    {
                        prefixes.Add(entry.Key);
                    }
                }

                IEnumerator<string> root = (IEnumerator<string>)rootNamespaceContext.GetPrefixes(namespaceURI);
                while (root.MoveNext())
                {
                    prefixes.Add(root.Current);
                }

                return prefixes.IIterator();
            }
        }
    }
}
