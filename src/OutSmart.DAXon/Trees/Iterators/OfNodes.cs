////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using System;

namespace OutSmart.DAXon.Trees.Iterators
{
    // Runtime 2026-06-10: ArrayIterator hollow stub REMOVED (no MakeSliceIterator; real file re-included for SubsequenceIterator, batch 4).
    // Phase 7.8 r3: OfNodes<T> at namespace level for `new OfNodes<NodeImpl>(...)` bare refs in ParentNodeImpl.
    // 2026-06-02: made a REAL IAxisIterator (the live ParentNodeImpl.IterateChildren casts it to IAxisIterator,
    // which the empty stub failed). Iterates the node array directly -- equivalent to the excluded
    // ArrayIterator.OfNodes : Of<N>, IAxisIterator, without dragging in the ArrayIterator/Of/Reverse cascade.
    public class OfNodes<N> : IAxisIterator
    {
        private readonly N[] _items;
        private int _pos;
        public OfNodes() { _items = new N[0]; }
        public OfNodes(N[] items) { _items = items ?? new N[0]; }
        public NodeInfo Next() => _pos < _items.Length ? (NodeInfo)(object)_items[_pos++] : null;
        IItem ISequenceIterator.Next() => Next();
        void ISequenceIterator.Dispose() { }
        void IDisposable.Dispose() { }
    }
}
