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
    // IntSet.Count extension method -- paulirwin bulk-rewrote .Size() -> .Count for
    // collections but Saxon's IntSet has Size() method, not Count property.
    internal static class IntSetExt
    {
        public static int Count(this IntSet self) => self?.Size() ?? 0;
    }
}
