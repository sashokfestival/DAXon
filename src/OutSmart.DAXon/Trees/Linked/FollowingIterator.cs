////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Trees.Iterators;

namespace OutSmart.DAXon.Trees.Linked
{
    // Phase 5: bulk iterator stubs for NodeImpl/ParentNodeImpl axis-iterator allocations.
    // Bare class (not IAxisIterator) Ã¢â‚¬â€ extending interface forces explicit ISequenceIterator/IDisposable
    // implementations that cascade further. Callers convert via implicit cast and we accept type mismatches
    // at runtime (compile-only goal).
    public class FollowingIterator : IAxisIterator
    {
        public FollowingIterator(object n, object t) { }
        public FollowingIterator(object a, object b, object c, object d) { }
        public NodeInfo Next() => throw new NotImplementedException("STUB: FollowingIterator.Next not ported (excluded stub)");
        IItem ISequenceIterator.Next() => null;
        void ISequenceIterator.Dispose() { }
        void IDisposable.Dispose() { }
    }
}
