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

    public class Calendar
    {
        // Java Calendar field constants
        public const int ERA = 0;
        public const int YEAR = 1;
        public const int MONTH = 2;
        public const int WEEK_OF_YEAR = 3;
        public const int WEEK_OF_MONTH = 4;
        public const int DATE = 5;
        public const int DAY_OF_MONTH = 5;
        public const int DAY_OF_YEAR = 6;
        public const int DAY_OF_WEEK = 7;
        public const int DAY_OF_WEEK_IN_MONTH = 8;
        public const int AM_PM = 9;
        public const int HOUR = 10;
        public const int HOUR_OF_DAY = 11;
        public const int MINUTE = 12;
        public const int SECOND = 13;
        public const int MILLISECOND = 14;
        public const int ZONE_OFFSET = 15;
        public const int DST_OFFSET = 16;
        public const int FIELD_COUNT = 17;
        // Month constants
        public const int JANUARY = 0;
        public const int FEBRUARY = 1;
        public const int MARCH = 2;
        public const int APRIL = 3;
        public const int MAY = 4;
        public const int JUNE = 5;
        public const int JULY = 6;
        public const int AUGUST = 7;
        public const int SEPTEMBER = 8;
        public const int OCTOBER = 9;
        public const int NOVEMBER = 10;
        public const int DECEMBER = 11;
        public const int UNDECIMBER = 12;
        // Day-of-week constants
        public const int SUNDAY = 1;
        public const int MONDAY = 2;
        public const int TUESDAY = 3;
        public const int WEDNESDAY = 4;
        public const int THURSDAY = 5;
        public const int FRIDAY = 6;
        public const int SATURDAY = 7;
        // AM/PM
        public const int AM = 0;
        public const int PM = 1;

        // Indexer for the `calendar[FIELD]` paulirwin bulk-rewrite pattern.
        public virtual int this[int field] { get => Get(field); set => Set(field, value); }
        // Phase 7.8f: Java Calendar.setFirstDayOfWeek(int) / getFirstDayOfWeek().
        public int FirstDayOfWeek { get; set; } = SUNDAY;
        public virtual TimeZone TimeZone { get => null; set { } }
        public virtual long TimeInMillis { get => 0; set { } }
        // Read-only: the former no-op setter silently discarded the value (nothing ever wrote it).
        public virtual global::System.DateTime Time => global::System.DateTime.Now;
        public virtual int Get(int field) => 0;
        public void SetFirstDayOfWeek(int value) { FirstDayOfWeek = value; }
        public int GetFirstDayOfWeek() => FirstDayOfWeek;
        public virtual void Set(int field, int value) { }
        public virtual void Set(int year, int month, int day, int hour, int minute, int second) { }
        public virtual void Set(int year, int month, int day) { }
        public virtual void Add(int field, int amount) { }
        public virtual void SetLenient(bool lenient) { }
        public virtual bool IsLenient() => true;
        public virtual int GetMinimum(int field) => 0;
        public virtual int GetMaximum(int field) => 0;
        public virtual int GetActualMinimum(int field) => 0;
        public virtual int GetActualMaximum(int field) => 0;
        public static Calendar GetInstance() => new GregorianCalendar();
        public static Calendar GetInstance(TimeZone tz) => new GregorianCalendar();
        // Phase 7.8: Java's Calendar.clear() / clear(field) -- reset state.
        public virtual void Clear() { }
        public virtual void Clear(int field) { }
    }
}
