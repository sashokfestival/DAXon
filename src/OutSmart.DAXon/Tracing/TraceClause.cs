////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Expressions.Flwor;

namespace OutSmart.DAXon.Tracing
{
    public class TraceClause
    {
        public TraceClause() { }
        public TraceClause(object e) { }
        public TraceClause(object e, object f) { }
        public static implicit operator Clause(TraceClause x) => throw new NotImplementedException("STUB: TraceClause.Clause not ported (excluded stub)");
    }
}
