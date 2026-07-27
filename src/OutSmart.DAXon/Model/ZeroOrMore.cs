////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
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
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Model
{
    public class ZeroOrMore<T> : IGroundedValue, IEnumerable<T>
    {
        private readonly IList<T> value;

        protected virtual IList<T> Value => value;

        public virtual UnicodeString UnicodeStringValue => SequenceTool.GetStringValue(this);
        public ZeroOrMore(IList<T> list)
        {
            this.value = list;
        }

        public static ZeroOrMore<T> FromSequenceIterator<T>(ISequenceIterator iter)
        {
            IList<T> list = new List<T>();
            for (IItem item; (item = iter.Next()) != null;)
            {
                list.Add((T)item);
            }

            return new ZeroOrMore<T>(list);
        }

        public virtual string GetStringValue()
        {
            return SequenceTool.Stringify(this);
        }

        public virtual T Head()
        {
            return ItemAt(0);
        }

        public virtual int GetLength()
        {
            return value.Count;
        }

        public virtual int GetCardinality()
        {
            switch (value.Count)
            {
                case 0:
                    return StaticProperty.EMPTY;
                case 1:
                    return StaticProperty.EXACTLY_ONE;
                default:
                    return StaticProperty.ALLOWS_ONE_OR_MORE;
            }
        }

        public virtual T ItemAt(int n)
        {
            if (n < 0 || n >= GetLength())
            {
                return default(T);
            }
            else
            {
                return value[n];
            }
        }

        public virtual ListIterator.Of<T> Iterate()
        {
            return new ListIterator.Of<T>(value);
        }

        public virtual ISequenceIterator ReverseIterate()
        {
            return Reverse.ReverseIterator(value);
        }

        /// <summary>
        /// Get the effective boolean value
        /// </summary>
        public virtual bool EffectiveBooleanValue()
        {
            int len = GetLength();
            if (len == 0)
            {
                return false;
            }
            else
            {
                IItem first = (IItem)value[0];
                if (first is NodeInfo)
                {
                    return true;
                }
                else if (len == 1 && first is AtomicValue)
                {
                    return first.EffectiveBooleanValue();
                }
                else
                {

                    // this will fail - reuse the error messages
                    return ExpressionTool.EffectiveBooleanValue(Iterate());
                }
            }
        }

        /// <summary>
        /// Get the effective boolean value
        /// </summary>
        public virtual IGroundedValue Subsequence(int start, int length)
        {
            if (start < 0)
            {
                start = 0;
            }

            if (start > value.Count)
            {
                return EmptySequence.GetInstance();
            }

            return new ListIterator.Of<T>(value.SubList(start, start + length)).Materialize().Reduce();
        }

        /// <summary>
        /// Get the effective boolean value
        /// </summary>
        public override string ToString()
        {
            StringBuilder fsb = new StringBuilder(64);
            for (int i = 0; i < value.Count; i++)
            {
                fsb.Append(i == 0 ? "(" : ", ");
                fsb.Append(value[i].ToString());
            }

            fsb.Append(')');
            return fsb.ToString();
        }

        /// <summary>
        /// Get the effective boolean value
        /// </summary>
        public virtual IGroundedValue Reduce()
        {
            int len = GetLength();
            if (len == 0)
            {
                return EmptySequence.GetInstance();
            }
            else if (len == 1)
            {
                return (IGroundedValue)ItemAt(0);
            }
            else
            {
                return this;
            }
        }

        /// <summary>
        /// Get the effective boolean value
        /// </summary>
        //@Override
        public virtual IEnumerator<T> IIterator()
        {
            return value.IIterator();
        }
        ISequenceIterator IGroundedValue.Iterate() => Iterate();
        IItem IGroundedValue.ItemAt(int arg0) => (IItem)(object)ItemAt(arg0);
        IItem IGroundedValue.Head() => (IItem)(object)Head();
        IItem ISequence.Head() => (IItem)(object)Head();
        ISequenceIterator ISequence.Iterate() => Iterate();
        public IEnumerator<T> GetEnumerator() => throw new NotImplementedException();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => throw new NotImplementedException();

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
        public virtual IGroundedValue Materialize() => this; // upstream GroundedValue default
        public virtual string ToShortString() => OutSmart.DAXon.Transformation.Err.DepictSequence(this); // upstream GroundedValue default
        public virtual IEnumerable<IItem> AsIterable() { foreach (T t in Value) yield return (IItem)(object)t; } // upstream GroundedValue.asIterable
        public virtual bool ContainsNode(NodeInfo sought) => OutSmart.DAXon.Expressions.SingletonIntersectExpression.ContainsNode(((OutSmart.DAXon.Model.ISequence)this).Iterate(), sought); // upstream GroundedValue default
        public virtual IGroundedValue Concatenate(IGroundedValue[] others)
        {
            // upstream GroundedValue default: chain this value's items with the others
            var __chain = new OutSmart.DAXon.Collections.Zeno.ZenoChain<OutSmart.DAXon.Model.IItem>().AddAll(((OutSmart.DAXon.Model.IGroundedValue)this).AsIterable());
            foreach (OutSmart.DAXon.Model.IGroundedValue __v in others)
                __chain = __chain.AddAll(__v.AsIterable());
            return new OutSmart.DAXon.Collections.Zeno.ZenoSequence(__chain);
        }
        public virtual ISequence MakeRepeatable() => this; // upstream Sequence.makeRepeatable default
    }
}