////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions.Sorting
{
    /// <summary>
    /// A comparer specifically for comparing two date, time, or dateTime values
    /// </summary>
    internal class CalendarValueComparer : IAtomicComparer
    {
        private readonly IXPathContext context;

        public virtual IStringCollator Collator => null;
        public CalendarValueComparer(IXPathContext context)
        {
            this.context = context;
        }

        public virtual IAtomicComparer ProvideContext(IXPathContext context)
        {
            return new CalendarValueComparer(context);
        }

        public virtual int CompareAtomicValues(AtomicValue a, AtomicValue b)
        {
            if (a == null)
            {
                return b == null ? 0 : -1;
            }
            else if (b == null)
            {
                return +1;
            }

            return ((CalendarValue)a).CompareTo((CalendarValue)b, context.GetImplicitTimezone());
        }

        public virtual bool ComparesEqual(AtomicValue a, AtomicValue b)
        {
            return CompareAtomicValues(a, b) == 0;
        }

        public virtual string Save()
        {
            return "CalVC";
        }
    }
}