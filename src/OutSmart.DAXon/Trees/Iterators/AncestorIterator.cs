////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;

namespace OutSmart.DAXon.Trees.Iterators
{
    // Runtime 2026-06-10: DescendingRangeIterator hollow stub REMOVED. Real class re-included (csproj).
    // Runtime 2026-06-23 (TIER-3 fn batch 1d, fn:path / ancestor axis): the hollow AncestorIterator.Next()=>NIE +
    // explicit ISequenceIterator.Next()=>null made the ancestor / ancestor-or-self axes return EMPTY (broke
    // Path_1.MakePath and ANY ancestor::/ancestor-or-self:: navigation). The real
    // poc/output/full/tree/tiny/AncestorIterator.cs is excluded and, when re-included, fails CS0535 -- it provides
    // only `public NodeInfo Next()` (the IAxisIterator slot, since IAxisIterator redeclares `new NodeInfo Next()`)
    // and never the ISequenceIterator.Next():IItem / Dispose bridge. Rather than re-include + patch + de-conflict the
    // Tree.Iter (stub) vs Tree.Tiny (real) namespace split, this durable type is a FAITHFUL port of upstream Saxon
    // 12.9 AncestorIterator (walk GetParent() applying the NodeTest) plus the covariant-redirect bridge (mirrors
    // DocumentOrderIterator). NOT hollow -- reproduces upstream behaviour exactly.
    public sealed class AncestorIterator : IAxisIterator
    {
        private readonly NodeInfo startNode;
        private NodeInfo current;
        private readonly NodeTest test;
        public AncestorIterator(NodeInfo node, NodeTest nodeTest) { test = nodeTest; startNode = node; current = startNode; }
        public NodeInfo Next()
        {
            if (current == null) { return null; }
            NodeInfo node = current.GetParent();
            while (node != null && !test.Test(node)) { node = node.GetParent(); }
            return current = node;
        }
        IItem ISequenceIterator.Next() => Next();
        void ISequenceIterator.Dispose() { }
        void IDisposable.Dispose() { }
    }
}
