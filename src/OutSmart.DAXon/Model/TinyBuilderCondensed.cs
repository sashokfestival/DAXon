////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Events;

// Phase 5: batch stubs for long-tail CS0246 missing types (30+ types, ~120 errors). Minimal
// `public class X { }` stubs in inferred namespaces.

namespace OutSmart.DAXon.Model
{
    public class TinyBuilderCondensed
    {
        public TinyBuilderCondensed() { }
        public TinyBuilderCondensed(object pipe) { }
        public void SetStatistics(object stats) { }
        public static implicit operator Builder(TinyBuilderCondensed x) => throw new NotImplementedException("STUB: TinyBuilderCondensed.Builder not ported (excluded stub)");
    }
}
