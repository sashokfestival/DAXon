////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
namespace OutSmart.DAXon.Values
{
    public class StringValue : AtomicValue
    {
        public static readonly StringValue EMPTY_STRING = new StringValue(EmptyUnicodeString.GetInstance());
        public static readonly StringValue SINGLE_SPACE = new StringValue(StringConstants.SINGLE_SPACE);
        public static readonly StringValue TRUE = new StringValue(StringConstants.TRUE);
        public static readonly StringValue FALSE = new StringValue(StringConstants.FALSE);
        public static readonly StringValue ZERO_LENGTH_UNTYPED = StringValue.MakeUntypedAtomic(EmptyUnicodeString.GetInstance());
        protected readonly UnicodeString content;

        /// <summary>
        /// Protected constructor for use by subtypes
        /// </summary>
        public override BuiltInAtomicType PrimitiveType => typeLabel == BuiltInAtomicType.UNTYPED_ATOMIC ? BuiltInAtomicType.UNTYPED_ATOMIC : BuiltInAtomicType.STRING;

        /// <summary>
        /// Protected constructor for use by subtypes
        /// </summary>
        public override UnicodeString PrimitiveStringValue => content;

        /// <summary>
        /// Protected constructor for use by subtypes
        /// </summary>
        public virtual UnicodeString Content => content;

        /// <summary>
        /// Protected constructor for use by subtypes
        /// </summary>
        public virtual Base64BinaryValue CodepointCollationKey
        {
            get
            {
                int len = Content.Length32();
                byte[] result = new byte[len * 3];
                for (int i = 0, j = 0; i < len; i++)
                {
                    int c = Content.CodePointAt(i);
                    result[j++] = (byte)(c >> 16);
                    result[j++] = (byte)(c >> 8);
                    result[j++] = (byte)c;
                }

                return new Base64BinaryValue(result);
            }
        }

        /// <summary>
        /// Protected constructor for use by subtypes
        /// </summary>
        public override UnicodeString UnicodeStringValue => content;
        /// <summary>
        /// Protected constructor for use by subtypes
        /// </summary>
        protected StringValue() : base(BuiltInAtomicType.STRING)
        {
            content = EmptyUnicodeString.GetInstance();
        }

        /// <summary>
        /// Protected constructor for use by subtypes
        /// </summary>
        protected StringValue(IAtomicType typeLabel) : base(typeLabel)
        {
            content = EmptyUnicodeString.GetInstance();
        }

        /// <summary>
        /// Protected constructor for use by subtypes
        /// </summary>
        public StringValue(UnicodeString content) : this(content, BuiltInAtomicType.STRING)
        {
        }

        /// <summary>
        /// Protected constructor for use by subtypes
        /// </summary>
        public StringValue(UnicodeString content, IAtomicType type) : base(type)
        {
            this.content = content;
        }

        /// <summary>
        /// Protected constructor for use by subtypes
        /// </summary>
        public StringValue(string value) : this(value, BuiltInAtomicType.STRING)
        {
        }

        /// <summary>
        /// Protected constructor for use by subtypes
        /// </summary>
        public StringValue(string value, IAtomicType typeLabel) : base(typeLabel)
        {
            this.content = StringTool.FromCharSequence(value);
        }

        /// <summary>
        /// Protected constructor for use by subtypes
        /// </summary>
        public static StringValue MakeUntypedAtomic(UnicodeString value)
        {
            return new StringValue(value, BuiltInAtomicType.UNTYPED_ATOMIC);
        }

        /// <summary>
        /// Protected constructor for use by subtypes
        /// </summary>
        public override AtomicValue CopyAsSubType(IAtomicType typeLabel)
        {
            if (typeLabel == this.typeLabel)
            {
                return this;
            }
            else
            {
                return new StringValue(this.content, typeLabel);
            }
        }

        /// <summary>
        /// Protected constructor for use by subtypes
        /// </summary>
        public static StringValue Bmp(string content)
        {

            //TODO: most if not all calls supply a literal String. Use a static constant pool.
            return new StringValue(BMPString.Of(content));
        }

        /// <summary>
        /// Protected constructor for use by subtypes
        /// </summary>
        public static StringValue MakeStringValue(string value)
        {
            if (value == null || value.Length == 0)
            {
                return StringValue.EMPTY_STRING;
            }
            else
            {
                return new StringValue(value.ToString());
            }
        }

        /// <summary>
        /// Protected constructor for use by subtypes
        /// </summary>
        public virtual StringValue Economize()
        {
            UnicodeString c2 = content.Economize();
            if (c2 == content)
            {
                return this;
            }

            return new StringValue(c2, typeLabel);
        }

        /// <summary>
        /// Protected constructor for use by subtypes
        /// </summary>
        public static StringValue MakeUStringValue(UnicodeString value)
        {
            if (value == null || value.IsEmpty())
            {
                return StringValue.EMPTY_STRING;
            }
            else
            {
                return new StringValue(value);
            }
        }

        /// <summary>
        /// Protected constructor for use by subtypes
        /// </summary>
        public virtual long Length()
        {
            return content.Length();
        }

        /// <summary>
        /// Protected constructor for use by subtypes
        /// </summary>
        public virtual int Length32()
        {
            return content.Length32();
        }

        /// <summary>
        /// Protected constructor for use by subtypes
        /// </summary>
        public virtual bool IsEmpty()
        {
            return content.IsEmpty();
        }

        /// <summary>
        /// Protected constructor for use by subtypes
        /// </summary>
        public virtual IAtomicIterator IterateCharacters()
        {
            lock (this)
            {
                return new CodepointIterator(CodePoints());
            }
        }

        /// <summary>
        /// Protected constructor for use by subtypes
        /// </summary>
        public override IAtomicMatchKey GetXPathMatchKey(IStringCollator collator, int implicitTimezone)
        {
            return collator.GetCollationKey(this.UnicodeStringValue);
        }

        /// <summary>
        /// Protected constructor for use by subtypes
        /// </summary>
        public virtual IIntIterator CodePoints()
        {
            return content.CodePoints();
        }

        /// <summary>
        /// Protected constructor for use by subtypes
        /// </summary>
        public override int GetHashCode()
        {

            // Same algorithm as String#hashCode(), but not cached; and truncated after 100 characters
            int h = 0;
            int count = 0;
            IIntIterator iter = CodePoints();
            while (iter.MoveNext())
            {
                h = 31 * h + iter.Current;
                if (++count >= 100)
                {
                    break;
                }
            }

            return h;
        }

        /// <summary>
        /// Protected constructor for use by subtypes
        /// </summary>
        public override bool Equals(object o)
        {
            if (o is StringValue)
            {
                return content.Equals(((StringValue)o).content);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Protected constructor for use by subtypes
        /// </summary>
        public override bool EffectiveBooleanValue()
        {
            return !IsEmpty();
        }

        /// <summary>
        /// Protected constructor for use by subtypes
        /// </summary>
        public override string ToString()
        {
            return Content.ToString();
        }

        /// <summary>
        /// Protected constructor for use by subtypes
        /// </summary>
        public override string ToShortString()
        {
            string s = content.ToString();
            if (s.Length > 40)
            {
                s = s.Substring(0, 20) + " ... " + s.Substring(s.Length - 20);
            }

            s = "\"" + s + '"';
            if (typeLabel == BuiltInAtomicType.UNTYPED_ATOMIC)
            {
                s = "u" + s;
            }

            return s;
        }

        /// <summary>
        /// Protected constructor for use by subtypes
        /// </summary>
        public override IXPathComparable GetXPathComparable(IStringCollator collator, int implicitTimezone)
        {
            return new AnonymousXPathComparable(this, collator);
        }

        /// <summary>
        /// Protected constructor for use by subtypes
        /// </summary>
        public override bool IsIdentical(AtomicValue v)
        {
            return v is StringValue && (this is AnyURIValue == v is AnyURIValue) && (this.IsUntypedAtomic() == v.IsUntypedAtomic()) && Equals(v);
        }

        private sealed class AnonymousXPathComparable : IXPathComparable
        {

            private readonly StringValue parent; private readonly IStringCollator collator;
            public AnonymousXPathComparable(StringValue parent, IStringCollator collator)
            {
                this.parent = parent; this.collator = collator;
            }
            public int CompareTo(IXPathComparable o)
            {
                if (o is StringValue)
                {
                    return collator.CompareStrings(parent.Content, ((StringValue)o).content);
                }
                else
                {
                    throw new InvalidCastException("Cannot compare xs:string to " + o.ToString());
                }
            }
        }

        /// <summary>
        /// Protected constructor for use by subtypes
        /// </summary>
        public sealed class CharacterIterator : IAtomicIterator
        {
            int inpos = 0; // 0-based index of the current Java char
            private readonly string value;
            public CharacterIterator(string value)
            {
                this.value = value;
            }

            public Int64Value Next()
            {
                if (inpos < value.Length)
                {
                    int c = value.CharAt(inpos++);
                    int current;
                    if (c >= 55296 && c <= 56319)
                    {

                        // we'll trust the data to be sound
                        try
                        {
                            current = ((c - 55296) * 1024) + ((int)value.CharAt(inpos++) - 56320) + 65536;
                        }
                        catch (IndexOutOfRangeException e)
                        {
                            throw new InvalidOperationException("Invalid surrogate at end of string: " + StringTool.DiagnosticDisplay(value.ToString()));
                        }
                    }
                    else
                    {
                        current = c;
                    }

                    return new Int64Value(current);
                }
                else
                {
                    return null;
                }
            }
            AtomicValue IAtomicIterator.Next() => Next();
            IItem ISequenceIterator.Next() => Next(); // redirect StubGen hollow to the real covariant Next(); default = silent empty iteration
            public void Dispose() { }
        }

        /// <summary>
        /// Protected constructor for use by subtypes
        /// </summary>
        public sealed class Builder : IUniStringConsumer
        {
            UnicodeBuilder buffer = new UnicodeBuilder();
            public IUniStringConsumer Accept(UnicodeString chars)
            {
                buffer.Accept(chars);
                return this;
            }

            public UnicodeString GetStringValue()
            {
                return buffer.ToUnicodeString();
            }

            // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
            public void Append(IItem item, ILocation locationId, int properties) { throw new NotImplementedException(); }
            public void Append(IItem item) { throw new NotImplementedException(); }
            public bool HandlesAppend() => throw new NotImplementedException();
            public void Open() { throw new NotImplementedException(); }
            public void Dispose() { throw new NotImplementedException(); }
        }
    }
}

