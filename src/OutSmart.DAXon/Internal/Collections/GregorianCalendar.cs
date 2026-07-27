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
    public class GregorianCalendar : Calendar
    {
        public const int AD = 1;
        public const int BC = 0;
        public new int Year, Month, Day, Hour, Minute, Second, Millisecond;
        public GregorianCalendar() { }
        public GregorianCalendar(int year, int month, int day) { Year = year; Month = month; Day = day; }
        public GregorianCalendar(TimeZone tz) { }
        public void SetGregorianChange(global::System.DateTime date) { }
        public void SetGregorianChange(object date) { }
        public new global::System.DateTime GetTime() => new global::System.DateTime(Year > 0 ? Year : 1, Month > 0 ? Month : 1, Day > 0 ? Day : 1, Hour, Minute, Second);
    }
}
