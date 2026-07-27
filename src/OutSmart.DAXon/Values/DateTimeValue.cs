////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal.Numerics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
using System.Numerics;
namespace OutSmart.DAXon.Values
{
    public sealed class DateTimeValue : CalendarValue, IXPathComparable
    {


        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        public static readonly DateTimeValue EPOCH = new DateTimeValue(1970, (byte)1, (byte)1, (byte)0, (byte)0, (byte)0, 0, 0, true);
        private readonly int year; // the year as written, +1 for BC years
        private readonly byte month; // the month as written, range 1-12
        private readonly byte day; // the day as written, range 1-31
        private readonly byte hour; // the hour as written (except for midnight), range 0-23
        private readonly byte minute; // the minutes as written, range 0-59
        private readonly byte second; // the seconds as written, range 0-59 (no leap seconds)
        private readonly int nanosecond; // the number of nanoseconds within the current second
        private readonly bool hasNoYearZero; // true if XSD 1.0 rules apply for negative years

        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        public override BuiltInAtomicType PrimitiveType => BuiltInAtomicType.DATE_TIME;

        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        public int Year => year;

        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        public byte Month => month;

        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        public byte Day => day;

        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        public byte Hour => hour;

        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        public byte Minute => minute;

        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        public byte Second => second;

        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        public int Microsecond => nanosecond / 1000;

        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        public int Nanosecond => nanosecond;

        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>

        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        public override UnicodeString PrimitiveStringValue
        {
            get
            {
                UnicodeBuilder sb = new UnicodeBuilder(32);
                int yr = year;
                if (year <= 0)
                {
                    yr = -yr + (hasNoYearZero ? 1 : 0); // no year zero in lexical space for XSD 1.0
                    if (yr != 0)
                    {
                        sb.Append('-');
                    }
                }

                AppendString(sb, yr, yr > 9999 ? (yr + "").Length : 4);
                sb.Append('-');
                AppendTwoDigits(sb, month);
                sb.Append('-');
                AppendTwoDigits(sb, day);
                sb.Append('T');
                AppendTwoDigits(sb, hour);
                sb.Append(':');
                AppendTwoDigits(sb, minute);
                sb.Append(':');
                AppendTwoDigits(sb, second);
                if (nanosecond != 0)
                {
                    sb.Append('.');
                    int ms = nanosecond;
                    int div = 100000000;
                    while (ms > 0)
                    {
                        int d = ms / div;
                        sb.Append((char)(d + '0'));
                        ms = ms % div;
                        div /= 10;
                    }
                }

                if (HasTimezone())
                {
                    AppendTimezone(sb);
                }

                return sb.ToUnicodeString();
            }
        }

        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        public override UnicodeString CanonicalLexicalRepresentation
        {
            get
            {
                if (HasTimezone() && TimezoneInMinutes != 0)
                {
                    return AdjustTimezone(0).UnicodeStringValue;
                }
                else
                {
                    return this.UnicodeStringValue;
                }
            }
        }

        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        public DateTimeComparable SchemaComparable => new DateTimeComparable(this);
        public DateTimeValue(int year, byte month, byte day, byte hour, byte minute, byte second, int nanosecond, bool hasNoYearZero, int tzMinutes, IAtomicType typeLabel) : base(typeLabel, tzMinutes)
        {
            this.year = year;
            this.month = month;
            this.day = day;
            this.hour = hour;
            this.minute = minute;
            this.second = second;
            this.nanosecond = nanosecond;
            this.hasNoYearZero = hasNoYearZero;
        }

        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        public DateTimeValue(int year, byte month, byte day, byte hour, byte minute, byte second, int nanosecond, int tz) : this(year, month, day, hour, minute, second, nanosecond, false, tz, BuiltInAtomicType.DATE_TIME)
        {
        }

        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        public DateTimeValue(int year, byte month, byte day, byte hour, byte minute, byte second, int microsecond, int tz, bool hasNoYearZero) : this(year, month, day, hour, minute, second, microsecond * 1000, hasNoYearZero, tz, BuiltInAtomicType.DATE_TIME)
        {
        }

        private MutableDateTimeValue MakeMutableCopy()
        {
            MutableDateTimeValue m = new MutableDateTimeValue();
            m.year = year;
            m.month = month;
            m.day = day;
            m.hour = hour;
            m.minute = minute;
            m.second = second;
            m.nanosecond = nanosecond;
            m.hasNoYearZero = hasNoYearZero;
            m.tzMinutes = TimezoneInMinutes;
            m.typeLabel = typeLabel;
            return m;
        }

        private static DateTimeValue FromMutableCopy(MutableDateTimeValue m)
        {
            return new DateTimeValue(m.year, m.month, m.day, m.hour, m.minute, m.second, m.nanosecond, m.hasNoYearZero, m.tzMinutes, m.typeLabel);
        }

        public static DateTimeValue GetCurrentDateTime(IXPathContext context)
        {
            Controller c;
            if (context == null || (c = context.GetController()) == null)
            {

                // non-XSLT/XQuery environment
                // We also take this path when evaluating compile-time expressions that require an implicit timezone.
                return Now();
            }
            else
            {
                return c.GetCurrentDateTime();
            }
        }

        public static DateTimeValue Now()
        {
            DateTimeOffset now = DateTimeOffset.Now;
            long subSecondTicks = now.Ticks % TimeSpan.TicksPerSecond; // 1 tick = 100 ns
            return new DateTimeValue(now.Year, (byte)now.Month, (byte)now.Day, (byte)now.Hour, (byte)now.Minute, (byte)now.Second,
                (int)(subSecondTicks * 100), false, (int)now.Offset.TotalMinutes, BuiltInAtomicType.DATE_TIME_STAMP);
        }

        public static DateTimeValue FromCalendar(Calendar calendar, bool tzSpecified)
        {
            MutableDateTimeValue m = new MutableDateTimeValue();
            int era = calendar[GregorianCalendar.ERA];
            m.year = calendar[Calendar.YEAR];
            if (era == GregorianCalendar.BC)
            {
                m.year = 1 - m.year;
            }

            m.month = (byte)(calendar[Calendar.MONTH] + 1);
            m.day = (byte)calendar[Calendar.DATE];
            m.hour = (byte)calendar[Calendar.HOUR_OF_DAY];
            m.minute = (byte)calendar[Calendar.MINUTE];
            m.second = (byte)calendar[Calendar.SECOND];
            m.nanosecond = calendar[Calendar.MILLISECOND] * 1000000;
            if (tzSpecified)
            {
                m.tzMinutes = (calendar[Calendar.ZONE_OFFSET] + calendar[Calendar.DST_OFFSET]) / 60000;
            }

            m.typeLabel = BuiltInAtomicType.DATE_TIME;
            m.hasNoYearZero = true;
            return FromMutableCopy(m);
        }

        public static DateTimeValue FromJavaDate(Date suppliedDate)
        {
            long millis = suppliedDate.GetTime();
            return (DateTimeValue)EPOCH.Add(DayTimeDurationValue.FromMilliseconds(millis));
        }

        public static DateTimeValue FromJavaTime(long time)
        {
            return (DateTimeValue)EPOCH.Add(DayTimeDurationValue.FromMilliseconds(time));
        }
        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        public static DateTimeValue MakeDateTimeValue(DateValue date, TimeValue time)
        {
            if (date == null || time == null)
            {
                return null;
            }

            int tz1 = date.TimezoneInMinutes;
            int tz2 = time.TimezoneInMinutes;
            if (tz1 != NO_TIMEZONE && tz2 != NO_TIMEZONE && tz1 != tz2)
            {
                throw new XPathException("Supplied date and time are in different timezones", "FORG0008");
            }

            MutableDateTimeValue v = date.ToDateTime().MakeMutableCopy();
            v.hour = time.Hour;
            v.minute = time.Minute;
            v.second = time.Second;
            v.nanosecond = time.Nanosecond;
            v.tzMinutes = System.Math.Max(tz1, tz2);
            v.typeLabel = BuiltInAtomicType.DATE_TIME;
            v.hasNoYearZero = date.hasNoYearZero;
            return FromMutableCopy(v);
        }

        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        public static IConversionResult MakeDateTimeValue(UnicodeString s, ConversionRules rules)
        {

            // input must have format [-]yyyy-mm-ddThh:mm:ss[.fff*][([+|-]hh:mm | Z)]
            MutableDateTimeValue dt = new MutableDateTimeValue();
            dt.hasNoYearZero = !rules.IsAllowYearZero();
            StringTokenizer tok = new StringTokenizer(Whitespace.Trim(s).ToString(), "-:.+TZ", true);
            IConversionResult dateError = ParseDateFields(tok, dt, rules, s);
            if (dateError != null)
            {
                return dateError;
            }

            IConversionResult timeError = ParseTimeFields(tok, dt, s);
            if (timeError != null)
            {
                return timeError;
            }

            IConversionResult tzError = ParseTimezoneTail(tok, dt, s);
            if (tzError != null)
            {
                return tzError;
            }

            bool midnight = false;
            if (dt.hour == 24)
            {
                dt.hour = 0;
                midnight = true;
            }


            // Check that this is a valid calendar date
            if (!DateValue.IsValidDate(dt.year, dt.month, dt.day))
            {
                return BadDate("Non-existent date", s);
            }


            // Adjust midnight to 00:00:00 on the next day
            if (midnight)
            {
                DateValue t = DateValue.Tomorrow(dt.year, dt.month, dt.day);
                dt.year = t.Year;
                dt.month = t.Month;
                dt.day = t.Day;
            }

            dt.typeLabel = BuiltInAtomicType.DATE_TIME;
            return FromMutableCopy(dt);
        }

        // Parse the [-]yyyy-mm-dd date fields into dt. Returns a BadDate result on error, null on success.
        private static IConversionResult ParseDateFields(StringTokenizer tok, MutableDateTimeValue dt, ConversionRules rules, UnicodeString s)
        {
            if (!tok.HasMoreTokens())
            {
                return BadDate("too short", s);
            }

            string part = tok.NextToken();
            int era = +1;
            if ("+".Equals(part))
            {
                return BadDate("Date must not start with '+' sign", s);
            }
            else if ("-".Equals(part))
            {
                era = -1;
                if (!tok.HasMoreTokens())
                {
                    return BadDate("No year after '-'", s);
                }

                part = tok.NextToken();
            }

            int value = DurationValue.SimpleInteger(part);
            if (value < 0)
            {
                if (value == -1)
                {
                    return BadDate("Non-numeric year component", s);
                }
                else
                {
                    return BadDate("Year is outside the range that Saxon can handle", s, "FODT0001");
                }
            }

            dt.year = value * era;
            if (part.Length < 4)
            {
                return BadDate("Year is less than four digits", s);
            }

            if (part.Length > 4 && part[0] == '0')
            {
                return BadDate("When year exceeds 4 digits, leading zeroes are not allowed", s);
            }

            if (dt.year == 0 && !rules.IsAllowYearZero())
            {
                return BadDate("Year zero is not allowed", s);
            }

            if (era < 0 && !rules.IsAllowYearZero())
            {
                dt.year++; // if year zero not allowed, -0001 is the year before +0001, represented as 0 internally.
            }

            if (!tok.HasMoreTokens())
            {
                return BadDate("Too short", s);
            }

            if (!"-".Equals(tok.NextToken()))
            {
                return BadDate("Wrong delimiter after year", s);
            }

            if (!tok.HasMoreTokens())
            {
                return BadDate("Too short", s);
            }

            part = tok.NextToken();
            if (part.Length != 2)
            {
                return BadDate("Month must be two digits", s);
            }

            value = DurationValue.SimpleInteger(part);
            if (value < 0)
            {
                return BadDate("Non-numeric month component", s);
            }

            dt.month = (byte)value;
            if (dt.month < 1 || dt.month > 12)
            {
                return BadDate("Month is out of range", s);
            }

            if (!tok.HasMoreTokens())
            {
                return BadDate("Too short", s);
            }

            if (!"-".Equals(tok.NextToken()))
            {
                return BadDate("Wrong delimiter after month", s);
            }

            if (!tok.HasMoreTokens())
            {
                return BadDate("Too short", s);
            }

            part = (string)tok.NextToken();
            if (part.Length != 2)
            {
                return BadDate("Day must be two digits", s);
            }

            value = DurationValue.SimpleInteger(part);
            if (value < 0)
            {
                return BadDate("Non-numeric day component", s);
            }

            dt.day = (byte)value;
            if (dt.day < 1 || dt.day > 31)
            {
                return BadDate("Day is out of range", s);
            }

            return null;
        }

        // Parse the Thh:mm:ss time fields into dt (24:00:00 checks included). Returns BadDate or null.
        private static IConversionResult ParseTimeFields(StringTokenizer tok, MutableDateTimeValue dt, UnicodeString s)
        {
            string part;
            int value;
            if (!tok.HasMoreTokens())
            {
                return BadDate("Too short", s);
            }

            if (!"T".Equals(tok.NextToken()))
            {
                return BadDate("Wrong delimiter after day", s);
            }

            if (!tok.HasMoreTokens())
            {
                return BadDate("Too short", s);
            }

            part = tok.NextToken();
            if (part.Length != 2)
            {
                return BadDate("Hour must be two digits", s);
            }

            value = DurationValue.SimpleInteger(part);
            if (value < 0)
            {
                return BadDate("Non-numeric hour component", s);
            }

            dt.hour = (byte)value;
            if (dt.hour > 24)
            {
                return BadDate("Hour is out of range", s);
            }

            if (!tok.HasMoreTokens())
            {
                return BadDate("Too short", s);
            }

            if (!":".Equals(tok.NextToken()))
            {
                return BadDate("Wrong delimiter after hour", s);
            }

            if (!tok.HasMoreTokens())
            {
                return BadDate("Too short", s);
            }

            part = tok.NextToken();
            if (part.Length != 2)
            {
                return BadDate("Minute must be two digits", s);
            }

            value = DurationValue.SimpleInteger(part);
            if (value < 0)
            {
                return BadDate("Non-numeric minute component", s);
            }

            dt.minute = (byte)value;
            if (dt.minute > 59)
            {
                return BadDate("Minute is out of range", s);
            }

            if (dt.hour == 24 && dt.minute != 0)
            {
                return BadDate("If hour is 24, minute must be 00", s);
            }

            if (!tok.HasMoreTokens())
            {
                return BadDate("Too short", s);
            }

            if (!":".Equals(tok.NextToken()))
            {
                return BadDate("Wrong delimiter after minute", s);
            }

            if (!tok.HasMoreTokens())
            {
                return BadDate("Too short", s);
            }

            part = tok.NextToken();
            if (part.Length != 2)
            {
                return BadDate("Second must be two digits", s);
            }

            value = DurationValue.SimpleInteger(part);
            if (value < 0)
            {
                return BadDate("Non-numeric second component", s);
            }

            dt.second = (byte)value;
            if (dt.second > 59)
            {
                return BadDate("Second is out of range", s);
            }

            if (dt.hour == 24 && dt.second != 0)
            {
                return BadDate("If hour is 24, second must be 00", s);
            }

            return null;
        }

        // Parse the optional fractional-seconds and timezone tail (state machine over the remaining
        // tokens). Returns BadDate on error, null on success.
        private static IConversionResult ParseTimezoneTail(StringTokenizer tok, MutableDateTimeValue dt, UnicodeString s)
        {
            string part;
            int value;
            int tz = 0;
            bool negativeTz = false;
            int state = 0;
            while (tok.HasMoreTokens())
            {
                if (state == 9)
                {
                    return BadDate("Characters after the end", s);
                }

                string delim = (string)tok.NextToken();
                if (".".Equals(delim))
                {
                    if (state != 0)
                    {
                        return BadDate("Decimal separator occurs twice", s);
                    }

                    if (!tok.HasMoreTokens())
                    {
                        return BadDate("Decimal point must be followed by digits", s);
                    }

                    part = tok.NextToken();
                    if (part.Length > 9 && part.Matches("^[0-9]+$"))
                    {
                        part = part.Substring(0, 9);
                    }

                    value = DurationValue.SimpleInteger(part);
                    if (value < 0)
                    {
                        return BadDate("Non-numeric fractional seconds component", s);
                    }

                    double fractionalSeconds = double.Parse('.' + part);
                    int nanoSeconds = (int)JavaMath.Round(fractionalSeconds * 1000000000);
                    if (nanoSeconds == 1000000000)
                    {
                        nanoSeconds--; // truncate fractional seconds to .999_999_999 if nanoseconds rounds to 1_000_000_000
                    }

                    dt.nanosecond = nanoSeconds;
                    if (dt.hour == 24 && dt.nanosecond != 0)
                    {
                        return BadDate("If hour is 24, fractional seconds must be 0", s);
                    }

                    state = 1;
                }
                else if ("Z".Equals(delim))
                {
                    if (state > 1)
                    {
                        return BadDate("Z cannot occur here", s);
                    }

                    tz = 0;
                    state = 9; // we've finished
                    dt.tzMinutes = 0;
                }
                else if ("+".Equals(delim) || "-".Equals(delim))
                {
                    if (state > 1)
                    {
                        return BadDate(delim + " cannot occur here", s);
                    }

                    state = 2;
                    if (!tok.HasMoreTokens())
                    {
                        return BadDate("Missing timezone", s);
                    }

                    part = tok.NextToken();
                    if (part.Length != 2)
                    {
                        return BadDate("Timezone hour must be two digits", s);
                    }

                    value = DurationValue.SimpleInteger(part);
                    if (value < 0)
                    {
                        return BadDate("Non-numeric timezone hour component", s);
                    }

                    tz = value;
                    if (tz > 14)
                    {
                        return BadDate("Timezone is out of range (-14:00 to +14:00)", s);
                    }

                    tz *= 60;
                    if ("-".Equals(delim))
                    {
                        negativeTz = true;
                    }
                }
                else if (":".Equals(delim))
                {
                    if (state != 2)
                    {
                        return BadDate("Misplaced ':'", s);
                    }

                    state = 9;
                    part = tok.NextToken();
                    value = DurationValue.SimpleInteger(part);
                    if (value < 0)
                    {
                        return BadDate("Non-numeric timezone minute component", s);
                    }

                    int tzminute = value;
                    if (part.Length != 2)
                    {
                        return BadDate("Timezone minute must be two digits", s);
                    }

                    if (tzminute > 59)
                    {
                        return BadDate("Timezone minute is out of range", s);
                    }

                    if (System.Math.Abs(tz) == 14 * 60 && tzminute != 0)
                    {
                        return BadDate("Timezone is out of range (-14:00 to +14:00)", s);
                    }

                    tz += tzminute;
                    if (negativeTz)
                    {
                        tz = -tz;
                    }

                    dt.tzMinutes = tz;
                }
                else
                {
                    return BadDate("Timezone format is incorrect", s);
                }
            }

            if (state == 2 || state == 3)
            {
                return BadDate("Timezone incomplete", s);
            }

            return null;
        }

        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>

        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        private static ValidationFailure BadDate(string msg, UnicodeString value)
        {
            ValidationFailure err = new ValidationFailure("Invalid dateTime value " + Err.Wrap(value, Err.VALUE) + " (" + msg + ")");
            err.SetErrorCode("FORG0001");
            return err;
        }

        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        private static ValidationFailure BadDate(string msg, UnicodeString value, string errorCode)
        {
            ValidationFailure err = new ValidationFailure("Invalid dateTime value " + Err.Wrap(value, Err.VALUE) + " (" + msg + ")");
            err.SetErrorCode(errorCode);
            return err;
        }

        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        public override DateTimeValue ToDateTime()
        {
            return this;
        }

        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        public bool IsXsd10Rules()
        {
            return hasNoYearZero;
        }

        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        public override void CheckValidInJavascript()
        {
            if (year <= 0 || year > 9999)
            {
                throw new XPathException("Year out of range for SaxonJS", "FODT0001");
            }
        }

        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        public DateTimeValue AdjustToUTC(int implicitTimezone)
        {
            if (HasTimezone())
            {
                return (DateTimeValue)AdjustTimezone(0);
            }
            else
            {
                if (implicitTimezone == CalendarValue.MISSING_TIMEZONE || implicitTimezone == CalendarValue.NO_TIMEZONE)
                {
                    throw new NoDynamicContextException("DateTime operation needs access to implicit timezone");
                }

                MutableDateTimeValue m = MakeMutableCopy();
                m.tzMinutes = implicitTimezone;
                return (DateTimeValue)FromMutableCopy(m).AdjustTimezone(0);
            }
        }

        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        public BigDecimal ToJulianInstant()
        {
            int julianDay = DateValue.GetJulianDayNumber(year, month, day);
            long julianSecond = (long)julianDay * 24 * 60 * 60;
            julianSecond += ((hour * 60 + minute) * 60) + second;
            BigDecimal j = BigDecimal.ValueOf(julianSecond);
            if (nanosecond == 0)
            {
                return j;
            }
            else
            {
                return j + BigDecimal.ValueOf(nanosecond).Divide(BigDecimalValue.BIG_DECIMAL_ONE_BILLION, 9, RoundingMode.HALF_EVEN);
            }
        }

        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        public static DateTimeValue FromJulianInstant(BigDecimal instant)
        {
            BigInteger julianSecond = instant.ToBigInteger();
            BigDecimal nanoseconds = (instant - new BigDecimal(julianSecond)) * BigDecimalValue.BIG_DECIMAL_ONE_BILLION;
            long js = julianSecond.LongValue();
            long jd = js / (24 * 60 * 60);
            DateValue date = DateValue.DateFromJulianDayNumber((int)jd);
            js = js % (24 * 60 * 60);
            byte hour = (byte)(js / (60 * 60));
            js = js % (60 * 60);
            byte minute = (byte)(js / 60);
            js = js % 60;
            return new DateTimeValue(date.Year, date.Month, date.Day, hour, minute, (byte)js, nanoseconds.IntValue(), 0);
        }

        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        public long RandomSeed()
        {
            return GetCalendar().TimeInMillis;
        }

        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        public override GregorianCalendar GetCalendar()
        {
            int tz = HasTimezone() ? TimezoneInMinutes * 60000 : 0;
            TimeZoneInfo zone = new SimpleTimeZone(tz, "LLL");
            GregorianCalendar calendar = new GregorianCalendar(zone);
            if (tz < calendar.GetMinimum(Calendar.ZONE_OFFSET) || tz > calendar.GetMaximum(Calendar.ZONE_OFFSET))
            {
                return AdjustTimezone(0).GetCalendar();
            }

            calendar.SetGregorianChange(new Date(long.MinValue));
            calendar.SetLenient(false);
            int yr = year;
            if (year <= 0)
            {
                yr = hasNoYearZero ? 1 - year : -year;
                calendar[Calendar.ERA] = GregorianCalendar.BC;
            }


            calendar.Set(yr, month - 1, day, hour, minute, second);
            calendar[Calendar.MILLISECOND] = nanosecond / 1000000; // loses precision unavoidably
            calendar[Calendar.ZONE_OFFSET] = tz;
            calendar[Calendar.DST_OFFSET] = 0;
            return calendar;
        }

        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        public DateValue ToDateValue()
        {
            return new DateValue(year, month, day, TimezoneInMinutes, hasNoYearZero);
        }

        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        public TimeValue ToTimeValue()
        {
            return new TimeValue(hour, minute, second, nanosecond, TimezoneInMinutes, BuiltInAtomicType.TIME);
        }

        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        public override AtomicValue CopyAsSubType(IAtomicType typeLabel)
        {
            MutableDateTimeValue v = MakeMutableCopy();
            v.typeLabel = typeLabel;
            return FromMutableCopy(v);
        }

        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        public override CalendarValue AdjustTimezone(int timezone)
        {
            if (!HasTimezone() || timezone == NO_TIMEZONE)
            {
                MutableDateTimeValue m = MakeMutableCopy();
                m.tzMinutes = timezone;
                return FromMutableCopy(m);
            }

            int oldtz = TimezoneInMinutes;
            if (oldtz == timezone)
            {
                return this;
            }

            int tz = timezone - oldtz;
            int h = hour;
            int mi = minute;
            mi += tz;
            if (mi < 0 || mi > 59)
            {
                h += (int)System.Math.Floor((double)mi / 60.0); // upstream: Math.floor(mi / 60.0). The old
                // (double)(mi / 60) divided as INTEGERS first (truncating toward zero, not flooring), so a
                // negative mi (adjusting to a negative timezone, e.g. 09:15Z -> -14:00) lost an hour of borrow.
                mi = (mi + 60 * 24) % 60;
            }

            if (h >= 0 && h < 24)
            {
                return new DateTimeValue(year, month, day, (byte)h, (byte)mi, second, nanosecond, hasNoYearZero, timezone, BuiltInAtomicType.DATE_TIME);
            }


            // Following code is designed to handle the corner case of adjusting from -14:00 to +14:00 or
            // vice versa, which can cause a change of two days in the date
            DateTimeValue dt = this;
            while (h < 0)
            {
                h += 24;
                DateValue t = DateValue.Yesterday(dt.Year, dt.Month, dt.Day);
                dt = new DateTimeValue(t.Year, t.Month, t.Day, (byte)h, (byte)mi, second, nanosecond, hasNoYearZero, timezone, BuiltInAtomicType.DATE_TIME);
            }

            if (h > 23)
            {
                h -= 24;
                DateValue t = DateValue.Tomorrow(year, month, day);
                dt = new DateTimeValue(t.Year, t.Month, t.Day, (byte)h, (byte)mi, second, nanosecond, hasNoYearZero, timezone, BuiltInAtomicType.DATE_TIME);
            }

            return dt;
        }

        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        public override CalendarValue Add(DurationValue duration)
        {
            if (duration is DayTimeDurationValue)
            {
                BigDecimal seconds = duration.TotalSeconds;
                BigDecimal julian = ToJulianInstant();
                julian = julian + seconds;
                MutableDateTimeValue dt = FromJulianInstant(julian).MakeMutableCopy();
                dt.tzMinutes = TimezoneInMinutes;
                dt.hasNoYearZero = this.hasNoYearZero;
                return FromMutableCopy(dt);
            }
            else if (duration is YearMonthDurationValue)
            {
                int months = ((YearMonthDurationValue)duration).LengthInMonths;
                int m = (month - 1) + months;
                int y = year + m / 12;
                m = m % 12;
                if (m < 0)
                {
                    m += 12;
                    y -= 1;
                }

                m++;
                int d = day;
                while (!DateValue.IsValidDate(y, m, d))
                {
                    d -= 1;
                }

                return new DateTimeValue(y, (byte)m, (byte)d, hour, minute, second, nanosecond, hasNoYearZero, TimezoneInMinutes, BuiltInAtomicType.DATE_TIME);
            }
            else
            {
                throw new XPathException("DateTime arithmetic is not supported on xs:duration, only on its subtypes").WithErrorCode("XPTY0004").AsTypeError();
            }
        }

        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        public override DayTimeDurationValue Subtract(CalendarValue other, IXPathContext context)
        {
            if (!(other is DateTimeValue))
            {
                throw new XPathException("First operand of '-' is a dateTime, but the second is not").WithErrorCode("XPTY0004").AsTypeError();
            }

            return base.Subtract(other, context);
        }

        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        public BigDecimal SecondsSinceEpoch()
        {
            try
            {
                DateTimeValue dtv = AdjustToUTC(0);
                BigDecimal d1 = dtv.ToJulianInstant();
                BigDecimal d2 = EPOCH.ToJulianInstant();
                return d1 - d2;
            }
            catch (NoDynamicContextException e)
            {
                throw new InvalidOperationException(e.Message, e);
            }
        }

        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        public override AtomicValue GetComponent(AccessorFn.Component component)
        {
            switch (component)
            {
                case AccessorFn.Component.YEAR_ALLOWING_ZERO:
                    return Int64Value.MakeIntegerValue(year);
                case AccessorFn.Component.YEAR:
                    return Int64Value.MakeIntegerValue(year > 0 || !hasNoYearZero ? year : year - 1);
                case AccessorFn.Component.MONTH:
                    return Int64Value.MakeIntegerValue(month);
                case AccessorFn.Component.DAY:
                    return Int64Value.MakeIntegerValue(day);
                case AccessorFn.Component.HOURS:
                    return Int64Value.MakeIntegerValue(hour);
                case AccessorFn.Component.MINUTES:
                    return Int64Value.MakeIntegerValue(minute);
                case AccessorFn.Component.SECONDS:
                    BigDecimal d = BigDecimal.ValueOf(nanosecond);
                    d = d.Divide(BigDecimalValue.BIG_DECIMAL_ONE_BILLION, 6, RoundingMode.HALF_UP);
                    d = d + BigDecimal.ValueOf(second);
                    return new BigDecimalValue(d);
                case AccessorFn.Component.WHOLE_SECONDS:
                    return Int64Value.MakeIntegerValue(second);
                case AccessorFn.Component.MICROSECONDS:

                    // internal use only
                    return new Int64Value(nanosecond / 1000);
                case AccessorFn.Component.NANOSECONDS:

                    // internal use only
                    return new Int64Value(nanosecond);
                case AccessorFn.Component.TIMEZONE:
                    if (HasTimezone())
                    {
                        return DayTimeDurationValue.FromMilliseconds(60000 * TimezoneInMinutes);
                    }
                    else
                    {
                        return null;
                    }

                default:
                    throw new ArgumentException("Unknown component for dateTime: " + component);
            }
        }

        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        public override int CompareTo(CalendarValue other, int implicitTimezone)
        {
            if (!(other is DateTimeValue))
            {
                throw new InvalidCastException("DateTime values are not comparable to " + other.GetType());
            }

            DateTimeValue v2 = (DateTimeValue)other;
            if (TimezoneInMinutes == v2.TimezoneInMinutes)
            {

                // both values are in the same timezone (explicitly or implicitly)
                if (year != v2.year)
                {
                    return IntegerValue.Signum(year - v2.year);
                }

                if (month != v2.month)
                {
                    return IntegerValue.Signum(month - v2.month);
                }

                if (day != v2.day)
                {
                    return IntegerValue.Signum(day - v2.day);
                }

                if (hour != v2.hour)
                {
                    return IntegerValue.Signum(hour - v2.hour);
                }

                if (minute != v2.minute)
                {
                    return IntegerValue.Signum(minute - v2.minute);
                }

                if (second != v2.second)
                {
                    return IntegerValue.Signum(second - v2.second);
                }

                if (nanosecond != v2.nanosecond)
                {
                    return IntegerValue.Signum(nanosecond - v2.nanosecond);
                }

                return 0;
            }

            return AdjustToUTC(implicitTimezone).CompareTo(v2.AdjustToUTC(implicitTimezone), implicitTimezone);
        }

        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        public override IXPathComparable GetXPathComparable(IStringCollator collator, int implicitTimezone)
        {
            if (HasTimezone())
            {
                return this;
            }
            else if (implicitTimezone == MISSING_TIMEZONE)
            {
                throw new NoDynamicContextException("Unknown implicit timezone");
            }
            else
            {
                return (IXPathComparable)AdjustTimezone(implicitTimezone);
            }
        }

        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        public int CompareTo(IXPathComparable v2)
        {
            if (v2 is DateTimeValue)
            {
                try
                {
                    return CompareTo((DateTimeValue)v2, MISSING_TIMEZONE);
                }
                catch (Exception err)
                {
                    throw new InvalidCastException("DateTime comparison requires access to implicit timezone");
                }
            }
            else
            {
                throw new InvalidCastException("Cannot compare xs:dateTime with " + v2.ToString());
            }
        }

        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        /// <summary>
        /// DateTimeComparable is an object that implements the XML Schema rules for comparing date/time values
        /// </summary>
        public override bool Equals(object o)
        {
            return o is DateTimeValue && CompareTo((DateTimeValue)o) == 0;
        }

        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        /// <summary>
        /// DateTimeComparable is an object that implements the XML Schema rules for comparing date/time values
        /// </summary>
        public override int GetHashCode()
        {
            return ComputeHashCode(year, month, day, hour, minute, second, nanosecond, TimezoneInMinutes);
        }

        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        /// <summary>
        /// DateTimeComparable is an object that implements the XML Schema rules for comparing date/time values
        /// </summary>
        public static int ComputeHashCode(int year, byte month, byte day, byte hour, byte minute, byte second, int nanosecond, int tzMinutes)
        {
            int tz = tzMinutes == CalendarValue.NO_TIMEZONE ? 0 : -tzMinutes;
            int h = hour;
            int mi = minute;
            mi += tz;
            if (mi < 0 || mi > 59)
            {
                h += (int)System.Math.Floor((double)mi / 60.0); // upstream: Math.floor(mi / 60.0). The old
                mi = (mi + 60 * 24) % 60;
            }

            while (h < 0)
            {
                h += 24;
                DateValue t = DateValue.Yesterday(year, month, day);
                year = t.Year;
                month = t.Month;
                day = t.Day;
            }

            while (h > 23)
            {
                h -= 24;
                DateValue t = DateValue.Tomorrow(year, month, day);
                year = t.Year;
                month = t.Month;
                day = t.Day;
            }

            return (year << 4) ^ (month << 28) ^ (day << 23) ^ (h << 18) ^ (mi << 13) ^ second ^ nanosecond;
        }

        protected class MutableDateTimeValue
        {
            public int year; // the year as written, +1 for BC years
            public byte month; // the month as written, range 1-12
            public byte day; // the day as written, range 1-31
            public byte hour; // the hour as written (except for midnight), range 0-23
            public byte minute; // the minutes as written, range 0-59
            public byte second; // the seconds as written, range 0-59 (no leap seconds)
            public int nanosecond; // the number of nanoseconds within the current second
            public bool hasNoYearZero; // true if XSD 1.0 rules apply for negative years
            public int tzMinutes = NO_TIMEZONE;
            public IAtomicType typeLabel = BuiltInAtomicType.DATE_TIME;
        }

        /// <summary>
        /// Fixed date/time used by Java (and Unix) as the origin of the universe: 1970-01-01T00:00:00Z
        /// </summary>
        /// <summary>
        /// DateTimeComparable is an object that implements the XML Schema rules for comparing date/time values
        /// </summary>
        public class DateTimeComparable : IComparable<DateTimeComparable>
        {
            private readonly DateTimeValue value;
            public DateTimeComparable(DateTimeValue value)
            {
                this.value = value;
            }

            public virtual int CompareTo(DateTimeComparable o)
            {
                DateTimeValue dt0 = value;
                DateTimeValue dt1 = o.value;
                if (dt0.HasTimezone())
                {
                    if (dt1.HasTimezone())
                    {
                        dt0 = (DateTimeValue)dt0.AdjustTimezone(0);
                        dt1 = (DateTimeValue)dt1.AdjustTimezone(0);
                        return dt0.CompareTo(dt1);
                    }
                    else
                    {
                        DateTimeValue dt1max = (DateTimeValue)dt1.AdjustTimezone(14 * 60);
                        if (dt0.CompareTo(dt1max) < 0)
                        {
                            return -1;
                        }

                        DateTimeValue dt1min = (DateTimeValue)dt1.AdjustTimezone(-14 * 60);
                        if (dt0.CompareTo(dt1min) > 0)
                        {
                            return +1;
                        }

                        return SequenceTool.INDETERMINATE_ORDERING;
                    }
                }
                else
                {
                    if (dt1.HasTimezone())
                    {
                        DateTimeValue dt0min = (DateTimeValue)dt0.AdjustTimezone(-14 * 60);
                        if (dt0min.CompareTo(dt1) < 0)
                        {
                            return -1;
                        }

                        DateTimeValue dt0max = (DateTimeValue)dt0.AdjustTimezone(14 * 60);
                        if (dt0max.CompareTo(dt1) > 0)
                        {
                            return +1;
                        }

                        return SequenceTool.INDETERMINATE_ORDERING;
                    }
                    else
                    {
                        dt0 = (DateTimeValue)dt0.AdjustTimezone(0);
                        dt1 = (DateTimeValue)dt1.AdjustTimezone(0);
                        return dt0.CompareTo(dt1);
                    }
                }
            }

            public override bool Equals(object o)
            {
                return o is DateTimeComparable && value.HasTimezone() == ((DateTimeComparable)o).value.HasTimezone() && CompareTo((DateTimeComparable)o) == 0;
            }

            public override int GetHashCode()
            {
                DateTimeValue dt0 = (DateTimeValue)value.AdjustTimezone(0);
                return (dt0.year << 20) ^ (dt0.month << 16) ^ (dt0.day << 11) ^ (dt0.hour << 7) ^ (dt0.minute << 2) ^ (dt0.second * 1000000000 + dt0.nanosecond);
            }
        }
    }
}