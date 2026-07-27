////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Values
{
    /// <summary>
    /// A boolean XPath value
    /// </summary>
    public sealed class BooleanValue : AtomicValue, IXPathComparable, IAtomicMatchKey, IContextFreeAtomicValue
    {
        /// <summary>
        /// The boolean value TRUE
        /// </summary>
        public static readonly BooleanValue TRUE = new BooleanValue(true);
        /// <summary>
        /// The boolean value FALSE
        /// </summary>
        public static readonly BooleanValue FALSE = new BooleanValue(false);
        private readonly bool value;

        /// <summary>
        /// The boolean value FALSE
        /// </summary>
        public override BuiltInAtomicType PrimitiveType => BuiltInAtomicType.BOOLEAN;

        /// <summary>
        /// The boolean value FALSE
        /// </summary>
        public override UnicodeString PrimitiveStringValue => value ? StringConstants.TRUE : StringConstants.FALSE;

        /// <summary>
        /// The boolean value FALSE
        /// </summary>
        public IXPathComparable XPathComparable => this;
        /// <summary>
        /// The boolean value FALSE
        /// </summary>
        private BooleanValue(bool value) : base(BuiltInAtomicType.BOOLEAN)
        {
            this.value = value;
        }

        /// <summary>
        /// The boolean value FALSE
        /// </summary>
        public BooleanValue(bool value, IAtomicType typeLabel) : base(typeLabel)
        {
            this.value = value;
        }

        /// <summary>
        /// The boolean value FALSE
        /// </summary>
        public static BooleanValue Get(bool value)
        {
            return value ? TRUE : FALSE;
        }

        /// <summary>
        /// The boolean value FALSE
        /// </summary>
        public override AtomicValue CopyAsSubType(IAtomicType typeLabel)
        {
            return new BooleanValue(value, typeLabel);
        }

        /// <summary>
        /// The boolean value FALSE
        /// </summary>
        public static IConversionResult FromString(UnicodeString s)
        {

            // implementation designed to avoid creating new objects or computing hash codes
            long start = Whitespace.TrimmedStart(s);
            long end = Whitespace.TrimmedEnd(s);
            if (start >= 0)
            {

                // start == -1 means empty string or all whitespace
                long len = end - start;
                if (len == 1)
                {
                    int first = s.CodePointAt(start);
                    if (first == '0')
                    {
                        return FALSE;
                    }
                    else if (first == '1')
                    {
                        return TRUE;
                    }
                }
                else if (len == 4)
                {
                    if (s.CodePointAt(start++) == 't' && s.CodePointAt(start++) == 'r' && s.CodePointAt(start++) == 'u' && s.CodePointAt(start) == 'e')
                    {
                        return TRUE;
                    }
                }
                else if (len == 5)
                {
                    if (s.CodePointAt(start++) == 'f' && s.CodePointAt(start++) == 'a' && s.CodePointAt(start++) == 'l' && s.CodePointAt(start++) == 's' && s.CodePointAt(start) == 'e')
                    {
                        return FALSE;
                    }
                }
            }

            ValidationFailure err = new ValidationFailure("The string " + Err.Wrap(s, Err.VALUE) + " cannot be cast to a boolean");
            err.SetErrorCode("FORG0001");
            return err;
        }

        /// <summary>
        /// The boolean value FALSE
        /// </summary>
        public bool GetBooleanValue()
        {
            return value;
        }

        /// <summary>
        /// The boolean value FALSE
        /// </summary>
        public override bool EffectiveBooleanValue()
        {
            return value;
        }

        /// <summary>
        /// The boolean value FALSE
        /// </summary>
        public override IAtomicMatchKey GetXPathMatchKey(IStringCollator collator, int implicitTimezone)
        {
            return this;
        }

        /// <summary>
        /// The boolean value FALSE
        /// </summary>
        public override IXPathComparable GetXPathComparable(IStringCollator collator, int implicitTimezone)
        {
            return this;
        }

        /// <summary>
        /// The boolean value FALSE
        /// </summary>
        public int CompareTo(IXPathComparable other)
        {
            if (other is BooleanValue)
            {
                if (value == ((BooleanValue)other).value)
                {
                    return 0;
                }

                if (value)
                {
                    return +1;
                }

                return -1;
            }
            else
            {
                throw new InvalidCastException("Cannot compare xs:boolean to " + other);
            }
        }

        /// <summary>
        /// The boolean value FALSE
        /// </summary>
        public override bool Equals(object other)
        {
            return other is BooleanValue && value == ((BooleanValue)other).value;
        }

        /// <summary>
        /// The boolean value FALSE
        /// </summary>
        public override int GetHashCode()
        {
            return value ? 0 : 1;
        }

        /// <summary>
        /// The boolean value FALSE
        /// </summary>
        public override string Show()
        {
            return this.UnicodeStringValue + "()";
        }
    }
}
