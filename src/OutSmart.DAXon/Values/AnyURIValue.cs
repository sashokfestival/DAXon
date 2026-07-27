////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal.Buffers;
using OutSmart.DAXon.Internal.Charsets;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Values
{
    public sealed class AnyURIValue : StringValue
    {
        public static readonly AnyURIValue EMPTY_URI = new AnyURIValue(""); // Used in bytecode

        public override BuiltInAtomicType PrimitiveType => BuiltInAtomicType.ANY_URI;
        public AnyURIValue(UnicodeString value) : base(value == null ? EmptyUnicodeString.GetInstance() : Whitespace.CollapseWhitespace(value), BuiltInAtomicType.ANY_URI)
        {
        }

        public AnyURIValue(string value) : this(StringView.Tidy(value))
        {
        }

        public AnyURIValue(UnicodeString value, IAtomicType type) : base(value == null ? "" : Whitespace.CollapseWhitespace(value).ToString(), type)
        {
        }

        public override AtomicValue CopyAsSubType(IAtomicType typeLabel)
        {
            return new AnyURIValue(this.UnicodeStringValue, typeLabel);
        }

        public StringValue ConvertToString()
        {
            return new StringValue(Content, BuiltInAtomicType.STRING);
        }

        public static string Decode(string s)
        {

            // Evaluates all escapes in s, applying UTF-8 decoding if needed.  Assumes
            // that escapes are well-formed syntactically, i.e., of the form %XX.  If a
            // sequence of escaped octets is not valid UTF-8 then the erroneous octets
            // are replaced with '\uFFFD'.
            // Exception: any "%" found between "[]" is left alone. It is an IPv6 literal
            //            with a scope_id
            //
            if (s == null)
            {
                return null;
            }

            int n = s.Length;
            if (n == 0)
            {
                return s;
            }

            if (s.IndexOf('%') < 0)
            {
                return s;
            }

            StringBuilder sb = new StringBuilder(n);
            ByteBuffer bb = ByteBuffer.Allocate(n);
            Charset utf8 = StandardCharsets.UTF_8;

            // This is not horribly efficient, but it will do for now
            char c = s[0];
            bool betweenBrackets = false;
            for (int i = 0; i < n;)
            {
                if (c == '[')
                {
                    betweenBrackets = true;
                }
                else if (betweenBrackets && c == ']')
                {
                    betweenBrackets = false;
                }

                if (c != '%' || betweenBrackets)
                {
                    sb.Append(c);
                    if (++i >= n)
                    {
                        break;
                    }

                    c = s[i];
                    continue;
                }

                bb.Clear();
                for (; ; )
                {
                    bb.Put(Hex(s[++i], s[++i]));
                    if (++i >= n)
                    {
                        break;
                    }

                    c = s[i];
                    if (c != '%')
                    {
                        break;
                    }
                }

                bb.Flip();
                sb.Append(utf8.Decode(bb));
            }

            return sb.ToString();
        }

        //
        // Loop invariant
        private static byte Hex(char high, char low)
        {
            return (byte)((HexToDec(high) << 4) | HexToDec(low));
        }

        private static int HexToDec(char c)
        {
            if (c >= '0' && c <= '9')
            {
                return c - '0';
            }
            else if (c >= 'a' && c <= 'f')
            {
                return c - 'a' + 10;
            }
            else if (c >= 'A' && c <= 'F')
            {
                return c - 'A' + 10;
            }
            else
            {
                return 0;
            }
        }
    }
}