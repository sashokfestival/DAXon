////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;

namespace OutSmart.DAXon.Collections
{
    // Phase 7.8c: IntSetPredicate/IntIntersectionPredicate/IntExceptPredicate stubs - real excluded.
    // Runtime 2026-06-10: IntSetPredicate hollow stub #2 REMOVED (ALWAYS_TRUE.Test=>false!). Real file re-included.
    public static class IntIntersectionPredicate
    {
        public static IIntPredicateProxy MakeIntersection(IIntPredicateProxy a, IIntPredicateProxy b) => a;
    }
}
