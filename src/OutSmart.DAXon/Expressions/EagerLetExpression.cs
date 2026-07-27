////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;

namespace OutSmart.DAXon.Expressions
{
    // Runtime 2026-06-10: TailExpression hollow stub REMOVED (GetItemType=>null NRE-d Atomizer on every subsequence/positional-tail rewrite). Real file re-included (batch 4).
    public class EagerLetExpression
    {
        public EagerLetExpression() { }
        public static implicit operator LetExpression(EagerLetExpression x) => throw new NotImplementedException("STUB: EagerLetExpression.LetExpression not ported (excluded stub)");
    }
}
