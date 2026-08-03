////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Collections;


// IGroundedValue.CodePoints() extension (paulirwin emits .CodePoints on IGroundedValue
// but the interface doesn't declare it). Returns IIntIterator over string-value code points.
namespace OutSmart.DAXon.Model
{
    internal static class IGroundedValueExtensions
    {
        public static IIntIterator CodePoints(this IGroundedValue v)
        {
            if (v == null)
                return null;
            var s = v.GetStringValue();
            if (string.IsNullOrEmpty(s))
                return null;
            return new _IGVStringCodePointIterator(s);
        }
        private sealed class _IGVStringCodePointIterator : AbstractIntIterator
        {
            private readonly string _s;
            private int _i;
            public _IGVStringCodePointIterator(string s) { _s = s ?? ""; }
            public override bool HasNext() => _i < _s.Length;
            public override int Next() => _i < _s.Length ? _s[_i++] : -1;
        }
    }
}
