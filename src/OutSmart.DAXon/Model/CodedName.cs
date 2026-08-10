////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using OutSmart.DAXon.Functions;

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Model
{
    internal class CodedName : INodeName
    {
        private readonly int fingerprint;
        private readonly string prefix;
        private readonly NamePool pool;
        // One name is asked several times per output event (HasURI twice, GetLocalPart for the
        // tag); resolve the pool lookup once. Benign race: idempotent write of an immutable QName.
        private StructuredQName resolved;

        public virtual string DisplayName => (prefix.Length == 0) ? GetLocalPart() : prefix + ":" + GetLocalPart();

        public virtual int Fingerprint => fingerprint;
        public CodedName(int fingerprint, string prefix, NamePool pool)
        {

            this.fingerprint = fingerprint;
            this.prefix = prefix;
            this.pool = pool;
        }

        private StructuredQName Resolve()
        {
            return resolved ?? (resolved = pool.GetUnprefixedQName(fingerprint));
        }

        public virtual string GetPrefix()
        {
            return prefix;
        }

        public virtual NamespaceUri GetNamespaceUri()
        {
            return Resolve().GetNamespaceUri();
        }

        public virtual string GetLocalPart()
        {
            return Resolve().GetLocalPart();
        }

        public virtual StructuredQName GetStructuredQName()
        {
            StructuredQName qn = Resolve();
            if ((prefix.Length == 0))
            {
                return qn;
            }
            else
            {
                return new StructuredQName(prefix, qn.GetNamespaceUri(), qn.GetLocalPart());
            }
        }

        public virtual bool HasURI(NamespaceUri ns)
        {
            return Resolve().HasURI(ns);
        }

        public virtual NamespaceBinding GetNamespaceBinding()
        {
            return new NamespaceBinding(prefix, pool.GetURI(fingerprint));
        }

        public virtual bool HasFingerprint()
        {
            return true;
        }

        public virtual int ObtainFingerprint(NamePool namePool)
        {
            return fingerprint;
        }

        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        public override int GetHashCode()
        {
            return StructuredQName.ComputeHashCode(GetNamespaceUri(), GetLocalPart());
        }

        public override bool Equals(object obj)
        {
            if (obj is INodeName)
            {
                INodeName n = (INodeName)obj;
                if (n.HasFingerprint())
                {
                    return Fingerprint == n.Fingerprint;
                }
                else
                {
                    return n.GetLocalPart().Equals(GetLocalPart()) && n.HasURI(GetNamespaceUri());
                }
            }
            else
            {
                return false;
            }
        }

        public virtual bool IsIdentical(IIdentityComparable other)
        {
            return other is INodeName && this.Equals(other) && this.GetPrefix().Equals(((INodeName)other).GetPrefix());
        }

        public virtual int IdentityHashCode()
        {
            return GetHashCode() ^ GetPrefix().GetHashCode();
        }

        public override string ToString()
        {
            return DisplayName;
        }

        public virtual string GetURI() => GetNamespaceUri().ToString(); // NodeImpl/Orphan.GetURI() route through this
    }
}
