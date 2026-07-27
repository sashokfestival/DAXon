////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Misc Java stdlib types Saxon references but we don't yet shim individually.

using System;
using System.Collections.Generic;
using System.IO;

namespace OutSmart.DAXon.Internal.Streams
{
    public sealed class SimpleStream<T> : Stream<T>
    {
        private readonly global::System.Collections.Generic.IEnumerable<T> _src;
        private global::System.Action _onClose;
        public SimpleStream(global::System.Collections.Generic.IEnumerable<T> src) { _src = src ?? global::System.Linq.Enumerable.Empty<T>(); }
        public Stream<T> OnClose(global::System.Action closeHandler) { _onClose = closeHandler; return this; }
        public global::System.Collections.Generic.IEnumerator<T> GetEnumerator()
        {
            foreach (T item in _src) { yield return item; }
            if (_onClose != null) { _onClose(); }
        }
        global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
