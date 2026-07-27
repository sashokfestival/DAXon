////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Lib;
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
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Values
{
    public sealed class YearMonthDurationValue : DurationValue, IXPathComparable, IContextFreeAtomicValue
    {

        public override BuiltInAtomicType PrimitiveType => BuiltInAtomicType.YEAR_MONTH_DURATION;

        public override UnicodeString PrimitiveStringValue
        {
            get
            {

                // The canonical representation has months in the range 0-11
                int y = Years;
                int m = Months;
                UnicodeBuilder sb = new UnicodeBuilder(16);
                if (_negative)
                {
                    sb.Append('-');
                }

                sb.Append('P');
                if (y != 0)
                {
                    sb.Append(y + "Y");
                }

                if (m != 0 || y == 0)
                {
                    sb.Append(m + "M");
                }

                return sb.ToUnicodeString();
            }
        }

        public int LengthInMonths => _months * (_negative ? -1 : +1);

        /// <summary>
        /// Multiply duration by a number.
        /// </summary>
        /// <summary>
        /// Multiply duration by a decimal.
        /// </summary>
        /// <summary>
        /// Divide duration by a number.
        /// </summary>
        /// <summary>
        /// Negate a duration (same as subtracting from zero, but it preserves the type of the original duration)
        /// </summary>
        public IXPathComparable XPathComparable => this;
        public YearMonthDurationValue(int months, IAtomicType typeLabel) : base(0, months, 0, 0, 0, 0, 0, typeLabel)
        {
        }

        public static IConversionResult MakeYearMonthDurationValue(UnicodeString s)
        {
            IConversionResult d = DurationValue.MakeDuration(s, true, false);
            if (d is ValidationFailure)
            {
                return d;
            }

            DurationValue dv = (DurationValue)d;
            return YearMonthDurationValue.FromMonths((dv.Years * 12 + dv.Months) * dv.Signum());
        }

        public override AtomicValue CopyAsSubType(IAtomicType typeLabel)
        {
            return new YearMonthDurationValue(LengthInMonths, typeLabel);
        }

        public static YearMonthDurationValue FromMonths(int months)
        {
            return new YearMonthDurationValue(months, BuiltInAtomicType.YEAR_MONTH_DURATION);
        }

        public override DurationValue Multiply(long factor)
        {

            // Fast path for simple cases
            if (System.Math.Abs(factor) < 30000 && System.Math.Abs(_months) < 30000)
            {
                return YearMonthDurationValue.FromMonths((int)factor * LengthInMonths);
            }
            else
            {
                return (YearMonthDurationValue)Multiply((double)factor);
            }
        }

        /// <summary>
        /// Multiply duration by a number.
        /// </summary>
        public override DurationValue Multiply(double n)
        {
            if (double.IsNaN(n))
            {
                throw new XPathException("Cannot multiply a duration by NaN", "FOCA0005");
            }

            double m = LengthInMonths;
            double product = n * m;
            if (double.IsInfinity(product) || product > int.MaxValue || product < int.MinValue)
            {
                throw new XPathException("Overflow when multiplying a duration by a number", "FODT0002");
            }


            // following code is needed to get the correct rounding on both Java and C#
            return FromMonths((int)new DoubleValue(product).Round(0).LongValue());
        }

        /// <summary>
        /// Multiply duration by a number.
        /// </summary>
        /// <summary>
        /// Multiply duration by a decimal.
        /// </summary>
        public override DurationValue Multiply(BigDecimal n)
        {
            int m = LengthInMonths;
            BigDecimal product = n * BigDecimal.ValueOf(m);
            if (product.Abs().CompareTo(BigDecimal.ValueOf(int.MaxValue)) > 0)
            {
                throw new XPathException("Overflow when multiplying a duration by a number", "FODT0002");
            }


            // following code is needed to get the correct rounding on both Java and C#
            return FromMonths((int)new BigDecimalValue(product).Round(0).LongValue());
        }

        /// <summary>
        /// Multiply duration by a number.
        /// </summary>
        /// <summary>
        /// Multiply duration by a decimal.
        /// </summary>
        /// <summary>
        /// Divide duration by a number.
        /// </summary>
        public override DurationValue Divide(double n)
        {
            if (double.IsNaN(n))
            {
                throw new XPathException("Cannot divide a duration by NaN", "FOCA0005");
            }

            double m = LengthInMonths;
            double product = m / n;
            if (double.IsInfinity(product) || product > int.MaxValue || product < int.MinValue)
            {
                throw new XPathException("Overflow when dividing a duration by a number", "FODT0002");
            }


            // following code is needed to get the correct rounding on both Java and C#
            return FromMonths((int)new DoubleValue(product).Round(0).LongValue());
        }

        /// <summary>
        /// Multiply duration by a number.
        /// </summary>
        /// <summary>
        /// Multiply duration by a decimal.
        /// </summary>
        /// <summary>
        /// Divide duration by a number.
        /// </summary>
        public override BigDecimalValue Divide(DurationValue other)
        {
            if (other is YearMonthDurationValue)
            {
                BigDecimal v1 = BigDecimal.ValueOf(LengthInMonths);
                BigDecimal v2 = BigDecimal.ValueOf(((YearMonthDurationValue)other).LengthInMonths);
                if (v2.Sign == 0)
                {
                    throw new XPathException("Divide by zero (durations)", "FOAR0001");
                }

                return new BigDecimalValue(DivideBigDecimal(v1, v2));
            }
            else
            {
                throw new XPathException("Cannot divide two durations of different type", "XPTY0004");
            }
        }

        /// <summary>
        /// Multiply duration by a number.
        /// </summary>
        /// <summary>
        /// Multiply duration by a decimal.
        /// </summary>
        /// <summary>
        /// Divide duration by a number.
        /// </summary>
        private BigDecimal DivideBigDecimal(BigDecimal v1, BigDecimal v2)
        {
            return v1.Divide(v2, 20, RoundingMode.HALF_EVEN);
        }

        /// <summary>
        /// Multiply duration by a number.
        /// </summary>
        /// <summary>
        /// Multiply duration by a decimal.
        /// </summary>
        /// <summary>
        /// Divide duration by a number.
        /// </summary>
        /// <summary>
        /// Add two year-month-durations
        /// </summary>
        public override DurationValue Add(DurationValue other)
        {
            if (other is YearMonthDurationValue)
            {
                return FromMonths(LengthInMonths + ((YearMonthDurationValue)other).LengthInMonths);
            }
            else
            {
                throw new XPathException("Cannot add two durations of different type", "XPTY0004").AsTypeError();
            }
        }

        /// <summary>
        /// Multiply duration by a number.
        /// </summary>
        /// <summary>
        /// Multiply duration by a decimal.
        /// </summary>
        /// <summary>
        /// Divide duration by a number.
        /// </summary>
        /// <summary>
        /// Subtract two year-month-durations
        /// </summary>
        public override DurationValue Subtract(DurationValue other)
        {
            if (other is YearMonthDurationValue)
            {
                return FromMonths(LengthInMonths - ((YearMonthDurationValue)other).LengthInMonths);
            }
            else
            {
                throw new XPathException("Cannot subtract two durations of different type", "XPTY0004").AsTypeError();
            }
        }

        /// <summary>
        /// Multiply duration by a number.
        /// </summary>
        /// <summary>
        /// Multiply duration by a decimal.
        /// </summary>
        /// <summary>
        /// Divide duration by a number.
        /// </summary>
        /// <summary>
        /// Negate a duration (same as subtracting from zero, but it preserves the type of the original duration)
        /// </summary>
        public override DurationValue Negate()
        {
            return FromMonths(-LengthInMonths);
        }

        /// <summary>
        /// Multiply duration by a number.
        /// </summary>
        /// <summary>
        /// Multiply duration by a decimal.
        /// </summary>
        /// <summary>
        /// Divide duration by a number.
        /// </summary>
        /// <summary>
        /// Negate a duration (same as subtracting from zero, but it preserves the type of the original duration)
        /// </summary>
        public int CompareTo(IXPathComparable other)
        {
            if (other is YearMonthDurationValue)
            {
                return LengthInMonths.CompareTo(((YearMonthDurationValue)other).LengthInMonths);
            }
            else
            {
                throw new InvalidCastException("Cannot compare xs:yearMonthDuration with " + other);
            }
        }

        /// <summary>
        /// Multiply duration by a number.
        /// </summary>
        /// <summary>
        /// Multiply duration by a decimal.
        /// </summary>
        /// <summary>
        /// Divide duration by a number.
        /// </summary>
        /// <summary>
        /// Negate a duration (same as subtracting from zero, but it preserves the type of the original duration)
        /// </summary>
        public override IXPathComparable GetXPathComparable(IStringCollator collator, int implicitTimezone)
        {
            return this;
        }

        /// <summary>
        /// Multiply duration by a number.
        /// </summary>
        /// <summary>
        /// Multiply duration by a decimal.
        /// </summary>
        /// <summary>
        /// Divide duration by a number.
        /// </summary>
        /// <summary>
        /// Negate a duration (same as subtracting from zero, but it preserves the type of the original duration)
        /// </summary>
        public override IAtomicMatchKey GetXPathMatchKey(IStringCollator collator, int implicitTimezone)
        {
            return this;
        }
    }
}
