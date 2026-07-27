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
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Collections
{
    public class IntHashSet : IntSet
    {
        private const int NBIT = 30; // MAX_SIZE = 2^NBIT
        /// <summary>
        /// The maximum number of elements this container can contain.
        /// </summary>
        private const int MAX_SIZE = 1 << NBIT; // maximum number of keys mapped
        /// <summary>
        /// The maximum number of elements this container can contain.
        /// </summary>
        /// <summary>
        /// This set's NO-DATA-VALUE.
        /// </summary>
        private readonly int ndv;
        /// <summary>
        /// The maximum number of elements this container can contain.
        /// </summary>
        /// <summary>
        /// This set's NO-DATA-VALUE.
        /// </summary>
        // private
        private int _nmax; // 0 <= _nmax = 2^nbit <= 2^NBIT = MAX_SIZE
        /// <summary>
        /// The maximum number of elements this container can contain.
        /// </summary>
        /// <summary>
        /// This set's NO-DATA-VALUE.
        /// </summary>
        // private
        private int _size; // 0 <= _size <= _nmax <= MAX_SIZE
        /// <summary>
        /// The maximum number of elements this container can contain.
        /// </summary>
        /// <summary>
        /// This set's NO-DATA-VALUE.
        /// </summary>
        // private
        private int _nlo; // _nmax*_factor (_size<=_nlo, if possible)
        /// <summary>
        /// The maximum number of elements this container can contain.
        /// </summary>
        /// <summary>
        /// This set's NO-DATA-VALUE.
        /// </summary>
        // private
        private int _nhi; //  MAX_SIZE*_factor (_size< _nhi, if possible)
        /// <summary>
        /// The maximum number of elements this container can contain.
        /// </summary>
        /// <summary>
        /// This set's NO-DATA-VALUE.
        /// </summary>
        // private
        private int _shift; // _shift = 1 + NBIT - nbit (see function hash() below)
        /// <summary>
        /// The maximum number of elements this container can contain.
        /// </summary>
        /// <summary>
        /// This set's NO-DATA-VALUE.
        /// </summary>
        // private
        private int _mask; // _mask = _nmax - 1
        /// <summary>
        /// The maximum number of elements this container can contain.
        /// </summary>
        /// <summary>
        /// This set's NO-DATA-VALUE.
        /// </summary>
        // private
        private int[] _values; // array[_nmax] of values

        /// <summary>
        /// The maximum number of elements this container can contain.
        /// </summary>
        /// <summary>
        /// This set's NO-DATA-VALUE.
        /// </summary>
        // private
        /// <summary>
        /// Initializes a set with a capacity of 8 and a load factor of 0,25.
        /// </summary>
        public virtual int[] Values
        {
            get
            {
                int index = 0;
                int[] values = new int[_size];
                foreach (int _value in _values)
                {
                    if (_value != ndv)
                    {
                        values[index++] = _value;
                    }
                }

                return values;
            }
        }
        /// <summary>
        /// The maximum number of elements this container can contain.
        /// </summary>
        /// <summary>
        /// This set's NO-DATA-VALUE.
        /// </summary>
        // private
        /// <summary>
        /// Initializes a set with a capacity of 8 and a load factor of 0,25.
        /// </summary>
        public IntHashSet() : this(8, int.MinValue)
        {
        }

        /// <summary>
        /// The maximum number of elements this container can contain.
        /// </summary>
        /// <summary>
        /// This set's NO-DATA-VALUE.
        /// </summary>
        // private
        /// <summary>
        /// Initializes a set with a capacity of 8 and a load factor of 0,25.
        /// </summary>
        public IntHashSet(int capacity) : this(capacity, int.MinValue)
        {
        }

        /// <summary>
        /// The maximum number of elements this container can contain.
        /// </summary>
        /// <summary>
        /// This set's NO-DATA-VALUE.
        /// </summary>
        // private
        /// <summary>
        /// Initializes a set with a capacity of 8 and a load factor of 0,25.
        /// </summary>
        public IntHashSet(int capacity, int noDataValue)
        {
            ndv = noDataValue;

            //_factor = 0.25;
            SetCapacity(capacity);
        }

        /// <summary>
        /// The maximum number of elements this container can contain.
        /// </summary>
        /// <summary>
        /// This set's NO-DATA-VALUE.
        /// </summary>
        // private
        /// <summary>
        /// Initializes a set with a capacity of 8 and a load factor of 0,25.
        /// </summary>
        public override IntSet Copy()
        {
            if (_size == 0)
            {
                return IntEmptySet.GetInstance();
            }
            else
            {
                IntHashSet s = new IntHashSet(_size, ndv);
                s._nmax = _nmax;
                s._size = _size;
                s._nlo = _nlo;
                s._nhi = _nhi;
                s._shift = _shift;
                s._mask = _mask;
                s._values = new int[_values.Length];
                Array.Copy(_values, 0, s._values, 0, _values.Length);
                return s;
            }
        }

        /// <summary>
        /// The maximum number of elements this container can contain.
        /// </summary>
        /// <summary>
        /// This set's NO-DATA-VALUE.
        /// </summary>
        // private
        /// <summary>
        /// Initializes a set with a capacity of 8 and a load factor of 0,25.
        /// </summary>
        public override IntSet MutableCopy()
        {
            return Copy();
        }

        /// <summary>
        /// The maximum number of elements this container can contain.
        /// </summary>
        /// <summary>
        /// This set's NO-DATA-VALUE.
        /// </summary>
        // private
        /// <summary>
        /// Initializes a set with a capacity of 8 and a load factor of 0,25.
        /// </summary>
        public override void Clear()
        {
            _size = 0;
            for (int i = 0; i < _nmax; ++i)
            {
                _values[i] = ndv;
            }
        }

        /// <summary>
        /// The maximum number of elements this container can contain.
        /// </summary>
        /// <summary>
        /// This set's NO-DATA-VALUE.
        /// </summary>
        // private
        /// <summary>
        /// Initializes a set with a capacity of 8 and a load factor of 0,25.
        /// </summary>
        public override int Size()
        {
            return _size;
        }

        /// <summary>
        /// The maximum number of elements this container can contain.
        /// </summary>
        /// <summary>
        /// This set's NO-DATA-VALUE.
        /// </summary>
        // private
        /// <summary>
        /// Initializes a set with a capacity of 8 and a load factor of 0,25.
        /// </summary>
        public override bool IsEmpty()
        {
            return _size == 0;
        }

        /// <summary>
        /// The maximum number of elements this container can contain.
        /// </summary>
        /// <summary>
        /// This set's NO-DATA-VALUE.
        /// </summary>
        // private
        /// <summary>
        /// Initializes a set with a capacity of 8 and a load factor of 0,25.
        /// </summary>
        public override bool Contains(int value)
        {
            return (_values[IndexOf(value)] != ndv);
        }

        /// <summary>
        /// The maximum number of elements this container can contain.
        /// </summary>
        /// <summary>
        /// This set's NO-DATA-VALUE.
        /// </summary>
        // private
        /// <summary>
        /// Initializes a set with a capacity of 8 and a load factor of 0,25.
        /// </summary>
        public override bool Remove(int value)
        {

            // Knuth, v. 3, 527, Algorithm R.
            int i = IndexOf(value);
            if (_values[i] == ndv)
            {
                return false;
            }

            --_size;
            for (; ; )
            {
                _values[i] = ndv;
                int j = i;
                int r;
                do
                {
                    i = (i - 1) & _mask;
                    if (_values[i] == ndv)
                    {
                        return true;
                    }

                    r = Hash(_values[i]);
                }
                while ((i <= r && r < j) || (r < j && j < i) || (j < i && i <= r));
                _values[j] = _values[i];
            }
        }

        /// <summary>
        /// The maximum number of elements this container can contain.
        /// </summary>
        /// <summary>
        /// This set's NO-DATA-VALUE.
        /// </summary>
        // private
        /// <summary>
        /// Initializes a set with a capacity of 8 and a load factor of 0,25.
        /// </summary>
        public override bool Add(int value)
        {
            if (value == ndv)
            {
                throw new ArgumentException("Can't add the 'no data' value");
            }

            int i = IndexOf(value);
            if (_values[i] == ndv)
            {
                ++_size;
                _values[i] = value;

                // Check new size
                if (_size > MAX_SIZE)
                {
                    throw new Exception("Too many elements (> " + MAX_SIZE + ')');
                }

                if (_nlo < _size && _size <= _nhi)
                {
                    SetCapacity(_size);
                }

                return true;
            }
            else
            {
                return false; // leave set unchanged
            }
        }

        /// <summary>
        /// The maximum number of elements this container can contain.
        /// </summary>
        /// <summary>
        /// This set's NO-DATA-VALUE.
        /// </summary>
        // private
        /// <summary>
        /// Initializes a set with a capacity of 8 and a load factor of 0,25.
        /// </summary>
        private int Hash(int key)
        {

            // Knuth, v. 3, 509-510. Randomize the 31 low-order bits of c*key
            // and return the highest nbits (where nbits <= 30) bits of these.
            // The constant c = 1327217885 approximates 2^31 * (sqrt(5)-1)/2.
            return ((1327217885 * key) >> _shift) & _mask;
        }

        /// <summary>
        /// The maximum number of elements this container can contain.
        /// </summary>
        /// <summary>
        /// This set's NO-DATA-VALUE.
        /// </summary>
        // private
        /// <summary>
        /// Initializes a set with a capacity of 8 and a load factor of 0,25.
        /// </summary>
        private int IndexOf(int value)
        {
            int i = Hash(value);
            while (_values[i] != ndv)
            {
                if (_values[i] == value)
                {
                    return i;
                }

                i = (i - 1) & _mask;
            }

            return i;
        }

        /// <summary>
        /// The maximum number of elements this container can contain.
        /// </summary>
        /// <summary>
        /// This set's NO-DATA-VALUE.
        /// </summary>
        // private
        /// <summary>
        /// Initializes a set with a capacity of 8 and a load factor of 0,25.
        /// </summary>
        private void SetCapacity(int capacity)
        {

            // Changed MHK in 8.9 to use a constant factor of 0.25, thus avoiding floating point arithmetic
            if (capacity < _size)
            {
                capacity = _size;
            }


            //double factor = 0.25;
            int nbit, nmax;
            for (nbit = 1, nmax = 2; nmax < capacity * 4 && nmax < MAX_SIZE; ++nbit, nmax *= 2)
            {
            }

            int nold = _nmax;
            if (nmax == nold)
            {
                return;
            }

            _nmax = nmax;
            _nlo = nmax / 4;
            _nhi = MAX_SIZE / 4;
            _shift = 1 + NBIT - nbit;
            _mask = nmax - 1;
            _size = 0;
            int[] values = _values;
            _values = new int[nmax];
            ArrayTools.Fill(_values, ndv); // empty all values
            if (values != null)
            {
                for (int i = 0; i < nold; ++i)
                {
                    int value = values[i];
                    if (value != ndv)
                    {

                        // Don't use add, because the capacity is necessarily large enough,
                        // and the value is necessarily unique (since in this set already)!
                        //add(values[i]);
                        ++_size;
                        _values[IndexOf(value)] = value;
                    }
                }
            }
        }

        /// <summary>
        /// The maximum number of elements this container can contain.
        /// </summary>
        /// <summary>
        /// This set's NO-DATA-VALUE.
        /// </summary>
        // private
        /// <summary>
        /// Initializes a set with a capacity of 8 and a load factor of 0,25.
        /// </summary>
        /// <summary>
        /// Get an iterator over the values
        /// </summary>
        public override IIntIterator IIterator()
        {
            return new IntHashSetIterator(this);
        }

        /// <summary>
        /// The maximum number of elements this container can contain.
        /// </summary>
        /// <summary>
        /// This set's NO-DATA-VALUE.
        /// </summary>
        // private
        /// <summary>
        /// Initializes a set with a capacity of 8 and a load factor of 0,25.
        /// </summary>
        /// <summary>
        /// Get an iterator over the values
        /// </summary>
        public static bool ContainsSome(IntSet one, IntSet two)
        {
            if (two is IntEmptySet)
            {
                return false;
            }

            if (two is IntUniversalSet)
            {
                return !one.IsEmpty();
            }

            if (two is IntComplementSet)
            {
                return !((IntComplementSet)two).Exclusions.ContainsAll(one);
            }

            IIntIterator it = two.IIterator();
            while (it.MoveNext())
            {
                if (one.Contains(it.Current))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The maximum number of elements this container can contain.
        /// </summary>
        /// <summary>
        /// This set's NO-DATA-VALUE.
        /// </summary>
        // private
        /// <summary>
        /// Initializes a set with a capacity of 8 and a load factor of 0,25.
        /// </summary>
        /// <summary>
        /// Get an iterator over the values
        /// </summary>
        public override bool Equals(object other)
        {
            if (other is IntSet)
            {
                IntHashSet s = (IntHashSet)other;
                return (Size() == s.Count && ContainsAll(s));
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// The maximum number of elements this container can contain.
        /// </summary>
        /// <summary>
        /// This set's NO-DATA-VALUE.
        /// </summary>
        // private
        /// <summary>
        /// Initializes a set with a capacity of 8 and a load factor of 0,25.
        /// </summary>
        /// <summary>
        /// Construct a hash key that supports the equals() test
        /// </summary>
        public override int GetHashCode()
        {

            // Note, hashcodes are the same as those used by IntArraySet
            int h = 936247625;
            IIntIterator it = IIterator();
            while (it.MoveNext())
            {
                h += it.Current;
            }

            return h;
        }

        /// <summary>
        /// The maximum number of elements this container can contain.
        /// </summary>
        /// <summary>
        /// This set's NO-DATA-VALUE.
        /// </summary>
        // private
        /// <summary>
        /// Initializes a set with a capacity of 8 and a load factor of 0,25.
        /// </summary>
        /// <summary>
        /// Construct a hash key that supports the equals() test
        /// </summary>
        public override string ToString()
        {
            return Stringify(IIterator());
        }

        /// <summary>
        /// The maximum number of elements this container can contain.
        /// </summary>
        /// <summary>
        /// This set's NO-DATA-VALUE.
        /// </summary>
        // private
        /// <summary>
        /// Initializes a set with a capacity of 8 and a load factor of 0,25.
        /// </summary>
        /// <summary>
        /// Construct a hash key that supports the equals() test
        /// </summary>
        public static string Stringify(IIntIterator it)
        {
            StringBuilder sb = new StringBuilder(100);
            while (it.MoveNext())
            {
                if (sb.Length == 0)
                {
                    sb.Append(it.Current);
                }
                else
                {
                    sb.Append(' ').Append(it.Current);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// The maximum number of elements this container can contain.
        /// </summary>
        /// <summary>
        /// This set's NO-DATA-VALUE.
        /// </summary>
        // private
        /// <summary>
        /// Initializes a set with a capacity of 8 and a load factor of 0,25.
        /// </summary>
        /// <summary>
        /// Construct a hash key that supports the equals() test
        /// </summary>
        public static IntHashSet Of(params int[] members)
        {
            IntHashSet @is = new IntHashSet(members.Length);
            foreach (int i in members)
            {
                @is.Add(i);
            }

            return @is;
        }

        /// <summary>
        /// The maximum number of elements this container can contain.
        /// </summary>
        /// <summary>
        /// This set's NO-DATA-VALUE.
        /// </summary>
        // private
        /// <summary>
        /// Initializes a set with a capacity of 8 and a load factor of 0,25.
        /// </summary>
        /// <summary>
        /// Construct a hash key that supports the equals() test
        /// </summary>
        private class IntHashSetIterator : AbstractIntIterator
        {
            private readonly IntHashSet container;
            private int i;
            public IntHashSetIterator(IntHashSet container)
            {
                this.container = container;
                i = 0;
            }

            public override bool HasNext()
            {
                while (i < container._values.Length)
                {
                    if (container._values[i] != container.ndv)
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
                return container._values[i++];
            }
        }
    }
}
