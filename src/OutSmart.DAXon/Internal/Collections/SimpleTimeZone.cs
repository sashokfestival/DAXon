////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Misc Java stdlib types Saxon references but we don't yet shim individually.

using System;
using System.Collections.Generic;
using System.IO;

namespace OutSmart.DAXon.Internal.Collections
{
    public class SimpleTimeZone : TimeZone
    {
        private readonly int _rawOffset;
        private readonly string _id;
        public override string ID => _id;
        public override int RawOffset => _rawOffset;
        public SimpleTimeZone(int rawOffsetMs, string id) { _rawOffset = rawOffsetMs; _id = id; }
    }
}
