////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace OutSmart.DAXon.Internal.Net
{
    /// <summary>
    /// Bounds blocking network I/O by the run's remaining wall-clock budget. The transformation
    /// deadline is cooperative, so a thread parked in a socket call never reaches a check: a server
    /// that never answers, or answers one byte at a time forever, would hold a worker thread for as
    /// long as it likes whatever TransformTimeout says. Two halves are needed - the request timeout
    /// bounds one stalled connect/read, the stream guard bounds an endless sequence of quick ones.
    /// </summary>
    internal static class NetworkDeadline
    {
        /// <summary>
        /// Give the socket layer the run's remaining time as its own connect/read timeout. A run
        /// with no deadline armed keeps the platform defaults (the host asked for no limit).
        /// </summary>
        internal static void Apply(System.Net.WebRequest request)
        {
            int remaining = OutSmart.DAXon.Core.Controller.RemainingMillis();
            if (request == null || remaining < 0)
            {
                return;
            }

            request.Timeout = remaining;
            if (request is System.Net.HttpWebRequest http)
            {
                http.ReadWriteTimeout = remaining;
            }
        }

        /// <summary>
        /// Wrap a response stream so the deadline is re-checked before every read. Bounds the
        /// trickling server, whose individual reads all return well inside the request timeout.
        /// </summary>
        internal static System.IO.Stream Guard(System.IO.Stream stream)
        {
            return stream == null ? null : new GuardedStream(stream);
        }

        // Read-only pass-through; the only behaviour it adds is the pre-read deadline check.
        private sealed class GuardedStream : System.IO.Stream
        {
            private readonly System.IO.Stream inner;

            internal GuardedStream(System.IO.Stream inner)
            {
                this.inner = inner;
            }

            public override bool CanRead => inner.CanRead;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => inner.Length;

            public override long Position
            {
                get => inner.Position;
                set => throw new System.NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                OutSmart.DAXon.Core.Controller.CheckActiveTimeoutNow();
                return inner.Read(buffer, offset, count);
            }

            public override int ReadByte()
            {
                OutSmart.DAXon.Core.Controller.CheckActiveTimeoutNow();
                return inner.ReadByte();
            }

            public override void Flush()
            {
                inner.Flush();
            }

            public override long Seek(long offset, System.IO.SeekOrigin origin) => throw new System.NotSupportedException();
            public override void SetLength(long value) => throw new System.NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new System.NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    inner.Dispose();
                }

                base.Dispose(disposing);
            }
        }
    }
}
