////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Misc Java stdlib types Saxon references but we don't yet shim individually.

using System;
using System.Collections.Generic;
using System.IO;

namespace OutSmart.DAXon.Internal.Buffers
{
    public class ByteBuffer
    {
        public static ByteBuffer Allocate(int capacity) => new();
        public static ByteBuffer Wrap(byte[] arr) => new();
        public ByteBuffer Put(byte b) => this;
        public byte Get() => 0;
        public int Position() => 0;
        public ByteBuffer Position(int p) => this;
        public int Limit() => 0;
        public ByteBuffer Clear() => this;
        public ByteBuffer Flip() => this;
    }
}
