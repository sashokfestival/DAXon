////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Trees.Tiny
{
    sealed class DescendantIterator : IAxisIterator
    {
        private readonly TinyTree tree;
        private int nextNodeNr;
        private readonly int startDepth;
        private readonly IIntPredicateProxy matcher;
        private NodeInfo pending = null;
        internal DescendantIterator(TinyTree doc, TinyNodeImpl node, NodeTest nodeTest)
        {
            tree = doc;
            nextNodeNr = node.nodeNr;
            startDepth = doc.depth[nextNodeNr];
            matcher = nodeTest.GetMatcher(doc);
        }

        public NodeInfo Next()
        {
            // try/catch(IndexOutOfRangeException) → explicit `>= numberOfNodes` bound: the stopper node
            // terminates via the depth test first, so the bound only guards a malformed stopper-less tree
            // (same recovery: return end-of-sequence), without the per-iteration exception-region overhead.
            // Java relies on the stopper and has no try/catch. Byte-identical (depth test + pending +
            // TEXTUAL_ELEMENT handling all unchanged).
            short[] d = tree.depth;
            byte[] nk = tree.nodeKind;
            int nn = tree.numberOfNodes;
            do
            {
                if (pending != null)
                {
                    NodeInfo p = pending;
                    pending = null;
                    return p;
                }

                nextNodeNr++;
                if (nextNodeNr >= nn || d[nextNodeNr] <= startDepth)
                {
                    nextNodeNr = -1;
                    return null;
                }

                if (nk[nextNodeNr] == Types.Type.TEXTUAL_ELEMENT)
                {
                    pending = ((TinyTextualElement)tree.GetNode(nextNodeNr)).TextNode;
                }
            }
            while (!matcher.Test(nextNodeNr));
            return tree.GetNode(nextNodeNr);
        }
        IItem ISequenceIterator.Next() => Next(); // redirect StubGen hollow to the real covariant Next(); default = silent empty iteration
        public void Dispose() { }
    }
}

