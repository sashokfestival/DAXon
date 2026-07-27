////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// The XPath 2.0 distinct-values() function, with the collation argument already known
    /// </summary>
    public class DistinctValues : CollatingFunctionFixed
    {
        /// <summary>
        /// A match key for use in situations where NaN = NaN
        /// </summary>
        public static readonly IAtomicMatchKey NaN_MATCH_KEY = new QNameValue("", NamespaceUri.SAXON, "+NaN+");
        /// <summary>
        /// A match key for use in situations where NaN = NaN
        /// </summary>
        public override string StreamerName => "DistinctValues";

        public static Func<DistinctValues> New() => () => new DistinctValues();

        /// <summary>
        /// A match key for use in situations where NaN = NaN
        /// </summary>
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            IStringCollator collator = StringCollator;
            return new LazySequence(new DistinctIterator(arguments[0].Iterate(), collator, context));
        }

        /// <summary>
        /// IIterator class to return the distinct values in a sequence
        /// </summary>
        public class DistinctIterator : ISequenceIterator
        {
            private readonly ISequenceIterator @base;
            private readonly IStringCollator collator;
            private readonly IXPathContext context;
            private readonly HashSet<IAtomicMatchKey> lookup = new HashSet<IAtomicMatchKey>(40);
            private IAction onDuplicates = null;
            public DistinctIterator(ISequenceIterator @base, IStringCollator collator, IXPathContext context)
            {
                this.@base = @base;
                this.collator = collator;
                this.context = context;
            }

            public virtual AtomicValue Next()
            {
                int implicitTimezone = context.GetImplicitTimezone();
                while (true)
                {
                    AtomicValue nextBase = (AtomicValue)@base.Next();
                    if (nextBase == null)
                    {
                        return null;
                    }

                    IAtomicMatchKey key;
                    if (nextBase.IsNaN())
                    {
                        key = NaN_MATCH_KEY;
                    }
                    else
                    {
                        try
                        {
                            key = nextBase.GetXPathMatchKey(collator, implicitTimezone);
                        }
                        catch (NoDynamicContextException e)
                        {
                            throw new UncheckedXPathException(e);
                        }
                    }

                    if (lookup.Add(key))
                    {

                        // returns true if newly added (if not, keep looking)
                        return nextBase;
                    }
                    else if (onDuplicates != null)
                    {
                        try
                        {
                            onDuplicates.DoAction();
                        }
                        catch (XPathException e)
                        {

                            // should not happen
                            throw new UncheckedXPathException(e);
                        }
                    }
                }
            }

            public virtual void Dispose()
            {
                @base.Dispose();
            }

            public virtual void NotifyDuplicates(IAction onDuplicates)
            {
                this.onDuplicates = onDuplicates;
            }
            IItem ISequenceIterator.Next() => Next(); // redirect StubGen hollow to the real covariant Next(); default = silent empty iteration
        }
    }
}
