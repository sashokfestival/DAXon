////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Trees.Wrappers;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Text;
using System.Runtime.CompilerServices;
namespace OutSmart.DAXon.Trees.Linked
{
    public abstract class NodeImpl : IMutableNodeInfo, ISteppingNode, ISiblingCountingNode, ILocation
    {
        public static readonly char[] NODE_LETTER = new[]
        {
            'x',
            'e',
            'a',
            't',
            'x',
            'x',
            'x',
            'p',
            'c',
            'r',
            'x',
            'x',
            'x',
            'n'
        };
        private ParentNodeImpl parent;
        private int index; // Set to -1 when the node is deleted

        public virtual int Fingerprint
        {
            get
            {
                INodeName name = GetNodeName();
                if (name == null)
                {
                    return -1;
                }
                else
                {
                    return name.ObtainFingerprint(GetConfiguration().GetNamePool());
                }
            }
        }

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        /// <summary>
        /// Get the system ID for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the base URI for the node. Default implementation for child nodes.
        /// </summary>
        protected internal virtual long SequenceNumber
        {
            get
            {
                NodeImpl prev = this;
                for (int i = 0; ; i++)
                {
                    if (prev is ParentNodeImpl)
                    {
                        long prevseq = prev.SequenceNumber;
                        return prevseq == -1 ? prevseq : prevseq + 0x10000 + i; // note the 0x10000 is to leave room for namespace and attribute nodes.
                    }

                    prev = prev.PreviousInDocument;
                }
            }
        }

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        /// <summary>
        /// Get the system ID for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the base URI for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the configuration
        /// </summary>
        public virtual string DisplayName
        {
            get
            {
                INodeName qName = GetNodeName();
                return qName == null ? "" : qName.DisplayName;
            }
        }

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        /// <summary>
        /// Get the system ID for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the base URI for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the line number of the node within its source document entity
        /// </summary>
        public virtual NodeInfo LastChild => null;

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        /// <summary>
        /// Get the system ID for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the base URI for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the line number of the node within its source document entity
        /// </summary>
        public virtual NodeInfo Root
        {
            get
            {
                NodeInfo parent = GetParent();
                if (parent == null)
                {
                    return this;
                }
                else
                {
                    return parent.Root;
                }
            }
        }

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        /// <summary>
        /// Get the system ID for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the base URI for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the line number of the node within its source document entity
        /// </summary>
        public virtual DocumentImpl PhysicalRoot
        {
            get
            {
                ParentNodeImpl up = parent;
                while (up != null && !(up is DocumentImpl))
                {
                    up = up.GetRawParent();
                }

                return (DocumentImpl)up;
            }
        }

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        /// <summary>
        /// Get the system ID for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the base URI for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the line number of the node within its source document entity
        /// </summary>
        public virtual NodeImpl PreviousInDocument
        {
            get
            {

                // finds the last child of the previous sibling if there is one;
                // otherwise the previous sibling element if there is one;
                // otherwise the parent, up to the anchor element.
                // If this reaches the document root, return null.
                NodeImpl prev = GetPreviousSibling();
                if (prev != null)
                {
                    return prev.LastDescendantOrSelf;
                }

                return GetParent();
            }
        }

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        /// <summary>
        /// Get the system ID for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the base URI for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the line number of the node within its source document entity
        /// </summary>
        private NodeImpl LastDescendantOrSelf
        {
            get
            {
                NodeImpl last = (NodeImpl)LastChild;
                if (last == null)
                {
                    return this;
                }

                return last.LastDescendantOrSelf;
            }
        }

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        /// <summary>
        /// Get the system ID for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the base URI for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the line number of the node within its source document entity
        /// </summary>
        public virtual NamespaceMap AllNamespaces => null;
        public virtual UnicodeString UnicodeStringValue => throw new NotImplementedException();
        public virtual NodeImpl Head()
        {
            return this;
        }

        //
        //    }
        public virtual ITreeInfo GetTreeInfo()
        {
            return PhysicalRoot;
        }

        public virtual ISchemaType GetSchemaType()
        {
            return Untyped.INSTANCE;
        }

        public virtual int GetColumnNumber()
        {
            if (parent == null)
            {
                return -1;
            }
            else
            {
                return parent.GetColumnNumber();
            }
        }

        public int GetSiblingPosition()
        {
            return index;
        }

        public void SetSiblingPosition(int index)
        {
            this.index = index;
        }

        public virtual IAtomicSequence Atomize()
        {
            ISchemaType stype = GetSchemaType();
            if (stype == Untyped.INSTANCE || stype == BuiltInAtomicType.UNTYPED_ATOMIC)
            {
                return StringValue.MakeUntypedAtomic(UnicodeStringValue);
            }
            else
            {
                return stype.Atomize(this);
            }
        }

        public virtual void SetSystemId(string uri)
        {

            // overridden in DocumentImpl and ElementImpl
            NodeInfo p = GetParent();
            if (p != null)
            {
                p.SetSystemId(uri);
            }
        }

        //
        //    }
        public override bool Equals(object other)
        {

            // default implementation: differs for attribute and namespace nodes
            return this == other;
        }

        //
        //    }
        public override int GetHashCode()
        {
            return RuntimeHelpers.GetHashCode(this);
        }

        public virtual INodeName GetNodeName()
        {
            return null;
        }

        public virtual bool HasFingerprint()
        {
            return true;
        }

        public virtual IAttributeMap Attributes()
        {
            return EmptyAttributeMap.GetInstance();
        }

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        public virtual void GenerateId(StringBuilder buffer)
        {
            long seq = SequenceNumber;
            if (seq == -1)
            {
                PhysicalRoot.GenerateId(buffer);
                buffer.Append(NODE_LETTER[GetNodeKind()]);
                buffer.Append(seq + "h" + GetHashCode());
            }
            else
            {
                parent.GenerateId(buffer);
                buffer.Append(NODE_LETTER[GetNodeKind()]);
                buffer.Append(index);
            }
        }

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        /// <summary>
        /// Get the system ID for the node. Default implementation for child nodes.
        /// </summary>
        public virtual string GetSystemId()
        {
            return parent.GetSystemId();
        }

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        /// <summary>
        /// Get the system ID for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the base URI for the node. Default implementation for child nodes.
        /// </summary>
        public virtual string GetBaseURI()
        {
            return parent.GetBaseURI();
        }

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        /// <summary>
        /// Get the system ID for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the base URI for the node. Default implementation for child nodes.
        /// </summary>
        public int CompareOrder(NodeInfo other)
        {
            if (other is NamespaceNode)
            {
                return 0 - other.CompareOrder(this);
            }

            long a = SequenceNumber;
            long b = ((NodeImpl)other).SequenceNumber;
            if (a == -1 || b == -1)
            {

                // Nodes added by XQuery Update do not have sequence numbers
                return Navigator.CompareOrder(this, (NodeImpl)other);
            }

            return a.CompareTo(b);
        }

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        /// <summary>
        /// Get the system ID for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the base URI for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the configuration
        /// </summary>
        public virtual Configuration GetConfiguration()
        {
            return PhysicalRoot.GetConfiguration();
        }

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        /// <summary>
        /// Get the system ID for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the base URI for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the configuration
        /// </summary>
        public virtual NamePool GetNamePool()
        {
            return PhysicalRoot.GetNamePool();
        }

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        /// <summary>
        /// Get the system ID for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the base URI for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the configuration
        /// </summary>
        public virtual string GetPrefix()
        {
            INodeName qName = GetNodeName();
            return qName == null ? "" : qName.GetPrefix();
        }

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        /// <summary>
        /// Get the system ID for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the base URI for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the configuration
        /// </summary>
        public virtual NamespaceUri GetNamespaceUri()
        {
            INodeName qName = GetNodeName();
            return qName == null ? NamespaceUri.NULL : qName.GetNamespaceUri();
        }

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        /// <summary>
        /// Get the system ID for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the base URI for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the configuration
        /// </summary>
        public virtual string GetLocalPart()
        {
            INodeName qName = GetNodeName();
            return qName == null ? "" : qName.GetLocalPart();
        }

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        /// <summary>
        /// Get the system ID for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the base URI for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the line number of the node within its source document entity
        /// </summary>
        public virtual int GetLineNumber()
        {
            return parent.GetLineNumber();
        }

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        /// <summary>
        /// Get the system ID for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the base URI for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the line number of the node within its source document entity
        /// </summary>
        public virtual ILocation SaveLocation()
        {
            return this;
        }

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        /// <summary>
        /// Get the system ID for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the base URI for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the line number of the node within its source document entity
        /// </summary>
        public NodeImpl GetParent()
        {
            if (parent is DocumentImpl && ((DocumentImpl)parent).IsImaginary())
            {
                return null;
            }

            return parent;
        }

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        /// <summary>
        /// Get the system ID for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the base URI for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the line number of the node within its source document entity
        /// </summary>
        protected ParentNodeImpl GetRawParent()
        {
            return parent;
        }

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        /// <summary>
        /// Get the system ID for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the base URI for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the line number of the node within its source document entity
        /// </summary>
        public void SetRawParent(ParentNodeImpl parent)
        {
            this.parent = parent;
        }

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        /// <summary>
        /// Get the system ID for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the base URI for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the line number of the node within its source document entity
        /// </summary>
        public virtual NodeImpl GetPreviousSibling()
        {
            if (parent == null)
            {
                return null;
            }

            return parent.GetNthChild(index - 1);
        }

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        /// <summary>
        /// Get the system ID for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the base URI for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the line number of the node within its source document entity
        /// </summary>
        public virtual NodeImpl GetNextSibling()
        {
            if (parent == null)
            {
                return null;
            }

            return parent.GetNthChild(index + 1);
        }

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        /// <summary>
        /// Get the system ID for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the base URI for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the line number of the node within its source document entity
        /// </summary>
        public virtual NodeImpl GetFirstChild()
        {
            return null;
        }

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        /// <summary>
        /// Get the system ID for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the base URI for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the line number of the node within its source document entity
        /// </summary>
        public virtual IAxisIterator IterateAxis(int axisNumber)
        {

            // Fast path for child axis
            if (axisNumber == AxisInfo.CHILD)
            {
                if (this is ParentNodeImpl)
                {
                    return ((ParentNodeImpl)this).IterateChildren(null);
                }
                else
                {
                    return EmptyIterator.OfNodes();
                }
            }
            else
            {
                return IterateAxis(axisNumber, AnyNodeTest.GetInstance());
            }
        }

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        /// <summary>
        /// Get the system ID for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the base URI for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the line number of the node within its source document entity
        /// </summary>
        public virtual IAxisIterator IterateAxis(int axisNumber, INodePredicate predicate)
        {
            NodeTest nodeTest = Navigator.NodeTestFromPredicate(predicate);
            switch (axisNumber)
            {
                case AxisInfo.ANCESTOR:
                    return new AncestorEnumeration(this, nodeTest, false);
                case AxisInfo.ANCESTOR_OR_SELF:
                    return new AncestorEnumeration(this, nodeTest, true);
                case AxisInfo.ATTRIBUTE:
                    if (GetNodeKind() != Types.Type.ELEMENT)
                    {
                        return EmptyIterator.OfNodes();
                    }
                    else
                    {
                        return ((ElementImpl)this).IterateAttributes(nodeTest);
                    }

                case AxisInfo.CHILD:
                    if (this is ParentNodeImpl)
                    {
                        return ((ParentNodeImpl)this).IterateChildren(nodeTest);
                    }
                    else
                    {
                        return EmptyIterator.OfNodes();
                    }

                case AxisInfo.DESCENDANT:
                    if (GetNodeKind() == Types.Type.DOCUMENT && nodeTest is NameTest && ((NameTest)nodeTest).PrimitiveType == Types.Type.ELEMENT)
                    {
                        return ((DocumentImpl)this).GetAllElements(((NameTest)nodeTest).Fingerprint);
                    }
                    else if (HasChildNodes())
                    {
                        return (IAxisIterator)new DescendantAxisIterator(this, false, nodeTest);
                    }
                    else
                    {
                        return EmptyIterator.OfNodes();
                    }

                case AxisInfo.DESCENDANT_OR_SELF:
                    return (IAxisIterator)new DescendantAxisIterator(this, true, nodeTest);
                case AxisInfo.FOLLOWING:
                    return new FollowingEnumeration(this, nodeTest);
                case AxisInfo.FOLLOWING_SIBLING:
                    return new FollowingSiblingEnumeration(this, nodeTest);
                case AxisInfo.NAMESPACE:
                    if (GetNodeKind() != Types.Type.ELEMENT)
                    {
                        return EmptyIterator.OfNodes();
                    }

                    return NamespaceNode.MakeIterator(this, nodeTest);
                case AxisInfo.PARENT:
                    NodeInfo parent = GetParent();
                    if (parent == null)
                    {
                        return EmptyIterator.OfNodes();
                    }

                    return Navigator.FilteredSingleton(parent, nodeTest);
                case AxisInfo.PRECEDING:
                    return new PrecedingEnumeration(this, nodeTest);
                case AxisInfo.PRECEDING_SIBLING:
                    return new PrecedingSiblingEnumeration(this, nodeTest);
                case AxisInfo.SELF:
                    return Navigator.FilteredSingleton(this, nodeTest);
                case AxisInfo.PRECEDING_OR_ANCESTOR:
                    return new PrecedingOrAncestorEnumeration(this, nodeTest);
                default:
                    throw new ArgumentException("Unknown axis number " + axisNumber);
            }
        }

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        /// <summary>
        /// Get the system ID for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the base URI for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the line number of the node within its source document entity
        /// </summary>


        public virtual string GetAttributeValue(string uri, string localName) => default;

        public virtual string GetAttributeValue(NamespaceUri uri, string localName)
        {
            return null;
        }

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        /// <summary>
        /// Get the system ID for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the base URI for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the line number of the node within its source document entity
        /// </summary>
        public virtual NodeImpl GetNextInDocument(NodeImpl anchor)
        {

            // find the first child node if there is one; otherwise the next sibling node
            // if there is one; otherwise the next sibling of the parent, grandparent, etc, up to the anchor element.
            // If this yields no result, return null.
            NodeImpl next = GetFirstChild();
            if (next != null)
            {
                return next;
            }

            if (this == anchor)
            {
                return null;
            }

            next = GetNextSibling();
            if (next != null)
            {
                return next;
            }

            NodeImpl parent = this;
            while (true)
            {
                parent = parent.GetParent();
                if (parent == null)
                {
                    return null;
                }

                if (parent == anchor)
                {
                    return null;
                }

                next = parent.GetNextSibling();
                if (next != null)
                {
                    return next;
                }
            }
        }

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        /// <summary>
        /// Get the system ID for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the base URI for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the line number of the node within its source document entity
        /// </summary>
        public virtual NodeImpl GetSuccessorElement(ISteppingNode anchor, NamespaceUri uri, string local)
        {
            NodeImpl next = GetNextInDocument((NodeImpl)anchor);
            while (next != null && !(next.GetNodeKind() == Types.Type.ELEMENT && (uri == null || next.GetNodeName().HasURI(uri)) && (local == null || local.Equals(next.GetLocalPart()))))
            {
                next = next.GetNextInDocument((NodeImpl)anchor);
            }

            return next;
        }

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        /// <summary>
        /// Get the system ID for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the base URI for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the line number of the node within its source document entity
        /// </summary>
        public virtual NamespaceBinding[] GetDeclaredNamespaces(NamespaceBinding[] buffer)
        {
            return null;
        }

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        /// <summary>
        /// Get the system ID for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the base URI for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the line number of the node within its source document entity
        /// </summary>
        public virtual bool HasChildNodes()
        {
            return GetFirstChild() != null;
        }

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        /// <summary>
        /// Get the system ID for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the base URI for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the line number of the node within its source document entity
        /// </summary>
        public virtual void SetTypeAnnotation(ISchemaType type)
        {
        }

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        /// <summary>
        /// Get the system ID for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the base URI for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the line number of the node within its source document entity
        /// </summary>
        /// <summary>
        /// Delete this node (that @is, detach it from its parent)
        /// </summary>
        public virtual void Delete()
        {

            // Overridden for attribute nodes
            if (parent != null)
            {
                parent.RemoveChild(this);
                DocumentImpl newRoot = new DocumentImpl();
                newRoot.SetConfiguration(GetConfiguration());
                newRoot.SetImaginary(true);
                parent = newRoot;
            }

            index = -1;
        }

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        /// <summary>
        /// Get the system ID for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the base URI for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the line number of the node within its source document entity
        /// </summary>
        /// <summary>
        /// Delete this node (that @is, detach it from its parent)
        /// </summary>
        public virtual bool IsDeleted()
        {
            return index == -1 || (parent != null && parent.IsDeleted());
        }

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        /// <summary>
        /// Get the system ID for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the base URI for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the line number of the node within its source document entity
        /// </summary>
        /// <summary>
        /// Delete this node (that @is, detach it from its parent)
        /// </summary>
        public virtual void SetAttributes(IAttributeMap attributes)
        {
            throw new NotSupportedException("setAttributes() applies only to element nodes");
        }

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        /// <summary>
        /// Get the system ID for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the base URI for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the line number of the node within its source document entity
        /// </summary>
        /// <summary>
        /// Delete this node (that @is, detach it from its parent)
        /// </summary>
        public virtual void RemoveAttribute(NodeInfo attribute)
        {
        }

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        /// <summary>
        /// Get the system ID for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the base URI for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the line number of the node within its source document entity
        /// </summary>
        /// <summary>
        /// Delete this node (that @is, detach it from its parent)
        /// </summary>
        public virtual void AddAttribute(INodeName name, ISimpleType attType, string value, int properties, bool inheritNamespaces)
        {
        }

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        /// <summary>
        /// Get the system ID for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the base URI for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the line number of the node within its source document entity
        /// </summary>
        /// <summary>
        /// Delete this node (that @is, detach it from its parent)
        /// </summary>
        public virtual void Rename(INodeName newNameCode, bool inherit)
        {
        }

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        /// <summary>
        /// Get the system ID for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the base URI for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the line number of the node within its source document entity
        /// </summary>
        /// <summary>
        /// Delete this node (that @is, detach it from its parent)
        /// </summary>
        public virtual void AddNamespace(NamespaceBinding nscode, bool inherit)
        {
        }

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        /// <summary>
        /// Get the system ID for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the base URI for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the line number of the node within its source document entity
        /// </summary>
        /// <summary>
        /// Delete this node (that @is, detach it from its parent)
        /// </summary>
        public virtual void Replace(NodeInfo[] replacement, bool inherit)
        {
            if (IsDeleted())
            {
                throw new InvalidOperationException("Cannot replace a deleted node");
            }

            if (GetParent() == null)
            {
                throw new InvalidOperationException("Cannot replace a parentless node");
            }

            parent.ReplaceChildrenAt(replacement, index, inherit);
            parent = null;
            index = -1; // mark the node as deleted
        }

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        /// <summary>
        /// Get the system ID for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the base URI for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the line number of the node within its source document entity
        /// </summary>
        /// <summary>
        /// Delete this node (that @is, detach it from its parent)
        /// </summary>
        public virtual void InsertChildren(NodeInfo[] source, bool atStart, bool inherit)
        {
        }

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        /// <summary>
        /// Get the system ID for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the base URI for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the line number of the node within its source document entity
        /// </summary>
        /// <summary>
        /// Delete this node (that @is, detach it from its parent)
        /// </summary>
        public virtual void InsertSiblings(NodeInfo[] source, bool before, bool inherit)
        {
            if (parent == null)
            {
                throw new InvalidOperationException("Cannot add siblings if there is no parent");
            }

            parent.InsertChildrenAt(source, before ? index : index + 1, inherit);
        }

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        /// <summary>
        /// Get the system ID for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the base URI for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the line number of the node within its source document entity
        /// </summary>
        /// <summary>
        /// Delete this node (that @is, detach it from its parent)
        /// </summary>
        public virtual void RemoveTypeAnnotation()
        {
        }

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        /// <summary>
        /// Get the system ID for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the base URI for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the line number of the node within its source document entity
        /// </summary>
        /// <summary>
        /// Delete this node (that @is, detach it from its parent)
        /// </summary>
        public virtual Builder NewBuilder()
        {
            return PhysicalRoot.NewBuilder();
        }

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        /// <summary>
        /// Get the system ID for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the base URI for the node. Default implementation for child nodes.
        /// </summary>
        /// <summary>
        /// Get the line number of the node within its source document entity
        /// </summary>
        /// <summary>
        /// Delete this node (that @is, detach it from its parent)
        /// </summary>
        public virtual bool EffectiveBooleanValue()
        {
            return true;
        }
        ISteppingNode ISteppingNode.GetParent() => GetParent();
        ISteppingNode ISteppingNode.GetNextSibling() => GetNextSibling();
        ISteppingNode ISteppingNode.GetPreviousSibling() => GetPreviousSibling();
        ISteppingNode ISteppingNode.GetFirstChild() => GetFirstChild();
        ISteppingNode ISteppingNode.GetSuccessorElement(ISteppingNode arg0, NamespaceUri arg1, string arg2) => GetSuccessorElement(arg0, arg1, arg2);
        NodeInfo NodeInfo.GetParent() => GetParent();
        IItem IGroundedValue.Head() => this;
        IItem ISequence.Head() => this;
        public virtual void ReplaceStringValue(UnicodeString arg0) => throw new NotImplementedException();
        public virtual int GetNodeKind() => throw new NotImplementedException();
        public virtual Genre GetGenre() => Genre.NODE; // upstream NodeInfo default
        // A node is a singleton grounded value (mirrors TinyNodeImpl; these were hollow NIE stubs —
        // a variable bound to a linked-tree node crashed VariableReference.Iterate, docbook-001).
        public virtual ISequenceIterator Iterate() => SingletonIterator.MakeIterator(this);
        public virtual IItem ItemAt(int arg0) => arg0 == 0 ? this : null;
        public virtual IGroundedValue Subsequence(int arg0, int arg1) => (arg0 <= 0 && (long)arg0 + arg1 > 0) ? (IGroundedValue)this : OutSmart.DAXon.Values.EmptySequence.GetInstance();
        public virtual int GetLength() => 1;
        public virtual string GetStringValue() => UnicodeStringValue.ToString();
        public virtual void Deliver(IReceiver arg0, ParseOptions arg1) => throw new NotImplementedException();
        public virtual string GetPublicId() => throw new NotImplementedException();
        IItem IItem.Head() => this;
        SingletonIterator IItem.Iterate() => new SingletonIterator(this);

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
        public virtual void RemoveNamespace(string prefix) { throw new NotImplementedException(); }
        public virtual void AddNamespace(string prefix, NamespaceUri uri) { throw new NotImplementedException(); }
        public virtual bool IsSameNodeInfo(NodeInfo other) => throw new NotImplementedException();
        public virtual string GetURI() => throw new NotImplementedException();
        public virtual IEnumerable<NodeInfo> Children() => new Navigator.ChildrenAsIterable(this);
        public virtual IEnumerable<NodeInfo> Children(INodePredicate filter) => new Navigator.ChildrenAsIterable(this, filter);
        public virtual void Copy(IReceiver @out, int copyOptions, ILocation locationId) { throw new NotImplementedException(); }
        public virtual IActiveSource AsActiveSource() => new NodeSource(this);
        public virtual bool IsId() => false; // upstream NodeInfo/Item default
        public virtual bool IsIdref() => false; // upstream NodeInfo/Item default
        public virtual bool IsNilled() => false; // upstream NodeInfo/Item default
        public virtual bool IsStreamed() => false; // upstream NodeInfo/Item default
        public virtual string ToShortString()
        {
            // upstream NodeInfo default toShortString()
            switch (GetNodeKind())
            {
                case OutSmart.DAXon.Types.Type.DOCUMENT: return "document-node()";
                case OutSmart.DAXon.Types.Type.ELEMENT: return "<" + DisplayName + "/>";
                case OutSmart.DAXon.Types.Type.ATTRIBUTE: return "@" + DisplayName;
                case OutSmart.DAXon.Types.Type.TEXT: return "text(\"" + OutSmart.DAXon.Transformation.Err.Truncate30(UnicodeStringValue) + "\")";
                case OutSmart.DAXon.Types.Type.COMMENT: return "<!--" + OutSmart.DAXon.Transformation.Err.Truncate30(UnicodeStringValue) + "-->";
                case OutSmart.DAXon.Types.Type.PROCESSING_INSTRUCTION: return "<?" + DisplayName + "?>";
                case OutSmart.DAXon.Types.Type.NAMESPACE:
                    string __prefix = GetLocalPart();
                    return "xmlns" + (__prefix.Length == 0 ? "" : ":" + __prefix) + "=\"" + UnicodeStringValue + "\"";
                default: return "";
            }
        }
        public virtual IGroundedValue Reduce() => this; // upstream GroundedValue default
        public virtual IGroundedValue Materialize() => this; // upstream GroundedValue default
        public virtual IEnumerable<IItem> AsIterable() => new IItem[] { this }; // singleton grounded value (upstream GroundedValue default for an Item)
        public virtual bool ContainsNode(NodeInfo sought) => OutSmart.DAXon.Expressions.SingletonIntersectExpression.ContainsNode(((OutSmart.DAXon.Model.ISequence)this).Iterate(), sought); // upstream GroundedValue default
        public virtual IGroundedValue Concatenate(IGroundedValue[] others)
        {
            // upstream GroundedValue default: chain this value's items with the others
            var __chain = new OutSmart.DAXon.Collections.Zeno.ZenoChain<OutSmart.DAXon.Model.IItem>().AddAll(((OutSmart.DAXon.Model.IGroundedValue)this).AsIterable());
            foreach (OutSmart.DAXon.Model.IGroundedValue __v in others)
                __chain = __chain.AddAll(__v.AsIterable());
            return new OutSmart.DAXon.Collections.Zeno.ZenoSequence(__chain);
        }
        public virtual ISequence MakeRepeatable() => this; // upstream Sequence.makeRepeatable default
    }
}

