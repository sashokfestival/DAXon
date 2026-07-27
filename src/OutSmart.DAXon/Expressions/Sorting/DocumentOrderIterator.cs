////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Expressions.Sorting
{
    public sealed class DocumentOrderIterator : ISequenceIterator
    {
        private readonly ISequenceIterator iterator;
        private readonly List<NodeInfo> sequence; // explicit type ArrayList used so C# List.Sort() is available
        private NodeInfo current = null;
        public DocumentOrderIterator(ISequenceIterator @base, IComparer<NodeInfo> comparer)
        {
            // 0/1-item inputs (typical: a docOrder wrapper around a singleton path inside a per-item
            // lambda) skip the list+sort machinery — ordering/dedup of at most one node is the identity.
            // The non-node XPTY0004 check is preserved for every item on both paths.
            IItem first = @base.Next();
            if (first == null)
            {
                sequence = null;
                iterator = EmptyIterator.GetInstance();
                return;
            }

            if (!(first is NodeInfo))
            {
                throw new XPathException("Item in input for sorting is not a node: " + Err.Depict(first), "XPTY0004");
            }

            IItem second = @base.Next();
            if (second == null)
            {
                sequence = null;
                iterator = SingletonIterator.MakeIterator(first);
                return;
            }

            int len = SequenceTool.SupportsGetLength(@base) ? SequenceTool.GetLength(@base) : 50;
            sequence = new List<NodeInfo>(len < 2 ? 2 : len);
            sequence.Add((NodeInfo)first);
            IItemConsumer<IItem> add = (item) =>
            {
                if (item is NodeInfo)
                {
                    sequence.Add((NodeInfo)item);
                }
                else
                {
                    throw new XPathException("Item in input for sorting is not a node: " + Err.Depict(item), "XPTY0004");
                }
            };
            add(second);
            SequenceTool.Supply(@base, add);
            sequence.Sort(comparer);
            iterator = new NodeListIterator(sequence);
        }

        // Implement the ISequenceIterator as a wrapper around the underlying iterator
        // over the sequenceExtent, but looking ahead to remove duplicates.
        public NodeInfo Next()
        {
            while (true)
            {
                NodeInfo next = (NodeInfo)iterator.Next();
                if (next == null)
                {
                    current = null;
                    return null;
                }

                if (!next.Equals(current))
                {
                    current = next;
                    return current;
                }
            }
        }
        IItem ISequenceIterator.Next() => Next(); // redirect StubGen hollow to the real covariant Next(); default = silent empty iteration
        public void Dispose() { }
    }
}

