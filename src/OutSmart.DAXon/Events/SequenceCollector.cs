////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Events
{
    public sealed class SequenceCollector : SequenceWriter
    {
        private IList<IItem> list;

        /// <summary>
        /// Method to be supplied by subclasses: output one item in the sequence.
        /// </summary>
        public IGroundedValue Sequence
        {
            get
            {
                switch (list.Count)
                {
                    case 0:
                        return EmptySequence.GetInstance();
                    case 1:
                        return list[0];
                    default:
                        // Was `(IGroundedValue)new ListIterator.Of<IItem>(list)` — a ListIterator is an ITERATOR,
                        // not a grounded value, so the cast threw InvalidCastException (fn:transform ?output raw
                        // sequence, e.g. fn-transform-64/84). SequenceExtent IS the grounded collection.
                        return new SequenceExtent.Of<IItem>(list);
                }
            }
        }

        /// <summary>
        /// Method to be supplied by subclasses: output one item in the sequence.
        /// </summary>
        public IList<IItem> List => list;

        /// <summary>
        /// Method to be supplied by subclasses: output one item in the sequence.
        /// </summary>
        public IItem FirstItem
        {
            get
            {
                if (list.IsEmpty())
                {
                    return null;
                }
                else
                {
                    return list[0];
                }
            }
        }
        public SequenceCollector(PipelineConfiguration pipe) : this(pipe, 20)
        {
        }

        public SequenceCollector(PipelineConfiguration pipe, int estimatedSize) : base(pipe)
        {
            this.list = new List<IItem>(estimatedSize);
        }

        /// <summary>
        /// Clear the contents of the SequenceCollector and make it available for reuse
        /// </summary>
        public void Reset()
        {
            list = new List<IItem>(System.Math.Min(list.Count + 10, 50));
        }

        /// <summary>
        /// Method to be supplied by subclasses: output one item in the sequence.
        /// </summary>
        public override void Write(IItem item)
        {
            list.Add(item);
        }

        /// <summary>
        /// Method to be supplied by subclasses: output one item in the sequence.
        /// </summary>
        public ISequenceIterator Iterate()
        {
            if (list.IsEmpty())
            {
                return EmptyIterator.GetInstance();
            }
            else
            {
                return new ListIterator.Of<IItem>(list);
            }
        }
    }
}
