using System;
using System.IO;
using OutSmart.DAXon.Transformation;

namespace OutSmart.DAXon.Internal.Streams
{
    /// <summary>
    /// Enforces the Processor's MaxInputBytes on resolver-fetched resources (doc/document/
    /// collection/unparsed-text/json-doc and compile-time includes): a seekable source is
    /// rejected up front by its exact length; an unseekable one (e.g. an HTTP response)
    /// aborts as soon as the running byte count crosses the cap. The error carries the
    /// retrieval-failure code of the requesting function (FODC0002 for documents,
    /// FOUT1170 for text), so xsl:try can catch it like any other fetch failure.
    /// </summary>
    public static class InputSizeLimit
    {
        public static System.IO.Stream Apply(System.IO.Stream stream, long max, string uri, string errorCode)
        {
            if (stream == null || max == long.MaxValue)
            {
                return stream;
            }

            long length = -1;
            if (stream.CanSeek)
            {
                try
                {
                    length = stream.Length;
                }
                catch (NotSupportedException)
                {
                }
            }

            if (length >= 0)
            {
                if (length > max)
                {
                    stream.Dispose();
                    throw Oversized(length, max, uri, errorCode);
                }

                return stream;   // exact length known and under the cap - no wrapper needed
            }

            return new CappedStream(stream, max, uri, errorCode);
        }

        internal static XPathException Oversized(long size, long max, string uri, string errorCode)
        {
            string sizePart = size >= 0 ? " (" + size + " bytes)" : "";
            return new XPathException("Input resource" + sizePart + " exceeds the Processor's MaxInputBytes limit of "
                + max + " bytes: " + uri).WithErrorCode(errorCode);
        }

        // Read-only pass-through counting wrapper for sources whose length is unknown up front.
        private sealed class CappedStream : System.IO.Stream
        {
            private readonly System.IO.Stream inner;
            private readonly long max;
            private readonly string uri;
            private readonly string errorCode;
            private long count;

            internal CappedStream(System.IO.Stream inner, long max, string uri, string errorCode)
            {
                this.inner = inner;
                this.max = max;
                this.uri = uri;
                this.errorCode = errorCode;
            }

            public override int Read(byte[] buffer, int offset, int length)
            {
                int n = inner.Read(buffer, offset, length);
                if (n > 0 && (count += n) > max)
                {
                    throw Oversized(-1, max, uri, errorCode);
                }

                return n;
            }

            public override int ReadByte()
            {
                int b = inner.ReadByte();
                if (b >= 0 && ++count > max)
                {
                    throw Oversized(-1, max, uri, errorCode);
                }

                return b;
            }

            public override bool CanRead => inner.CanRead;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => count; set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int length) => throw new NotSupportedException();

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
