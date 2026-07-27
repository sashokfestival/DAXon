////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Security.Cryptography;

namespace OutSmart.DAXon.Internal.Security
{
    /// <summary>
    /// Java.security.MessageDigest shim. Saxon's DigestMaker uses this to compute
    /// content-addressable checksums. We wrap System.Security.Cryptography.HashAlgorithm.
    /// </summary>
    public class MessageDigest : IDisposable
    {
        private readonly HashAlgorithm _hash;
        public string Algorithm { get; }
        public virtual int DigestLength => _hash.HashSize / 8;

        protected MessageDigest(HashAlgorithm hash, string algorithm)
        {
            _hash = hash;
            Algorithm = algorithm;
        }

        public static MessageDigest GetInstance(string algorithm)
        {
            HashAlgorithm h = algorithm switch
            {
                "MD5" => MD5.Create(),
                "SHA-1" => SHA1.Create(),
                "SHA-256" => SHA256.Create(),
                "SHA-384" => SHA384.Create(),
                "SHA-512" => SHA512.Create(),
                _ => throw new NotSupportedException($"MessageDigest algorithm not supported: {algorithm}")
            };
            return new MessageDigest(h, algorithm);
        }

        public virtual void Update(byte input) => _hash.TransformBlock(new[] { input }, 0, 1, null, 0);
        public virtual void Update(byte[] input) => _hash.TransformBlock(input, 0, input.Length, null, 0);
        public virtual void Update(byte[] input, int offset, int len) => _hash.TransformBlock(input, offset, len, null, 0);

        public virtual byte[] Digest()
        {
            _hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return _hash.Hash;
        }

        public virtual byte[] Digest(byte[] input)
        {
            Update(input);
            return Digest();
        }

        public virtual void Reset() { /* System hash impls reset on TransformFinalBlock automatically */ }

        public void Dispose() => _hash?.Dispose();
    }
}
