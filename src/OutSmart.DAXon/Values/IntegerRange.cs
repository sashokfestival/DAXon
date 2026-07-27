////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Values
{
    public class IntegerRange : IAtomicSequence
    {
        public long start;
        public long step;
        public long end; // the adjusted end, so it is actually the last number returned

        public virtual long Start => start;

        public virtual long End => end;

        /// <summary>
        /// Get the length of the sequence
        /// </summary>
        public virtual UnicodeString CanonicalLexicalRepresentation => UnicodeStringValue;

        /// <summary>
        /// Get the length of the sequence
        /// </summary>
        public virtual UnicodeString UnicodeStringValue
        {
            get
            {
                try
                {
                    return SequenceTool.GetStringValue(this);
                }
                catch (XPathException err)
                {
                    throw new InvalidOperationException(err.Message, err);
                }
            }
        }
        public IntegerRange(long start, long step, long end)
        {
            if (step == 0)
            {
                throw new ArgumentException("step = 0 in IntegerRange");
            }

            if (end != start && (end > start != step > 0))
            {
                throw new ArgumentException("end before start in IntegerRange");
            }

            if (System.Math.Abs((end - start) / step) > int.MaxValue)
            {
                throw new ArgumentException("Maximum length of sequence in Saxon is " + int.MaxValue);
            }

            this.start = start;
            this.step = step;
            this.end = start + step * (end - start) / step;
        }

        public virtual long GetStep()
        {
            return step;
        }

        // Closed-form total of the arithmetic series start, start+step, ..., last (inclusive),
        // all in checked long arithmetic: count*start + step*(0+1+...+(count-1)). False on
        // overflow — callers then iterate, which reproduces the generic behaviour (including
        // BigInteger promotion). Shared by fn:sum's fast paths and the range iterators.
        internal static bool TrySum(long start, long step, long last, out long total)
        {
            try
            {
                checked
                {
                    long count = (last - start) / step + 1;
                    long tri = (count % 2 == 0) ? (count / 2) * (count - 1) : count * ((count - 1) / 2);
                    total = count * start + step * tri;
                    return true;
                }
            }
            catch (OverflowException)
            {
                total = 0;
                return false;
            }
        }

        public virtual IAtomicIterator Iterate()
        {

            // Written this way for C# conversion
            if (step > 0)
            {
                return new AscendingRangeIterator(start, step, end);
            }
            else
            {
                return new DescendingRangeIterator(start, -step, end);
            }
        }

        public virtual IntegerValue ItemAt(int n)
        {
            if (n < 0 || n >= GetLength())
            {
                return null;
            }

            return Int64Value.MakeIntegerValue(start + (n * step));
        }

        public virtual IGroundedValue Subsequence(int start, int length)
        {
            if (length <= 0)
            {
                return EmptySequence.GetInstance();
            }

            long newStart = this.start + System.Math.Max(start, 0);
            long newEnd = newStart + ((long)length * step) - 1;
            if (newEnd > end)
            {
                newEnd = end;
            }

            if (newEnd >= newStart)
            {
                return new IntegerRange(newStart, step, newEnd);
            }
            else
            {
                return EmptySequence.GetInstance();
            }
        }

        /// <summary>
        /// Get the length of the sequence
        /// </summary>
        public virtual int GetLength()
        {
            return (int)((end - start) / step) + 1;
        }

        /// <summary>
        /// Get the length of the sequence
        /// </summary>
        public virtual IntegerValue Head()
        {
            return new Int64Value(start);
        }

        /// <summary>
        /// Get the length of the sequence
        /// </summary>
        public virtual string GetStringValue()
        {
            try
            {
                return SequenceTool.Stringify(this);
            }
            catch (XPathException err)
            {
                throw new InvalidOperationException(err.Message, err);
            }
        }

        /// <summary>
        /// Get the length of the sequence
        /// </summary>
        public virtual bool EffectiveBooleanValue()
        {
            return ExpressionTool.EffectiveBooleanValue(Iterate());
        }

        /// <summary>
        /// Get the length of the sequence
        /// </summary>
        public virtual IGroundedValue Reduce()
        {
            if (start == end)
            {
                return ItemAt(0);
            }
            else
            {
                return this;
            }
        }

        /// <summary>
        /// Get the length of the sequence
        /// </summary>
        public override string ToString()
        {
            return "(" + start + (step == 1 ? "" : (" by " + step)) + " to " + end + ")";
        }

        /// <summary>
        /// Get the length of the sequence
        /// </summary>
        public virtual IEnumerator<AtomicValue> IIterator()
        {
            return new IntegerRangeIterator(this);
        }
        AtomicValue IAtomicSequence.Head() => Head(); // redirect StubGen hollow to the real typed member; default = silent null
        AtomicValue IAtomicSequence.ItemAt(int arg0) => ItemAt(arg0);
        ISequenceIterator IGroundedValue.Iterate() => Iterate();
        IItem IGroundedValue.ItemAt(int arg0) => ItemAt(arg0);
        IItem IGroundedValue.Head() => Head();
        IItem ISequence.Head() => Head();
        ISequenceIterator ISequence.Iterate() => Iterate();
        public IEnumerator<AtomicValue> GetEnumerator() => throw new NotImplementedException();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => throw new NotImplementedException();

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
        // IntegerRange is already an in-memory grounded value, so materialize/makeRepeatable return itself,
        // and an integer range contains no nodes. (Materialize was a throwing stub, so `1 to 5` blew up wherever
        // it had to be grounded, e.g. map:entry("k", 1 to 5).)
        public virtual IGroundedValue Materialize() => this;
        public virtual string ToShortString() => "(" + Start + " to " + End + ")";
        // Streaming, not a materialized list: a range can be huge (bounded only by the
        // int.MaxValue sequence cap), and eager buffering turned every whole-range consumer
        // (Literal type checks, foreach bridges) into an O(N)-memory walk — sum(1 to 1e9)
        // exhausted memory at compile time through Literal.IsInstance.
        public virtual IEnumerable<IItem> AsIterable()
        {
            var it = Iterate();
            for (IItem i = it.Next(); i != null; i = it.Next())
            {
                yield return i;
            }
        }
        public virtual bool ContainsNode(NodeInfo sought) => false;
        public virtual IGroundedValue Concatenate(IGroundedValue[] others)
        {
            // upstream GroundedValue default: chain this value's items with the others
            var __chain = new OutSmart.DAXon.Collections.Zeno.ZenoChain<OutSmart.DAXon.Model.IItem>().AddAll(((OutSmart.DAXon.Model.IGroundedValue)this).AsIterable());
            foreach (OutSmart.DAXon.Model.IGroundedValue __v in others)
                __chain = __chain.AddAll(__v.AsIterable());
            return new OutSmart.DAXon.Collections.Zeno.ZenoSequence(__chain);
        }
        public virtual ISequence MakeRepeatable() => this;

        /// <summary>
        /// Get the length of the sequence
        /// </summary>
        private class IntegerRangeIterator : IEnumerator<AtomicValue>
        {
            private IntegerRange range;
            private long current;
            public AtomicValue Current => default;
            object System.Collections.IEnumerator.Current => null;
            public IntegerRangeIterator(IntegerRange range)
            {
                this.range = range;
                current = range.start;
            }

            public virtual bool HasNext()
            {
                return current <= range.end;
            }

            public virtual void Remove()
            {
                throw new NotSupportedException();
            }

            public virtual AtomicValue Next()
            {
                return new Int64Value(current++);
            }
            void IDisposable.Dispose() { }
            bool System.Collections.IEnumerator.MoveNext() => false;
            void System.Collections.IEnumerator.Reset() { }
        }
    }
}
