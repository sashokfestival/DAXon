////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Collections
{
    public class IntHashMap<T>
    {

        /// <summary>
        /// Clears the map.
        /// </summary>
        // private
        private const int NBIT = 30; // NMAX = 2^NBIT
        /// <summary>
        /// Clears the map.
        /// </summary>
        // private
        private const int NMAX = 1 << NBIT; // maximum number of keys mapped
        /// <summary>
        /// Clears the map.
        /// </summary>
        // private
        private readonly double _factor; // 0.0 <= _factor <= 1.0
        /// <summary>
        /// Clears the map.
        /// </summary>
        // private
        private int _nmax; // 0 <= _nmax = 2^nbit <= 2^NBIT = NMAX
        /// <summary>
        /// Clears the map.
        /// </summary>
        // private
        private int _n; // 0 <= _n <= _nmax <= NMAX
        /// <summary>
        /// Clears the map.
        /// </summary>
        // private
        private int _nlo; // _nmax*_factor (_n<=_nlo, if possible)
        /// <summary>
        /// Clears the map.
        /// </summary>
        // private
        private int _nhi; //  NMAX*_factor (_n< _nhi, if possible)
        /// <summary>
        /// Clears the map.
        /// </summary>
        // private
        private int _shift; // _shift = 1 + NBIT - nbit (see function hash() below)
        /// <summary>
        /// Clears the map.
        /// </summary>
        // private
        private int _mask; // _mask = _nmax - 1
        /// <summary>
        /// Clears the map.
        /// </summary>
        // private
        private int[] _key; // array[_nmax] of keys
        /// <summary>
        /// Clears the map.
        /// </summary>
        // private
        private T[] _value; // array[_nmax] of values

        /// <summary>
        /// Clears the map.
        /// </summary>
        // PHASE7_INDEXER_INTHM
        public T this[int key] { get { return Get(key); } set { Put(key, value); } }

        /// <summary>
        /// Clears the map.
        /// </summary>
        public int Count { get { return Size(); } }
        /// <summary>
        /// Initializes a map with a capacity of 8 and a load factor of 0,25.
        /// </summary>
        public IntHashMap() : this(8, 0.25)
        {
        }

        /// <summary>
        /// Initializes a map with a capacity of 8 and a load factor of 0,25.
        /// </summary>
        public IntHashMap(int capacity) : this(capacity, 0.25)
        {
        }

        /// <summary>
        /// Initializes a map with a capacity of 8 and a load factor of 0,25.
        /// </summary>
        public IntHashMap(int capacity, double factor)
        {
            _factor = factor;
            SetCapacity(capacity);
        }

        /// <summary>
        /// Clears the map.
        /// </summary>
        public virtual void Clear()
        {
            _n = 0;
            for (int i = 0; i < _nmax; ++i)
            {
                _value[i] = NullValue();
            }
        }

        /// <summary>
        /// Clears the map.
        /// </summary>
        private T NullValue()
        {
            return default(T);
        }

        /// <summary>
        /// Clears the map.
        /// </summary>
        private bool IsNull(T value)
        {
            return value == null;
        }
        public virtual T Get(int key)
        {
            return _value[IndexOf(key)];
        }
        public virtual int Size()
        {
            return _n;
        }

        /// <summary>
        /// Clears the map.
        /// </summary>
        public virtual bool Remove(int key)
        {

            // Knuth, v. 3, 527, Algorithm R.
            int i = IndexOf(key);

            //if (!_filled[i]) {
            if (_value[i] == null)
            {
                return false;
            }

            --_n;
            for (; ; )
            {

                //_filled[i] = false;
                _value[i] = NullValue();
                int j = i;
                int r;
                do
                {
                    i = (i - 1) & _mask;

                    //if (!_filled[i]) {
                    if (IsNull(_value[i]))
                    {
                        return true;
                    }

                    r = Hash(_key[i]);
                }
                while ((i <= r && r < j) || (r < j && j < i) || (j < i && i <= r));
                _key[j] = _key[i];
                _value[j] = _value[i]; //_filled[j] = _filled[i];
            }
        }

        /// <summary>
        /// Clears the map.
        /// </summary>
        public virtual T Put(int key, T value)
        {
            if (IsNull(value))
            {
                throw new NullReferenceException("IntHashMap does not allow null values");
            }

            int i = IndexOf(key);
            T old = _value[i];
            if (!IsNull(old))
            {
                _value[i] = value;
            }
            else
            {
                _key[i] = key;
                _value[i] = value;
                Grow();
            }

            return old;
        }
        /// <summary>
        /// Clears the map.
        /// </summary>
        // private
        private int Hash(int key)
        {

            // Knuth, v. 3, 509-510. Randomize the 31 low-order bits of c*key
            // and return the highest nbits (where nbits <= 30) bits of these.
            // The constant c = 1327217885 approximates 2^31 * (sqrt(5)-1)/2.
            return ((1327217885 * key) >> _shift) & _mask;
        }

        /// <summary>
        /// Clears the map.
        /// </summary>
        // private
        private int IndexOf(int key)
        {
            int i = Hash(key);

            //while (_filled[i]) {
            while (!IsNull(_value[i]))
            {
                if (_key[i] == key)
                {
                    return i;
                }

                i = (i - 1) & _mask;
            }

            return i;
        }

        /// <summary>
        /// Clears the map.
        /// </summary>
        // private
        private void Grow()
        {
            ++_n;
            if (_n > NMAX)
            {
                throw new Exception("number of keys mapped exceeds " + NMAX);
            }

            if (_nlo < _n && _n <= _nhi)
            {
                SetCapacity(_n);
            }
        }

        /// <summary>
        /// Clears the map.
        /// </summary>
        // private
        private void SetCapacity(int capacity)
        {
            if (capacity < _n)
            {
                capacity = _n;
            }

            double factor = (_factor < 0.01) ? 0.01 : (_factor > 0.99) ? 0.99 : _factor;
            int nbit, nmax;
            for (nbit = 1, nmax = 2; nmax * factor < capacity && nmax < NMAX; ++nbit, nmax *= 2)
            {
            }

            int nold = _nmax;
            if (nmax == nold)
            {
                return;
            }

            _nmax = nmax;
            _nlo = (int)(nmax * factor);
            _nhi = (int)(NMAX * factor);
            _shift = 1 + NBIT - nbit;
            _mask = nmax - 1;
            int[] key = _key;
            T[] value = _value;

            //boolean[] filled = _filled;
            _n = 0;
            _key = new int[nmax];

            // semantically equivalent to _value = new V[nmax]
            _value = MakeValueArray(nmax);

            //_filled = new boolean[nmax];
            if (key != null)
            {
                for (int i = 0; i < nold; ++i)
                {

                    //if (filled[i]) {
                    if (!IsNull(value[i]))
                    {
                        Put(key[i], value[i]);
                    }
                }
            }
        }

        /// <summary>
        /// Clears the map.
        /// </summary>
        // private
        // no-op
        private T[] MakeValueArray(int size)
        {
            return new T[size];
        }

        /// <summary>
        /// Clears the map.
        /// </summary>
        // private
        // no-op
        public virtual IIntIterator KeyIterator()
        {
            return new IntHashMapKeyIterator<T>(this);
        }

        /// <summary>
        /// Clears the map.
        /// </summary>
        // private
        // no-op
        public virtual IEnumerator<T> ValueIterator()
        {
            return new IntHashMapValueIterator<T>(this);
        }

        /// <summary>
        /// Clears the map.
        /// </summary>
        // private
        // no-op
        // ValueIterator() returns an IEnumerATOR; casting it to IEnumerable<T> is an InvalidCast at runtime
        // (hit by WindowClause compilation iterating the clause's variable bindings). Enumerate it instead.
        public virtual IEnumerable<T> ValueSet()
        {
            var it = this.ValueIterator();
            while (it.MoveNext())
            {
                yield return it.Current;
            }
        }

        /// <summary>
        /// Clears the map.
        /// </summary>
        // private
        // no-op
        public virtual IntHashMap<T> Copy()
        {
            IntHashMap<T> n = new IntHashMap<T>(Size());
            IIntIterator it = KeyIterator();
            while (it.MoveNext())
            {
                int k = it.Current;
                n.Put(k, Get(k));
            }

            return n;
        }

        /// <summary>
        /// Clears the map.
        /// </summary>
        // private
        // no-op
        public virtual IntSet KeySet()
        {
            return new IntHashMapKeySet<T>(this);
        }

        /// <summary>
        /// Clears the map.
        /// </summary>
        // private
        // no-op
        private class IntHashMapKeyIterator<V> : AbstractIntIterator
        {
            private int i = 0;
            private readonly IntHashMap<V> map;
            public IntHashMapKeyIterator(IntHashMap<V> map)
            {
                this.map = map;
                i = 0;
            }

            public override bool HasNext()
            {
                while (i < map._key.Length)
                {
                    if (!map.IsNull(map._value[i]))
                    {
                        return true;
                    }
                    else
                    {
                        i++;
                    }
                }

                return false;
            }

            public override int Next()
            {
                return map._key[i++];
            }
        }

        /// <summary>
        /// Clears the map.
        /// </summary>
        // private
        // no-op
        private class IntHashMapValueIterator<W> : IEnumerator<W>
        {
            private int i = 0;
            private readonly IntHashMap<W> map;



            // .NET IEnumerator<W> shim methods (Phase 3.6.5)

            private W _current;

            public W Current => _current;

            object System.Collections.IEnumerator.Current => _current;
            public IntHashMapValueIterator(IntHashMap<W> map)
            {
                this.map = map;
                i = 0;
            }

            public virtual bool HasNext()
            {
                while (i < map._key.Length)
                {
                    if (!map.IsNull(map._value[i]))
                    {
                        return true;
                    }
                    else
                    {
                        i++;
                    }
                }

                return false;
            }

            public virtual W Next()
            {
                W temp = map._value[i++];
                if (map.IsNull(temp))
                {
                    throw new InvalidOperationException();
                }

                return temp;
            }

            public virtual void Remove()

            {

                throw new NotSupportedException("remove");

            }

            public bool MoveNext()

            {

                if (HasNext()) { _current = Next(); return true; }

                return false;

            }

            public void Reset() { i = 0; _current = default; }

            public void Dispose() { }

        }

        /// <summary>
        /// Clears the map.
        /// </summary>
        // private
        // no-op
        private class IntHashMapKeySet<U> : IntSet
        {
            private readonly IntHashMap<U> map;
            public IntHashMapKeySet(IntHashMap<U> map)
            {
                this.map = map;
            }

            public override void Clear()
            {
                throw new NotSupportedException("Immutable set");
            }

            public override IntSet Copy()
            {
                IntHashSet s = new IntHashSet();
                IIntIterator ii = IIterator();
                while (ii.MoveNext())
                {
                    s.Add(ii.Current);
                }

                return s;
            }

            public override IntSet MutableCopy()
            {
                return Copy();
            }

            public override bool IsMutable()
            {
                return false;
            }

            public override int Size()
            {
                return map._n;
            }

            public override bool IsEmpty()
            {
                return map._n == 0;
            }

            public override bool Contains(int key)
            {
                return map._value[map.IndexOf(key)] != null;
            }

            public override bool Remove(int value)
            {
                throw new NotSupportedException("Immutable set");
            }

            public override bool Add(int value)
            {
                throw new NotSupportedException("Immutable set");
            }

            public override IIntIterator IIterator()
            {
                return new IntHashMapKeyIterator<U>(map);
            }

            public override IntSet Union(IntSet other)
            {
                return Copy().Union(other);
            }

            public override IntSet Intersect(IntSet other)
            {
                return Copy().Intersect(other);
            }

            public override IntSet Except(IntSet other)
            {
                return Copy().Except(other);
            }

            public override bool ContainsAll(IntSet other)
            {
                return Copy().ContainsAll(other);
            }

            public override string ToString()
            {
                return IntHashSet.Stringify(IIterator());
            }
        }
    }
}
