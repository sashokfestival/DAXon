////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Lib;
using System.IO;
using System.Text;

namespace OutSmart.DAXon.Resources
{

    public class BinaryResource
    {
        // FACTORY typed as IResourceFactory for RegisterMediaType callers.
        public static readonly IResourceFactory FACTORY = new GenericResourceFactory();
        public byte[] Data => throw new NotImplementedException("STUB: BinaryResource.GetData not ported (excluded stub)");
        public BinaryResource() { }
        public BinaryResource(object src, byte[] data) { }
        // AbstractResourceCollection invokes BinaryResource.Encode/Decode.
        public static string Encode(byte[] data) => Convert.ToBase64String(data ?? new byte[0]);
        public static byte[] Decode(string s) => string.IsNullOrEmpty(s) ? new byte[0] : Convert.FromBase64String(s);
        public static byte[] Encode(string s, string encoding) { try { return Encoding.GetEncoding(encoding).GetBytes(s ?? ""); } catch { return Encoding.UTF8.GetBytes(s ?? ""); } }
        public static string Decode(byte[] value, string encoding) { try { return Encoding.GetEncoding(encoding).GetString(value ?? new byte[0]); } catch { return Encoding.UTF8.GetString(value ?? new byte[0]); } }
        public static byte[] ReadBinaryFromStream(Stream stream, string uri) => Array.Empty<byte>();
        public static byte[] ReadBinaryFromStream(Stream stream) => Array.Empty<byte>();
    }
}
