////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Trees.Tiny;
using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Trees.Wrappers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Model
{
    internal class NameOfNode : INodeName
    {
        private readonly NodeInfo node;

        public virtual string DisplayName => node.DisplayName;

        public virtual int Fingerprint
        {
            get
            {
                if (HasFingerprint())
                {
                    return node.Fingerprint;
                }
                else
                {
                    return -1;
                }
            }
        }
        private NameOfNode(NodeInfo node)
        {
            this.node = node;
        }

        public static INodeName MakeName(NodeInfo node)
        {
            if (node is IMutableNodeInfo)
            {
                return new FingerprintedQName(node.GetPrefix(), node.GetNamespaceUri(), node.GetLocalPart());
            }
            else if (node is AbstractVirtualNode)
            {
                return new NameOfNode((NodeInfo)((AbstractVirtualNode)node).UnderlyingNode);
            }
            else
            {
                return new NameOfNode(node);
            }
        }

        public virtual string GetPrefix()
        {
            return node.GetPrefix();
        }

        public virtual NamespaceUri GetNamespaceUri()
        {
            return node.GetNamespaceUri();
        }

        public virtual string GetLocalPart()
        {
            return node.GetLocalPart();
        }

        public virtual StructuredQName GetStructuredQName()
        {
            return new StructuredQName(GetPrefix(), GetNamespaceUri(), GetLocalPart());
        }

        public virtual bool HasURI(NamespaceUri ns)
        {
            if (node is TinyNodeImpl)
            {

                // fast path (avoids object allocation)
                return ((TinyNodeImpl)node).HasURI(ns);
            }

            return node.GetNamespaceUri().Equals(ns);
        }

        public virtual NamespaceBinding GetNamespaceBinding()
        {
            return new NamespaceBinding(GetPrefix(), GetNamespaceUri());
        }

        public virtual bool HasFingerprint()
        {
            return node.HasFingerprint();
        }

        public virtual int ObtainFingerprint(NamePool namePool)
        {
            if (node.HasFingerprint())
            {
                return node.Fingerprint;
            }
            else
            {
                return namePool.AllocateFingerprint(node.GetNamespaceUri(), node.GetLocalPart());
            }
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
                if (node.HasFingerprint() && n.HasFingerprint())
                {
                    return node.Fingerprint == n.Fingerprint;
                }
                else
                {
                    return n.GetLocalPart().Equals(node.GetLocalPart()) && n.HasURI(node.GetNamespaceUri());
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

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
        public virtual string GetURI() => GetNamespaceUri().ToString(); // NodeImpl/Orphan.GetURI() route through this
    }
}
