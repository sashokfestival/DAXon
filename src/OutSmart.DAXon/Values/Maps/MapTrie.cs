////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using OutSmart.DAXon.Internal;
using System.Collections;
using System.Collections.Generic;
using OutSmart.DAXon.Collections.Trie;
using OutSmart.DAXon.Expressions.Sorting;

namespace OutSmart.DAXon.Values.Maps
{
    /// <summary>
    /// The HAMT behind <see cref="HashTrieMap"/>, specialized so that a <see cref="KeyValuePair"/>
    /// IS the leaf node: node ::= null (empty root) | KeyValuePair | ListNode | Branched | Single.
    /// Same shape and bucket math as the generic <c>ImmutableHashTrieMap</c> (5 bits/level,
    /// bucket-ascending compact arrays, head-first collision lists), so iteration order is
    /// identical — but each entry costs one object instead of leaf-node + pair, and enumeration
    /// yields the stored pairs without per-element wrappers. Leaves compare by
    /// <see cref="KeyValuePair.MatchKey"/> (cached, seeded at insert).
    /// </summary>
    internal static class MapTrie
    {
        private const int BITS = 5;
        private const int FANOUT = 1 << BITS;
        private const int MASK = FANOUT - 1;

        internal static KeyValuePair Get(object node, IAtomicMatchKey key)
        {
            if (node == null)
            {
                return null;
            }

            int hash = key.GetHashCode();
            int shift = 0;
            while (true)
            {
                if (node is Branched b)
                {
                    int bit = 1 << (hash >> shift & MASK);
                    if ((b.bitmap & bit) == 0)
                    {
                        return null;
                    }

                    node = b.subnodes[Bits.BitCount(b.bitmap & (bit - 1))];
                    shift += BITS;
                    continue;
                }

                if (node is Single s)
                {
                    if ((hash >> shift & MASK) != s.bucket)
                    {
                        return null;
                    }

                    node = s.subnode;
                    shift += BITS;
                    continue;
                }

                return LeafGet(node, key);
            }
        }

        private static KeyValuePair LeafGet(object node, IAtomicMatchKey key)
        {
            if (node is KeyValuePair leaf)
            {
                return leaf.MatchKey.Equals(key) ? leaf : null;
            }

            foreach (KeyValuePair e in ((ListNode)node).entries)
            {
                if (e.MatchKey.Equals(key))
                {
                    return e;
                }
            }

            return null;
        }

        internal static object Put(object node, IAtomicMatchKey key, KeyValuePair v)
        {
            return Put(node, 0, key.GetHashCode(), key, v);
        }

        private static object Put(object node, int shift, int hash, IAtomicMatchKey key, KeyValuePair v)
        {
            if (node == null)
            {
                return v;
            }

            if (node is KeyValuePair leaf)
            {
                IAtomicMatchKey lk = leaf.MatchKey;
                if (lk.Equals(key))
                {
                    return v;
                }

                int lh = lk.GetHashCode();
                if (lh == hash)
                {
                    return new ListNode(leaf, v);
                }

                return JoinLeaves(shift, lh, leaf, hash, v);
            }

            if (node is Branched b)
            {
                int bit = 1 << (hash >> shift & MASK);
                int idx = Bits.BitCount(b.bitmap & (bit - 1));
                if ((b.bitmap & bit) != 0)
                {
                    object[] newNodes = new object[b.count];
                    Array.Copy(b.subnodes, 0, newNodes, 0, b.count);
                    newNodes[idx] = Put(b.subnodes[idx], shift + BITS, hash, key, v);
                    return new Branched(b.bitmap, newNodes);
                }

                object[] grown = new object[b.count + 1];
                Array.Copy(b.subnodes, 0, grown, 0, idx);
                grown[idx] = v;
                Array.Copy(b.subnodes, idx, grown, idx + 1, b.count - idx);
                return new Branched(b.bitmap | bit, grown);
            }

            if (node is Single s)
            {
                int bkt = hash >> shift & MASK;
                if (bkt == s.bucket)
                {
                    return new Single(s.bucket, Put(s.subnode, shift + BITS, hash, key, v));
                }

                return new Branched(s.bucket, s.subnode, bkt, v);
            }

            return ListPut((ListNode)node, shift, hash, key, v);
        }

        private static object ListPut(ListNode node, int shift, int hash, IAtomicMatchKey key, KeyValuePair v)
        {
            int lh = node.entries.Head().MatchKey.GetHashCode();
            if (lh != hash)
            {
                return JoinLeaves(shift, lh, node, hash, v);
            }

            ImmutableList<KeyValuePair> newList = ImmutableList<KeyValuePair>.Empty();
            bool found = false;
            foreach (KeyValuePair e in node.entries)
            {
                if (e.MatchKey.Equals(key))
                {
                    newList = newList.Prepend(v);
                    found = true;
                }
                else
                {
                    newList = newList.Prepend(e);
                }
            }

            if (!found)
            {
                newList = newList.Prepend(v);
            }

            return new ListNode(newList);
        }

        /// <summary>
        /// In-place put for a PRIVATELY OWNED trie (see the generic trie's PutOwned contract):
        /// interior nodes created by the owner's own puts update child slots directly; shape
        /// changes and leaf semantics fall through to the immutable Put.
        /// </summary>
        internal static object PutOwned(object node, IAtomicMatchKey key, KeyValuePair v)
        {
            return PutOwned(node, 0, key.GetHashCode(), key, v);
        }

        private static object PutOwned(object node, int shift, int hash, IAtomicMatchKey key, KeyValuePair v)
        {
            if (node is Branched b)
            {
                int bit = 1 << (hash >> shift & MASK);
                int idx = Bits.BitCount(b.bitmap & (bit - 1));
                if ((b.bitmap & bit) != 0)
                {
                    b.subnodes[idx] = PutOwned(b.subnodes[idx], shift + BITS, hash, key, v);
                    return b;
                }

                InsertOwned(b, bit, idx, v);
                return b;
            }

            if (node is Single s)
            {
                if ((hash >> shift & MASK) == s.bucket)
                {
                    s.subnode = PutOwned(s.subnode, shift + BITS, hash, key, v);
                    return s;
                }
            }

            return Put(node, shift, hash, key, v);
        }

        /// <summary>
        /// Put-if-absent for a privately owned trie: ONE descent decides existence and inserts.
        /// An existing key leaves the trie untouched (inserted=false).
        /// </summary>
        internal static object PutIfAbsentOwned(object node, IAtomicMatchKey key, KeyValuePair v, out bool inserted)
        {
            return PutIfAbsentOwned(node, 0, key.GetHashCode(), key, v, out inserted);
        }

        private static object PutIfAbsentOwned(object node, int shift, int hash, IAtomicMatchKey key, KeyValuePair v, out bool inserted)
        {
            if (node == null)
            {
                inserted = true;
                return v;
            }

            if (node is Branched b)
            {
                int bit = 1 << (hash >> shift & MASK);
                int idx = Bits.BitCount(b.bitmap & (bit - 1));
                if ((b.bitmap & bit) != 0)
                {
                    b.subnodes[idx] = PutIfAbsentOwned(b.subnodes[idx], shift + BITS, hash, key, v, out inserted);
                    return b;
                }

                inserted = true;
                InsertOwned(b, bit, idx, v);
                return b;
            }

            if (node is Single s)
            {
                if ((hash >> shift & MASK) == s.bucket)
                {
                    s.subnode = PutIfAbsentOwned(s.subnode, shift + BITS, hash, key, v, out inserted);
                    return s;
                }

                inserted = true;
                return Put(node, shift, hash, key, v);
            }

            KeyValuePair existing = LeafGet(node, key);
            if (existing != null)
            {
                inserted = false;
                return node;
            }

            inserted = true;
            return Put(node, shift, hash, key, v);
        }

        // Owned shape change: insert the new bucket in place, growing with slack (doubling,
        // capped at FANOUT) so a filling node pays O(log) reallocations.
        private static void InsertOwned(Branched b, int bit, int idx, KeyValuePair v)
        {
            if (b.count == b.subnodes.Length)
            {
                object[] wider = new object[Math.Min(FANOUT, b.count * 2)];
                Array.Copy(b.subnodes, 0, wider, 0, b.count);
                b.subnodes = wider;
            }

            Array.Copy(b.subnodes, idx, b.subnodes, idx + 1, b.count - idx);
            b.subnodes[idx] = v;
            b.bitmap |= bit;
            b.count++;
        }

        internal static object Remove(object node, IAtomicMatchKey key)
        {
            return Remove(node, 0, key.GetHashCode(), key);
        }

        private static object Remove(object node, int shift, int hash, IAtomicMatchKey key)
        {
            if (node == null)
            {
                return null;
            }

            if (node is KeyValuePair leaf)
            {
                return leaf.MatchKey.Equals(key) ? null : node;
            }

            if (node is Branched b)
            {
                int bit = 1 << (hash >> shift & MASK);
                if ((b.bitmap & bit) == 0)
                {
                    return node;
                }

                int idx = Bits.BitCount(b.bitmap & (bit - 1));
                object newSub = Remove(b.subnodes[idx], shift + BITS, hash, key);
                if (newSub != null)
                {
                    object[] newNodes = new object[b.count];
                    Array.Copy(b.subnodes, 0, newNodes, 0, b.count);
                    newNodes[idx] = newSub;
                    return new Branched(b.bitmap, newNodes);
                }

                // The bucket became empty. When exactly one bucket remains, collapse this node: a
                // lone interior sub-node is re-wrapped in a Single, a lone entry/list floats up.
                if (b.count == 2)
                {
                    object orphan = b.subnodes[1 - idx];
                    if (orphan is Branched || orphan is Single)
                    {
                        return new Single(Bits.TrailingZeros(b.bitmap & ~bit), orphan);
                    }

                    return orphan;
                }

                object[] shrunk = new object[b.count - 1];
                Array.Copy(b.subnodes, 0, shrunk, 0, idx);
                Array.Copy(b.subnodes, idx + 1, shrunk, idx, b.count - idx - 1);
                return new Branched(b.bitmap & ~bit, shrunk);
            }

            if (node is Single s)
            {
                if ((hash >> shift & MASK) != s.bucket)
                {
                    return node;
                }

                object newNode = Remove(s.subnode, shift + BITS, hash, key);
                if (newNode is Branched || newNode is Single)
                {
                    return new Single(s.bucket, newNode);
                }

                return newNode;
            }

            return ListRemove((ListNode)node, key);
        }

        private static object ListRemove(ListNode node, IAtomicMatchKey key)
        {
            ImmutableList<KeyValuePair> newList = ImmutableList<KeyValuePair>.Empty();
            int size = 0;
            foreach (KeyValuePair e in node.entries)
            {
                if (!e.MatchKey.Equals(key))
                {
                    newList = newList.Prepend(e);
                    size++;
                }
            }

            if (size == 1)
            {
                return newList.Head();
            }

            return new ListNode(newList);
        }

        /// <summary>
        /// Create a chain combining two nodes whose hash codes differ: single-bucket nodes for as
        /// long as the hashes agree at the current level, then a branched node where they diverge.
        /// </summary>
        private static object JoinLeaves(int shift, int hash1, object subNode1, int hash2, object subNode2)
        {
            int curShift = shift;
            int h1 = hash1 >> shift & MASK;
            int h2 = hash2 >> shift & MASK;
            List<int> buckets = new List<int>();
            while (h1 == h2)
            {
                buckets.Insert(0, h1);
                curShift += BITS;
                h1 = hash1 >> curShift & MASK;
                h2 = hash2 >> curShift & MASK;
            }

            object newNode = new Branched(h1, subNode1, h2, subNode2);
            foreach (int bucket in buckets)
            {
                newNode = new Single(bucket, newNode);
            }

            return newNode;
        }

        internal static IEnumerator<KeyValuePair> Enumerate(object root)
        {
            return new Walker(root);
        }


        // Compact interior node: 32-bit occupancy bitmap plus a dense array of ONLY the occupied
        // sub-nodes, ascending bucket order. Non-readonly fields serve the owned paths only; the
        // immutable paths always build exact-size arrays, so consumers index by count, never Length.
        private sealed class Branched
        {
            internal int bitmap;
            internal object[] subnodes;
            internal int count;

            // Combine two sub-nodes at distinct buckets (h1 != h2, guaranteed by JoinLeaves).
            internal Branched(int h1, object subNode1, int h2, object subNode2)
            {
                bitmap = (1 << h1) | (1 << h2);
                count = 2;
                subnodes = new object[2];
                if (h1 < h2)
                {
                    subnodes[0] = subNode1;
                    subnodes[1] = subNode2;
                }
                else
                {
                    subnodes[0] = subNode2;
                    subnodes[1] = subNode1;
                }
            }

            internal Branched(int bitmap, object[] subnodes)
            {
                this.bitmap = bitmap;
                this.subnodes = subnodes;
                this.count = Bits.BitCount(bitmap);
            }
        }

        private sealed class Single
        {
            internal readonly int bucket;
            internal object subnode;   // non-readonly for the owned paths only

            internal Single(int bucket, object subnode)
            {
                this.bucket = bucket;
                this.subnode = subnode;
            }
        }

        /// <summary>Entries whose keys all share the same hash code, head-first.</summary>
        private sealed class ListNode
        {
            internal readonly ImmutableList<KeyValuePair> entries;

            internal ListNode(KeyValuePair oldEntry, KeyValuePair newEntry)
            {
                entries = ImmutableList<KeyValuePair>.Empty().Prepend(oldEntry).Prepend(newEntry);
            }

            internal ListNode(ImmutableList<KeyValuePair> entries)
            {
                // Size should be at least 2
                this.entries = entries;
            }
        }

        // One explicit-stack walker for the whole trie; emission order matches the generic trie's
        // FlatTrieEnumerator (compact arrays bucket-ascending, collision lists head-first).
        private sealed class Walker : IEnumerator<KeyValuePair>
        {
            private readonly List<object> stack = new List<object>(8); // trie nodes + collision-list tails
            private KeyValuePair current;

            internal Walker(object root)
            {
                if (root != null)
                {
                    stack.Add(root);
                }
            }

            public KeyValuePair Current => current;
            object IEnumerator.Current => current;

            public bool MoveNext()
            {
                List<object> s = stack;
                while (s.Count > 0)
                {
                    object top = s[s.Count - 1];
                    s.RemoveAt(s.Count - 1);
                    if (top is KeyValuePair leaf)
                    {
                        current = leaf;
                        return true;
                    }

                    if (top is Branched branch)
                    {
                        for (int i = branch.count - 1; i >= 0; i--)
                        {
                            s.Add(branch.subnodes[i]);
                        }

                        continue;
                    }

                    if (top is Single single)
                    {
                        s.Add(single.subnode);
                        continue;
                    }

                    if (top is ListNode list)
                    {
                        top = list.entries;
                    }

                    var tail = (ImmutableList<KeyValuePair>)top;
                    if (!tail.IsEmpty())
                    {
                        current = tail.Head();
                        s.Add(tail.Tail());
                        return true;
                    }
                }

                current = null;
                return false;
            }

            public void Reset()
            {
                throw new NotSupportedException();
            }

            public void Dispose() { }
        }
    }
}
