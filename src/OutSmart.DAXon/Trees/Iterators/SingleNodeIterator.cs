////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Trees.Iterators
{
    /// <summary>
    /// SingleNodeIterator: an iterator over a sequence of zero or one nodes
    /// </summary>
    internal class SingleNodeIterator : IAxisIterator, IReversibleIterator, ILastPositionFinder, IGroundedIterator, ILookaheadIterator
    {
        private readonly NodeInfo item;
        private int position = 0;

        public virtual bool HasNext => position == 0;

        public virtual NodeInfo Value => item;
        private SingleNodeIterator(NodeInfo value)
        {
            this.item = value;
        }

        public static IAxisIterator MakeIterator(NodeInfo item)
        {
            if (item == null)
            {
                return EmptyIterator.OfNodes();
            }
            else
            {
                return new SingleNodeIterator(item);
            }
        }

        public virtual bool SupportsHasNext()
        {
            return true;
        }

        public virtual NodeInfo Next()
        {
            if (position == 0)
            {
                position = 1;
                return item;
            }
            else if (position == 1)
            {
                position = -1;
                return null;
            }
            else
            {
                return null;
            }
        }

        public virtual bool SupportsGetLength()
        {
            return true;
        }

        public virtual int GetLength()
        {
            return 1;
        }

        public virtual ISequenceIterator GetReverseIterator()
        {
            return new SingleNodeIterator(item);
        }

        public virtual bool IsActuallyGrounded()
        {
            return true;
        }

        public virtual IGroundedValue Materialize()
        {
            return SequenceTool.ItemOrEmpty(item);
        }

        public virtual IGroundedValue GetResidue()
        {
            return SequenceTool.ItemOrEmpty(item);
        }
        IItem ISequenceIterator.Next() => Next(); // runtime: StubGen wrote => default (null) which re-broke the single-child CHILD axis; delegate to the real NodeInfo Next()
        public virtual void Dispose() { }
    }
}

