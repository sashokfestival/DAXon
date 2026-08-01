////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions
{
    public class TailIterator : ISequenceIterator, ILastPositionFinder, ILookaheadIterator
    {
        private readonly ISequenceIterator @base;
        private readonly int start;

        public virtual bool HasNext => ((ILookaheadIterator)@base).HasNext;
        private TailIterator(ISequenceIterator @base, int start)
        {
            this.@base = @base;
            this.start = start;
        }

        public static ISequenceIterator Make(ISequenceIterator @base, int start)
        {
            if (start <= 1)
            {
                return @base;
            }
            else if (@base is ArrayIterator)
            {
                return ((ArrayIterator)@base).MakeSliceIterator(start, int.MaxValue);
            }
            else if (@base is IGroundedIterator && ((IGroundedIterator)@base).IsActuallyGrounded())
            {
                try
                {
                    IGroundedValue value = SequenceTool.ToGroundedValue(@base);
                    if (start > value.GetLength())
                    {
                        return EmptyIterator.GetInstance();
                    }
                    else
                    {
                        return new ValueTailIterator(value, start - 1);
                    }
                }
                catch (UncheckedXPathException e)
                {
                    throw e.GetXPathException();
                }
            }
            else
            {

                // discard the first n-1 items from the underlying iterator
                for (int i = 0; i < start - 1; i++)
                {
                    IItem b = @base.Next();
                    if (b == null)
                    {
                        return EmptyIterator.GetInstance();
                    }
                }

                return new TailIterator(@base, start);
            }
        }

        public virtual IItem Next()
        {
            return @base.Next();
        }

        public virtual bool SupportsHasNext()
        {
            return @base is ILookaheadIterator && ((ILookaheadIterator)@base).SupportsHasNext();
        }

        public virtual bool SupportsGetLength()
        {
            return SequenceTool.SupportsGetLength(@base);
        }

        public virtual int GetLength()
        {
            int bl = SequenceTool.GetLength(@base) - start + 1;
            return Math.Max(bl, 0);
        }

        public virtual void Dispose()
        {
            @base.Dispose();
        }
    }
}