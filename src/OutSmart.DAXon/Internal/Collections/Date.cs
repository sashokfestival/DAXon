////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Misc Java stdlib types Saxon references but we don't yet shim individually.

using System;
using System.Collections.Generic;
using System.IO;

namespace OutSmart.DAXon.Internal.Collections
{
    // Phase 5: Java's Util.Date (legacy timestamp). GetTime returns ms since epoch.
    public class Date
    {
        private readonly global::System.DateTime _value;
        public Date() { _value = global::System.DateTime.UtcNow; }
        public Date(long millis)
        {
            var epoch = new global::System.DateTime(1970, 1, 1, 0, 0, 0, global::System.DateTimeKind.Utc);
            // Java's Date accepts the full long range; .NET DateTime does not. Callers use Long.MIN_VALUE as a
            // "far past" sentinel (e.g. GregorianCalendar.setGregorianChange for a proleptic calendar), so clamp
            // an out-of-range millis to DateTime.Min/Max rather than throwing ArgumentOutOfRangeException.
            try { _value = epoch.AddMilliseconds(millis); }
            catch (global::System.ArgumentOutOfRangeException) { _value = millis < 0 ? global::System.DateTime.MinValue : global::System.DateTime.MaxValue; }
        }
        public long GetTime() => (long)(_value - new global::System.DateTime(1970, 1, 1, 0, 0, 0, global::System.DateTimeKind.Utc)).TotalMilliseconds;
        public override string ToString() => _value.ToString("o");
    }
}
