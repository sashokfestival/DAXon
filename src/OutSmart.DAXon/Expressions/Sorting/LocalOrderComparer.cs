////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal.Collections;

namespace OutSmart.DAXon.Expressions.Sorting
{
    // Phase 7.29: COLLATION_KEY_NaN typed as IAtomicMatchKey (was const string, but
    // callers assign return of GetMapKey()/AsMapKey() which return IAtomicMatchKey).
    // Runtime 2026-06-11: AtomicSortComparer hollow stub REMOVED (no IAtomicComparer surface; default
    // xsl:sort comparer fell through to nothing). Real expr/sort/AtomicSortComparer.cs re-included (batch3).
    // Phase B: real LocalOrderComparer.cs (excluded) is a Comparator<NodeInfo> whose
    // Compare(NodeInfo,NodeInfo) => a.CompareOrder(b); the stub lacked it so KeyIndex's
    // comparer.Compare(curr, nodes[i]) mis-resolved to a 4-arg extension (CS7036). Add the real method.
    // I5 B4a (2026-06-12): implements System.Collections.Generic.IComparer<NodeInfo> (was the now-retired
    // OutSmart.DAXon.Internal.Collections.Comparator<NodeInfo>) so DocumentSorter's stage-0-renamed
    // `(IComparer<NodeInfo>)LocalOrderComparer.GetInstance()` cast (DocumentSorter.cs:38, building the
    // document-order sort for an xsl:for-each select) no longer throws InvalidCastException. Compare already
    // delegates to NodeInfo.CompareOrder (faithful to the excluded real LocalOrderComparer.cs, which is
    // `: Comparator<NodeInfo>` with the same single Compare member).
    public class LocalOrderComparer : IComparer<NodeInfo>
    {
        private static readonly LocalOrderComparer _instance = new LocalOrderComparer();
        public LocalOrderComparer() { }
        public static LocalOrderComparer GetInstance() => _instance;
        public int Compare(NodeInfo a, NodeInfo b) => a.CompareOrder(b);
    }
}
