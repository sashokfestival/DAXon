////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2013-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Trees;
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
namespace OutSmart.DAXon.Transformation
{
    public class KeyIndex
    {

        // The entry in an index is either a NodeInfo or a List<NodeInfo>
        private readonly Dictionary<IAtomicMatchKey, object> index;
        private UType keyTypesPresent = UType.VOID;
        private UType keyTypesConvertedFromUntyped = UType.STRING_LIKE;
        private HashSet<UnicodeString> untypedKeys;
        private ConversionRules rules;
        private int implicitTimezone;
        private IStringCollator collation;
        private readonly long creatingThread;
        private Status status;

        public virtual Dictionary<IAtomicMatchKey, object> UnderlyingMap => index;
        public KeyIndex(bool isRangeKey)
        {
            if (isRangeKey)
            {
                index = new Dictionary<IAtomicMatchKey, object>(); // TODO: should be using an IXPathComparable here, for sorting purposes?
            }
            else
            {
                index = new Dictionary<IAtomicMatchKey, object>(100);
            }

            creatingThread = Environment.CurrentManagedThreadId;
            status = Status.UNDER_CONSTRUCTION; //Instrumentation.count("Building KeyIndex");
        }

        public virtual bool IsCreatedInThisThread()
        {
            return creatingThread == Environment.CurrentManagedThreadId;
        }

        public virtual Status GetStatus()
        {
            return status;
        }

        public virtual void SetStatus(Status status)
        {
            this.status = status;
        }

        public virtual void BuildIndex(KeyDefinitionSet keySet, ITreeInfo doc, IXPathContext context)
        {
            IList<KeyDefinition> definitions = keySet.KeyDefinitions;

            // There may be multiple xsl:key definitions with the same name. Index them all.
            for (int k = 0; k < definitions.Count; k++)
            {
                ConstructIndex(doc, definitions[k], context, k == 0);
            }

            this.rules = context.GetConfiguration().GetConversionRules();
            this.implicitTimezone = context.GetImplicitTimezone();
            this.collation = definitions[0].Collation;
        }

        private void ConstructIndex(ITreeInfo doc, KeyDefinition keydef, IXPathContext context, bool isFirst)
        {

            Patterns.Pattern match = keydef.Match;

            //NodeInfo curr;
            XPathContextMajor xc = context.NewContext();
            xc.Origin = keydef;
            xc.SetCurrentComponent(keydef.DeclaringComponent);
            xc.TemporaryOutputState = StandardNames.XSL_KEY;

            // The use expression (or sequence constructor) may contain local variables.
            SlotManager map = keydef.GetStackFrameMap();
            if (map != null)
            {
                xc.OpenStackFrame(map);
            }

            SequenceTool.Supply(match.SelectNodes(doc, xc), (node) => ProcessNode((NodeInfo)node, keydef, xc, isFirst));
        }

        private void ProcessNode(NodeInfo node, KeyDefinition keydef, IXPathContext xc, bool isFirst)
        {

            // Make the node we are testing the context node,
            // with context position and context size set to 1
            ManualIterator si = new ManualIterator(node);
            xc.SetCurrentIterator(si);
            IStringCollator collation = keydef.Collation;
            int implicitTimezone = xc.GetImplicitTimezone();

            // Evaluate the "use" expression against this context node
            IPullEvaluator use = keydef.ObtainUseEvaluator();
            ISequenceIterator useval = use.Iterate(xc);
            if (keydef.IsComposite())
            {
                IList<IAtomicMatchKey> amks = new List<IAtomicMatchKey>(4);
                SequenceTool.Supply(useval, (keyVal) => amks.Add(GetCollationKey((AtomicValue)keyVal, collation, implicitTimezone)));
                AddEntry(new CompositeAtomicMatchKey(amks), node, isFirst);
            }
            else
            {
                AtomicValue keyVal;
                while ((keyVal = (AtomicValue)useval.Next()) != null)
                {
                    if (keyVal.IsNaN())
                    {
                        continue;
                    }

                    UType actualUType = keyVal.GetUType();
                    if (!keyTypesPresent.Subsumes(actualUType))
                    {
                        keyTypesPresent = keyTypesPresent.Union(actualUType);
                    }

                    IAtomicMatchKey amk = GetCollationKey(keyVal, collation, implicitTimezone);
                    if (actualUType.Equals(UType.UNTYPED_ATOMIC) && keydef.IsConvertUntypedToOther())
                    {
                        if (untypedKeys == null)
                        {
                            untypedKeys = new HashSet<UnicodeString>(20);
                        }

                        untypedKeys.Add(keyVal.UnicodeStringValue);
                    }

                    AddEntry(amk, node, isFirst);
                }
            }
        }

        private void AddEntry(IAtomicMatchKey val, NodeInfo curr, bool isFirst)
        {
            object value = index.Get(val);
            if (value == null)
            {

                // this is the first node with this key value; we store the entry as a singleton
                // node to avoid the overhead of creating a list
                index.Put(val, curr);
            }
            else
            {
                IList<NodeInfo> nodes;
                if (value is NodeInfo)
                {

                    // replace the singleton key entry with a list-valued key entry
                    nodes = new List<NodeInfo>(4);
                    nodes.Add((NodeInfo)value);
                    index.Put(val, nodes);
                }
                else
                {
                    nodes = (IList<NodeInfo>)value;
                }


                // this is not the first node with this key value.
                // add the node to the list of nodes for this key,
                // unless it's already there
                if (isFirst)
                {

                    // if this is the first index definition that we're processing,
                    // then this node must be after all existing nodes in document
                    // order, or the same node as the last existing node
                    if (nodes[nodes.Count - 1] != curr)
                    {
                        nodes.Add(curr);
                    }
                }
                else
                {

                    // otherwise, we need to insert the node at the correct
                    // position in document order. This code does an insertion sort:
                    // not ideal for performance, but it's very unusual to have more than
                    // one key definition for a key. We start looking at the end because
                    // it's most likely that the new node will come after all the others.
                    // See bug 2092 in saxonica.plan.io
                    LocalOrderComparer comparer = LocalOrderComparer.GetInstance();
                    bool found = false;
                    for (int i = nodes.Count - 1; i >= 0; i--)
                    {
                        int d = comparer.Compare(curr, nodes[i]);
                        if (d >= 0)
                        {
                            if (d == 0)
                            {
                            }
                            else
                            {

                                // add the node at this position
                                nodes.Add(i + 1, curr);
                            }

                            found = true;
                            break;
                        } // else continue round the loop
                    }


                    // if we're still here, add the new node at the start
                    if (!found)
                    {
                        nodes.Add(0, curr);
                    }
                }
            }
        }

        public virtual void ReindexUntypedValues(BuiltInAtomicType type)
        {
            UType uType = type.GetUType();
            if (UType.STRING_LIKE.Subsumes(uType))
            {
                return;
            }

            if (UType.NUMERIC.Subsumes(uType))
            {
                type = BuiltInAtomicType.DOUBLE;
            }

            StringConverter converter = type.GetStringConverter(rules);
            foreach (UnicodeString v in untypedKeys)
            {
                IAtomicMatchKey uk = GetCollationKey(new StringValue(v), collation, implicitTimezone);
                AtomicValue convertedValue = converter.ConvertString(v).AsAtomic();
                IAtomicMatchKey amk = GetCollationKey(convertedValue, collation, implicitTimezone);
                object value = index.Get(uk);
                if (value is NodeInfo)
                {
                    AddEntry(amk, ((NodeInfo)value), false);
                }
                else
                {
                    IList<NodeInfo> nodes = (IList<NodeInfo>)value;
                    foreach (NodeInfo node in nodes)
                    {
                        AddEntry(amk, node, false);
                    }
                }
            }
        }

        public virtual bool IsEmpty()
        {
            return index.IsEmpty();
        }

        public virtual ISequenceIterator GetNodes(AtomicValue soughtValue)
        {
            if (untypedKeys != null && !keyTypesConvertedFromUntyped.Subsumes(soughtValue.GetUType()))
            {
                ReindexUntypedValues(soughtValue.PrimitiveType);
                keyTypesConvertedFromUntyped = keyTypesConvertedFromUntyped.Union(soughtValue.PrimitiveType.GetUType());
            }

            if (soughtValue.IsUntypedAtomic())
            {
                IList<NodeInfo> resultNodes = new List<NodeInfo>();
                int counter = 0;
                foreach (PrimitiveUType type in keyTypesPresent.Decompose())
                {
                    IAtomicType targetType = (IAtomicType)type.ToItemType();
                    AtomicValue converted = (AtomicValue)Converter.Convert(soughtValue, targetType, rules);
                    object value = index.Get(GetCollationKey(converted, collation, implicitTimezone));
                    if (value != null)
                    {
                        counter++;
                        if (value is NodeInfo)
                        {
                            resultNodes.Add(((NodeInfo)value));
                        }
                        else
                        {
                            resultNodes.AddAll((IList<NodeInfo>)value);
                        }
                    }
                }

                // No (IList<IItem>) cast: .NET generic lists are invariant, a List<NodeInfo> is not an
                // IList<IItem> (threw InvalidCastException on untyped-key key() lookups).
                ISequenceIterator result = new ListIterator.Of<NodeInfo>(resultNodes);
                if (counter > 1)
                {
                    result = new DocumentOrderIterator(result, GlobalOrderComparer.GetInstance());
                }

                return result;
            }
            else
            {
                object value = index.Get(GetCollationKey(soughtValue, collation, implicitTimezone));
                return EntryIterator(value);
            }
        }

        private ISequenceIterator EntryIterator(object value)
        {
            if (value == null)
            {
                return EmptyIterator.OfNodes();
            }
            else if (value is NodeInfo)
            {
                return SingleNodeIterator.MakeIterator((NodeInfo)value);
            }
            else
            {
                IList<NodeInfo> nodes = (IList<NodeInfo>)value;
                return new NodeListIterator(nodes);
            }
        }

        public virtual ISequenceIterator GetComposite(ISequenceIterator soughtValue)
        {
            IList<IAtomicMatchKey> amks = new List<IAtomicMatchKey>(4);
            SequenceTool.Supply(soughtValue, (keyVal) => amks.Add(GetCollationKey((AtomicValue)keyVal, collation, implicitTimezone)));
            object value = index.Get(new CompositeAtomicMatchKey(amks));
            return EntryIterator(value);
        }

        private static IAtomicMatchKey GetCollationKey(AtomicValue value, IStringCollator collation, int implicitTimezone)
        {
            if (UType.STRING_LIKE.Subsumes(value.GetUType()))
            {
                if (collation == null)
                {
                    return value.UnicodeStringValue.Tidy();
                }
                else
                {
                    return collation.GetCollationKey(value.UnicodeStringValue);
                }
            }
            else
            {
                return value.GetXPathMatchKey(collation, implicitTimezone);
            }
        }
        public enum Status
        {
            UNDER_CONSTRUCTION,
            BUILT,
            FAILED
        }

        private class CompositeAtomicMatchKey : IAtomicMatchKey
        {
            private readonly IList<IAtomicMatchKey> keys;
            public CompositeAtomicMatchKey(IList<IAtomicMatchKey> keys)
            {
                this.keys = keys;
            }

            public virtual AtomicValue AsAtomic()
            {
                throw new NotSupportedException();
            }

            public override bool Equals(object obj)
            {
                if (obj is CompositeAtomicMatchKey && ((CompositeAtomicMatchKey)obj).keys.Count == keys.Count)
                {
                    IList<IAtomicMatchKey> keys2 = ((CompositeAtomicMatchKey)obj).keys;
                    for (int i = 0; i < keys.Count; i++)
                    {
                        if (!keys[i].Equals(keys2[i]))
                        {
                            return false;
                        }
                    }

                    return true;
                }

                return false;
            }

            public override int GetHashCode()
            {
                int h = 0x1ab27cd6;
                foreach (IAtomicMatchKey amk in keys)
                {
                    h ^= amk.GetHashCode();
                    h = h << 1;
                }

                return h;
            }
        }
    }
}