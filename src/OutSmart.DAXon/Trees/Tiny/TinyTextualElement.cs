////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Core;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Jaxp.Transform;
namespace OutSmart.DAXon.Trees.Tiny
{
    public class TinyTextualElement : TinyElementImpl
    {
        private TinyTextualElementText textNode = null;

        public override NamespaceMap AllNamespaces
        {
            get
            {
                TinyNodeImpl parent = GetParent();
                if (parent is TinyElementImpl)
                {
                    return parent.AllNamespaces;
                }
                else
                {
                    return NamespaceMap.EmptyMap();
                }
            }
        }

        public override UnicodeString UnicodeStringValue => TinyTextImpl.GetStringValue(tree, nodeNr);

        public virtual TinyTextualElementText TextNode
        {
            get
            {
                if (textNode == null)
                {
                    textNode = new TinyTextualElementText(this);
                }

                return textNode;
            }
        }
        public TinyTextualElement(TinyTree tree, int nodeNr) : base(tree, nodeNr)
        {
        }

        public override NamespaceBinding[] GetDeclaredNamespaces(NamespaceBinding[] buffer)
        {
            return NamespaceBinding.EMPTY_ARRAY;
        }

        public override string GetAttributeValue(NamespaceUri uri, string local)
        {
            return null;
        }

        public override string GetAttributeValue(int fp)
        {
            return null;
        }

        public override void Copy(IReceiver receiver, int copyOptions, ILocation location)
        {
            bool typed = CopyOptions.Includes(copyOptions, CopyOptions.TYPE_ANNOTATIONS);
            ISchemaType type = typed ? GetSchemaType() : Untyped.INSTANCE;
            bool disallowNamespaceSensitiveContent = ((copyOptions & CopyOptions.TYPE_ANNOTATIONS) != 0) && ((copyOptions & CopyOptions.ALL_NAMESPACES) == 0);
            if (disallowNamespaceSensitiveContent)
            {
                try
                {
                    CheckNotNamespaceSensitiveElement(type, nodeNr);
                }
                catch (CopyNamespaceSensitiveException e)
                {
                    throw e.WithErrorCode(receiver.GetPipelineConfiguration().IsXSLT() ? "XTTE0950" : "XQTY0086");
                }
            }

            Func<NodeInfo, Object> informee = receiver.GetPipelineConfiguration().CopyInformee;
            if (informee != null)
            {
                object o = informee(this);
                if (o is ILocation)
                {
                    location = (ILocation)o;
                }
            }

            NamespaceMap namespaces;
            if ((copyOptions & CopyOptions.ALL_NAMESPACES) != 0)
            {

                // Don't bother with LOCAL_NAMESPACES because there aren't any
                namespaces = AllNamespaces;
            } // Bug 5616
            else if (!GetNamespaceUri().IsEmpty())
            {

                // Bug 5616
                namespaces = NamespaceMap.Of(GetPrefix(), GetNamespaceUri());
            }
            else
            {
                namespaces = NamespaceMap.EmptyMap();
            }

            receiver.StartElement(NameOfNode.MakeName(this), type, EmptyAttributeMap.GetInstance(), namespaces, location, ReceiverOption.NONE);
            receiver.Characters(UnicodeStringValue, location, ReceiverOption.NONE);
            receiver.EndElement();
        }

        public override bool HasChildNodes()
        {
            return true;
        }

        public override IAxisIterator IterateAxis(int axisNumber)
        {
            switch (axisNumber)
            {
                case AxisInfo.ATTRIBUTE:
                    return EmptyIterator.OfNodes();
                case AxisInfo.CHILD:
                case AxisInfo.DESCENDANT:
                    return SingleNodeIterator.MakeIterator(TextNode);
                case AxisInfo.DESCENDANT_OR_SELF:
                    return new OfNodes<NodeInfo>(new NodeInfo[] { this, TextNode });
                default:
                    return base.IterateAxis(axisNumber);
            }
        }

        public override IAxisIterator IterateAxis(int axisNumber, INodePredicate nodeTest)
        {
            switch (axisNumber)
            {
                case AxisInfo.ATTRIBUTE:
                    return EmptyIterator.OfNodes();
                case AxisInfo.CHILD:
                case AxisInfo.DESCENDANT:
                    return Navigator.FilteredSingleton(TextNode, nodeTest);
                case AxisInfo.DESCENDANT_OR_SELF:
                    IList<NodeInfo> list = new List<NodeInfo>(2);
                    if (nodeTest.Test(this))
                    {
                        list.Add(this);
                    }

                    if (nodeTest.Test(TextNode))
                    {
                        list.Add(TextNode);
                    }

                    return new NodeListIterator(list);
                default:
                    return base.IterateAxis(axisNumber, nodeTest);
            }
        }

        public override bool IsAncestorOrSelf(TinyNodeImpl d)
        {
            return this.Equals(d);
        }

        /// <summary>
        /// Inner class representing the text node; this is created on demand
        /// </summary>
        public class TinyTextualElementText : NodeInfo, SourceLocator
        {
            private readonly TinyTextualElement element;

            /// <summary>
            /// Set the system ID for the entity containing the node.
            /// </summary>
            public virtual UnicodeString UnicodeStringValue => element.UnicodeStringValue;

            /// <summary>
            /// Get a character string that uniquely identifies this node
            /// </summary>
            /// <summary>
            /// Get the system ID for the entity containing the node.
            /// </summary>
            /// <summary>
            /// Get the fingerprint of the node, used for matching names
            /// </summary>
            public virtual int Fingerprint => -1;

            /// <summary>
            /// Get a character string that uniquely identifies this node
            /// </summary>
            /// <summary>
            /// Get the system ID for the entity containing the node.
            /// </summary>
            /// <summary>
            /// Get the fingerprint of the node, used for matching names
            /// </summary>
            public virtual string DisplayName => "";

            /// <summary>
            /// Get a character string that uniquely identifies this node
            /// </summary>
            /// <summary>
            /// Get the system ID for the entity containing the node.
            /// </summary>
            /// <summary>
            /// Get the fingerprint of the node, used for matching names
            /// </summary>
            public virtual NamespaceMap AllNamespaces => null;

            /// <summary>
            /// Get a character string that uniquely identifies this node
            /// </summary>
            /// <summary>
            /// Get the system ID for the entity containing the node.
            /// </summary>
            /// <summary>
            /// Get the fingerprint of the node, used for matching names
            /// </summary>
            public virtual NodeInfo Root => element.Root;
            public TinyTextualElementText(TinyTextualElement element)
            {
                this.element = element;
            }

            public virtual bool HasFingerprint()
            {
                return true;
            }

            public virtual ITreeInfo GetTreeInfo()
            {
                return element.GetTreeInfo();
            }

            /// <summary>
            /// Set the system ID for the entity containing the node.
            /// </summary>
            public virtual void SetSystemId(string systemId)
            {
            }

            /// <summary>
            /// Set the system ID for the entity containing the node.
            /// </summary>
            public virtual int GetNodeKind()
            {
                return Types.Type.TEXT;
            }

            /// <summary>
            /// Set the system ID for the entity containing the node.
            /// </summary>
            public override bool Equals(object other)
            {
                return other is TinyTextualElementText && GetParent().Equals(((TinyTextualElementText)other).GetParent());
            }

            /// <summary>
            /// Set the system ID for the entity containing the node.
            /// </summary>
            public override int GetHashCode()
            {
                return GetParent().GetHashCode() ^ 0x01010101;
            }

            /// <summary>
            /// Get a character string that uniquely identifies this node
            /// </summary>
            public virtual void GenerateId(StringBuilder buffer)
            {
                element.GenerateId(buffer);
                buffer.Append("T");
            }

            /// <summary>
            /// Get a character string that uniquely identifies this node
            /// </summary>
            /// <summary>
            /// Get the system ID for the entity containing the node.
            /// </summary>
            public virtual string GetSystemId()
            {
                return element.GetSystemId();
            }

            /// <summary>
            /// Get a character string that uniquely identifies this node
            /// </summary>
            /// <summary>
            /// Get the system ID for the entity containing the node.
            /// </summary>
            public virtual string GetBaseURI()
            {
                return element.GetBaseURI();
            }

            /// <summary>
            /// Get a character string that uniquely identifies this node
            /// </summary>
            /// <summary>
            /// Get the system ID for the entity containing the node.
            /// </summary>
            public virtual int CompareOrder(NodeInfo other)
            {
                if (other.Equals(this))
                {
                    return 0;
                }
                else if (other.Equals(GetParent()))
                {
                    return 1;
                }
                else
                {
                    return GetParent().CompareOrder(other);
                }
            }

            /// <summary>
            /// Get a character string that uniquely identifies this node
            /// </summary>
            /// <summary>
            /// Get the system ID for the entity containing the node.
            /// </summary>
            /// <summary>
            /// Get the fingerprint of the node, used for matching names
            /// </summary>
            public virtual string GetPrefix()
            {
                return "";
            }

            /// <summary>
            /// Get a character string that uniquely identifies this node
            /// </summary>
            /// <summary>
            /// Get the system ID for the entity containing the node.
            /// </summary>
            /// <summary>
            /// Get the fingerprint of the node, used for matching names
            /// </summary>
            public virtual NamespaceUri GetNamespaceUri()
            {
                return NamespaceUri.NULL;
            }

            /// <summary>
            /// Get a character string that uniquely identifies this node
            /// </summary>
            /// <summary>
            /// Get the system ID for the entity containing the node.
            /// </summary>
            /// <summary>
            /// Get the fingerprint of the node, used for matching names
            /// </summary>
            public virtual string GetLocalPart()
            {
                return "";
            }

            /// <summary>
            /// Get a character string that uniquely identifies this node
            /// </summary>
            /// <summary>
            /// Get the system ID for the entity containing the node.
            /// </summary>
            /// <summary>
            /// Get the fingerprint of the node, used for matching names
            /// </summary>
            public virtual bool HasChildNodes()
            {
                return false;
            }

            /// <summary>
            /// Get a character string that uniquely identifies this node
            /// </summary>
            /// <summary>
            /// Get the system ID for the entity containing the node.
            /// </summary>
            /// <summary>
            /// Get the fingerprint of the node, used for matching names
            /// </summary>
            public virtual string GetAttributeValue(NamespaceUri uri, string local)
            {
                return null;
            }

            /// <summary>
            /// Get a character string that uniquely identifies this node
            /// </summary>
            /// <summary>
            /// Get the system ID for the entity containing the node.
            /// </summary>
            /// <summary>
            /// Get the fingerprint of the node, used for matching names
            /// </summary>
            public virtual int GetLineNumber()
            {
                return GetParent().GetLineNumber();
            }

            /// <summary>
            /// Get a character string that uniquely identifies this node
            /// </summary>
            /// <summary>
            /// Get the system ID for the entity containing the node.
            /// </summary>
            /// <summary>
            /// Get the fingerprint of the node, used for matching names
            /// </summary>
            public virtual int GetColumnNumber()
            {
                return GetParent().GetColumnNumber();
            }

            /// <summary>
            /// Get a character string that uniquely identifies this node
            /// </summary>
            /// <summary>
            /// Get the system ID for the entity containing the node.
            /// </summary>
            /// <summary>
            /// Get the fingerprint of the node, used for matching names
            /// </summary>
            public virtual ILocation SaveLocation()
            {
                return this;
            }

            /// <summary>
            /// Get a character string that uniquely identifies this node
            /// </summary>
            /// <summary>
            /// Get the system ID for the entity containing the node.
            /// </summary>
            /// <summary>
            /// Get the fingerprint of the node, used for matching names
            /// </summary>
            public virtual ISchemaType GetSchemaType()
            {
                return null;
            }

            /// <summary>
            /// Get a character string that uniquely identifies this node
            /// </summary>
            /// <summary>
            /// Get the system ID for the entity containing the node.
            /// </summary>
            /// <summary>
            /// Get the fingerprint of the node, used for matching names
            /// </summary>
            public virtual NamespaceBinding[] GetDeclaredNamespaces(NamespaceBinding[] buffer)
            {
                return null;
            }

            /// <summary>
            /// Get a character string that uniquely identifies this node
            /// </summary>
            /// <summary>
            /// Get the system ID for the entity containing the node.
            /// </summary>
            /// <summary>
            /// Get the fingerprint of the node, used for matching names
            /// </summary>
            public virtual IAtomicSequence Atomize()
            {
                return StringValue.MakeUntypedAtomic(UnicodeStringValue);
            }

            /// <summary>
            /// Get a character string that uniquely identifies this node
            /// </summary>
            /// <summary>
            /// Get the system ID for the entity containing the node.
            /// </summary>
            /// <summary>
            /// Get the fingerprint of the node, used for matching names
            /// </summary>
            public virtual IAxisIterator IterateAxis(int axisNumber)
            {
                switch (axisNumber)
                {
                    case AxisInfo.ANCESTOR:
                        return element.IterateAxis(AxisInfo.ANCESTOR_OR_SELF);
                    case AxisInfo.PRECEDING_OR_ANCESTOR:
                        return new Navigator.PrecedingEnumeration(this, true);
                    case AxisInfo.ANCESTOR_OR_SELF:
                        return new PrependAxisIterator(this, GetParent().IterateAxis(AxisInfo.ANCESTOR_OR_SELF));
                    case AxisInfo.FOLLOWING:
                        return new Navigator.FollowingEnumeration(this);
                    case AxisInfo.PRECEDING:
                        return new Navigator.PrecedingEnumeration(this, false);
                    case AxisInfo.PARENT:
                        return SingleNodeIterator.MakeIterator(GetParent());
                    case AxisInfo.ATTRIBUTE:
                    case AxisInfo.CHILD:
                    case AxisInfo.DESCENDANT:
                    case AxisInfo.FOLLOWING_SIBLING:
                    case AxisInfo.NAMESPACE:
                    case AxisInfo.PRECEDING_SIBLING:
                        return EmptyIterator.OfNodes();
                    case AxisInfo.SELF:
                    case AxisInfo.DESCENDANT_OR_SELF:
                        return SingleNodeIterator.MakeIterator(this);
                    default:
                        throw new ArgumentException("Unknown axis number " + axisNumber);
                }
            }

            /// <summary>
            /// Get a character string that uniquely identifies this node
            /// </summary>
            /// <summary>
            /// Get the system ID for the entity containing the node.
            /// </summary>
            /// <summary>
            /// Get the fingerprint of the node, used for matching names
            /// </summary>
            public virtual IAxisIterator IterateAxis(int axisNumber, INodePredicate predicate)
            {
                NodeTest nodeTest = Navigator.NodeTestFromPredicate(predicate);
                switch (axisNumber)
                {
                    case AxisInfo.ANCESTOR:
                        return GetParent().IterateAxis(AxisInfo.ANCESTOR_OR_SELF, nodeTest);
                    case AxisInfo.PRECEDING_OR_ANCESTOR:
                        return new Navigator.AxisFilter(new Navigator.PrecedingEnumeration(this, true), nodeTest);
                    case AxisInfo.ANCESTOR_OR_SELF:
                        return new Navigator.AxisFilter(new PrependAxisIterator(this, GetParent().IterateAxis(AxisInfo.ANCESTOR_OR_SELF)), nodeTest);
                    case AxisInfo.FOLLOWING:
                        return new Navigator.AxisFilter(new Navigator.FollowingEnumeration(this), nodeTest);
                    case AxisInfo.PRECEDING:
                        return new Navigator.AxisFilter(new Navigator.PrecedingEnumeration(this, false), nodeTest);
                    case AxisInfo.PARENT:
                        return Navigator.FilteredSingleton(GetParent(), nodeTest);
                    case AxisInfo.ATTRIBUTE:
                    case AxisInfo.CHILD:
                    case AxisInfo.DESCENDANT:
                    case AxisInfo.FOLLOWING_SIBLING:
                    case AxisInfo.NAMESPACE:
                    case AxisInfo.PRECEDING_SIBLING:
                        return EmptyIterator.OfNodes();
                    case AxisInfo.SELF:
                    case AxisInfo.DESCENDANT_OR_SELF:
                        return Navigator.FilteredSingleton(this, nodeTest);
                    default:
                        throw new ArgumentException("Unknown axis number " + axisNumber);
                }
            }

            /// <summary>
            /// Get a character string that uniquely identifies this node
            /// </summary>
            /// <summary>
            /// Get the system ID for the entity containing the node.
            /// </summary>
            /// <summary>
            /// Get the fingerprint of the node, used for matching names
            /// </summary>
            public virtual NodeInfo GetParent()
            {
                return element;
            }

            /// <summary>
            /// Get a character string that uniquely identifies this node
            /// </summary>
            /// <summary>
            /// Get the system ID for the entity containing the node.
            /// </summary>
            /// <summary>
            /// Get the fingerprint of the node, used for matching names
            /// </summary>
            /// <summary>
            /// Copy the node to a given Outputter
            /// </summary>
            public virtual void Copy(IReceiver @out, int copyOptions, ILocation locationId)
            {
                @out.Characters(UnicodeStringValue, locationId, ReceiverOption.NONE);
            }

            // [Fix-PhaseB-TinyTextualElement-Reinclude inner-class members]
            // NodeInfo / IItem / IGroundedValue / ISequence / ILocation interface obligations. The C# port
            // flattened the upstream Java `default` methods into plain abstract members, so this direct
            // interface implementor must supply them. Bodies mirror the upstream default-method bodies /
            // the equivalent TinyNodeImpl overrides, specialized for a text node (delegating to the parent
            // element where the upstream default does). Lib/Expr.Parser types fully qualified (the file's
            // usings do not import them).
            public virtual Configuration GetConfiguration()
            {
                return GetTreeInfo().GetConfiguration();
            }

            public virtual bool IsSameNodeInfo(NodeInfo other)
            {
                return Equals(other);
            }

            public virtual string GetPublicId()
            {
                return null;
            }

            public virtual string GetURI()
            {
                return GetNamespaceUri().ToString();
            }

            public virtual IEnumerable<NodeInfo> Children()
            {
                var __it = IterateAxis(AxisInfo.CHILD);
                for (var __n = __it.Next(); __n != null; __n = __it.Next()) { yield return __n; }
            }

            public virtual IEnumerable<NodeInfo> Children(INodePredicate filter)
            {
                var __it = IterateAxis(AxisInfo.CHILD, filter);
                for (var __n = __it.Next(); __n != null; __n = __it.Next()) { yield return __n; }
            }

            public virtual IAttributeMap Attributes()
            {
                throw new NotImplementedException();
            }

            public virtual void Deliver(IReceiver receiver, ParseOptions options)
            {
                throw new NotImplementedException();
            }

            public virtual IActiveSource AsActiveSource()
            {
                return new NodeSource(this);
            }

            public virtual bool IsId()
            {
                return false;
            }

            public virtual bool IsIdref()
            {
                return false;
            }

            public virtual bool IsNilled()
            {
                return false;
            }

            public virtual bool IsStreamed()
            {
                return false;
            }

            public virtual string ToShortString()
            {
                return "text(\"" + UnicodeStringValue + "\")";
            }

            public virtual Genre GetGenre()
            {
                return Genre.NODE;
            }

            public virtual IItem Head()
            {
                return this;
            }

            public virtual string GetStringValue()
            {
                return UnicodeStringValue.ToString();
            }

            public virtual ISequenceIterator Iterate()
            {
                return SingletonIterator.MakeIterator(this);
            }

            SingletonIterator IItem.Iterate()
            {
                return (SingletonIterator)SingletonIterator.MakeIterator(this);
            }

            public virtual IItem ItemAt(int n)
            {
                return n == 0 ? (IItem)this : null;
            }

            public virtual IGroundedValue Subsequence(int start, int length)
            {
                throw new NotImplementedException();
            }

            public virtual int GetLength()
            {
                return 1;
            }

            public virtual bool EffectiveBooleanValue()
            {
                return ExpressionTool.EffectiveBooleanValue(Iterate());
            }

            public virtual IGroundedValue Reduce()
            {
                return this;
            }

            public virtual IGroundedValue Materialize()
            {
                return this;
            }

            public virtual IEnumerable<IItem> AsIterable()
            {
                throw new NotImplementedException();
            }

            public virtual bool ContainsNode(NodeInfo sought) => OutSmart.DAXon.Expressions.SingletonIntersectExpression.ContainsNode(((OutSmart.DAXon.Model.ISequence)this).Iterate(), sought); // upstream GroundedValue default

            public virtual IGroundedValue Concatenate(params IGroundedValue[] others)
            {
                // upstream GroundedValue default: chain this value's items with the others
                var __chain = new OutSmart.DAXon.Collections.Zeno.ZenoChain<OutSmart.DAXon.Model.IItem>().AddAll(((OutSmart.DAXon.Model.IGroundedValue)this).AsIterable());
                foreach (OutSmart.DAXon.Model.IGroundedValue __v in others)
                    __chain = __chain.AddAll(__v.AsIterable());
                return new OutSmart.DAXon.Collections.Zeno.ZenoSequence(__chain);
            }

            public virtual ISequence MakeRepeatable()
            {
                return this;
            }
            IEnumerable<NodeInfo> NodeInfo.Children() => Children();
            IEnumerable<NodeInfo> NodeInfo.Children(INodePredicate arg0) => Children(arg0);
            IEnumerable<IItem> IGroundedValue.AsIterable() => new IItem[] { this };
        }
    }
}
