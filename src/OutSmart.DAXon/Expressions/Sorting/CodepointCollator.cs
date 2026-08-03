////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Text;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions.Sorting
{
    /// <summary>
    /// A collating sequence that uses Unicode codepoint ordering
    /// </summary>
    internal class CodepointCollator : IStringCollator, ISubstringMatcher
    {
        private static readonly CodepointCollator theInstance = new CodepointCollator();

        public virtual string CollationURI => NamespaceConstant.CODEPOINT_COLLATION_URI;
        public static CodepointCollator GetInstance()
        {
            return theInstance;
        }

        public virtual int CompareStrings(UnicodeString a, UnicodeString b)
        {
            return a.CompareTo(b);
        }

        public virtual bool ComparesEqual(UnicodeString s1, UnicodeString s2)
        {
            return s1.Equals(s2);
        }

        public virtual bool Contains(UnicodeString s1, UnicodeString s2)
        {
            return s1.IndexOf(s2, 0) >= 0;
        }

        public virtual bool EndsWith(UnicodeString s1, UnicodeString s2)
        {
            if (s2.Length() > s1.Length())
            {
                return false;
            }

            return s1.HasSubstring(s2, s1.Length() - s2.Length());
        }

        public virtual bool StartsWith(UnicodeString s1, UnicodeString s2)
        {
            return s1.HasSubstring(s2, 0);
        }

        public virtual UnicodeString SubstringAfter(UnicodeString s1, UnicodeString s2)
        {
            long i = s1.IndexOf(s2, 0);
            if (i < 0)
            {
                return EmptyUnicodeString.GetInstance();
            }

            return s1.Substring(i + s2.Length());
        }

        public virtual UnicodeString SubstringBefore(UnicodeString s1, UnicodeString s2)
        {
            long j = s1.IndexOf(s2, 0);
            if (j < 0)
            {
                return EmptyUnicodeString.GetInstance();
            }

            return s1.Prefix(j);
        }

        public virtual IAtomicMatchKey GetCollationKey(UnicodeString s)
        {
            return s;
        }

        public virtual bool IsEqualToEmpty(UnicodeString s1)
        {
            return s1.IsEmpty();
        }
    }
}