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
    sealed class DescendantIteratorSansText : IAxisIterator
    {
        private readonly TinyTree tree;
        private int nextNodeNr;
        private readonly int startDepth;
        private readonly IIntPredicateProxy matcher;
        internal DescendantIteratorSansText(TinyTree doc, TinyNodeImpl node, NodeTest nodeTest)
        {
            tree = doc;
            nextNodeNr = node.nodeNr;
            startDepth = doc.depth[nextNodeNr];
            matcher = nodeTest.GetMatcher(doc);
        }

        public NodeInfo Next()
        {
            // The stopper node (depth 0, at numberOfNodes-1) terminates the walk via the depth test
            // before the array end; the explicit `n >= nn` bound is the same belt-and-suspenders the
            // old try/catch(IndexOutOfRangeException) gave (a malformed, stopper-less tree), minus the
            // per-iteration exception-region overhead that kept the JIT from optimising this hot loop.
            // Java has no try/catch here — it relies on the stopper. Byte-identical: the depth test is
            // unchanged; only the recovery path (return end-of-sequence) is reached the same way.
            short[] d = tree.depth;
            int nn = tree.numberOfNodes;
            int n = nextNodeNr;
            do
            {
                n++;
                if (n >= nn || d[n] <= startDepth)
                {
                    nextNodeNr = -1;
                    return null;
                }
            }
            while (!matcher.Test(n));
            nextNodeNr = n;
            return tree.GetNode(n);
        }
        IItem ISequenceIterator.Next() => Next(); // redirect StubGen hollow to the real covariant Next(); default = silent empty iteration
        public void Dispose() { }
    }
}

