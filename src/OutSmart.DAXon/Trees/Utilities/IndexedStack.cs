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
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Trees.Utilities
{
    public class IndexedStack<T> : IEnumerable<T>
    {
        private readonly List<T> items;

        // Phase 7.1: shadow LINQ Count extension; expose lowercase Java-style alias
        public int Count => items.Count;

        /// <summary>
        /// Create an empty stack with a default initial space allocation
        /// </summary>
        // PHASE7_INDEXER_IS
        public T this[int i] { get { return Get(i); } set { Set(i, value); } }
        /// <summary>
        /// Create an empty stack with a default initial space allocation
        /// </summary>
        public IndexedStack()
        {
            items = new List<T>(20);
        }

        /// <summary>
        /// Create an empty stack with a default initial space allocation
        /// </summary>
        public IndexedStack(int size)
        {
            items = new List<T>(size);
        }

        /// <summary>
        /// Create an empty stack with a default initial space allocation
        /// </summary>
        public virtual int Size()
        {
            return items.Count;
        }
        public int size() => items.Count;

        /// <summary>
        /// Create an empty stack with a default initial space allocation
        /// </summary>
        public virtual bool IsEmpty()
        {
            return items.IsEmpty();
        }

        /// <summary>
        /// Create an empty stack with a default initial space allocation
        /// </summary>
        public virtual void IPush(T item)
        {
            items.Add(item);
        }

        /// <summary>
        /// Create an empty stack with a default initial space allocation
        /// </summary>
        public virtual T Peek()
        {
            if (items.IsEmpty())
            {
                throw new EmptyStackException();
            }
            else
            {
                return items[items.Count - 1];
            }
        }

        /// <summary>
        /// Create an empty stack with a default initial space allocation
        /// </summary>
        public virtual T Pop()
        {
            if (items.IsEmpty())
            {
                throw new EmptyStackException();
            }
            else
            {
                return items.Remove(items.Count - 1);
            }
        }
        public virtual T Get(int i)
        {
            return items[i];
        }

        /// <summary>
        /// Create an empty stack with a default initial space allocation
        /// </summary>
        public virtual void Set(int i, T value)
        {
            items[i] = value;
        }

        /// <summary>
        /// Create an empty stack with a default initial space allocation
        /// </summary>
        public virtual bool Contains(T value)
        {
            return items.Contains(value);
        }

        /// <summary>
        /// Create an empty stack with a default initial space allocation
        /// </summary>
        public virtual int IndexOf(T value)
        {
            return items.IndexOf(value);
        }

        /// <summary>
        /// Create an empty stack with a default initial space allocation
        /// </summary>
        public virtual IEnumerator<T> GetEnumerator()
        {
            return items.IIterator();
        }
        // (r1-injected NIE GetEnumerator removed - the renamed real GetEnumerator above implements IEnumerable<T>)
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}