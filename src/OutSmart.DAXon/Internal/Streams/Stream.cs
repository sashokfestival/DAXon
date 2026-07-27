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
    public interface Stream<T> : global::System.Collections.Generic.IEnumerable<T>
    {
        Stream<T> OnClose(global::System.Action closeHandler);
    }
    // Phase B: non-generic Stream factory (Java Stream.of) + a minimal concrete Stream so XdmItem/XdmNode
    // (Stream.Of(this)) and XdmSequenceIterator (StreamSupport.Stream(..).OnClose(..)) compile AND actually
    // iterate. SimpleStream wraps the source sequence; OnClose stores the handler and runs it after the
    // sequence is exhausted (Java Stream.onClose for the common foreach/terminal case).
    public static class Stream
    {
        public static Stream<T> Of<T>(params T[] items) => new SimpleStream<T>(items);
        public static Stream<T> Empty<T>() => new SimpleStream<T>(global::System.Linq.Enumerable.Empty<T>());
    }
}
