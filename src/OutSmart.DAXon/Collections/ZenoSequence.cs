////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Collections.Zeno
{
    public class ZenoSequence : IGroundedValue
    {
        private readonly ZenoChain<IItem> chain;

        /// <summary>
        /// Construct an empty ZenoSequence
        /// </summary>
        public virtual UnicodeString UnicodeStringValue
        {
            get
            {
                switch (GetLength())
                {
                    case 0:
                        return EmptyUnicodeString.GetInstance();
                    case 1:
                        return this.Head().UnicodeStringValue;
                    default:
                        UnicodeBuilder builder = new UnicodeBuilder();
                        UnicodeString separator = EmptyUnicodeString.GetInstance();
                        foreach (IItem item in chain)
                        {
                            builder.Append(separator);
                            separator = StringConstants.SINGLE_SPACE;
                            builder.Append(item.GetStringValue());
                        }

                        return builder.ToUnicodeString();
                }
            }
        }
        /// <summary>
        /// Construct an empty ZenoSequence
        /// </summary>
        public ZenoSequence()
        {
            chain = new ZenoChain<IItem>();
        }

        /// <summary>
        /// Construct an empty ZenoSequence
        /// </summary>
        public ZenoSequence(ZenoChain<IItem> chain)
        {
            this.chain = chain;
        }

        /// <summary>
        /// Construct an empty ZenoSequence
        /// </summary>
        public static ZenoSequence FromList(IList<IItem> items)
        {
            ZenoChain<IItem> chain = new ZenoChain<IItem>().AddAll(items);
            return new ZenoSequence(chain);
        }

        /// <summary>
        /// Construct an empty ZenoSequence
        /// </summary>
        public virtual ISequenceIterator Iterate()
        {
            return new ZenoSequenceIterator(this);
        }

        /// <summary>
        /// Construct an empty ZenoSequence
        /// </summary>
        public virtual IItem ItemAt(int n)
        {
            try
            {
                return chain[n];
            }
            catch (IndexOutOfRangeException e)
            {
                return null;
            }
        }

        /// <summary>
        /// Construct an empty ZenoSequence
        /// </summary>
        public virtual IItem Head()
        {
            return chain.IsEmpty() ? null : chain[0];
        }

        /// <summary>
        /// Construct an empty ZenoSequence
        /// </summary>
        public virtual IGroundedValue Subsequence(int start, int length)
        {
            if (start < 0)
            {
                start = 0;
            }

            int size = chain.Count();
            if (start >= size || length <= 0)
            {
                return EmptySequence.GetInstance();
            }

            if ((long)start + (long)length > (long)size)
            {
                length = size - start;
            }

            if (length == 1)
            {
                return ItemAt(start);
            }
            else
            {
                return new ZenoSequence(chain.SubList(start, start + length));
            }
        }

        /// <summary>
        /// Construct an empty ZenoSequence
        /// </summary>
        public virtual int GetLength()
        {
            return chain.Count();
        }

        /// <summary>
        /// Construct an empty ZenoSequence
        /// </summary>
        public virtual string GetStringValue()
        {
            switch (GetLength())
            {
                case 0:
                    return "";
                case 1:
                    return this.Head().GetStringValue();
                default:
                    StringBuilder builder = new StringBuilder();
                    string separator = "";
                    foreach (IItem item in chain)
                    {
                        builder.Append(separator);
                        separator = " ";
                        builder.Append(item.GetStringValue());
                    }

                    return builder.ToString();
            }
        }

        /// <summary>
        /// Construct an empty ZenoSequence
        /// </summary>
        public virtual ZenoSequence Append(IItem item)
        {
            return new ZenoSequence(chain.Add(item));
        }

        /// <summary>
        /// Construct an empty ZenoSequence
        /// </summary>
        public virtual ZenoSequence AppendSequence(IGroundedValue items)
        {
            if (chain.IsEmpty() && items is ZenoSequence)
            {
                return (ZenoSequence)items;
            }

            switch (items.GetLength())
            {
                case 0:
                    return this;
                case 1:
                    IItem item = items.Head();
                    return new ZenoSequence(chain.Add(item));
                default:
                    if (items is ZenoSequence)
                    {
                        return new ZenoSequence(chain.Concat(((ZenoSequence)items).chain));
                    }
                    else
                    {
                        return new ZenoSequence(chain.AddAll(items.AsIterable()));
                    }

                    break;
            }
        }

        /// <summary>
        /// Construct an empty ZenoSequence
        /// </summary>
        public static ZenoSequence Join(IList<IGroundedValue> segments)
        {

            // Note: currently used only for testing
            ZenoChain<IItem> list = new ZenoChain<IItem>();
            foreach (IGroundedValue val in segments)
            {
                if (val is ZenoSequence)
                {
                    list = list.Concat(((ZenoSequence)val).chain);
                }
                else
                {
                    foreach (IItem item in val.AsIterable())
                    {
                        list = list.Add(item);
                    }
                }
            }

            return new ZenoSequence(list);
        }

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
        public virtual bool EffectiveBooleanValue() => throw new NotImplementedException();
        public virtual IGroundedValue Reduce() => this;
        // A ZenoSequence is already a GroundedValue, so materialize()/reduce() return itself
        // (GroundedValue defaults). The stubs threw, breaking fold-left etc. whose accumulator is a
        // ZenoSequence sequence value.
        public virtual IGroundedValue Materialize() => this;
        public virtual string ToShortString() => OutSmart.DAXon.Transformation.Err.DepictSequence(this); // upstream GroundedValue default
        public virtual IEnumerable<IItem> AsIterable() { ISequenceIterator it = Iterate(); IItem i; while ((i = it.Next()) != null) yield return i; } // upstream GroundedValue.asIterable
        public virtual bool ContainsNode(NodeInfo sought) => OutSmart.DAXon.Expressions.SingletonIntersectExpression.ContainsNode(((OutSmart.DAXon.Model.ISequence)this).Iterate(), sought); // upstream GroundedValue default
        public virtual IGroundedValue Concatenate(IGroundedValue[] others)
        {
            // upstream GroundedValue default: chain this value's items with the others
            var __chain = new OutSmart.DAXon.Collections.Zeno.ZenoChain<OutSmart.DAXon.Model.IItem>().AddAll(((OutSmart.DAXon.Model.IGroundedValue)this).AsIterable());
            foreach (OutSmart.DAXon.Model.IGroundedValue __v in others)
                __chain = __chain.AddAll(__v.AsIterable());
            return new OutSmart.DAXon.Collections.Zeno.ZenoSequence(__chain);
        }
        // A ZenoSequence is an immutable grounded value (Materialize() => this), so it is already
        // repeatable — matches AtomicValue/EmptySequence/IntegerRange. Was a hollow stub that threw when a
        // `let $x := <zeno-seq>` binding is read more than once (XPathContextMinor.SetLocalVariable).
        public virtual ISequence MakeRepeatable() => this;

        /// <summary>
        /// Construct an empty ZenoSequence
        /// </summary>
        /// <summary>
        /// A ISequenceIterator over a ZenoSequence
        /// </summary>
        public class ZenoSequenceIterator : IGroundedIterator, ILastPositionFinder, ILookaheadIterator
        {
            // This class is not a LookAheadIterator on C#, because the underlying C# enumerator has no side-effect-free
            // hasNext() operation.
            private readonly ZenoSequence sequence;
            private readonly IEnumerator<IItem> chainIterator;
            private IItem lookahead;
            private bool lookaheadFilled;
            private int position = 0;

            public virtual bool HasNext
            {
                get
                {
                    if (!lookaheadFilled && chainIterator.MoveNext())
                    {
                        lookahead = chainIterator.Current;
                        lookaheadFilled = true;
                    }

                    return lookaheadFilled;
                }
            }
            public ZenoSequenceIterator(ZenoSequence sequence)
            {
                this.sequence = sequence;
                this.chainIterator = sequence.chain.IIterator();
            }

            public virtual IItem Next()
            {
                position++;
                if (lookaheadFilled)
                {
                    lookaheadFilled = false;
                    return lookahead;
                }

                return chainIterator.MoveNext() ? chainIterator.Current : null;
            }

            public virtual bool SupportsGetLength()
            {
                return true;
            }

            public virtual int GetLength()
            {
                return sequence.GetLength();
            }

            public virtual bool IsActuallyGrounded()
            {
                return true;
            }

            public virtual IGroundedValue GetResidue()
            {
                return sequence.Subsequence(position, int.MaxValue);
            }

            public virtual IGroundedValue Materialize()
            {
                return sequence;
            }

            public virtual bool SupportsHasNext()
            {
                return true;
            }
            public virtual void Dispose() { }
        }
    }
}