////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal.Functional;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Trees.Iterators
{
    public class ManualIterator : IFocusIterator, ISequenceIterator, IReversibleIterator, ILastPositionFinder, IGroundedIterator, ILookaheadIterator
    {
        private IItem item;
        private int _position;
        private Func<int> lengthFinder;

        /// <summary>
        /// Advance the current position by one.
        /// </summary>
        public virtual bool HasNext => Position() != GetLength();
        public ManualIterator()
        {
            item = null;
            _position = 0;
        }

        public ManualIterator(IItem value, int position)
        {
            this.item = value;
            this._position = position;
        }

        public ManualIterator(IItem value)
        {
            this.item = value;
            this._position = 1;
            this.lengthFinder = () => 1;
        }

        public virtual void SetContextItem(IItem value)
        {
            this.item = value;
        }

        public virtual void SetLengthFinder(Func<int> finder)
        {
            this.lengthFinder = finder;
        }

        /// <summary>
        /// Advance the current position by one.
        /// </summary>
        public virtual void IncrementPosition()
        {
            _position++;
        }

        /// <summary>
        /// Advance the current position by one.
        /// </summary>
        public virtual void SetPosition(int position)
        {
            this._position = position;
        }

        /// <summary>
        /// Advance the current position by one.
        /// </summary>
        public virtual bool SupportsHasNext()
        {
            return true;
        }

        /// <summary>
        /// Advance the current position by one.
        /// </summary>
        public virtual IItem Next()
        {
            return null;
        }

        /// <summary>
        /// Advance the current position by one.
        /// </summary>
        public virtual IItem Current()
        {
            return item;
        }

        /// <summary>
        /// Advance the current position by one.
        /// </summary>
        public virtual int Position()
        {
            return _position;
        }

        /// <summary>
        /// Advance the current position by one.
        /// </summary>
        public virtual bool SupportsGetLength()
        {
            return true;
        }

        /// <summary>
        /// Advance the current position by one.
        /// </summary>
        public virtual int GetLength()
        {
            if (lengthFinder == null)
            {
                throw new UncheckedXPathException("Saxon streaming restriction: last() cannot be used when consuming a sequence of streamed nodes, even if the items being processed are grounded");
            }
            else
            {
                return lengthFinder.Get();
            }
        }

        /// <summary>
        /// Advance the current position by one.
        /// </summary>
        public virtual bool IsActuallyGrounded()
        {
            return true;
        }

        /// <summary>
        /// Advance the current position by one.
        /// </summary>
        public virtual ManualIterator GetReverseIterator()
        {
            return new ManualIterator(item);
        }

        /// <summary>
        /// Advance the current position by one.
        /// </summary>
        public virtual IGroundedValue Materialize()
        {
            return item;
        }

        /// <summary>
        /// Advance the current position by one.
        /// </summary>
        public virtual IGroundedValue GetResidue()
        {
            return item;
        }
        ISequenceIterator IReversibleIterator.GetReverseIterator() => GetReverseIterator();
        public virtual void Dispose() { }
    }
}

