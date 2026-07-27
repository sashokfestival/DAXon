////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Internal.Numerics;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Functions.Exslt
{
    public sealed class Date
    {
        /// <summary>
        /// Private constructor to disallow instantiation
        /// </summary>
        private Date()
        {
        }

        public static StringValue DateTime(IXPathContext context)
        {
            return new StringValue(context.GetCurrentDateTime().UnicodeStringValue);
        }

        public static string DateFn(IXPathContext context, StringValue datetimeIn)
        {
            ConversionRules rules = context.GetConfiguration().GetConversionRules();
            datetimeIn = Nn(datetimeIn);
            if (datetimeIn.Content.IndexOf('T') >= 0)
            {
                IConversionResult cr = DateTimeValue.MakeDateTimeValue(datetimeIn.UnicodeStringValue, rules);
                if (cr is ValidationFailure)
                {
                    return "";
                }
                else
                {
                    return ((DateTimeValue)cr).ToDateValue().GetStringValue();
                }
            }
            else
            {
                IConversionResult cr = DateValue.MakeDateValue(datetimeIn.UnicodeStringValue, rules);
                if (cr is ValidationFailure)
                {
                    return "";
                }
                else
                {
                    return ((AtomicValue)cr).GetStringValue();
                }
            }
        }

        public static string DateFn(IXPathContext context)
        {
            return DateFn(context, DateTime(context));
        }

        public static string Time(IXPathContext context, StringValue dateTime)
        {
            dateTime = Nn(dateTime);
            if (dateTime.Content.IndexOf('T') >= 0)
            {
                IConversionResult cr = DateTimeValue.MakeDateTimeValue(dateTime.UnicodeStringValue, context.GetConfiguration().GetConversionRules());
                if (cr is ValidationFailure)
                {
                    return "";
                }
                else
                {
                    return ((DateTimeValue)cr).ToTimeValue().GetStringValue();
                }
            }
            else
            {
                IConversionResult cr = TimeValue.MakeTimeValue(dateTime.UnicodeStringValue);
                if (cr is ValidationFailure)
                {
                    return "";
                }
                else
                {
                    return ((AtomicValue)cr).GetStringValue();
                }
            }
        }

        public static string Time(IXPathContext context)
        {
            return Time(context, DateTime(context));
        }

        public static double Year(IXPathContext context, StringValue datetimeIn)
        {
            datetimeIn = Nn(datetimeIn);
            try
            {
                IConversionResult cr = CalendarValue.MakeCalendarValue(datetimeIn.UnicodeStringValue, context.GetConfiguration().GetConversionRules());
                if (cr is ValidationFailure)
                {
                    return double.NaN;
                }

                if (cr is GMonthValue || cr is GMonthDayValue || cr is GDayValue || cr is TimeValue)
                {
                    return double.NaN;
                }

                AtomicValue year = ((CalendarValue)cr).GetComponent(AccessorFn.Component.YEAR);
                return ((NumericValue)year).GetDoubleValue();
            }
            catch (XPathException e)
            {
                return double.NaN;
            }
        }

        public static double Year(IXPathContext context)
        {
            return Year(context, DateTime(context));
        }

        public static bool LeapYear(IXPathContext context, StringValue dateTime)
        {
            dateTime = Nn(dateTime);
            double year = Year(context, dateTime);
            if (double.IsNaN(year))
            {
                return false;
            }

            int y = (int)year;
            return (y % 4 == 0) && !((y % 100 == 0) && !(y % 400 == 0));
        }

        public static bool LeapYear(IXPathContext context)
        {
            return LeapYear(context, DateTime(context));
        }

        public static double MonthInYear(IXPathContext context, StringValue dateTime)
        {
            dateTime = Nn(dateTime);
            try
            {
                IConversionResult cr = CalendarValue.MakeCalendarValue(dateTime.UnicodeStringValue, context.GetConfiguration().GetConversionRules());
                if (cr is ValidationFailure)
                {
                    return double.NaN;
                }

                if (cr is GYearValue || cr is GDayValue || cr is TimeValue)
                {
                    return double.NaN;
                }

                AtomicValue month = ((CalendarValue)cr).GetComponent(AccessorFn.Component.MONTH);
                return ((NumericValue)month).GetDoubleValue();
            }
            catch (XPathException e)
            {
                return double.NaN;
            }
        }

        public static double MonthInYear(IXPathContext context)
        {
            return MonthInYear(context, DateTime(context));
        }

        public static string MonthName(IXPathContext context, StringValue date)
        {
            date = Nn(date);
            string[] months = new[]
            {
                "January",
                "February",
                "March",
                "April",
                "May",
                "June",
                "July",
                "August",
                "September",
                "October",
                "November",
                "December"
            };
            double m = MonthInYear(context, date);
            if (double.IsNaN(m))
            {
                return "";
            }

            return months[(int)m - 1];
        }

        public static string MonthName(IXPathContext context)
        {
            return MonthName(context, DateTime(context));
        }

        public static string MonthAbbreviation(IXPathContext context, StringValue date)
        {
            date = Nn(date);
            string[] months = new[]
            {
                "Jan",
                "Feb",
                "Mar",
                "Apr",
                "May",
                "Jun",
                "Jul",
                "Aug",
                "Sep",
                "Oct",
                "Nov",
                "Dec"
            };
            double m = MonthInYear(context, date);
            if (double.IsNaN(m))
            {
                return "";
            }

            return months[(int)m - 1];
        }

        public static string MonthAbbreviation(IXPathContext context)
        {
            return MonthAbbreviation(context, DateTime(context));
        }

        public static double WeekInYear(IXPathContext context, StringValue dateTime)
        {
            dateTime = Nn(dateTime);
            int dayInYear = (int)DayInYear(context, dateTime);
            StringValue firstJan = new StringValue(new StringValue(dateTime.Content.Prefix(4)).Content.Concat(StringValue.Bmp("-01-01").Content));
            int jan1day = ((int)DayInWeek(context, firstJan) + 5) % 7;
            int daysInFirstWeek = jan1day == 0 ? 0 : 7 - jan1day;
            int rawWeek = (dayInYear - daysInFirstWeek + 6) / 7;
            if (daysInFirstWeek >= 4)
            {
                return rawWeek + 1;
            }
            else
            {
                if (rawWeek > 0)
                {
                    return rawWeek;
                }
                else
                {

                    // week number should be 52 or 53: same as 31 Dec in previous year
                    int lastYear = int.Parse((new StringValue(dateTime.Content.Prefix(4)).GetStringValue())) - 1;
                    StringValue dec31 = StringValue.Bmp(lastYear + "-12-31");

                    // assumes year > 999
                    return WeekInYear(context, dec31);
                }
            }
        }

        public static double WeekInYear(IXPathContext context)
        {
            return WeekInYear(context, DateTime(context));
        }

        public static double WeekInMonth(IXPathContext context, StringValue dateTime)
        {
            dateTime = Nn(dateTime);
            return (int)((DayInMonth(context, dateTime) - 1) / 7 + 1);
        }

        public static double WeekInMonth(IXPathContext context)
        {
            return WeekInMonth(context, DateTime(context));
        }

        public static double DayInYear(IXPathContext context, StringValue dateTime)
        {
            dateTime = Nn(dateTime);
            int month = (int)MonthInYear(context, dateTime);
            int day = (int)DayInMonth(context, dateTime);
            int[] prev = new[]
            {
                0,
                31,
                31 + 28,
                31 + 28 + 31,
                31 + 28 + 31 + 30,
                31 + 28 + 31 + 30 + 31,
                31 + 28 + 31 + 30 + 31 + 30,
                31 + 28 + 31 + 30 + 31 + 30 + 31,
                31 + 28 + 31 + 30 + 31 + 30 + 31 + 31,
                31 + 28 + 31 + 30 + 31 + 30 + 31 + 31 + 30,
                31 + 28 + 31 + 30 + 31 + 30 + 31 + 31 + 30 + 31,
                31 + 28 + 31 + 30 + 31 + 30 + 31 + 31 + 30 + 31 + 30,
                31 + 28 + 31 + 30 + 31 + 30 + 31 + 31 + 30 + 31 + 30 + 31
            };
            int leap = month > 2 && LeapYear(context, dateTime) ? 1 : 0;
            return prev[month - 1] + leap + day;
        }

        public static double DayInYear(IXPathContext context)
        {
            return DayInYear(context, DateTime(context));
        }

        public static double DayInMonth(IXPathContext context, StringValue dateTime)
        {
            dateTime = Nn(dateTime);
            try
            {
                IConversionResult cr = CalendarValue.MakeCalendarValue(dateTime.UnicodeStringValue, context.GetConfiguration().GetConversionRules());
                if (cr is ValidationFailure)
                {
                    return double.NaN;
                }

                if (cr is GYearValue || cr is GYearMonthValue || cr is GMonthValue || cr is TimeValue)
                {
                    return double.NaN;
                }

                AtomicValue day = ((CalendarValue)cr).GetComponent(AccessorFn.Component.DAY);
                return ((NumericValue)day).GetDoubleValue();
            }
            catch (XPathException e)
            {
                return double.NaN;
            }
        }

        public static double DayInMonth(IXPathContext context)
        {
            return DayInMonth(context, DateTime(context));
        }

        public static double DayOfWeekInMonth(IXPathContext context, StringValue dateTime)
        {
            dateTime = Nn(dateTime);
            double dd = DayInMonth(context, dateTime);
            if (double.IsNaN(dd))
            {
                return dd;
            }

            return ((int)dd - 1) / 7 + 1;
        }

        public static double DayOfWeekInMonth(IXPathContext context)
        {
            return DayOfWeekInMonth(context, DateTime(context));
        }

        public static double DayInWeek(IXPathContext context, StringValue dateTime)
        {
            dateTime = Nn(dateTime);
            double yy = Year(context, dateTime);
            double mm = MonthInYear(context, dateTime);
            double dd = DayInMonth(context, dateTime);
            if (double.IsNaN(yy) || double.IsNaN(mm) || double.IsNaN(dd))
            {
                return double.NaN;
            }

            GregorianCalendar calDate = new GregorianCalendar((int)yy, (int)mm - 1, (int)dd);
            calDate.SetFirstDayOfWeek(Calendar.SUNDAY);
            return calDate[Calendar.DAY_OF_WEEK];
        }

        public static double DayInWeek(IXPathContext context)
        {
            return DayInWeek(context, DateTime(context));
        }

        public static string DayName(IXPathContext context, StringValue dateTime)
        {
            dateTime = Nn(dateTime);
            string[] days = new[]
            {
                "Sunday",
                "Monday",
                "Tuesday",
                "Wednesday",
                "Thursday",
                "Friday",
                "Saturday"
            };
            double d = DayInWeek(context, dateTime);
            if (double.IsNaN(d))
            {
                return "";
            }

            return days[(int)d - 1];
        }

        public static string DayName(IXPathContext context)
        {
            return DayName(context, DateTime(context));
        }

        public static string DayAbbreviation(IXPathContext context, StringValue dateTime)
        {
            dateTime = Nn(dateTime);
            string[] days = new[]
            {
                "Sun",
                "Mon",
                "Tue",
                "Wed",
                "Thu",
                "Fri",
                "Sat"
            };
            double d = DayInWeek(context, dateTime);
            if (double.IsNaN(d))
            {
                return "";
            }

            return days[(int)d - 1];
        }

        public static string DayAbbreviation(IXPathContext context)
        {
            return DayAbbreviation(context, DateTime(context));
        }

        public static double HourInDay(IXPathContext context, StringValue dateTime)
        {
            dateTime = Nn(dateTime);
            try
            {
                IConversionResult cr = CalendarValue.MakeCalendarValue(dateTime.UnicodeStringValue, context.GetConfiguration().GetConversionRules());
                if (cr is ValidationFailure)
                {
                    return double.NaN;
                }

                if (!(cr is DateTimeValue || cr is TimeValue))
                {
                    return double.NaN;
                }

                AtomicValue hour = ((CalendarValue)cr).GetComponent(AccessorFn.Component.HOURS);
                return ((NumericValue)hour).GetDoubleValue();
            }
            catch (XPathException e)
            {
                return double.NaN;
            }
        }

        public static double HourInDay(IXPathContext context)
        {
            return HourInDay(context, DateTime(context));
        }

        public static double MinuteInHour(IXPathContext context, StringValue dateTime)
        {
            dateTime = Nn(dateTime);
            try
            {
                IConversionResult cr = CalendarValue.MakeCalendarValue(dateTime.UnicodeStringValue, context.GetConfiguration().GetConversionRules());
                if (cr is ValidationFailure)
                {
                    return double.NaN;
                }

                if (!(cr is DateTimeValue || cr is TimeValue))
                {
                    return double.NaN;
                }

                AtomicValue minute = ((CalendarValue)cr).GetComponent(AccessorFn.Component.MINUTES);
                return ((NumericValue)minute).GetDoubleValue();
            }
            catch (XPathException e)
            {
                return double.NaN;
            }
        }

        public static double MinuteInHour(IXPathContext context)
        {
            return MinuteInHour(context, DateTime(context));
        }

        public static double SecondInMinute(IXPathContext context, StringValue dateTime)
        {
            dateTime = Nn(dateTime);
            try
            {
                IConversionResult cr = CalendarValue.MakeCalendarValue(dateTime.UnicodeStringValue, context.GetConfiguration().GetConversionRules());
                if (cr is ValidationFailure)
                {
                    return double.NaN;
                }

                if (!(cr is DateTimeValue || cr is TimeValue))
                {
                    return double.NaN;
                }

                AtomicValue second = ((CalendarValue)cr).GetComponent(AccessorFn.Component.SECONDS);
                return ((NumericValue)second).GetDoubleValue();
            }
            catch (XPathException e)
            {
                return double.NaN;
            }
        }

        public static double SecondInMinute(IXPathContext context)
        {
            return SecondInMinute(context, DateTime(context));
        }

        public static string Add(IXPathContext context, StringValue datetimeIn, StringValue durationIn)
        {
            datetimeIn = Nn(datetimeIn);
            durationIn = Nn(durationIn);
            IConversionResult cr0 = CalendarValue.MakeCalendarValue(datetimeIn.UnicodeStringValue, context.GetConfiguration().GetConversionRules());
            if (cr0 is ValidationFailure)
            {
                return "";
            }

            CalendarValue cv0 = (CalendarValue)cr0;
            if (Specificity(cv0) < 0)
            {
                return "";
            }

            DateTimeValue v0 = cv0.ToDateTime();
            IConversionResult cr1 = DurationValue.MakeDuration(durationIn.UnicodeStringValue);
            if (cr1 is ValidationFailure)
            {
                return "";
            }

            DurationValue v1 = (DurationValue)cr1;
            YearMonthDurationValue v1m = (YearMonthDurationValue)Converter.DurationToYearMonthDuration.INSTANCE.Convert(v1);
            DayTimeDurationValue v1s = (DayTimeDurationValue)Converter.DurationToDayTimeDuration.INSTANCE.Convert(v1);
            DateTimeValue sum = (DateTimeValue)v0.Add(v1m).Add(v1s);
            return ((AtomicValue)Converter.Convert(sum, cv0.PrimitiveType, context.GetConfiguration().GetConversionRules())).GetStringValue();
        }

        public static string Sum(ISequenceIterator durations)
        {
            DurationValue tot = (DurationValue)DurationValue.MakeDuration(BMPString.Of("PT0S"));
            while (true)
            {
                IItem it = durations.Next();
                if (it == null)
                {
                    break;
                }

                IConversionResult cr = DurationValue.MakeDuration(it.UnicodeStringValue);
                if (cr is ValidationFailure)
                {
                    return "";
                }

                tot = AddDurationValues(tot, (DurationValue)cr);
                if (tot == null)
                {
                    return "";
                }
            }

            return tot.GetStringValue();
        }

        public string AddDuration(StringValue duration0, StringValue duration1)
        {
            duration0 = Nn(duration0);
            duration1 = Nn(duration1);
            IConversionResult dv0 = DurationValue.MakeDuration(duration0.UnicodeStringValue);
            IConversionResult dv1 = DurationValue.MakeDuration(duration1.UnicodeStringValue);
            if (dv0 is ValidationFailure || dv1 is ValidationFailure)
            {
                return "";
            }

            DurationValue result = AddDurationValues((DurationValue)dv0, (DurationValue)dv1);
            return result == null ? "" : result.GetStringValue();
        }

        private static DurationValue AddDurationValues(DurationValue dv0, DurationValue dv1)
        {
            YearMonthDurationValue dv0m = (YearMonthDurationValue)Converter.DurationToYearMonthDuration.INSTANCE.Convert(dv0);
            DayTimeDurationValue dv0s = (DayTimeDurationValue)Converter.DurationToDayTimeDuration.INSTANCE.Convert(dv0);
            YearMonthDurationValue dv1m = (YearMonthDurationValue)Converter.DurationToYearMonthDuration.INSTANCE.Convert(dv1);
            DayTimeDurationValue dv1s = (DayTimeDurationValue)Converter.DurationToDayTimeDuration.INSTANCE.Convert(dv1);
            int months = dv0m.LengthInMonths + dv1m.LengthInMonths;
            long micros = dv0s.LengthInMicroseconds + dv1s.LengthInMicroseconds;
            if (System.Math.Sign(months) * System.Math.Sign(micros) < 0)
            {
                return null;
            }

            bool positive = months >= 0 && micros >= 0;
            if (!positive)
            {
                months = -months;
                micros = -micros;
            }

            return new DurationValue(positive, 0, months, 0, 0, 0, (int)(micros / 1000000), (int)(micros % 1000000), BuiltInAtomicType.DURATION);
        }

        public static string Difference(IXPathContext context, StringValue dateLeftIn, StringValue dateRightIn)
        {
            try
            {
                dateLeftIn = Nn(dateLeftIn);
                dateRightIn = Nn(dateRightIn);
                ConversionRules rules = context.GetConfiguration().GetConversionRules();
                IConversionResult op0 = CalendarValue.MakeCalendarValue(dateLeftIn.UnicodeStringValue, rules);
                IConversionResult op1 = CalendarValue.MakeCalendarValue(dateRightIn.UnicodeStringValue, rules);
                if (op0 is ValidationFailure || op1 is ValidationFailure)
                {
                    return "";
                }

                CalendarValue v0 = (CalendarValue)op0;
                CalendarValue v1 = (CalendarValue)op1;
                int s0 = Specificity(v0);
                int s1 = Specificity(v1);
                if (s0 < 0 || s1 < 0)
                {
                    return "";
                }

                if (s0 < s1)
                {
                    v1 = (CalendarValue)Converter.Convert(v1, v0.PrimitiveType, rules);
                }
                else if (s1 < s0)
                {
                    v0 = (CalendarValue)Converter.Convert(v0, v1.PrimitiveType, rules);
                }

                if (v0 is GYearValue)
                {
                    int y0 = ((GYearValue)v0).Year;
                    int y1 = ((GYearValue)v1).Year;
                    return YearMonthDurationValue.FromMonths(12 * (y1 - y0)).GetStringValue();
                }
                else if (v0 is GYearMonthValue)
                {
                    int y0 = ((GYearMonthValue)v0).Year;
                    int y1 = ((GYearMonthValue)v1).Year;
                    int m0 = ((GYearMonthValue)v0).Month;
                    int m1 = ((GYearMonthValue)v1).Month;
                    return YearMonthDurationValue.FromMonths(12 * (y1 - y0) + (m1 - m0)).GetStringValue();
                }
                else
                {
                    DateTimeValue dt0 = v0.ToDateTime();
                    DateTimeValue dt1 = v1.ToDateTime();
                    return dt1.Subtract(dt0, context).GetStringValue();
                }
            }
            catch (XPathException e)
            {
                return "";
            }
        }

        private static int Specificity(CalendarValue val)
        {
            if (val is GYearValue)
            {
                return 0;
            }
            else if (val is GYearMonthValue)
            {
                return 1;
            }
            else if (val is DateValue)
            {
                return 2;
            }
            else if (val is DateTimeValue)
            {
                return 3;
            }
            else
            {
                return -1;
            }
        }

        public static string Duration(double seconds)
        {
            DayTimeDurationValue v = DayTimeDurationValue.FromSeconds(new BigDecimal(seconds));
            return v.GetStringValue();
        }

        public static double Seconds(IXPathContext context)
        {
            DateTimeValue now = context.GetCurrentDateTime();
            DurationValue diff = now.Subtract(DateTimeValue.EPOCH, context);
            return diff.LengthInSeconds;
        }

        public static double Seconds(IXPathContext context, StringValue datetimeIn)
        {
            try
            {
                datetimeIn = Nn(datetimeIn);
                IConversionResult cr = CalendarValue.MakeCalendarValue(datetimeIn.UnicodeStringValue, context.GetConfiguration().GetConversionRules());
                if (cr is DateTimeValue || cr is DateValue || cr is GYearValue || cr is GYearMonthValue)
                {
                    DateTimeValue dateTime = ((CalendarValue)cr).ToDateTime();
                    DayTimeDurationValue diff = dateTime.Subtract(DateTimeValue.EPOCH, context);
                    return diff.LengthInSeconds;
                }

                cr = DurationValue.MakeDuration(datetimeIn.UnicodeStringValue);
                if (cr is DurationValue)
                {
                    DurationValue duration = (DurationValue)cr;
                    if (duration.Years != 0 || duration.Months != 0)
                    {
                        return double.NaN;
                    }
                    else
                    {
                        return duration.LengthInSeconds;
                    }
                }
                else
                {
                    return double.NaN;
                }
            }
            catch (XPathException e)
            {
                return double.NaN;
            }
        }

        private static StringValue Nn(StringValue @in)
        {
            return @in == null ? StringValue.EMPTY_STRING : @in;
        }
    }
}