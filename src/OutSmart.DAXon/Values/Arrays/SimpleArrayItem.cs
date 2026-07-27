////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Values.Arrays
{
    /// <summary>
    /// A simple implementation of XDM array items, in which the array is backed by a Java List.
    /// </summary>
    public class SimpleArrayItem : AbstractArrayItem
    {
        /// <summary>
        /// Static constant value representing an empty array
        /// </summary>
        public static readonly SimpleArrayItem EMPTY_ARRAY = new SimpleArrayItem(new List<IGroundedValue>());
        /// <summary>
        /// Static constant value representing an empty array
        /// </summary>
        private readonly IList<IGroundedValue> _members;
        /// <summary>
        /// Static constant value representing an empty array
        /// </summary>
        private bool knownToBeGrounded = false;
        /// <summary>
        /// Static constant value representing an empty array
        /// </summary>
        private IPingable conversionPingable;

        /// <summary>
        /// Static constant value representing an empty array
        /// </summary>
        public override OperandRole[] OperandRoles => new OperandRole[]
            {
                OperandRole.SINGLE_ATOMIC
            };
        /// <summary>
        /// Static constant value representing an empty array
        /// </summary>
        public SimpleArrayItem(IList<IGroundedValue> members)
        {
            this._members = members;
        }

        /// <summary>
        /// Static constant value representing an empty array
        /// </summary>
        public static SimpleArrayItem MakeSimpleArrayItem(ISequenceIterator input)
        {
            IList<IGroundedValue> members = input is Regex.SingleCharTokenIterator tok
                ? tok.DrainRemaining()               // exact-size fast path for array{tokenize(...)}
                : Collect(input);
            SimpleArrayItem result = new SimpleArrayItem(members);
            result.knownToBeGrounded = true;
            return result;
        }

        // 64KB of refs per chunk: sub-LOH, so a large member sequence avoids the List doubling
        // ladder (repeated large-object allocations + copies, each a full-GC trigger candidate).
        private const int COLLECT_CHUNK = 8192;

        private static IList<IGroundedValue> Collect(ISequenceIterator input)
        {
            List<IGroundedValue> head = new List<IGroundedValue>();
            IItem item;
            while (head.Count < COLLECT_CHUNK && (item = input.Next()) != null)
            {
                head.Add(item);
            }

            if (head.Count < COLLECT_CHUNK)
            {
                return head;
            }

            // Large: accumulate fixed chunks, then assemble ONCE at exact size.
            List<IGroundedValue[]> chunks = new List<IGroundedValue[]>();
            IGroundedValue[] cur = new IGroundedValue[COLLECT_CHUNK];
            int used = 0;
            while ((item = input.Next()) != null)
            {
                if (used == COLLECT_CHUNK)
                {
                    chunks.Add(cur);
                    cur = new IGroundedValue[COLLECT_CHUNK];
                    used = 0;
                }

                cur[used++] = item;
            }

            List<IGroundedValue> all = new List<IGroundedValue>(head.Count + chunks.Count * COLLECT_CHUNK + used);
            all.AddRange(head);
            foreach (IGroundedValue[] c in chunks)
            {
                all.AddRange(c);
            }

            IGroundedValue[] tail = new IGroundedValue[used];
            Array.Copy(cur, tail, used);
            all.AddRange(tail);
            return all;
        }

        /// <summary>
        /// Static constant value representing an empty array
        /// </summary>
        public virtual void RequestNotification(IPingable informee)
        {
            this.conversionPingable = informee;
        }

        /// <summary>
        /// Notify conversion to an ImmutableArrayItem
        /// </summary>
        public virtual void NotifyConversion()
        {
            if (conversionPingable != null)
            {
                conversionPingable.Ping();
            }
        }

        /// <summary>
        /// Notify conversion to an ImmutableArrayItem
        /// </summary>
        public virtual void MakeGrounded()
        {
            if (!knownToBeGrounded)
            {
                lock (this)
                {
                    for (int i = 0; i < _members.Count; i++)
                    {
                        _members[i] = ((ISequence)_members[i]).Materialize();
                    }

                    knownToBeGrounded = true;
                }
            }
        }

        /// <summary>
        /// Notify conversion to an ImmutableArrayItem
        /// </summary>
        public override AnnotationList GetAnnotations()
        {
            return AnnotationList.EMPTY;
        }

        /// <summary>
        /// Notify conversion to an ImmutableArrayItem
        /// </summary>
        public override IGroundedValue Get(int index)
        {
            return _members[index];
        }

        /// <summary>
        /// Notify conversion to an ImmutableArrayItem
        /// </summary>
        public override ArrayItem Put(int index, IGroundedValue newValue)
        {
            NotifyConversion();
            ImmutableArrayItem a2 = new ImmutableArrayItem(this);
            return a2.Put(index, newValue);
        }

        /// <summary>
        /// Notify conversion to an ImmutableArrayItem
        /// </summary>
        public override int ArrayLength()
        {
            return _members.Count;
        }

        /// <summary>
        /// Notify conversion to an ImmutableArrayItem
        /// </summary>
        public override bool IsEmpty()
        {
            return _members.IsEmpty();
        }

        /// <summary>
        /// Notify conversion to an ImmutableArrayItem
        /// </summary>
        public override IEnumerable<IGroundedValue> Members()
        {
            return _members;
        }

        /// <summary>
        /// Notify conversion to an ImmutableArrayItem
        /// </summary>
        public override ISequenceIterator Parcels()
        {
            return (ISequenceIterator)(new SequenceIteratorOverJavaIterator<IGroundedValue>(_members.IIterator(), (member) => new Parcel(member)));
        }

        /// <summary>
        /// Notify conversion to an ImmutableArrayItem
        /// </summary>
        public override ArrayItem RemoveSeveral(IntSet positions)
        {
            NotifyConversion();
            ImmutableArrayItem a2 = new ImmutableArrayItem(this);
            return a2.RemoveSeveral(positions);
        }

        /// <summary>
        /// Notify conversion to an ImmutableArrayItem
        /// </summary>
        public override ArrayItem Remove(int pos)
        {
            NotifyConversion();
            ImmutableArrayItem a2 = new ImmutableArrayItem(this);
            return a2.Remove(pos);
        }

        /// <summary>
        /// Notify conversion to an ImmutableArrayItem
        /// </summary>
        public override ArrayItem SubArray(int start, int end)
        {
            return new SimpleArrayItem(_members.SubList(start, end));
        }

        /// <summary>
        /// Notify conversion to an ImmutableArrayItem
        /// </summary>
        public override ArrayItem Insert(int position, IGroundedValue member)
        {
            NotifyConversion();
            ImmutableArrayItem a2 = new ImmutableArrayItem(this);
            return a2.Insert(position, member);
        }

        /// <summary>
        /// Notify conversion to an ImmutableArrayItem
        /// </summary>
        public override ArrayItem Append(IGroundedValue newMember)
        {
            NotifyConversion();
            ImmutableArrayItem a2 = new ImmutableArrayItem(this);
            return a2.Append(newMember);
        }

        /// <summary>
        /// Notify conversion to an ImmutableArrayItem
        /// </summary>
        public override ArrayItem Concat(ArrayItem other)
        {
            NotifyConversion();
            ImmutableArrayItem a2 = new ImmutableArrayItem(this);
            return a2.Concat(other);
        }

        /// <summary>
        /// Notify conversion to an ImmutableArrayItem
        /// </summary>
        public virtual IList<IGroundedValue> GetMembers()
        {
            return _members;
        }

        /// <summary>
        /// Notify conversion to an ImmutableArrayItem
        /// </summary>
        public override string ToShortString()
        {
            int size = GetMembers().Count;
            if (size == 0)
            {
                return "[]";
            }
            else if (size > 5)
            {
                return "[(:size " + size + ":)]";
            }
            else
            {
                StringBuilder buff = new StringBuilder(256);
                buff.Append("[");
                foreach (IGroundedValue entry in Members())
                {
                    buff.Append(Err.DepictSequence(entry).ToString().Trim());
                    buff.Append(", ");
                }

                if (size == 1)
                {
                    buff.Append("]");
                }
                else
                {
                    buff.SetCharAt(buff.Length - 2, ']');
                }

                return buff.ToString().Trim();
            }
        }
    }
}
