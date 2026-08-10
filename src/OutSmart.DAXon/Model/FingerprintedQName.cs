////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Model
{
    internal class FingerprintedQName : INodeName
    {
        private readonly StructuredQName qName;
        private int fingerprint = -1;

        public virtual int Fingerprint => fingerprint;

        public virtual string DisplayName => qName.DisplayName;
        public FingerprintedQName(string prefix, NamespaceUri uri, string localName)
        {
            qName = new StructuredQName(prefix, uri, localName);
        }

        public FingerprintedQName(string prefix, NamespaceUri uri, string localName, int fingerprint)
        {
            qName = new StructuredQName(prefix, uri, localName);
            this.fingerprint = fingerprint;
        }

        public FingerprintedQName(StructuredQName qName)
        {
            this.qName = qName;
        }

        public FingerprintedQName(StructuredQName qName, NamePool pool)
        {
            this.qName = qName;
            this.fingerprint = pool.AllocateFingerprint(qName.GetNamespaceUri(), qName.GetLocalPart());
        }

        public static FingerprintedQName FromClarkName(string expandedName)
        {
            string @namespace;
            string localName;
            if (expandedName[0] == '{')
            {
                int closeBrace = expandedName.IndexOf('}');
                if (closeBrace < 0)
                {
                    throw new ArgumentException("No closing '}' in Clark name");
                }

                @namespace = expandedName.Substring(1, closeBrace - 1) /*Java substring(begin,END) -> C# (start,LENGTH)*/;
                if (closeBrace == expandedName.Length)
                {
                    throw new ArgumentException("Missing local part in Clark name");
                }

                localName = expandedName.Substring(closeBrace + 1);
            }
            else
            {
                @namespace = "";
                localName = expandedName;
            }

            return new FingerprintedQName("", NamespaceUri.Of(@namespace), localName);
        }

        public static FingerprintedQName FromEQName(string expandedName)
        {
            string @namespace;
            string localName;
            if (expandedName.StartsWith("Q{", StringComparison.Ordinal))
            {
                int closeBrace = expandedName.IndexOf('}', 2);
                if (closeBrace < 0)
                {
                    throw new ArgumentException("No closing '}' in EQName");
                }

                @namespace = expandedName.Substring(2, closeBrace - 2) /*Java substring(begin,END) -> C# (start,LENGTH)*/;
                if (closeBrace == expandedName.Length)
                {
                    throw new ArgumentException("Missing local part in EQName");
                }

                localName = expandedName.Substring(closeBrace + 1);
            }
            else
            {
                @namespace = "";
                localName = expandedName;
            }

            return new FingerprintedQName("", NamespaceUri.Of(@namespace), localName);
        }

        public virtual bool HasFingerprint()
        {
            return fingerprint != -1;
        }

        public virtual int ObtainFingerprint(NamePool pool)
        {
            if (fingerprint == -1)
            {
                fingerprint = pool.AllocateFingerprint(GetNamespaceUri(), GetLocalPart());
            }

            return fingerprint;
        }

        public virtual string GetPrefix()
        {
            return qName.GetPrefix();
        }

        public virtual NamespaceUri GetNamespaceUri()
        {
            return qName.GetNamespaceUri();
        }

        public virtual string GetLocalPart()
        {
            return qName.GetLocalPart();
        }

        public virtual StructuredQName GetStructuredQName()
        {
            return qName;
        }

        public virtual bool HasURI(NamespaceUri ns)
        {
            return qName.HasURI(ns);
        }

        public virtual NamespaceBinding GetNamespaceBinding()
        {
            return qName.GetNamespaceBinding();
        }

        public virtual int IdentityHashCode()
        {
            return 0;
        }

        /*
     * Compare two names for equality
     */
        public override bool Equals(object other)
        {
            if (other is INodeName)
            {
                if (fingerprint != -1 && ((INodeName)other).HasFingerprint())
                {
                    return Fingerprint == ((INodeName)other).Fingerprint;
                }
                else
                {
                    return GetLocalPart().Equals(((INodeName)other).GetLocalPart()) && HasURI(((INodeName)other).GetNamespaceUri());
                }
            }
            else
            {
                return false;
            }
        }

        /*
     * Compare two names for equality
     */
        public override int GetHashCode()
        {
            return qName.GetHashCode();
        }

        /*
     * Compare two names for equality
     */
        public virtual bool IsIdentical(IIdentityComparable other)
        {
            return other is INodeName && this.Equals(other) && this.GetPrefix().Equals(((INodeName)other).GetPrefix());
        }

        /*
     * Compare two names for equality
     */
        public override string ToString()
        {
            return qName.DisplayName;
        }

        public virtual string GetURI() => GetNamespaceUri().ToString(); // NodeImpl/Orphan.GetURI() route through this
    }
}