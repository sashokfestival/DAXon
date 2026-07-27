////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Collections.Trie;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal;
using System.IO;
namespace OutSmart.DAXon.Values.Maps
{
    /// <summary>
    /// An immutable map. This implementation, which uses a hash trie, was introduced in Saxon 9.6
    /// </summary>
    public class HashTrieMap : MapItem
    {
        private IImmutableMap<IAtomicMatchKey, KeyValuePair> imap;
        // type.
        private UType keyUType = UType.VOID;
        // type.
        private UType valueUType = UType.VOID;
        // type.
        private IAtomicType keyAtomicType = ErrorType.GetInstance();
        // type.
        private ItemType valueItemType = ErrorType.GetInstance();
        // type.
        private int valueCardinality = 0;
        // type.
        private int entries = -1;

        // type.
        /// <summary>
        /// Create an empty map
        /// </summary>
        /// <summary>
        /// Get the size of the map
        /// </summary>
        public override UType KeyUType => keyUType;
        // type.
        /// <summary>
        /// Create an empty map
        /// </summary>
        public HashTrieMap()
        {
            this.imap = ImmutableHashTrieMap<IAtomicMatchKey, KeyValuePair>.Empty();
            this.entries = 0;
        }

        // type.
        /// <summary>
        /// Create an empty map
        /// </summary>
        public HashTrieMap(IImmutableMap<IAtomicMatchKey, KeyValuePair> imap)
        {
            this.imap = imap;
            entries = -1;
        }

        // type.
        /// <summary>
        /// Create an empty map
        /// </summary>
        public static HashTrieMap Singleton(AtomicValue key, IGroundedValue value)
        {
            return (HashTrieMap)new HashTrieMap().AddEntry(key, value);
        }

        // type.
        /// <summary>
        /// Create an empty map
        /// </summary>
        public static HashTrieMap Copy(MapItem map)
        {
            if (map is HashTrieMap)
            {
                return (HashTrieMap)map;
            }

            HashTrieMap m2 = new HashTrieMap();
            foreach (KeyValuePair pair in map.KeyValuePairs())
            {
                m2 = (HashTrieMap)m2.AddEntry(pair.key, pair.value);
            }

            return m2;
        }

        // type.
        /// <summary>
        /// Create an empty map
        /// </summary>
        private void UpdateTypeInformation(AtomicValue key, ISequence val, bool wasEmpty)
        {

            //        if (Instrumentation.ACTIVE) {
            //        }
            if (wasEmpty)
            {
                keyUType = key.GetUType();
                valueUType = SequenceTool.GetUType(val);
                keyAtomicType = key.GetItemType();
                valueItemType = MapItem.GetItemTypeOfSequence(val);
                valueCardinality = SequenceTool.GetCardinality(val);
            }
            else
            {
                keyUType = keyUType.Union(key.GetUType());
                valueUType = valueUType.Union(SequenceTool.GetUType(val));
                valueCardinality = Cardinality.Union(valueCardinality, SequenceTool.GetCardinality(val));
                if (key.GetItemType() != keyAtomicType)
                {
                    keyAtomicType = null;
                }

                if (!MapItem.IsKnownToConform(val, valueItemType))
                {
                    valueItemType = null;
                }
            }
        }

        // type.
        /// <summary>
        /// Create an empty map
        /// </summary>
        /// <summary>
        /// Get the size of the map
        /// </summary>
        public override int Size()
        {
            if (entries >= 0)
            {
                return entries;
            }

            int count = 0;

            foreach (KeyValuePair entry in KeyValuePairs())
            {
                count++;
            }

            return entries = count;
        }

        // type.
        /// <summary>
        /// Create an empty map
        /// </summary>
        /// <summary>
        /// Get the size of the map
        /// </summary>
        public override bool IsEmpty()
        {
            return entries == 0 || !imap.Any();
        }

        // type.
        /// <summary>
        /// Create an empty map
        /// </summary>
        /// <summary>
        /// Get the size of the map
        /// </summary>
        public override bool Conforms(IPlainType requiredKeyType, SequenceType requiredValueType, TypeHierarchy th)
        {
            if (IsEmpty())
            {
                return true;
            }

            if (keyAtomicType == requiredKeyType && valueItemType == requiredValueType.PrimaryType && Cardinality.Subsumes(requiredValueType.GetCardinality(), valueCardinality))
            {
                return true;
            }

            bool needFullCheck = false;
            if (requiredKeyType != BuiltInAtomicType.ANY_ATOMIC)
            {
                ItemType upperBoundKeyType = keyUType.ToItemType();
                Affinity rel = th.Relationship(requiredKeyType, upperBoundKeyType);
                if (rel == Affinity.SAME_TYPE || rel == Affinity.SUBSUMES)
                {
                }
                else if (rel == Affinity.DISJOINT)
                {
                    return false;
                }
                else
                {
                    needFullCheck = true;
                }
            }

            ItemType requiredValueItemType = requiredValueType.PrimaryType;
            if (requiredValueItemType != BuiltInAtomicType.ANY_ATOMIC)
            {
                ItemType upperBoundValueType = valueUType.ToItemType();
                Affinity rel = th.Relationship(requiredValueItemType, upperBoundValueType);
                if (rel == Affinity.SAME_TYPE || rel == Affinity.SUBSUMES)
                {
                }
                else if (rel == Affinity.DISJOINT)
                {
                    return false;
                }
                else
                {
                    needFullCheck = true;
                }
            }

            int requiredValueCard = requiredValueType.GetCardinality();
            if (!Cardinality.Subsumes(requiredValueCard, valueCardinality))
            {
                needFullCheck = true;
            }

            if (needFullCheck)
            {

                // we need to test the entries individually
                IAtomicIterator keyIter = Keys();
                AtomicValue key;
                while ((key = keyIter.Next()) != null)
                {
                    if (!requiredKeyType.Matches(key, th))
                    {
                        return false;
                    }

                    if (!requiredValueType.Matches(Get(key), th))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        // type.
        /// <summary>
        /// Create an empty map
        /// </summary>
        /// <summary>
        /// Get the size of the map
        /// </summary>
        public override ItemType GetItemType(TypeHierarchy th)
        {
            UType keyType = UType.VOID;
            UType valueType = UType.VOID;
            int valueCard = 0;

            // we need to test the entries individually
            IAtomicIterator keyIter = Keys();
            AtomicValue key;
            while ((key = keyIter.Next()) != null)
            {
                IGroundedValue val = Get(key);
                keyType = keyType.Union(key.GetUType());
                valueType = valueType.Union(SequenceTool.GetUType(val));
                valueCard = Cardinality.Union(valueCard, SequenceTool.GetCardinality(val));
            }

            ItemType keyItemType = keyType.ToItemType();
            ItemType valueItemType = valueType.ToItemType();
            if (keyType == null)
            {

                // implies the map is empty
                return MapType.ANY_MAP_TYPE;
            }
            else
            {
                this.keyUType = keyType;
                this.valueUType = valueType;
                this.valueCardinality = valueCard;
                return new MapType((IAtomicType)keyItemType, SequenceType.MakeSequenceType(valueItemType, valueCard));
            }
        }

        // type.
        /// <summary>
        /// Create an empty map
        /// </summary>
        /// <summary>
        /// Get the size of the map
        /// </summary>
        public override MapItem AddEntry(AtomicValue key, IGroundedValue value)
        {
            IAtomicMatchKey amk = MakeKey(key);
            bool isNew = imap[amk] == null;
            bool empty = IsEmpty();
            IImmutableMap<IAtomicMatchKey, KeyValuePair> imap2 = imap.Put(amk, new KeyValuePair(key, value));
            HashTrieMap t2 = new HashTrieMap(imap2);
            t2.valueCardinality = this.valueCardinality;
            t2.keyUType = keyUType;
            t2.valueUType = valueUType;
            t2.keyAtomicType = keyAtomicType;
            t2.valueItemType = valueItemType;
            t2.UpdateTypeInformation(key, value, empty);
            if (entries >= 0)
            {
                t2.entries = isNew ? entries + 1 : entries;
            }

            return t2;
        }

        /// <summary>
        /// Accumulator for map:merge over a stream of small maps. Performs the same put sequence
        /// as a chain of AddEntry calls starting from an empty map — identical resulting trie
        /// structure and type-information — but the trie is privately owned until ToMap(), so
        /// array nodes update child slots in place (<see cref="ImmutableHashTrieMap{K,V}.PutOwned"/>)
        /// instead of path-copying per entry. No put may follow ToMap().
        /// </summary>
        internal sealed class MergeBuilder
        {
            private readonly HashTrieMap m = new HashTrieMap();
            private ImmutableHashTrieMap<IAtomicMatchKey, KeyValuePair> trie =
                ImmutableHashTrieMap<IAtomicMatchKey, KeyValuePair>.Empty();
            private int count;

            internal ISequence GetExisting(AtomicValue key, out IAtomicMatchKey amk)
            {
                amk = key.AsMapKey();
                KeyValuePair kvp = trie.Get(amk);
                return kvp == null ? null : kvp.value;
            }

            internal void Put(IAtomicMatchKey amk, AtomicValue key, IGroundedValue value, bool isNew)
            {
                bool wasEmpty = count == 0;
                trie = trie.PutOwned(amk, new KeyValuePair(key, value));
                if (isNew)
                {
                    count++;
                }

                m.UpdateTypeInformation(key, value, wasEmpty);
            }

            internal HashTrieMap ToMap()
            {
                m.imap = trie;
                m.entries = count;
                return m;
            }
        }

        // type.
        /// <summary>
        /// Create an empty map
        /// </summary>
        /// <summary>
        /// Get the size of the map
        /// </summary>
        public virtual bool InitialPut(AtomicValue key, IGroundedValue value)
        {

            //        if (Instrumentation.ACTIVE) {
            //        }
            bool empty = IsEmpty();
            IAtomicMatchKey amk = MakeKey(key);
            bool exists = imap[amk] != null;
            imap = imap.Put(amk, new KeyValuePair(key, value));
            UpdateTypeInformation(key, value, empty);
            entries = -1;
            return exists;
        }

        // type.
        /// <summary>
        /// Create an empty map
        /// </summary>
        /// <summary>
        /// Get the size of the map
        /// </summary>
        private IAtomicMatchKey MakeKey(AtomicValue key)
        {
            return key.AsMapKey();
        }

        // type.
        /// <summary>
        /// Create an empty map
        /// </summary>
        /// <summary>
        /// Get the size of the map
        /// </summary>
        public override MapItem Remove(AtomicValue key)
        {

            // This code used to assume that if the key wasn't in the
            // map, imap.remove() would return the original object
            // unchanged. But that was only true if the hash bucket
            // that would have contained the value was empty. That
            // won't be the case if other values happen to have been
            // assigned that bucket. So now we do an explicit check.
            // This is probably slower, but remove() is an uncommon
            // operation. And it gives the correct result!
            if (imap[MakeKey(key)] == null)
            {

                // The key is not present; the map is unchanged
                return this;
            }

            IImmutableMap<IAtomicMatchKey, KeyValuePair> m2 = imap.Remove(MakeKey(key));
            HashTrieMap result = new HashTrieMap(m2);
            result.keyUType = keyUType;
            result.valueUType = valueUType;
            result.valueCardinality = valueCardinality;
            result.entries = entries - 1;
            return result;
        }

        // type.
        /// <summary>
        /// Create an empty map
        /// </summary>
        /// <summary>
        /// Get the size of the map
        /// </summary>
        public override IGroundedValue Get(AtomicValue key)
        {
            KeyValuePair o = imap[MakeKey(key)];
            return o == null ? null : o.value;
        }

        // type.
        /// <summary>
        /// Create an empty map
        /// </summary>
        /// <summary>
        /// Get the size of the map
        /// </summary>
        public virtual KeyValuePair GetKeyValuePair(AtomicValue key)
        {
            return imap[MakeKey(key)];
        }

        // type.
        /// <summary>
        /// Create an empty map
        /// </summary>
        /// <summary>
        /// Get the size of the map
        /// </summary>
        public override IAtomicIterator Keys()
        {
            return new AnonymousAtomicIterator(this);
        }

        // type.
        /// <summary>
        /// Create an empty map
        /// </summary>
        /// <summary>
        /// Get the size of the map
        /// </summary>
        public override IEnumerable<KeyValuePair> KeyValuePairs()
        {

            // For C# - don't use a lambda expression here
            return new AnonymousIterable(this);
        }

        // type.
        /// <summary>
        /// Create an empty map
        /// </summary>
        /// <summary>
        /// Get the size of the map
        /// </summary>
        public virtual void DiagnosticDump()
        {
            Console.Error.WriteLine("IMap details:");
            foreach (TrieKVP<IAtomicMatchKey, KeyValuePair> entry in imap)
            {
                IAtomicMatchKey k1 = entry.key;
                AtomicValue k2 = entry.value.key;
                ISequence v = entry.value.value;
                Console.Error.WriteLine(k1.GetType() + " " + k1 + " #:" + k1.GetHashCode() + " = (" + k2.GetType() + " " + k2 + " : " + v + ")");
            }
        }

        // type.
        /// <summary>
        /// Create an empty map
        /// </summary>
        /// <summary>
        /// Get the size of the map
        /// </summary>
        //    public String toShortString() {
        //        int size = size();
        //        if (size == 0) {
        //            return "map{}";
        //        } else if (size > 5) {
        //            return "map{(:size " + size + ":)}";
        //        } else {
        //            StringBuilder buff = new StringBuilder(256);
        //            buff.append("map{");
        //            IIterator<Tuple2<IAtomicMatchKey, KeyValuePair>> iter = imap.iterator();
        //                Tuple2<IAtomicMatchKey, KeyValuePair> entry = iter.next();
        //                IAtomicMatchKey k1 = entry._1;
        //                AtomicValue k2 = entry._2.key;
        //                ISequence v = entry._2.value;
        //                buff.append(k2.toShortString());
        //                buff.append(':');
        //                buff.append(Err.depictSequence(v).toString().trim());
        //                buff.append(", ");
        //            if (size == 1) {
        //                buff.append("}");
        //            } else {
        //            return buff.toString().trim();
        //    }
        public override string ToString()
        {
            return MapItem.MapToString(this);
        }

        private sealed class AnonymousAtomicIterator : IAtomicIterator
        {

            private readonly HashTrieMap parent;
            private readonly IEnumerator<TrieKVP<IAtomicMatchKey, KeyValuePair>> baseIter;
            public AnonymousAtomicIterator(HashTrieMap parent)
            {
                this.parent = parent;
                this.baseIter = parent.imap.IIterator();
            }
            public AtomicValue Next()
            {
                return baseIter.MoveNext() ? baseIter.Current.value.key : null;
            }
            IItem ISequenceIterator.Next() => Next(); // redirect StubGen hollow to the real covariant Next(); default = silent empty iteration
            public void Dispose() { }
        }

        private sealed class AnonymousIEnumerator : IEnumerator<KeyValuePair>, IIterator<KeyValuePair>
        {
            private KeyValuePair __cur_kvp;

            private readonly HashTrieMap parent;
            private readonly IEnumerator<TrieKVP<IAtomicMatchKey, KeyValuePair>> baseIter;
            private KeyValuePair lookahead;
            private bool lookaheadFilled;
            public KeyValuePair Current => __cur_kvp;
            object System.Collections.IEnumerator.Current => __cur_kvp;
            public AnonymousIEnumerator(HashTrieMap parent)
            {
                this.parent = parent;
                this.baseIter = parent.imap.IIterator();
            }
            public bool MoveNext() { if (HasNext()) { __cur_kvp = Next(); return true; } return false; }
            public void Reset() { }
            public void Dispose() { }
            public bool HasNext()
            {
                if (!lookaheadFilled && baseIter.MoveNext())
                {
                    lookahead = baseIter.Current.value;
                    lookaheadFilled = true;
                }

                return lookaheadFilled;
            }

            public KeyValuePair Next()
            {
                if (!lookaheadFilled)
                {
                    baseIter.MoveNext();
                    lookahead = baseIter.Current.value;
                }

                lookaheadFilled = false;
                return lookahead;
            }

            public void Remove()
            {
                throw new NotSupportedException(); // immutable trie map: Iterator.remove() unsupported
            }
        }

        private sealed class AnonymousIterable : IEnumerable<KeyValuePair>
        {

            private readonly HashTrieMap parent;
            public AnonymousIterable(HashTrieMap parent)
            {
                this.parent = parent;
            }
            public IEnumerator<KeyValuePair> GetEnumerator() => new AnonymousIEnumerator(parent);
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
            public IIterator<KeyValuePair> IIterator()
            {
                return new AnonymousIEnumerator(parent);
            }
        }
    }
}
