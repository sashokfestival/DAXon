////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using System.IO;
namespace OutSmart.DAXon.Text
{
    public sealed class LargeTextBuffer
    {
        private const int BITS = 16;
        private const int SEGLEN = 1 << BITS;
        private const int MASK = SEGLEN - 1;
        private static readonly int MAX_SEGMENTS = 1 << (31 - BITS);
        private static readonly ISegment EMPTY_SEGMENT = new Segment8(new byte[] { });
        private readonly IList<ISegment> completeSegments;
        private ISegment lastSegment;
        private int lastSegmentLength;
        private int initialSize;

        public LargeTextBuffer(int initialSize)
        {
            completeSegments = new List<ISegment>(4);
            lastSegment = EMPTY_SEGMENT;
            lastSegmentLength = 0;
            this.initialSize = System.Math.Max(initialSize, 65536);
        }

        private void AddSegment(ISegment segment)
        {
            if (completeSegments.Count == MAX_SEGMENTS)
            {
                throw new InvalidOperationException("TinyTree capacity exceeded: more than 2^31 characters of text data");
            }

            completeSegments.Add(segment);
        }

        private ISegment GetSegment(int n)
        {
            if (n == completeSegments.Count)
            {
                return lastSegment;
            }
            else
            {
                return completeSegments[n];
            }
        }

        public void AppendUnicodeString(UnicodeString chars)
        {
            if (chars.IsEmpty())
            {
                return;
            }

            long charsSupplied = chars.Length32(); // read once; reused below and for the first segment size
            if (lastSegment == EMPTY_SEGMENT)
            {

                // indicates this is the first string being added; a Latin1-content
                // BMPString (typical stylesheet literal) starts a byte segment
                int newWidth = chars.Width;
                if (newWidth == 16 && chars is BMPString firstBmp && firstBmp.IsLatin1)
                {
                    newWidth = 8;
                }

                int newLength = System.Math.Max(initialSize, (int)charsSupplied) & 65535;
                if (newWidth <= 8)
                {
                    lastSegment = new Segment8(new byte[newLength]);
                }
                else if (newWidth == 16)
                {
                    lastSegment = new Segment16(new char[newLength]);
                }
                else
                {
                    lastSegment = new Segment24(new byte[newLength * 3]);
                }
            }

            int spaceAvailableInLastSegment = SEGLEN - lastSegmentLength;

            if (charsSupplied < spaceAvailableInLastSegment)
            {
                ExtendLastSegment(chars);
            }
            else
            {
                long start = 0;
                ExtendLastSegment(chars.Substring(0, spaceAvailableInLastSegment));
                charsSupplied -= spaceAvailableInLastSegment;
                start += spaceAvailableInLastSegment;
                while (charsSupplied > SEGLEN)
                {

                    ExtendLastSegment(chars.Substring(start, start + SEGLEN));
                    charsSupplied -= SEGLEN;
                    start += SEGLEN;
                }

                if (charsSupplied > 0)
                {

                    ExtendLastSegment(chars.Substring(start, start + charsSupplied));
                }
            }
        }

        private void ExtendLastSegment(UnicodeString chars)
        {

            // A Latin1-content BMPString (typical stylesheet literal) appends as bytes:
            // one ASCII literal must not widen the whole segment to char[] for good
            int width = chars.Width;
            if (width == 16 && lastSegment.Width <= 8 && chars is BMPString bmp && bmp.IsLatin1)
            {
                width = 8;
            }

            int addedLen = chars.Length32(); // hoisted: was called twice (Stretch + lastSegmentLength)
            lastSegment = lastSegment.Stretch(lastSegmentLength, lastSegmentLength + addedLen, width);
            if (lastSegment is Segment8)
            {
                chars.Copy8bit(((Segment8)lastSegment).bytes, lastSegmentLength);
            }
            else if (lastSegment is Segment16)
            {
                chars.Copy16bit(((Segment16)lastSegment).chars, lastSegmentLength);
            }
            else
            {
                chars.Copy24bit(((Segment24)lastSegment).bytes, lastSegmentLength * 3);
            }

            lastSegmentLength += addedLen;

            //            lastSegment.substring(0, lastSegmentLength).verifyCharacters();
            //        }
            if (lastSegmentLength == SEGLEN)
            {
                AddSegment(lastSegment);
                lastSegment = new Segment8(new byte[1024]);
                lastSegmentLength = 0;
            } //showSegmentLengths();
        }

        // Diagnostic method
        private void ShowSegmentLengths()
        {
            StringBuilder sb = new StringBuilder();
            foreach (ISegment s in completeSegments)
            {
                sb.Append(s.AsUnicodeString().Length()).Append(", ");
            }

            sb.Append(lastSegmentLength);
            Console.Error.WriteLine(sb);
        }

        public UnicodeString Substring(int start, int end)
        {
            int firstSeg = start >> BITS;
            int lastSeg = (end - 1) >> BITS;
            int lastCP = end & MASK;
            if (lastCP == 0)
            {
                lastCP = SEGLEN;
            }

            if (firstSeg == lastSeg)
            {

                // String falls entirely within one segment
                try
                {
                    ISegment seg = GetSegment(firstSeg);
                    return seg.Substring(start & MASK, lastCP);
                }
                catch (IndexOutOfRangeException e)
                {
                    e.ToString();
                    throw e;
                }
            }
            else
            {

                // Concatenate strings from two or more segments
                UnicodeBuilder ub = new UnicodeBuilder();
                int segNr = firstSeg;
                ub.Accept(GetSegment(segNr++).Substring(start & MASK, SEGLEN));
                while (segNr < lastSeg)
                {
                    ub.Accept(GetSegment(segNr++).AsUnicodeString());
                }

                ub.Accept(GetSegment(lastSeg).Substring(0, lastCP));
                return ub.ToUnicodeString();
            }
        }

        public void Dispose()
        {
        }

        public int Length()
        {
            if (lastSegment == EMPTY_SEGMENT)
            {
                return 0;
            }
            else if (lastSegment == null)
            {
                return (completeSegments.Count - 1) * SEGLEN + lastSegmentLength;
            }
            else
            {
                return completeSegments.Count * SEGLEN + lastSegmentLength;
            }
        }

        public void SetLength(int newLength)
        {

            // used to remove a text node if it's found to be a duplicate
            if (newLength < Length())
            {
                int segCount = completeSegments.Count;
                if (newLength <= segCount * SEGLEN)
                {

                    // drop the current "last segment", and make the last segment in the completed list
                    // the new "last segment"
                    lastSegment = completeSegments[segCount - 1];
                    completeSegments.Remove(segCount - 1);
                }

                lastSegmentLength = newLength & MASK;
            }
        }
        private interface ISegment
        {
            int Width { get; }
            ISegment Stretch(int oldLength, int newLength, int newWidth);
            UnicodeString AsUnicodeString();
            UnicodeString Substring(int start, int end);
        }

        /// <summary>
        /// A ISegment comprising 8-bit characters (codepoints in the range 0-255)
        /// </summary>
        private class Segment8 : ISegment
        {
            public byte[] bytes;

            public virtual int Width => 8;
            public Segment8(byte[] bytes)
            {
                this.bytes = bytes;
            }

            public virtual ISegment Stretch(int oldLength, int newLength, int newWidth)
            {
                if (newWidth <= 8)
                {
                    if (newLength > bytes.Length)
                    {
                        bytes = ArrayTools.CopyOf(bytes, System.Math.Max(newLength, System.Math.Min(oldLength * 2, SEGLEN)));
                    }

                    return this;
                }
                else if (newWidth == 16)
                {
                    char[] array16 = new char[newLength];
                    StringTool.Copy8to16(bytes, 0, array16, 0, oldLength);
                    return new Segment16(array16);
                }
                else
                {
                    byte[] array24 = new byte[newLength * 3];
                    StringTool.Copy8to24(bytes, 0, array24, 0, oldLength);
                    return new Segment24(array24);
                }
            }

            public virtual UnicodeString AsUnicodeString()
            {
                return new Twine8(bytes);
            }

            public virtual UnicodeString Substring(int start, int end)
            {
                return new Slice8(bytes, start, end);
            }
        }

        /// <summary>
        /// A ISegment comprising 16-bit characters (codepoints in the range 0-65535)
        /// </summary>
        private class Segment16 : ISegment
        {
            public char[] chars;

            public virtual int Width => 16;
            public Segment16(char[] chars)
            {
                this.chars = chars;
            }

            public virtual ISegment Stretch(int oldLength, int newLength, int newWidth)
            {
                if (newWidth <= 16)
                {
                    if (newLength > chars.Length)
                    {
                        chars = ArrayTools.CopyOf(chars, System.Math.Max(newLength, System.Math.Min(oldLength * 2, SEGLEN)));
                    }

                    return this;
                }
                else
                {
                    byte[] array24 = new byte[newLength * 3];
                    StringTool.Copy16to24(chars, 0, array24, 0, oldLength);
                    return new Segment24(array24);
                }
            }

            public virtual UnicodeString AsUnicodeString()
            {
                return new Twine16(chars);
            }

            public virtual UnicodeString Substring(int start, int end)
            {
                return new Slice16(chars, start, end);
            }
        }

        /// <summary>
        /// A ISegment comprising 24-bit characters (any Unicode codepoints)
        /// </summary>
        private class Segment24 : ISegment
        {
            public byte[] bytes;

            public virtual int Width => 24;
            public Segment24(byte[] bytes)
            {
                this.bytes = bytes;
            }

            public virtual ISegment Stretch(int oldLength, int newLength, int newWidth)
            {
                if (newLength * 3 > bytes.Length)
                {
                    bytes = ArrayTools.CopyOf(bytes, System.Math.Max(newLength * 3, System.Math.Min(oldLength * 6, SEGLEN * 3)));
                }

                return this;
            }

            public virtual UnicodeString Substring(int start, int length)
            {
                return new Slice24(bytes, start, length);
            }

            public virtual UnicodeString AsUnicodeString()
            {
                return new Twine24(bytes);
            }
        }
    }
}