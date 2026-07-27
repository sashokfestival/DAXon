////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Misc Java stdlib types Saxon references but we don't yet shim individually.

using System;
using System.Collections.Generic;
using System.IO;

namespace OutSmart.DAXon.Internal
{
    public sealed class Short
    {
        public static readonly int MAX_VALUE = short.MaxValue;
        public static readonly int MIN_VALUE = short.MinValue;
        public short Value;
        public Short(short v) { Value = v; }
        public static short ParseShort(string s) => short.Parse(s);
        public int IntValue() => Value;
        public static implicit operator Short(int v) => new Short((short)v);
        public static implicit operator Short(short v) => new Short(v);
        public static implicit operator short(Short s) => s?.Value ?? 0;
        public static implicit operator int(Short s) => s?.Value ?? 0;
    }
}
