////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;

namespace OutSmart.DAXon.Types
{
    public static class AlphaCode
    {
        public static string FromItemType(ItemType t) => "";
        public static ItemType ToItemType(string s) => throw new NotImplementedException("STUB: AlphaCode.ToItemType not ported (excluded stub)");
        // Phase 7.8: AlphaCode.FromSequenceType used by export visitors.
        public static string FromSequenceType(object t) => "";
    }
}
