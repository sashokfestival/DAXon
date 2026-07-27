////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Trees.Tiny
{
    public sealed class TinyAttributeImpl : TinyNodeImpl
    {

        /// <summary>
        /// Get the parent node
        /// </summary>
        public override NodeInfo Root
        {
            get
            {
                NodeInfo parent = GetParent();
                if (parent == null)
                {
                    return this; // doesn't happen - parentless attributes are represented by the Orphan class
                }
                else
                {
                    return parent.Root;
                }
            }
        }

        /// <summary>
        /// Get the parent node
        /// </summary>
        public override long SequenceNumber => GetParent().SequenceNumber + 0x8000 + (nodeNr - tree.alpha[tree.attParent[nodeNr]]);

        /// <summary>
        /// Get the parent node
        /// </summary>
        public override UnicodeString UnicodeStringValue => StringView.Of(tree.attValue[nodeNr]).Tidy();

        /// <summary>
        /// Get the parent node
        /// </summary>
        /// <summary>
        /// Get the fingerprint of the node, used for matching names
        /// </summary>
        public override int Fingerprint => tree.attCode[nodeNr] & 0xfffff;

        /// <summary>
        /// Get the parent node
        /// </summary>
        /// <summary>
        /// Get the fingerprint of the node, used for matching names
        /// </summary>
        public int NameCode => tree.attCode[nodeNr];

        /// <summary>
        /// Get the parent node
        /// </summary>
        /// <summary>
        /// Get the fingerprint of the node, used for matching names
        /// </summary>
        public override string DisplayName
        {
            get
            {
                int code = tree.attCode[nodeNr];
                if (code < 0)
                {
                    return "";
                }

                if (NamePool.IsPrefixed(code))
                {
                    return GetPrefix() + ":" + GetLocalPart();
                }
                else
                {
                    return GetLocalPart();
                }
            }
        }
        public TinyAttributeImpl(TinyTree tree, int nodeNr) : base(tree, nodeNr)
        {
        }

        public override void SetSystemId(string uri)
        {
        }

        public override string GetSystemId()
        {
            NodeInfo parent = GetParent();
            return parent == null ? null : GetParent().GetSystemId();
        }

        /// <summary>
        /// Get the parent node
        /// </summary>
        public override TinyNodeImpl GetParent()
        {
            return tree.GetNode(tree.attParent[nodeNr]);
        }

        /// <summary>
        /// Get the parent node
        /// </summary>
        public override int GetNodeKind()
        {
            return Types.Type.ATTRIBUTE;
        }

        /// <summary>
        /// Get the parent node
        /// </summary>
        /// <summary>
        /// Get the fingerprint of the node, used for matching names
        /// </summary>
        public override string GetPrefix()
        {
            int code = tree.attCode[nodeNr];
            if (!NamePool.IsPrefixed(code))
            {
                return "";
            }

            return tree.prefixPool.GetPrefix(code >> 20);
        }

        /// <summary>
        /// Get the parent node
        /// </summary>
        /// <summary>
        /// Get the fingerprint of the node, used for matching names
        /// </summary>
        public override string GetLocalPart()
        {
            return tree.GetNamePool().GetLocalName(tree.attCode[nodeNr]);
        }

        /// <summary>
        /// Get the parent node
        /// </summary>
        /// <summary>
        /// Get the fingerprint of the node, used for matching names
        /// </summary>
        public override NamespaceUri GetNamespaceUri()
        {
            int code = tree.attCode[nodeNr];
            if (!NamePool.IsPrefixed(code))
            {
                return NamespaceUri.NULL;
            }

            return tree.GetNamePool().GetURI(code);
        }

        /// <summary>
        /// Get the parent node
        /// </summary>
        /// <summary>
        /// Get the fingerprint of the node, used for matching names
        /// </summary>
        public override bool HasURI(NamespaceUri ns)
        {
            int code = tree.attCode[nodeNr];
            if (!NamePool.IsPrefixed(code))
            {
                return ns.IsEmpty();
            }

            return GetNamePool().GetStructuredQName(code).HasURI(ns);
        }

        /// <summary>
        /// Get the parent node
        /// </summary>
        /// <summary>
        /// Get the fingerprint of the node, used for matching names
        /// </summary>
        public override ISchemaType GetSchemaType()
        {
            if (tree.attType == null)
            {
                return BuiltInAtomicType.UNTYPED_ATOMIC;
            }

            return tree.GetAttributeType(nodeNr);
        }

        /// <summary>
        /// Get the parent node
        /// </summary>
        /// <summary>
        /// Get the fingerprint of the node, used for matching names
        /// </summary>
        public override IAtomicSequence Atomize()
        {
            return tree.GetTypedValueOfAttribute(this, nodeNr);
        }

        /// <summary>
        /// Get the parent node
        /// </summary>
        /// <summary>
        /// Get the fingerprint of the node, used for matching names
        /// </summary>
        public override void GenerateId(StringBuilder buffer)
        {
            GetParent().GenerateId(buffer);
            buffer.Append("a");
            TinyNodeImpl.AppendIdDigits(buffer, tree.attCode[nodeNr]); // we previously used the attribute name. But this breaks the requirement
            // that the result of generate-id consists entirely of alphanumeric ASCII
            // characters
        }

        /// <summary>
        /// Get the parent node
        /// </summary>
        /// <summary>
        /// Get the fingerprint of the node, used for matching names
        /// </summary>
        /// <summary>
        /// Copy this node to a given {@code IReceiver}
        /// </summary>
        public override void Copy(IReceiver @out, int copyOptions, ILocation locationId)
        {
            throw new NotSupportedException("copy() applied to attribute node");
        }

        /// <summary>
        /// Get the parent node
        /// </summary>
        /// <summary>
        /// Get the fingerprint of the node, used for matching names
        /// </summary>
        /// <summary>
        /// Copy this node to a given {@code IReceiver}
        /// </summary>
        /// <summary>
        /// Get the line number of the node within its source document entity
        /// </summary>
        public override int GetLineNumber()
        {
            return GetParent().GetLineNumber();
        }

        /// <summary>
        /// Get the parent node
        /// </summary>
        /// <summary>
        /// Get the fingerprint of the node, used for matching names
        /// </summary>
        /// <summary>
        /// Copy this node to a given {@code IReceiver}
        /// </summary>
        /// <summary>
        /// Get the column number of the node within its source document entity
        /// </summary>
        public override int GetColumnNumber()
        {
            return GetParent().GetColumnNumber();
        }

        /// <summary>
        /// Get the parent node
        /// </summary>
        /// <summary>
        /// Get the fingerprint of the node, used for matching names
        /// </summary>
        /// <summary>
        /// Copy this node to a given {@code IReceiver}
        /// </summary>
        /// <summary>
        /// Get the column number of the node within its source document entity
        /// </summary>
        public override bool IsId()
        {
            return tree.IsIdAttribute(nodeNr);
        }

        /// <summary>
        /// Get the parent node
        /// </summary>
        /// <summary>
        /// Get the fingerprint of the node, used for matching names
        /// </summary>
        /// <summary>
        /// Copy this node to a given {@code IReceiver}
        /// </summary>
        /// <summary>
        /// Get the column number of the node within its source document entity
        /// </summary>
        public override bool IsIdref()
        {
            return tree.IsIdrefAttribute(nodeNr);
        }

        /// <summary>
        /// Get the parent node
        /// </summary>
        /// <summary>
        /// Get the fingerprint of the node, used for matching names
        /// </summary>
        /// <summary>
        /// Copy this node to a given {@code IReceiver}
        /// </summary>
        /// <summary>
        /// Get the column number of the node within its source document entity
        /// </summary>
        public override bool IsNilled()
        {
            return false;
        }

        /// <summary>
        /// Get the parent node
        /// </summary>
        /// <summary>
        /// Get the fingerprint of the node, used for matching names
        /// </summary>
        /// <summary>
        /// Copy this node to a given {@code IReceiver}
        /// </summary>
        /// <summary>
        /// Get the column number of the node within its source document entity
        /// </summary>
        public bool IsDefaultedAttribute()
        {
            return tree.IsDefaultedAttribute(nodeNr);
        }

        /// <summary>
        /// Get the parent node
        /// </summary>
        /// <summary>
        /// Get the fingerprint of the node, used for matching names
        /// </summary>
        /// <summary>
        /// Copy this node to a given {@code IReceiver}
        /// </summary>
        /// <summary>
        /// Get the column number of the node within its source document entity
        /// </summary>
        public override int GetHashCode()
        {
            return ((int)(tree.GetDocumentNumber() & 0x3ff) << 20) ^ nodeNr ^ 7 << 17;
        }
    }
}
