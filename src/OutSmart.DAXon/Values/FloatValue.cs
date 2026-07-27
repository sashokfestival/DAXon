////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Functions;
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
namespace OutSmart.DAXon.Values
{
    /// <summary>
    /// A numeric (single precision floating point) value
    /// </summary>
    public sealed class FloatValue : NumericValue
    {
        public static readonly FloatValue ZERO = new FloatValue((float)0);
        public static readonly FloatValue NEGATIVE_ZERO = new FloatValue(-0.0f);
        public static readonly FloatValue ONE = new FloatValue((float)1);
        public static readonly FloatValue NaN = new FloatValue(float.NaN);
        private readonly float value;

        public override BuiltInAtomicType PrimitiveType => BuiltInAtomicType.FLOAT;

        /// <summary>
        /// Get the value
        /// </summary>
        /// <summary>
        /// Test whether the value is the double/float value NaN
        /// </summary>
        //    }
        public override UnicodeString PrimitiveStringValue => FloatToString(value);

        /// <summary>
        /// Get the value
        /// </summary>
        /// <summary>
        /// Test whether the value is the double/float value NaN
        /// </summary>
        //    }
        public override UnicodeString CanonicalLexicalRepresentation
        {
            get
            {
                UnicodeBuilder fsb = new UnicodeBuilder(32);
                return FloatingPointConverter.AppendFloat(fsb, value, true);
            }
        }
        public FloatValue(float value) : base(BuiltInAtomicType.FLOAT)
        {
            this.value = value;
        }

        public FloatValue(float value, IAtomicType typeLabel) : base(typeLabel)
        {
            this.value = value;
        }

        public static FloatValue MakeFloatValue(float value)
        {
            return new FloatValue(value);
        }

        public override AtomicValue CopyAsSubType(IAtomicType typeLabel)
        {
            return new FloatValue(value, typeLabel);
        }

        /// <summary>
        /// Get the value
        /// </summary>
        public override float GetFloatValue()
        {
            return value;
        }

        /// <summary>
        /// Get the value
        /// </summary>
        public override double GetDoubleValue()
        {
            return value;
        }

        /// <summary>
        /// Get the value
        /// </summary>
        public override //@CSharpReplaceBody(code="return Singulink.Numerics.BigDecimal.Parse(value.ToString(System.Globalization.CultureInfo.InvariantCulture));")
        BigDecimal GetDecimalValue()
        {
            try
            {
                return BigDecimal.ValueOf(value);
            }
            catch (FormatException e)
            {
                throw new ValidationException(e);
            }
        }

        /// <summary>
        /// Get the value
        /// </summary>
        public override long LongValue()
        {
            return (long)value;
        }

        /// <summary>
        /// Get the value
        /// </summary>
        public override int GetHashCode()
        {
            if (value > int.MinValue && value < int.MaxValue)
            {
                return (int)value;
            }
            else
            {
                return (int)(double)(GetDoubleValue()).GetHashCode();
            }
        }

        /// <summary>
        /// Get the value
        /// </summary>
        /// <summary>
        /// Test whether the value is the double/float value NaN
        /// </summary>
        public override bool IsNaN()
        {
            return float.IsNaN(value);
        }

        /// <summary>
        /// Get the value
        /// </summary>
        /// <summary>
        /// Test whether the value is the double/float value NaN
        /// </summary>
        public override bool EffectiveBooleanValue()
        {
            return (value != 0 && !float.IsNaN(value));
        }

        /// <summary>
        /// Get the value
        /// </summary>
        /// <summary>
        /// Test whether the value is the double/float value NaN
        /// </summary>
        //    }
        public static UnicodeString FloatToString(float value)
        {
            return FloatingPointConverter.AppendFloat(new UnicodeBuilder(), value, false);
        }

        /// <summary>
        /// Get the value
        /// </summary>
        /// <summary>
        /// Test whether the value is the double/float value NaN
        /// </summary>
        //    }
        /// <summary>
        /// Negate the value
        /// </summary>
        public override NumericValue Negate()
        {
            return new FloatValue(-value);
        }

        /// <summary>
        /// Get the value
        /// </summary>
        /// <summary>
        /// Test whether the value is the double/float value NaN
        /// </summary>
        //    }
        /// <summary>
        /// Implement the XPath floor() function
        /// </summary>
        public override NumericValue Floor()
        {
            return new FloatValue((float)System.Math.Floor(value));
        }

        /// <summary>
        /// Get the value
        /// </summary>
        /// <summary>
        /// Test whether the value is the double/float value NaN
        /// </summary>
        //    }
        /// <summary>
        /// Implement the XPath ceiling() function
        /// </summary>
        public override NumericValue Ceiling()
        {
            return new FloatValue((float)System.Math.Ceiling(value));
        }

        /// <summary>
        /// Get the value
        /// </summary>
        /// <summary>
        /// Test whether the value is the double/float value NaN
        /// </summary>
        //    }
        /// <summary>
        /// Implement the XPath round() function
        /// </summary>
        public override NumericValue Round(int scale)
        {
            if (float.IsNaN(value))
            {
                return this;
            }

            if (float.IsInfinity(value))
            {
                return this;
            }

            if (value == 0)
            {
                return this; // handles the negative zero case
            }

            if (scale == 0 && value > int.MinValue && value < int.MaxValue)
            {
                if (value >= -0.5 && value < 0)
                {
                    return new FloatValue(-0F);
                }

                return new FloatValue((float)JavaMath.Round(value));
            }

            DoubleValue d = new DoubleValue(GetDoubleValue());
            d = (DoubleValue)d.Round(scale);
            return new FloatValue(d.GetFloatValue());
        }

        /// <summary>
        /// Get the value
        /// </summary>
        /// <summary>
        /// Test whether the value is the double/float value NaN
        /// </summary>
        //    }
        /// <summary>
        /// Implement the XPath round() function
        /// </summary>
        /// <summary>
        /// Implement the XPath round-to-half-even() function
        /// </summary>
        public override NumericValue Round(int scale, Round.RoundingRule roundingRule)
        {
            DoubleValue d = new DoubleValue(GetDoubleValue());
            d = (DoubleValue)d.Round(scale, roundingRule);
            return new FloatValue(d.GetFloatValue());
        }

        /// <summary>
        /// Get the value
        /// </summary>
        /// <summary>
        /// Test whether the value is the double/float value NaN
        /// </summary>
        //    }
        /// <summary>
        /// Implement the XPath round() function
        /// </summary>
        /// <summary>
        /// Implement the XPath round-to-half-even() function
        /// </summary>
        public override int Signum()
        {
            if (float.IsNaN(value))
            {
                return 0;
            }

            return CompareTo(0);
        }

        /// <summary>
        /// Get the value
        /// </summary>
        /// <summary>
        /// Test whether the value is the double/float value NaN
        /// </summary>
        //    }
        /// <summary>
        /// Implement the XPath round() function
        /// </summary>
        /// <summary>
        /// Implement the XPath round-to-half-even() function
        /// </summary>
        public override bool IsNegativeZero()
        {
            return value == 0 && (FloatingPointConverter.SingleToInt32Bits(value) & FloatingPointConverter.FLOAT_SIGN_MASK) != 0;
        }

        /// <summary>
        /// Get the value
        /// </summary>
        /// <summary>
        /// Test whether the value is the double/float value NaN
        /// </summary>
        //    }
        /// <summary>
        /// Implement the XPath round() function
        /// </summary>
        /// <summary>
        /// Implement the XPath round-to-half-even() function
        /// </summary>
        public override bool IsWholeNumber()
        {
            return value == System.Math.Floor(value) && !float.IsInfinity(value);
        }

        /// <summary>
        /// Get the value
        /// </summary>
        /// <summary>
        /// Test whether the value is the double/float value NaN
        /// </summary>
        //    }
        /// <summary>
        /// Implement the XPath round() function
        /// </summary>
        /// <summary>
        /// Implement the XPath round-to-half-even() function
        /// </summary>
        public override int AsSubscript()
        {
            if (IsWholeNumber() && value > 0 && value <= int.MaxValue)
            {
                return (int)value;
            }
            else
            {
                return -1;
            }
        }

        /// <summary>
        /// Get the value
        /// </summary>
        /// <summary>
        /// Test whether the value is the double/float value NaN
        /// </summary>
        //    }
        /// <summary>
        /// Implement the XPath round() function
        /// </summary>
        /// <summary>
        /// Implement the XPath round-to-half-even() function
        /// </summary>
        public override NumericValue Abs()
        {
            if (value > 0)
            {
                return this;
            }
            else
            {
                return new FloatValue(System.Math.Abs(value));
            }
        }

        /// <summary>
        /// Get the value
        /// </summary>
        /// <summary>
        /// Test whether the value is the double/float value NaN
        /// </summary>
        //    }
        /// <summary>
        /// Implement the XPath round() function
        /// </summary>
        /// <summary>
        /// Implement the XPath round-to-half-even() function
        /// </summary>
        public override int CompareTo(IXPathComparable other)
        {
            if (other is NumericValue)
            {
                if (other is FloatValue)
                {
                    float otherFloat = ((FloatValue)other).value;

                    // Do not rewrite as Float.compare() - see IntelliJ bug IDEA-196419
                    if (value == otherFloat)
                    {
                        return 0;
                    }
                    else if (value < otherFloat)
                    {
                        return -1;
                    }
                    else
                    {
                        return +1;
                    }
                }

                if (other is DoubleValue)
                {
                    return base.CompareTo(other);
                }

                return CompareTo((FloatValue)Converter.NumericToFloat.INSTANCE.Convert((NumericValue)other));
            }
            else
            {
                throw new InvalidCastException("Cannot compare xs:float to " + other);
            }
        }

        /// <summary>
        /// Get the value
        /// </summary>
        /// <summary>
        /// Test whether the value is the double/float value NaN
        /// </summary>
        //    }
        /// <summary>
        /// Implement the XPath round() function
        /// </summary>
        /// <summary>
        /// Implement the XPath round-to-half-even() function
        /// </summary>
        public override int CompareTo(long other)
        {
            float otherFloat = (float)other;
            if (value == otherFloat)
            {
                return 0;
            }

            return value < otherFloat ? -1 : +1;
        }

        /// <summary>
        /// Get the value
        /// </summary>
        /// <summary>
        /// Test whether the value is the double/float value NaN
        /// </summary>
        //    }
        /// <summary>
        /// Implement the XPath round() function
        /// </summary>
        /// <summary>
        /// Implement the XPath round-to-half-even() function
        /// </summary>
        public override IAtomicMatchKey AsMapKey()
        {
            if (IsNaN())
            {
                return AtomicSortComparer.COLLATION_KEY_NaN;
            }
            else if (double.IsInfinity(value))
            {
                return new DoubleValue(value);
            }
            else
            {
                try
                {
                    return new BigDecimalValue(value);
                }
                catch (ValidationException e)
                {

                    // We have already ruled out the values that fail (NaN and INF)
                    throw new InvalidOperationException(e.Message, e);
                }
            }
        }

        /// <summary>
        /// Get the value
        /// </summary>
        /// <summary>
        /// Test whether the value is the double/float value NaN
        /// </summary>
        //    }
        /// <summary>
        /// Implement the XPath round() function
        /// </summary>
        /// <summary>
        /// Implement the XPath round-to-half-even() function
        /// </summary>
        public override bool IsIdentical(AtomicValue v)
        {
            return v is FloatValue && DoubleSortComparer.GetInstance().ComparesEqual(this, v);
        }

        /// <summary>
        /// Get the value
        /// </summary>
        /// <summary>
        /// Test whether the value is the double/float value NaN
        /// </summary>
        //    }
        /// <summary>
        /// Implement the XPath round() function
        /// </summary>
        /// <summary>
        /// Implement the XPath round-to-half-even() function
        /// </summary>
        public override AtomicValue AsAtomic()
        {
            return this;
        }
    }
}
