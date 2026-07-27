////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;

namespace OutSmart.DAXon.Trees.Iterators
{
    // Runtime 2026-06-10: Tree.Iter.InsertIterator bridge stub REMOVED (delegated to the hollow Next()=>null
    // nested stub - map:put/map:merge duplicate-key combining silently produced EMPTY sequences). MapFunctionSet
    // bare `new InsertIterator` sites repointed to the real nested InsertBefore.InsertIterator (42zx865).
    // Runtime 2026-06-10: RangeIterator hollow stub REMOVED (ContainsEq=>false broke range comparisons). Real abstract base re-included (csproj).
    // Runtime 2026-06-10: DocumentOrderIterator stub REMOVED (no ISequenceIterator -> InvalidCast in fn:document). Real file re-included.
    // Runtime 2026-06-23 (TIER-3 fn batch 1d, fn:path): the hollow PrependAxisIterator.Next()=>NIE broke fn:path()
    // -- node.IterateAxis(ANCESTOR_OR_SELF) returns a PrependAxisIterator and Path_1.MakePath pulls it. The real
    // poc/output/full/tree/iter/{PrependSequenceIterator,PrependAxisIterator}.cs CANNOT be re-included cleanly: the
    // real PrependSequenceIterator lives in OutSmart.DAXon.Trees.Iterators, but a durable stub of the same name lives in
    // OutSmart.DAXon.Expressions.Instructions (for ForEach), and expr/compat/GeneralComparison10.cs imports BOTH namespaces ->
    // re-include yields CS0104 ambiguity (4 sites) + a behaviour change in ForEach's separator path. To avoid that
    // cascade this durable type is a FAITHFUL port of upstream Saxon 12.9 PrependSequenceIterator.next() (return the
    // prepended `start` once, then delegate to the base iterator) + PrependAxisIterator.next() (narrow to NodeInfo).
    // NOT hollow -- reproduces upstream behaviour exactly. IAxisIterator redeclares `new NodeInfo Next()` hiding
    // ISequenceIterator.Next():IItem, so the public NodeInfo Next() implements the axis slot and the explicit
    // ISequenceIterator.Next() redirects to it (mirrors DocumentOrderIterator's covariant-redirect idiom).
    public class PrependAxisIterator : IAxisIterator
    {
        private IItem start;
        private readonly ISequenceIterator @base;
        public PrependAxisIterator(NodeInfo start, ISequenceIterator @base) { this.start = start; this.@base = @base; }
        public NodeInfo Next()
        {
            if (start != null) { IItem temp = start; start = null; return (NodeInfo)temp; }
            return (NodeInfo)@base.Next();
        }
        IItem ISequenceIterator.Next() => Next();
        void ISequenceIterator.Dispose() { @base.Dispose(); }
        void IDisposable.Dispose() { @base.Dispose(); }
    }
}
