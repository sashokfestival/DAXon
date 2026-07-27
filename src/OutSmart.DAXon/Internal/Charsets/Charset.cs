////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Misc Java stdlib types Saxon references but we don't yet shim individually.

using System;
using System.Collections.Generic;
using System.IO;

namespace OutSmart.DAXon.Internal.Charsets
{
    public class Charset
    {
        // Phase 5: Inner Encoding for str.GetBytes(Charset) ext.
        public global::System.Text.Encoding Inner { get; }
        public Charset() { Inner = global::System.Text.Encoding.UTF8; }
        public Charset(global::System.Text.Encoding e) { Inner = e ?? global::System.Text.Encoding.UTF8; }
        public string Name() => Inner?.WebName ?? "";
        public static Charset ForName(string name) { try { return new Charset(global::System.Text.Encoding.GetEncoding(name)); } catch { return new Charset(); } }
        public static Charset DefaultCharset() => new Charset();
        // Java Charset.decode(ByteBuffer) -> CharBuffer; stub ByteBuffer has no content, so empty.
        public string Decode(global::OutSmart.DAXon.Internal.Buffers.ByteBuffer bb) => "";
        // Phase 7.8: CharacterSetFactory iterates Charset.AvailableCharsets().KeySet().
        public static global::System.Collections.Generic.SortedDictionary<string, Charset> AvailableCharsets()
        {
            var m = new global::System.Collections.Generic.SortedDictionary<string, Charset>();
            foreach (var enc in global::System.Text.Encoding.GetEncodings())
            {
                try { m[enc.Name] = new Charset(enc.GetEncoding()); } catch { /* skip */ }
            }
            return m;
        }
    }
}
