////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Trees.Iterators;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions
{
    public class ValueTailIterator : ISequenceIterator, IGroundedIterator, ILookaheadIterator
    {
        private readonly IGroundedValue baseValue;
        private readonly int start; // zero-based
        private int pos = 0;

        public virtual bool HasNext => baseValue.ItemAt(start + pos) != null;
        public ValueTailIterator(IGroundedValue @base, int start)
        {
            baseValue = @base;
            this.start = start;
            pos = 0;
        }

        public virtual IItem Next()
        {
            return baseValue.ItemAt(start + pos++);
        }

        public virtual bool SupportsHasNext()
        {
            return true;
        }

        public virtual bool IsActuallyGrounded()
        {
            return true;
        }

        public virtual IGroundedValue Materialize()
        {
            if (start == 0)
            {
                return baseValue;
            }
            else
            {
                return baseValue.Subsequence(start, int.MaxValue);
            }
        }

        public virtual IGroundedValue GetResidue()
        {
            if (start == 0 && pos == 0)
            {
                return baseValue;
            }
            else
            {
                return baseValue.Subsequence(start + pos, int.MaxValue);
            }
        }
        public virtual void Dispose() { }
    }
}
