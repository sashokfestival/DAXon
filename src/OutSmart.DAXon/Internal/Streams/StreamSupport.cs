////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Misc Java stdlib types Saxon references but we don't yet shim individually.

using System;
using System.Collections.Generic;
using System.IO;
// I4: Spliterator lived in the java.util shim namespace, whose types were visible here via
// the OLD enclosing chain (java.util.stream -> java.util). Runtime.Streams is deliberately
// NOT a child of Runtime.Collections, so the visibility must come from an explicit using now.
using OutSmart.DAXon.Internal.Collections;

namespace OutSmart.DAXon.Internal.Streams
{
    public static class StreamSupport
    {
        public static Stream<T> Stream<T>(object spliterator, bool parallel)
        {
            var src = spliterator as global::System.Collections.Generic.IEnumerable<T>;
            if (src == null && spliterator is Spliterator<T> sp && sp.Source != null) { src = Iterate<T>(sp.Source); }
            return new SimpleStream<T>(src);
        }
        private static global::System.Collections.Generic.IEnumerable<T> Iterate<T>(global::System.Collections.IEnumerator it)
        {
            while (it.MoveNext()) { yield return (T)it.Current; }
        }
    }
}
