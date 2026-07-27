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
    public class Spliterator
    {
        public const int ORDERED = 16;
        public const int DISTINCT = 1;
        public const int SORTED = 4;
        public const int SIZED = 64;
        public const int NONNULL = 256;
        public const int IMMUTABLE = 1024;
        public const int CONCURRENT = 4096;
    }
    public class Spliterator<T> : Spliterator
    {
        public readonly global::System.Collections.IEnumerator Source;
        public Spliterator() { }
        public Spliterator(global::System.Collections.IEnumerator source) { Source = source; }
    }
}
