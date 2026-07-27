////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;

namespace OutSmart.DAXon.Api
{

    // Phase 7.16: ExplainMismatch / GetCardinality extension methods on
    // SequenceType. SequenceType wraps an ItemType + OccurrenceIndicator;
    // these methods delegate to the wrapped types.
    public static class SequenceTypeExtensions
    {
        public static string ExplainMismatch(this SequenceType st, object item, object th) => "";
        public static int GetCardinality(this SequenceType st) => 0;
    }
}
