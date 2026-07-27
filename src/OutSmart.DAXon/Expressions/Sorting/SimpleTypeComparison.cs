////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;

namespace OutSmart.DAXon.Expressions.Sorting
{
    // Runtime 2026-06-10: ContextFreeAtomicComparer stub REMOVED (no IAtomicComparer -> InvalidCast in fn:max/min
    // PreAllocateComparer; Compare=>0 made everything equal). Real class re-included (csproj). CodepointCollatingComparer twin.
    // Runtime 2026-06-10: CodepointCollatingComparer stub REMOVED (no IAtomicComparer -> InvalidCast; Compare=>0 made everything equal). Real class re-included (csproj).
    // Runtime 2026-06-11: DoubleSortComparer hollow stub REMOVED (Compare=>0 made every numeric sort
    // key equal -> xsl:sort data-type=number left input order untouched). Real file re-included (batch3).
    public class SimpleTypeComparison
    {
        private static readonly SimpleTypeComparison _i = new SimpleTypeComparison();
        public static SimpleTypeComparison GetInstance() => _i;
        public bool Equal(object a, object b) => object.Equals(a, b);
        public int Hash(object key) => key?.GetHashCode() ?? 0;
    }
}
