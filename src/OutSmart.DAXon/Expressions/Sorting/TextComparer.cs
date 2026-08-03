////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Text;
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
    internal class TextComparer : IAtomicComparer
    {
        private readonly IAtomicComparer baseComparer;

        public virtual IAtomicComparer BaseComparer => baseComparer;

        public virtual IStringCollator Collator => baseComparer.Collator;
        public TextComparer(IAtomicComparer baseComparer)
        {
            this.baseComparer = baseComparer;
        }

        public virtual IAtomicComparer ProvideContext(IXPathContext context)
        {
            IAtomicComparer newBase = baseComparer.ProvideContext(context);
            if (newBase != baseComparer)
            {
                return new TextComparer(newBase);
            }
            else
            {
                return this;
            }
        }

        public virtual int CompareAtomicValues(AtomicValue a, AtomicValue b)
        {
            return baseComparer.CompareAtomicValues(ToStringValue(a), ToStringValue(b));
        }

        private StringValue ToStringValue(AtomicValue a)
        {
            return new StringValue(a == null ? (UnicodeString)EmptyUnicodeString.GetInstance() : a.UnicodeStringValue);
        }

        public virtual bool ComparesEqual(AtomicValue a, AtomicValue b)
        {
            return CompareAtomicValues(a, b) == 0;
        }

        public virtual string Save()
        {
            return "TEXT|" + baseComparer.Save();
        }
    }
}