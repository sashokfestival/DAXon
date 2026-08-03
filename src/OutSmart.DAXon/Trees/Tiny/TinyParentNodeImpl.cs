////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Text;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Types;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Trees.Tiny
{
    internal abstract class TinyParentNodeImpl : TinyNodeImpl
    {

        public override UnicodeString UnicodeStringValue => GetStringValue(tree, nodeNr);
        public TinyParentNodeImpl(TinyTree tree, int nodeNr) : base(tree, nodeNr)
        {
        }

        public override bool HasChildNodes()
        {
            return nodeNr + 1 < tree.numberOfNodes && tree.depth[nodeNr + 1] > tree.depth[nodeNr];
        }

        public static UnicodeString GetStringValue(TinyTree tree, int nodeNr)
        {
            int level = tree.depth[nodeNr];

            // note, we can't rely on the value being contiguously stored because of whitespace
            // nodes: the data for these may still be present.
            int next = nodeNr + 1;

            // we optimize two special cases: firstly, where the node has no children, and secondly,
            // where it has a single text node as a child.
            if (tree.nodeKind[nodeNr] == Types.Type.TEXTUAL_ELEMENT)
            {
                return TinyTextImpl.GetStringValue(tree, nodeNr);
            } // bug 4445
            else if (next < tree.numberOfNodes)
            {

                // bug 4445
                if (tree.depth[next] <= level)
                {
                    return EmptyUnicodeString.GetInstance();
                }
                else if (tree.nodeKind[next] == Types.Type.TEXT && (next + 1 >= tree.numberOfNodes || tree.depth[next + 1] <= level))
                {
                    return TinyTextImpl.GetStringValue(tree, next);
                }
            }


            // now handle the general case
            UnicodeBuilder sb = null;
            while (next < tree.numberOfNodes && tree.depth[next] > level)
            {
                byte kind = tree.nodeKind[next];
                if (kind == Types.Type.TEXT || kind == Types.Type.TEXTUAL_ELEMENT)
                {
                    if (sb == null)
                    {
                        sb = new UnicodeBuilder();
                    }

                    sb.Accept(TinyTextImpl.GetStringValue(tree, next));
                }
                else if (kind == Types.Type.WHITESPACE_TEXT)
                {
                    if (sb == null)
                    {
                        sb = new UnicodeBuilder();
                    }

                    WhitespaceTextImpl.AppendStringValue(tree, next, sb);
                }

                next++;
            }

            if (sb == null)
            {
                return EmptyUnicodeString.GetInstance();
            }

            return sb.ToUnicodeString();
        }
    }
}
