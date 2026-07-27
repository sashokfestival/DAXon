////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Internal.Charsets;
using OutSmart.DAXon.Internal.Security;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Lib
{
    public class DigestMaker
    {
        private string hexDigest = null;
        private readonly MessageDigest digest;

        public virtual string Digest
        {
            get
            {

                // You can only call .digest() once
                if (hexDigest == null)
                {

                    // Java's String.format("%064x", new BigInteger(1, bytes)) == the 32 digest bytes as
                    // lowercase hex, zero-padded to 64. (C# String.Format has no %-conversions -- the
                    // literal translation returned the string "%064x" for every checksum.)
                    StringBuilder sb = new StringBuilder(64);
                    foreach (byte b in digest.Digest())
                    {
                        sb.Append(b.ToString("x2"));
                    }
                    hexDigest = sb.ToString();
                }

                return hexDigest;
            }
        }
        public DigestMaker()
        {
            digest = MessageDigest.GetInstance("SHA-256");
        }

        public virtual void Update(int value)
        {
            digest.Update(Convert.ToString(value).GetBytes(StandardCharsets.UTF_8));
        }

        public virtual void Update(string value)
        {
            digest.Update(value.GetBytes(StandardCharsets.UTF_8));
        }
    }
}