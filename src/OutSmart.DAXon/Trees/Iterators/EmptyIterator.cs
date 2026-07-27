////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Core;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Trees.Iterators
{
    public class EmptyIterator : ISequenceIterator, IReversibleIterator, ILastPositionFinder, IGroundedIterator, ILookaheadIterator, IAtomizedValueIterator
    {
        private static readonly EmptyIterator theInstance = new EmptyIterator();

        /// <summary>
        /// Protected constructor
        /// </summary>
        public virtual bool HasNext => false;

        /// <summary>
        /// Protected constructor
        /// </summary>
        protected EmptyIterator()
        {
        }
        public static EmptyIterator GetInstance()
        {
            return theInstance;
        }

        /// <summary>
        /// Protected constructor
        /// </summary>
        public virtual IAtomicSequence NextAtomizedValue()
        {
            return null;
        }

        /// <summary>
        /// Protected constructor
        /// </summary>
        public virtual IItem Next()
        {
            return null;
        }

        /// <summary>
        /// Protected constructor
        /// </summary>
        public virtual bool SupportsGetLength()
        {
            return true;
        }

        /// <summary>
        /// Protected constructor
        /// </summary>
        public virtual int GetLength()
        {
            return 0;
        }

        /// <summary>
        /// Protected constructor
        /// </summary>
        public virtual EmptyIterator GetReverseIterator()
        {
            return this;
        }

        /// <summary>
        /// Protected constructor
        /// </summary>
        public virtual IGroundedValue Materialize()
        {
            return EmptySequence.GetInstance();
        }

        /// <summary>
        /// Protected constructor
        /// </summary>
        public virtual IGroundedValue GetResidue()
        {
            return EmptySequence.GetInstance();
        }

        /// <summary>
        /// Protected constructor
        /// </summary>
        public virtual bool SupportsHasNext()
        {
            return true;
        }

        /// <summary>
        /// Protected constructor
        /// </summary>
        public virtual bool IsActuallyGrounded()
        {
            return true;
        }

        /// <summary>
        /// Protected constructor
        /// </summary>
        public static IAxisIterator OfNodes()
        {
            return OfNodesIter.THE_INSTANCE;
        }

        /// <summary>
        /// Protected constructor
        /// </summary>
        public static IAtomicIterator OfAtomic()
        {
            return OfAtomicIter.THE_INSTANCE;
        }
        ISequenceIterator IReversibleIterator.GetReverseIterator() => GetReverseIterator();
        public virtual void Dispose() { }

        /// <summary>
        /// Protected constructor
        /// </summary>
        /// <summary>
        /// An empty iterator for use where a sequence of nodes is required
        /// </summary>
        private class OfNodesIter : EmptyIterator, IAxisIterator
        {
            public static readonly OfNodesIter THE_INSTANCE = new OfNodesIter();
            public override IItem Next()
            {
                return null;
            }
            NodeInfo IAxisIterator.Next() => null;
        }

        /// <summary>
        /// Protected constructor
        /// </summary>
        /// <summary>
        /// An empty iterator for use where a sequence of atomic values is required
        /// </summary>
        private class OfAtomicIter : EmptyIterator, IAtomicIterator
        {
            public static readonly OfAtomicIter THE_INSTANCE = new OfAtomicIter();
            public override IItem Next()
            {
                return null;
            }
            AtomicValue IAtomicIterator.Next() => null;
        }
    }
}


