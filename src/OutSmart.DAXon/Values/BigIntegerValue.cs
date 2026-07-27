////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Functions;
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
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
using System.Numerics;
namespace OutSmart.DAXon.Values
{
    public sealed class BigIntegerValue : IntegerValue
    {
        private static readonly BigInteger MAX_INT = new BigInteger(int.MaxValue);
        private static readonly BigInteger MIN_INT = new BigInteger(int.MinValue);
        public static readonly BigInteger MAX_LONG = new BigInteger(long.MaxValue);
        public static readonly BigInteger MIN_LONG = new BigInteger(long.MinValue);
        public static readonly BigInteger MAX_UNSIGNED_LONG = BigIntegers.FromString("18446744073709551615");
        public static readonly BigIntegerValue ZERO = new BigIntegerValue(BigInteger.Zero);
        private readonly BigInteger value;

        public override UnicodeString PrimitiveStringValue => BMPString.Of(value.ToString());
        public BigIntegerValue(BigInteger value) : base(BuiltInAtomicType.INTEGER)
        {
            this.value = value;
        }

        public BigIntegerValue(BigInteger value, IAtomicType typeLabel) : base(typeLabel)
        {
            this.value = value;
        }

        public BigIntegerValue(long value) : base(BuiltInAtomicType.INTEGER)
        {
            this.value = new BigInteger(value);
        }

        public override AtomicValue CopyAsSubType(IAtomicType typeLabel)
        {
            if (typeLabel.PrimitiveType == StandardNames.XS_INTEGER)
            {
                return new BigIntegerValue(value, typeLabel);
            }
            else
            {
                return new BigDecimalValue(new BigDecimal(value), typeLabel);
            }
        }

        public override ValidationFailure ValidateAgainstSubType(BuiltInAtomicType type)
        {
            if (IntegerValue.CheckBigRange(value, type))
            {
                return null;
            }
            else
            {
                ValidationFailure err = new ValidationFailure("Integer value is out of range for subtype " + type.DisplayName);
                err.SetErrorCode("FORG0001");
                return err;
            }
        }

        public override int GetHashCode()
        {
            if (value.CompareTo(MIN_INT) >= 0 && value.CompareTo(MAX_INT) <= 0)
            {
                return value.IntValue();
            }
            else
            {
                return (int)(double)(GetDoubleValue()).GetHashCode();
            }
        }

        public override long LongValue()
        {
            return value.LongValue();
        }

        public override BigInteger AsBigInteger()
        {
            return value;
        }

        public bool IsWithinLongRange()
        {
            return value.CompareTo(MIN_LONG) >= 0 && value.CompareTo(MAX_LONG) <= 0;
        }

        public BigDecimal AsDecimal()
        {
            return new BigDecimal(value);
        }

        public override bool EffectiveBooleanValue()
        {
            return value.CompareTo(BigInteger.Zero) != 0;
        }

        public override int CompareTo(IXPathComparable other)
        {
            if (other is NumericValue)
            {
                if (other is BigIntegerValue)
                {
                    return value.CompareTo(((BigIntegerValue)other).value);
                }
                else if (other is Int64Value)
                {
                    return value.CompareTo(new BigInteger(((Int64Value)other).LongValue()));
                }
                else if (other is BigDecimalValue)
                {
                    return AsDecimal().CompareTo(((BigDecimalValue)other).GetDecimalValue());
                }
                else
                {
                    return base.CompareTo(other);
                }
            }
            else
            {
                throw new InvalidCastException("Cannot compare xs:integer to " + other);
            }
        }

        public override int CompareTo(long other)
        {
            if (other == 0)
            {
                return value.Sign;
            }

            return value.CompareTo(new BigInteger(other));
        }

        public override double GetDoubleValue()
        {
            return value.DoubleValue();
        }

        public override BigDecimal GetDecimalValue()
        {
            return new BigDecimal(value);
        }

        public override float GetFloatValue()
        {
            return (float)GetDoubleValue();
        }

        public override NumericValue Negate()
        {
            return new BigIntegerValue(-value);
        }

        public override NumericValue Floor()
        {
            return this;
        }

        public override NumericValue Ceiling()
        {
            return this;
        }

        public override NumericValue Round(int scale)
        {
            return Round(scale, Functions.Round.RoundingRule.HALF_TO_CEILING);
        }

        public override NumericValue Round(int scale, Round.RoundingRule roundingRule)
        {
            if (scale >= 0 || value.Sign == 0)
            {
                return this;
            }
            else
            {
                bool negative = value.Sign < 0;

                // factor is 1 for scale=0, 10 for scale=-1, 100 for scale=-2, etc
                long factor = 1;
                for (long i = 1; i <= -scale; i++)
                {
                    factor *= 10;
                }

                BigInteger factorB = new BigInteger(factor);
                BigInteger towardsZero = value / factorB * factorB;
                if (towardsZero.Equals(value))
                {
                    return this;
                }

                BigInteger awayFromZero = negative ? towardsZero - factorB : towardsZero + factorB;
                BigInteger floor = negative ? awayFromZero : towardsZero;
                BigInteger ceiling = negative ? towardsZero : awayFromZero;
                BigInteger midpoint = floor + (ceiling - floor) / new BigInteger(2);
                bool midway = value.Equals(midpoint);
                BigInteger nearest = value.CompareTo(midpoint) > 0 ? ceiling : floor;
                switch (roundingRule)
                {
                    case Functions.Round.RoundingRule.FLOOR:
                        return IntegerValue.MakeIntegerValue(floor);
                    case Functions.Round.RoundingRule.TOWARD_ZERO:
                        return IntegerValue.MakeIntegerValue(towardsZero);
                    case Functions.Round.RoundingRule.CEILING:
                        return IntegerValue.MakeIntegerValue(ceiling);
                    case Functions.Round.RoundingRule.AWAY_FROM_ZERO:
                        return IntegerValue.MakeIntegerValue(awayFromZero);
                    case Functions.Round.RoundingRule.HALF_TO_FLOOR:
                        return IntegerValue.MakeIntegerValue(midway ? floor : nearest);
                    case Functions.Round.RoundingRule.HALF_TO_CEILING:
                    default:
                        return IntegerValue.MakeIntegerValue(midway ? ceiling : nearest);
                    case Functions.Round.RoundingRule.HALF_TOWARD_ZERO:
                        return IntegerValue.MakeIntegerValue(midway ? towardsZero : nearest);
                    case Functions.Round.RoundingRule.HALF_AWAY_FROM_ZERO:
                        return IntegerValue.MakeIntegerValue(midway ? awayFromZero : nearest);
                    case Functions.Round.RoundingRule.HALF_TO_EVEN:
                        return IntegerValue.MakeIntegerValue(midway ? ((floor / factorB).Mod(new BigInteger(2)).Sign == 0 ? floor : ceiling) : nearest);
                }
            }
        }

        public override int Signum()
        {
            return value.Sign;
        }

        public override NumericValue Abs()
        {
            if (value.Sign >= 0)
            {
                return this;
            }
            else
            {
                return new BigIntegerValue(BigInteger.Abs(value));
            }
        }

        public override bool IsWholeNumber()
        {
            return true;
        }

        public override int AsSubscript()
        {
            if (value.CompareTo(BigInteger.Zero) > 0 && value.CompareTo(MAX_INT) <= 0)
            {
                return (int)LongValue();
            }
            else
            {
                return -1;
            }
        }

        /// <summary>
        /// Add another integer
        /// </summary>
        public override IntegerValue Plus(IntegerValue other)
        {
            if (other is BigIntegerValue)
            {
                return MakeIntegerValue(value + ((BigIntegerValue)other).value);
            }
            else
            {

                return MakeIntegerValue(value + new BigInteger(((Int64Value)other).LongValue()));
            }
        }

        /// <summary>
        /// Add another integer
        /// </summary>
        /// <summary>
        /// Subtract another integer
        /// </summary>
        public override IntegerValue Minus(IntegerValue other)
        {
            if (other is BigIntegerValue)
            {
                return MakeIntegerValue(value - ((BigIntegerValue)other).value);
            }
            else
            {

                return MakeIntegerValue(value - new BigInteger(((Int64Value)other).LongValue()));
            }
        }

        /// <summary>
        /// Add another integer
        /// </summary>
        /// <summary>
        /// Subtract another integer
        /// </summary>
        /// <summary>
        /// Multiply by another integer
        /// </summary>
        public override IntegerValue Times(IntegerValue other)
        {
            if (other is BigIntegerValue)
            {
                return MakeIntegerValue(value * ((BigIntegerValue)other).value);
            }
            else
            {

                return MakeIntegerValue(value * new BigInteger(((Int64Value)other).LongValue()));
            }
        }

        /// <summary>
        /// Add another integer
        /// </summary>
        /// <summary>
        /// Subtract another integer
        /// </summary>
        /// <summary>
        /// Multiply by another integer
        /// </summary>
        public override NumericValue Div(IntegerValue other)
        {
            BigInteger oi;
            if (other is BigIntegerValue)
            {
                oi = ((BigIntegerValue)other).value;
            }
            else
            {
                oi = new BigInteger(other.LongValue());
            }

            BigDecimalValue a = new BigDecimalValue(new BigDecimal(value));
            BigDecimalValue b = new BigDecimalValue(new BigDecimal(oi));
            return Calculator.DecimalDivide(a, b);
        }

        /// <summary>
        /// Add another integer
        /// </summary>
        /// <summary>
        /// Subtract another integer
        /// </summary>
        /// <summary>
        /// Multiply by another integer
        /// </summary>
        public override IntegerValue Mod(IntegerValue other)
        {
            if (other.Signum() == 0)
            {
                throw new XPathException("Integer modulo zero", "FOAR0001");
            }

            if (other is BigIntegerValue)
            {
                return MakeIntegerValue(value % ((BigIntegerValue)other).value);
            }
            else
            {
                return MakeIntegerValue(value % new BigInteger(other.LongValue()));
            }
        }

        /// <summary>
        /// Add another integer
        /// </summary>
        /// <summary>
        /// Subtract another integer
        /// </summary>
        /// <summary>
        /// Multiply by another integer
        /// </summary>
        public override IntegerValue Idiv(IntegerValue other)
        {
            if (other.Signum() == 0)
            {
                throw new XPathException("Integer division by zero", "FOAR0001");
            }

            BigInteger oi;
            if (other is BigIntegerValue)
            {
                oi = ((BigIntegerValue)other).value;
            }
            else
            {
                oi = new BigInteger(other.LongValue());
            }

            return MakeIntegerValue(value / oi);
        }

        /// <summary>
        /// Add another integer
        /// </summary>
        /// <summary>
        /// Subtract another integer
        /// </summary>
        /// <summary>
        /// Multiply by another integer
        /// </summary>
        /// <summary>
        /// Reduce a value to its simplest form.
        /// </summary>
        public override IGroundedValue Reduce()
        {
            if (CompareTo(long.MaxValue) < 0 && CompareTo(long.MinValue) > 0)
            {
                return new Int64Value(LongValue(), typeLabel);
            }

            return this;
        }
    }
}