////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Trees.Tiny
{
    public class TinyBuilder : Builder
    {

        private const int PARENT_POINTER_INTERVAL = 10;
        private TinyTree tree;
        private readonly Stack<NamespaceMap> namespaceStack = new Stack<NamespaceMap>();
        private int currentDepth = 0;
        private int nodeNr = 0; // this is the local sequence within this document
        private bool ended = false;
        private bool noNewNamespaces = true;
        private Statistics statistics;
        private bool markDefaultedAttributes = false;
        private Eligibility textualElementEligibilityState = Eligibility.INELIGIBLE;
        private UnicodeBuilder commentBuilder = new UnicodeBuilder();

        private int[] prevAtDepth = new int[100];
        private int[] siblingsAtDepth = new int[100];
        private bool isIDElement = false;
        public virtual TinyTree Tree => tree;

        public virtual int CurrentDepth => currentDepth;

        /// <summary>
        /// Notify the end of an element node
        /// </summary>
        public virtual TinyNodeImpl LastCompletedElement
        {
            get
            {
                if (tree == null)
                {
                    return null;
                }


                return (TinyNodeImpl)tree.GetNode(currentDepth >= 0 ? prevAtDepth[currentDepth] : 0); // Note: reading an incomplete tree needs care if it constructs a prior index, etc.
            }
        }
        public TinyBuilder(PipelineConfiguration pipe) : base(pipe)
        {
            Configuration config = pipe.GetConfiguration();
            statistics = config.GetTreeStatistics().TEMPORARY_TREE_STATISTICS;
            markDefaultedAttributes = config.IsExpandAttributeDefaults() && config.GetBooleanProperty(Feature<bool>.MARK_DEFAULTED_ATTRIBUTES); //System.Console.Error.println("TinyBuilder " + this);
        }

        public virtual void SetStatistics(Statistics stats)
        {
            statistics = stats;
        }

        /// <summary>
        /// Open the event stream
        /// </summary>
        public override void Open()
        {

            if (started)
            {

                // this happens when using an IdentityTransformer
                return;
            }

            if (tree == null)
            {
                tree = new TinyTree(config, statistics);
                currentDepth = 0;
                if (lineNumbering)
                {
                    tree.SetLineNumbering();
                }

                uniformBaseURI = true;
                tree.UniformBaseUri = baseURI;
                tree.SetDurability(GetDurability());
            }

            if (useEventLocation)
            {
                object copier = GetPipelineConfiguration().GetComponent(typeof(ICopyInformee).FullName);
                if (copier is LocationCopier)
                {
                    SetSystemId(((LocationCopier)copier).GetSystemId());
                }
            }

            base.Open();
        }

        /// <summary>
        /// Open the event stream
        /// </summary>
        public override void StartDocument(int properties)
        {
            if ((started && !ended) || currentDepth > 0)
            {

                // this happens when using an IdentityTransformer, or when copying a document node to form
                // the content of an element
                return;
            }

            started = true;
            ended = false;
            TinyTree tt = tree;
            currentRoot = new TinyDocumentImpl(tt);
            TinyDocumentImpl doc = (TinyDocumentImpl)currentRoot;
            doc.SetSystemId(GetSystemId());
            doc.SetBaseURI(BaseURI);
            currentDepth = 0;
            int nodeNr = tt.AddDocumentNode((TinyDocumentImpl)currentRoot);
            prevAtDepth[0] = nodeNr;
            prevAtDepth[1] = -1;
            siblingsAtDepth[0] = 0;
            siblingsAtDepth[1] = 0;
            tt.next[nodeNr] = -1;
            currentDepth++;
        }

        public override void EndDocument()
        {

            tree.commentBuffer = commentBuilder.ToUnicodeString();

            // Add a stopper node to ensure no-one walks off the end of the array; but
            // decrement numberOfNodes so the next node will overwrite it
            tree.AddNode(Types.Type.STOPPER, 0, 0, 0, -1);
            tree.numberOfNodes--;
            if (currentDepth > 1)
            {
                return;
            }


            // happens when copying a document node as the child of an element
            if (ended)
            {
                return; // happens when using an IdentityTransformer
            }

            ended = true;
            prevAtDepth[currentDepth] = -1;
            currentDepth--;
        }

        public override void Reset()
        {
            base.Reset();
            tree = null;
            currentDepth = 0;
            nodeNr = 0;
            ended = false;
            statistics = config.GetTreeStatistics().TEMPORARY_TREE_STATISTICS;
        }

        public override void Close()
        {

            TinyTree tt = tree;
            if (tt != null)
            {
                tree.commentBuffer = commentBuilder.ToUnicodeString();
                tree.textBuffer.Dispose();
                tt.AddNode(Types.Type.STOPPER, 0, 0, 0, -1);
                tt.Condense(statistics);
            }

            base.Close();
        }

        public override void StartElement(INodeName elemName, ISchemaType type, IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties)
        {

            // if the number of siblings exceeds a certain threshold, add a parent pointer, in the form
            // of a pseudo-node
            TinyTree tt = tree;
            textualElementEligibilityState = Eligibility.INELIGIBLE;
            StartElementSetupNamespaces(namespaces);
            StartElementSetupSiblings(tt);
            StartElementAddNode(elemName, type, tt, properties);
            StartElementCalculateDepth(tt);
            StartElementLocalSystemId(tt, location);
            if (lineNumbering)
            {
                tt.SetLineNumber(nodeNr, location.GetLineNumber(), location.GetColumnNumber());
            }

            if (location is ISourceLocator && ((ISourceLocator)location).LevelInEntity == 0 && currentDepth >= 1)
            {
                tt.MarkTopWithinEntity(nodeNr);
            }

            // index loop: the interface enumerators allocate per element (List(1) for singleton
            // maps; net472 allocates even for the empty-map enumerator), ItemAt never does
            int attCount = attributes.Size();
            for (int i = 0; i < attCount; i++)
            {
                AttributeInfo att = attributes.ItemAt(i);
                Attribute2(att.GetNodeName(), att.GetType(), GetAttValue(att), location, att.GetProperties());
            }

            textualElementEligibilityState = (noNewNamespaces && !lineNumbering) ? Eligibility.PRIMED : Eligibility.INELIGIBLE;
            tree.AddNamespaces(nodeNr, namespaceStack.Peek());
            nodeNr++;
        }

        private void StartElementSetupNamespaces(NamespaceMap namespaces)
        {
            noNewNamespaces = true;
            if (namespaceStack.Count == 0)
            {
                noNewNamespaces = false;
                namespaceStack.Push(namespaces);
            }
            else
            {
                noNewNamespaces = namespaces == namespaceStack.Peek();
                namespaceStack.Push(namespaces);
            }
        }

        private void StartElementSetupSiblings(TinyTree tt)
        {
            if (siblingsAtDepth[currentDepth] > PARENT_POINTER_INTERVAL)
            {
                nodeNr = tt.AddNode(Types.Type.PARENT_POINTER, currentDepth, prevAtDepth[currentDepth - 1], 0, 0);
                int prev = prevAtDepth[currentDepth];
                if (prev > 0)
                {
                    tt.next[prev] = nodeNr;
                }

                tt.next[nodeNr] = prevAtDepth[currentDepth - 1];
                prevAtDepth[currentDepth] = nodeNr;
                siblingsAtDepth[currentDepth] = 0;
            }
        }

        private void StartElementAddNode(INodeName elemName, ISchemaType type, TinyTree tt, int properties)
        {

            // now add the element node itself
            int fp = elemName.ObtainFingerprint(namePool);
            int prefixCode = tree.prefixPool.ObtainPrefixCode(elemName.GetPrefix());
            int nameCode = (prefixCode << 20) | fp;
            nodeNr = tt.AddNode(Types.Type.ELEMENT, currentDepth, -1, -1, nameCode);
            isIDElement = ReceiverOption.Contains(properties, ReceiverOption.IS_ID);
            int typeCode = type.Fingerprint;
            if (typeCode != StandardNames.XS_UNTYPED)
            {
                tt.SetElementAnnotation(nodeNr, type);
                if (ReceiverOption.Contains(properties, ReceiverOption.NILLED_ELEMENT))
                {
                    tt.SetNilled(nodeNr);
                }

                if (!isIDElement && type.IsIdType())
                {
                    isIDElement = true;
                }
            }
        }

        private void StartElementCalculateDepth(TinyTree tt)
        {
            if (currentDepth == 0)
            {
                prevAtDepth[0] = nodeNr;
                prevAtDepth[1] = -1;
                currentRoot = tt.GetNode(nodeNr);
            }
            else
            {
                int prev = prevAtDepth[currentDepth];
                if (prev > 0)
                {
                    tt.next[prev] = nodeNr;
                }

                tt.next[nodeNr] = prevAtDepth[currentDepth - 1]; // *O* owner pointer in last sibling
                prevAtDepth[currentDepth] = nodeNr;
                siblingsAtDepth[currentDepth]++;
            }

            currentDepth++;
            if (currentDepth == prevAtDepth.Length)
            {
                Array.Resize(ref prevAtDepth, currentDepth * 2);
                Array.Resize(ref siblingsAtDepth, currentDepth * 2);
            }

            prevAtDepth[currentDepth] = -1;
            siblingsAtDepth[currentDepth] = 0;
        }

        private void StartElementLocalSystemId(TinyTree tt, ILocation location)
        {
            string localSystemId = location.GetSystemId();
            if (IsUseEventLocation() && localSystemId != null)
            {
                tt.SetSystemId(nodeNr, localSystemId);
            }
            else if (currentDepth == 1)
            {
                tt.SetSystemId(nodeNr, systemId);
            }

            if (uniformBaseURI && localSystemId != null && !localSystemId.Equals(baseURI))
            {
                uniformBaseURI = false;
                tt.UniformBaseUri = null;
            }
        }

        protected virtual string GetAttValue(AttributeInfo att)
        {
            return att.Value;
        }

        private void Attribute2(INodeName attName, ISimpleType type, string value, ILocation locationId, int properties)
        {

            int fp = attName.ObtainFingerprint(namePool);
            string prefix = attName.GetPrefix();
            int nameCode = (prefix.Length == 0) ? fp : (tree.prefixPool.ObtainPrefixCode(prefix) << 20) | fp;
            tree.AddAttribute(currentRoot, nodeNr, nameCode, type, value, properties);
            if (markDefaultedAttributes && ReceiverOption.Contains(properties, ReceiverOption.DEFAULTED_VALUE))
            {
                tree.MarkDefaultedAttribute(tree.numberOfAttributes - 1);
            }

            if (fp == StandardNames.XML_BASE)
            {
                uniformBaseURI = false;
                tree.UniformBaseUri = null;
            }
        }

        /// <summary>
        /// Notify the end of an element node
        /// </summary>
        public override void EndElement()
        {

            bool eligibleAsTextualElement = textualElementEligibilityState == Eligibility.ELIGIBLE;
            textualElementEligibilityState = Eligibility.INELIGIBLE;
            prevAtDepth[currentDepth] = -1;
            siblingsAtDepth[currentDepth] = 0;
            currentDepth--;
            namespaceStack.Pop();
            if (isIDElement)
            {

                // we're relying on the fact that an ID element has no element children!
                tree.IndexIDElement(currentRoot, prevAtDepth[currentDepth]);
                isIDElement = false;
            }
            else if (eligibleAsTextualElement && tree.nodeKind[nodeNr] == Types.Type.TEXT && tree.nodeKind[nodeNr - 1] == Types.Type.ELEMENT && tree.alpha[nodeNr - 1] == -1 && noNewNamespaces)
            {

                // Collapse a simple element with text content and no attributes or namespaces into a single node
                // of type TRIVIAL_ELEMENT
                tree.nodeKind[nodeNr - 1] = (byte)Types.Type.TEXTUAL_ELEMENT;
                tree.alpha[nodeNr - 1] = tree.alpha[nodeNr];
                tree.beta[nodeNr - 1] = tree.beta[nodeNr];
                nodeNr--;
                tree.numberOfNodes--;
                if (currentDepth == 0)
                {
                    currentRoot = tree.GetNode(nodeNr);
                }
            }
        }

        /// <summary>
        /// Notify a text node
        /// </summary>
        public override void Characters(UnicodeString chars, ILocation locationId, int properties)
        {

            if (chars is CompressedWhitespace && ReceiverOption.Contains(properties, ReceiverOption.WHOLE_TEXT_NODE))
            {
                TinyTree tt = tree;
                long lvalue = ((CompressedWhitespace)chars).CompressedValue;
                nodeNr = tt.AddNode(Types.Type.WHITESPACE_TEXT, currentDepth, (int)(lvalue >> 32), (int)lvalue, -1);
                int prev = prevAtDepth[currentDepth];
                if (prev > 0)
                {
                    tt.next[prev] = nodeNr;
                }

                tt.next[nodeNr] = prevAtDepth[currentDepth - 1]; // *O* owner pointer in last sibling
                prevAtDepth[currentDepth] = nodeNr;
                siblingsAtDepth[currentDepth]++;
                if (lineNumbering)
                {
                    tt.SetLineNumber(nodeNr, locationId.GetLineNumber(), locationId.GetColumnNumber());
                }

                return;
            }

            if (!chars.IsEmpty())
            {
                nodeNr = MakeTextNode(chars.Tidy());
                if (lineNumbering)
                {
                    tree.SetLineNumber(nodeNr, locationId.GetLineNumber(), locationId.GetColumnNumber());
                }

                textualElementEligibilityState = textualElementEligibilityState == Eligibility.PRIMED ? Eligibility.ELIGIBLE : Eligibility.INELIGIBLE;
            }
        }

        /// <summary>
        /// Notify a text node
        /// </summary>
        protected virtual int MakeTextNode(UnicodeString chars)
        {

            //            chars.verifyCharacters();
            //            //System.Console.Error.println("make text node length " + chars.length());
            //        }
            TinyTree tt = tree;
            LargeTextBuffer buffer = tt.textBuffer;
            int bufferStart = buffer.Length();

            // AppendChars appends exactly `chars`, so the added length is chars.Length32() -
            // no need to re-read buffer.Length() afterwards (one fewer virtual call per text node)
            int len = chars.Length32();
            tt.AppendChars(chars);
            int n = tt.numberOfNodes - 1;
            if (tt.nodeKind[n] == Types.Type.TEXT && tt.depth[n] == currentDepth)
            {

                // merge this text node with the previous text node
                tt.beta[n] += len;
            }
            else
            {

                nodeNr = tt.AddNode(Types.Type.TEXT, currentDepth, bufferStart, len, -1);

                //nodeNr = tt.addNode(global::OutSmart.DAXon.Types.Type.TEXT, currentDepth, tt.textChunksUsed, -1, -1);
                int prev = prevAtDepth[currentDepth];
                if (prev > 0)
                {
                    tt.next[prev] = nodeNr;
                }

                tt.next[nodeNr] = prevAtDepth[currentDepth - 1];
                prevAtDepth[currentDepth] = nodeNr;
                siblingsAtDepth[currentDepth]++;
            }

            return nodeNr;
        }

        public override void ProcessingInstruction(string piname, UnicodeString remainder, ILocation locationId, int properties)
        {
            TinyTree tt = tree;
            textualElementEligibilityState = Eligibility.INELIGIBLE;
            int s = (int)commentBuilder.Length();
            commentBuilder.Accept(remainder);
            int nameCode = namePool.AllocateFingerprint(NamespaceUri.NULL, piname);
            nodeNr = tt.AddNode(Types.Type.PROCESSING_INSTRUCTION, currentDepth, s, remainder.Length32(), nameCode);
            int prev = prevAtDepth[currentDepth];
            if (prev > 0)
            {
                tt.next[prev] = nodeNr;
            }

            tt.next[nodeNr] = prevAtDepth[currentDepth - 1]; // *O* owner pointer in last sibling
            prevAtDepth[currentDepth] = nodeNr;
            siblingsAtDepth[currentDepth]++;
            string localLocation = locationId.GetSystemId();
            if (IsUseEventLocation() && localLocation != null)
            {
                tt.SetSystemId(nodeNr, localLocation);
            }
            else if (currentDepth == 1)
            {
                tt.SetSystemId(nodeNr, systemId);
            }

            if (localLocation != null && !localLocation.Equals(baseURI))
            {
                uniformBaseURI = false;
                tree.UniformBaseUri = null;
            }

            if (lineNumbering)
            {
                tt.SetLineNumber(nodeNr, locationId.GetLineNumber(), locationId.GetColumnNumber());
            }
        }

        public override void Comment(UnicodeString chars, ILocation locationId, int properties)
        {
            TinyTree tt = tree;
            textualElementEligibilityState = Eligibility.INELIGIBLE;
            int s = (int)commentBuilder.Length();
            commentBuilder.Accept(chars);
            nodeNr = tt.AddNode(Types.Type.COMMENT, currentDepth, s, chars.Tidy().Length32(), -1);
            int prev = prevAtDepth[currentDepth];
            if (prev > 0)
            {
                tt.next[prev] = nodeNr;
            }

            tt.next[nodeNr] = prevAtDepth[currentDepth - 1]; // *O* owner pointer in last sibling
            prevAtDepth[currentDepth] = nodeNr;
            siblingsAtDepth[currentDepth]++;
            if (lineNumbering)
            {
                tt.SetLineNumber(nodeNr, locationId.GetLineNumber(), locationId.GetColumnNumber());
            }
        }

        /// <summary>
        /// Set an unparsed entity in the document
        /// </summary>
        public override void SetUnparsedEntity(string name, string uri, string publicId)
        {
            if (tree.GetUnparsedEntity(name) == null)
            {

                // bug 2187
                tree.SetUnparsedEntity(name, uri, publicId);
            }
        }

        /// <summary>
        /// Set an unparsed entity in the document
        /// </summary>
        public override BuilderMonitor GetBuilderMonitor()
        {
            return new TinyBuilderMonitor(this);
        }
        private enum Eligibility
        {
            INELIGIBLE,
            PRIMED,
            ELIGIBLE
        }
    }
}