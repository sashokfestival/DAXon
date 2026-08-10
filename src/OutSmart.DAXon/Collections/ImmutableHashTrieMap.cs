////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//
// Faithful port of upstream net/sf/saxon/ma/trie/ImmutableHashTrieMap.java (a hash-array-mapped trie, HAMT).
// Replaces the Phase 4.8c stub which backed the map with a copy-on-write Dictionary and copied the whole
// dictionary on every Put/Remove -- O(n) per mutation, so building a large map was O(n^2) and appeared to
// hang. This structural-sharing trie is O(log32 n) per mutation and iterates in the same hash-bucket order
// as Java Saxon. The XDM map itself now uses the specialized Values.Maps.MapTrie (same shape, the
// KeyValuePair doubles as the leaf); this generic form backs the remaining keyed stores (ParseOptions).
//
// Original author: Michael Froh (published on Github). Released under MPL 2.0 by Saxonica Limited with
// permission from the author.

using System;
using OutSmart.DAXon.Internal;
using System.Collections;
using System.Collections.Generic;

namespace OutSmart.DAXon.Collections.Trie
{
    /// <summary>
    /// An immutable map implemented as a hash trie. The value stored against each key is retrieved by
    /// descending the trie 5 bits of the key's hash code at a time (32-way fan-out per level).
    /// </summary>
    internal abstract class ImmutableHashTrieMap<K, V> : IImmutableMap<K, V>
    {
        // "shift" denotes how far (in bits) through the hash code we are currently looking. At each level we
        // add BITS to shift.
        private const int BITS = 5;
        private const int FANOUT = 1 << BITS;
        private const int MASK = FANOUT - 1;

        public V this[K key]
        {
            get { return DoGet(0, key); }
        }
        internal virtual bool IsEmptyNode
        {
            get { return false; }
        }

        /// <summary>Return the empty map.</summary>
        public static ImmutableHashTrieMap<K, V> Empty()
        {
            return EmptyHashNode.INSTANCE;
        }

        private static int GetBucket(int shift, K key)
        {
            return key.GetHashCode() >> shift & MASK;
        }

        // ---- IImmutableMap<K,V> surface ----
        public IImmutableMap<K, V> Put(K key, V value)
        {
            return DoPut(0, key, value);
        }

        public IImmutableMap<K, V> Remove(K key)
        {
            return DoRemove(0, key);
        }

        public IEnumerator<TrieKVP<K, V>> GetEnumerator()
        {
            return new FlatTrieEnumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        // One explicit-stack walker for the whole trie, replacing the per-node-class nested yield
        // enumerators — those stacked a compiler state machine per trie level, so every element of
        // a large map paid a MoveNext chain the full depth of its path. Same emission order: the
        // compact arrays are bucket-ascending and collision lists walk head-first.
        private sealed class FlatTrieEnumerator : IEnumerator<TrieKVP<K, V>>
        {
            private readonly List<object> stack = new List<object>(8); // trie nodes + collision-list tails
            private TrieKVP<K, V> current;

            internal FlatTrieEnumerator(ImmutableHashTrieMap<K, V> root)
            {
                if (!root.IsEmptyNode)
                {
                    stack.Add(root);
                }
            }

            public TrieKVP<K, V> Current => current;
            object IEnumerator.Current => current;

            public bool MoveNext()
            {
                List<object> s = stack;
                while (s.Count > 0)
                {
                    object top = s[s.Count - 1];
                    s.RemoveAt(s.Count - 1);
                    if (top is EntryHashNode entry)
                    {
                        current = new TrieKVP<K, V>(entry.key, entry.value);
                        return true;
                    }

                    if (top is BranchedArrayHashNode branch)
                    {
                        for (int i = branch.count - 1; i >= 0; i--)
                        {
                            s.Add(branch.subnodes[i]);
                        }

                        continue;
                    }

                    if (top is SingletonArrayHashNode single)
                    {
                        s.Add(single.subnode);
                        continue;
                    }

                    if (top is ListHashNode list)
                    {
                        top = list.entries;
                    }
                    else if (top is EmptyHashNode)
                    {
                        continue;
                    }

                    var tail = (ImmutableList<TrieKVP<K, V>>)top;
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

        // ---- internal recursion, one abstract method per operation ----
        internal abstract ImmutableHashTrieMap<K, V> DoPut(int shift, K key, V value);
        internal abstract ImmutableHashTrieMap<K, V> DoRemove(int shift, K key);
        internal abstract V DoGet(int shift, K key);
        internal abstract bool IsArrayNode();

        /// <summary>
        /// Create a new node combining two existing nodes whose hash codes differ. Descends creating a chain
        /// of single-bucket array nodes for as long as the two hashes agree at the current level, then a
        /// branched node at the level where they diverge.
        /// </summary>
        private static ImmutableHashTrieMap<K, V> NewArrayHashNode(int shift, int hash1, ImmutableHashTrieMap<K, V> subNode1, int hash2, ImmutableHashTrieMap<K, V> subNode2)
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

            ImmutableHashTrieMap<K, V> newNode = new BranchedArrayHashNode(h1, subNode1, h2, subNode2);
            foreach (int bucket in buckets)
            {
                newNode = new SingletonArrayHashNode(bucket, newNode);
            }

            return newNode;
        }

        /// <summary>Implementation for an empty map.</summary>
        private sealed class EmptyHashNode : ImmutableHashTrieMap<K, V>
        {
            internal static readonly EmptyHashNode INSTANCE = new EmptyHashNode();

            internal override bool IsEmptyNode
            {
                get { return true; }
            }

            internal override ImmutableHashTrieMap<K, V> DoPut(int shift, K key, V value)
            {
                return new EntryHashNode(key, value);
            }

            internal override ImmutableHashTrieMap<K, V> DoRemove(int shift, K key)
            {
                return this;
            }

            internal override bool IsArrayNode()
            {
                return false;
            }

            internal override V DoGet(int shift, K key)
            {
                return default(V);
            }
        }

        /// <summary>Implementation for a single-entry map.</summary>
        private sealed class EntryHashNode : ImmutableHashTrieMap<K, V>
        {
            internal readonly K key;
            internal readonly V value;

            internal EntryHashNode(K key, V value)
            {
                this.key = key;
                this.value = value;
            }

            internal override ImmutableHashTrieMap<K, V> DoPut(int shift, K key, V value)
            {
                if (this.key.Equals(key))
                {
                    // Overwriting this entry
                    return new EntryHashNode(key, value);
                }
                else if (this.key.GetHashCode() == key.GetHashCode())
                {
                    // This is a collision. Return a new ListHashNode.
                    return new ListHashNode(new TrieKVP<K, V>(this.key, this.value), new TrieKVP<K, V>(key, value));
                }

                // Split this node into an ArrayHashNode with this and the new value as entries.
                return NewArrayHashNode(shift, this.key.GetHashCode(), this, key.GetHashCode(), new EntryHashNode(key, value));
            }

            internal override ImmutableHashTrieMap<K, V> DoRemove(int shift, K key)
            {
                if (this.key.Equals(key))
                {
                    return Empty();
                }

                return this;
            }

            internal override bool IsArrayNode()
            {
                return false;
            }

            internal override V DoGet(int shift, K key)
            {
                if (this.key.Equals(key))
                {
                    return value;
                }

                return default(V);
            }
        }

        /// <summary>Implementation for a set of entries whose keys all share the same hash code.</summary>
        private sealed class ListHashNode : ImmutableHashTrieMap<K, V>
        {
            internal readonly ImmutableList<TrieKVP<K, V>> entries;

            internal ListHashNode(TrieKVP<K, V> entry1, TrieKVP<K, V> entry2)
            {
                // These entries must collide
                ImmutableList<TrieKVP<K, V>> newList = ImmutableList<TrieKVP<K, V>>.Empty();
                entries = newList.Prepend(entry1).Prepend(entry2);
            }

            private ListHashNode(ImmutableList<TrieKVP<K, V>> entries)
            {
                // Size should be at least 2
                this.entries = entries;
            }

            internal override ImmutableHashTrieMap<K, V> DoPut(int shift, K key, V value)
            {
                if (entries.Head().key.GetHashCode() != key.GetHashCode())
                {
                    return NewArrayHashNode(shift, entries.Head().key.GetHashCode(), this, key.GetHashCode(), new EntryHashNode(key, value));
                }

                ImmutableList<TrieKVP<K, V>> newList = ImmutableList<TrieKVP<K, V>>.Empty();
                bool found = false;
                foreach (TrieKVP<K, V> entry in entries)
                {
                    if (entry.key.Equals(key))
                    {
                        // Node replacement
                        newList = newList.Prepend(new TrieKVP<K, V>(key, value));
                        found = true;
                    }
                    else
                    {
                        newList = newList.Prepend(entry);
                    }
                }

                if (!found)
                {
                    // Adding a new entry
                    newList = newList.Prepend(new TrieKVP<K, V>(key, value));
                }

                return new ListHashNode(newList);
            }

            internal override ImmutableHashTrieMap<K, V> DoRemove(int shift, K key)
            {
                ImmutableList<TrieKVP<K, V>> newList = ImmutableList<TrieKVP<K, V>>.Empty();
                int size = 0;
                foreach (TrieKVP<K, V> entry in entries)
                {
                    if (!entry.key.Equals(key))
                    {
                        newList = newList.Prepend(entry);
                        size++;
                    }
                }

                if (size == 1)
                {
                    TrieKVP<K, V> entry = newList.Head();
                    return new EntryHashNode(entry.key, entry.value);
                }

                return new ListHashNode(newList);
            }

            internal override bool IsArrayNode()
            {
                return false;
            }

            internal override V DoGet(int shift, K key)
            {
                foreach (TrieKVP<K, V> entry in entries)
                {
                    if (entry.key.Equals(key))
                    {
                        return entry.value;
                    }
                }

                return default(V);
            }
        }

        private abstract class ArrayHashNode : ImmutableHashTrieMap<K, V>
        {
            internal override bool IsArrayNode()
            {
                return true;
            }
        }

        // Compact HAMT node: a 32-bit occupancy bitmap plus a dense array of ONLY the occupied
        // sub-nodes, in ascending bucket order. Replaces the former FANOUT(=32)-slot array (29 of
        // whose slots pointed at the Empty singleton for a typical small map) -- that dense array,
        // retained for millions of tiny maps, was the json-map allocation floor. Iteration order is
        // unchanged (the compact array IS bucket-ascending), so map serialization is byte-for-byte
        // identical to the dense form.
        private sealed class BranchedArrayHashNode : ArrayHashNode
        {
            private readonly int bitmap;                              // bit b set => bucket b occupied
            internal readonly ImmutableHashTrieMap<K, V>[] subnodes;  // occupied sub-nodes, ascending bucket
            internal readonly int count;                              // occupied buckets == Bits.BitCount(bitmap)

            // Combine two sub-nodes at distinct buckets (h1 != h2, guaranteed by NewArrayHashNode).
            internal BranchedArrayHashNode(int h1, ImmutableHashTrieMap<K, V> subNode1, int h2, ImmutableHashTrieMap<K, V> subNode2)
            {
                bitmap = (1 << h1) | (1 << h2);
                count = 2;
                subnodes = new ImmutableHashTrieMap<K, V>[2];
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

            private BranchedArrayHashNode(int bitmap, ImmutableHashTrieMap<K, V>[] subnodes)
            {
                this.bitmap = bitmap;
                this.subnodes = subnodes;
                this.count = Bits.BitCount(bitmap);
            }
            internal override V DoGet(int shift, K key)
            {
                int bit = 1 << GetBucket(shift, key);
                if ((bitmap & bit) == 0)
                {
                    return default(V);
                }

                return subnodes[Bits.BitCount(bitmap & (bit - 1))].DoGet(shift + BITS, key);
            }

            internal override ImmutableHashTrieMap<K, V> DoPut(int shift, K key, V value)
            {
                int bit = 1 << GetBucket(shift, key);
                int idx = Bits.BitCount(bitmap & (bit - 1));
                if ((bitmap & bit) != 0)
                {
                    ImmutableHashTrieMap<K, V>[] newNodes = new ImmutableHashTrieMap<K, V>[count];
                    Array.Copy(subnodes, 0, newNodes, 0, count);
                    newNodes[idx] = subnodes[idx].DoPut(shift + BITS, key, value);
                    return new BranchedArrayHashNode(bitmap, newNodes);
                }

                ImmutableHashTrieMap<K, V>[] grown = new ImmutableHashTrieMap<K, V>[count + 1];
                Array.Copy(subnodes, 0, grown, 0, idx);
                grown[idx] = new EntryHashNode(key, value);
                Array.Copy(subnodes, idx, grown, idx + 1, count - idx);
                return new BranchedArrayHashNode(bitmap | bit, grown);
            }

            internal override ImmutableHashTrieMap<K, V> DoRemove(int shift, K key)
            {
                int bit = 1 << GetBucket(shift, key);
                if ((bitmap & bit) == 0)
                {
                    return this;
                }

                int idx = Bits.BitCount(bitmap & (bit - 1));
                ImmutableHashTrieMap<K, V> newSub = subnodes[idx].DoRemove(shift + BITS, key);
                if (!newSub.IsEmptyNode)
                {
                    ImmutableHashTrieMap<K, V>[] newNodes = new ImmutableHashTrieMap<K, V>[count];
                    Array.Copy(subnodes, 0, newNodes, 0, count);
                    newNodes[idx] = newSub;
                    return new BranchedArrayHashNode(bitmap, newNodes);
                }

                // The bucket became empty. When exactly one bucket remains, collapse this node: a lone
                // array sub-node is re-wrapped in a SingletonArrayHashNode, a lone entry/list floats up.
                if (count == 2)
                {
                    ImmutableHashTrieMap<K, V> orphan = subnodes[1 - idx];
                    if (orphan.IsArrayNode())
                    {
                        return new SingletonArrayHashNode(Bits.TrailingZeros(bitmap & ~bit), orphan);
                    }

                    return orphan;
                }

                ImmutableHashTrieMap<K, V>[] shrunk = new ImmutableHashTrieMap<K, V>[count - 1];
                Array.Copy(subnodes, 0, shrunk, 0, idx);
                Array.Copy(subnodes, idx + 1, shrunk, idx, count - idx - 1);
                return new BranchedArrayHashNode(bitmap & ~bit, shrunk);
            }
        }

        private sealed class SingletonArrayHashNode : ArrayHashNode
        {
            private readonly int bucket;
            internal readonly ImmutableHashTrieMap<K, V> subnode;

            internal SingletonArrayHashNode(int bucket, ImmutableHashTrieMap<K, V> subnode)
            {
                this.bucket = bucket;
                this.subnode = subnode;
            }

            internal override ImmutableHashTrieMap<K, V> DoPut(int shift, K key, V value)
            {
                int b = GetBucket(shift, key);
                if (b == this.bucket)
                {
                    return new SingletonArrayHashNode(bucket, subnode.DoPut(shift + BITS, key, value));
                }

                return new BranchedArrayHashNode(this.bucket, subnode, b, new EntryHashNode(key, value));
            }

            internal override ImmutableHashTrieMap<K, V> DoRemove(int shift, K key)
            {
                int b = GetBucket(shift, key);
                if (b == this.bucket)
                {
                    ImmutableHashTrieMap<K, V> newNode = subnode.DoRemove(shift + BITS, key);
                    if (!newNode.IsArrayNode())
                    {
                        return newNode;
                    }

                    return new SingletonArrayHashNode(bucket, newNode);
                }

                return this;
            }

            internal override V DoGet(int shift, K key)
            {
                int b = GetBucket(shift, key);
                if (b == this.bucket)
                {
                    return subnode.DoGet(shift + BITS, key);
                }

                return default(V);
            }
        }
    }

    /// <summary>
    /// A minimal immutable singly-linked list, ported from upstream net/sf/saxon/ma/trie/ImmutableList.java.
    /// Used only for hash-collision buckets (ListHashNode) in <see cref="ImmutableHashTrieMap{K,V}"/>.
    /// Original author: Michael Froh (MPL 2.0).
    /// </summary>
    internal abstract class ImmutableList<T> : IEnumerable<T>
    {
        public static ImmutableList<T> Empty()
        {
            return EmptyList.INSTANCE;
        }

        public abstract T Head();
        public abstract ImmutableList<T> Tail();
        public abstract bool IsEmpty();

        public ImmutableList<T> Prepend(T element)
        {
            return new NonEmptyList(element, this);
        }

        public IEnumerator<T> GetEnumerator()
        {
            ImmutableList<T> list = this;
            while (!list.IsEmpty())
            {
                yield return list.Head();
                list = list.Tail();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private sealed class EmptyList : ImmutableList<T>
        {
            internal static readonly EmptyList INSTANCE = new EmptyList();

            public override T Head()
            {
                throw new InvalidOperationException("head() called on empty list");
            }

            public override ImmutableList<T> Tail()
            {
                throw new InvalidOperationException("tail() called on empty list");
            }

            public override bool IsEmpty()
            {
                return true;
            }
        }

        private sealed class NonEmptyList : ImmutableList<T>
        {
            private readonly T element;
            private readonly ImmutableList<T> _tail;

            internal NonEmptyList(T element, ImmutableList<T> tail)
            {
                this.element = element;
                this._tail = tail;
            }

            public override T Head()
            {
                return element;
            }

            public override ImmutableList<T> Tail()
            {
                return _tail;
            }

            public override bool IsEmpty()
            {
                return false;
            }
        }
    }
}
