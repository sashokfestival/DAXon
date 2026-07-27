////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Api.Streams;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Internal.Collections;
using OutSmart.DAXon.Internal.Streams;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Api
{
    public class XdmSequenceIterator<T> : IEnumerator<T>
    {
        private readonly ILookaheadIterator @base;
        private bool closed = false;
        public T Current => default;
        object System.Collections.IEnumerator.Current => null;
        public XdmSequenceIterator(ISequenceIterator @base)
        {
            try
            {
                this.@base = LookaheadIteratorImpl.MakeLookaheadIterator(@base);
            }
            catch (UncheckedXPathException uxe)
            {
                throw new DAXonApiUncheckedException(uxe.GetXPathException());
            }
            catch (XPathException xe)
            {
                throw new DAXonApiUncheckedException(xe);
            }
        }

        public static XdmSequenceIterator<XdmNode> OfNodes(IAxisIterator @base)
        {
            return new XdmSequenceIterator<XdmNode>(@base);
        }

        public static XdmSequenceIterator<XdmAtomicValue> OfAtomicValues(ISequenceIterator @base)
        {
            return new XdmSequenceIterator<XdmAtomicValue>(@base);
        }

        protected static XdmSequenceIterator<XdmNode> OfNode(XdmNode node)
        {
            return new XdmSequenceIterator<XdmNode>(SingletonIterator.MakeIterator(node.UnderlyingNode));
        }

        public virtual bool HasNext()
        {
            return !closed && @base.HasNext;
        }

        public virtual T Next()
        {
            try
            {
                IItem it = @base.Next();
                if (it == null)
                {
                    throw new InvalidOperationException();
                }
                else
                {
                    return (T)(object)XdmItem.WrapItem(it);
                }
            }
            catch (UncheckedXPathException e)
            {
                throw new DAXonApiUncheckedException(e.GetXPathException());
            }
        }

        public virtual void Remove()
        {
            throw new NotSupportedException();
        }

        public virtual void Dispose()
        {
            closed = true;
            @base.Dispose();
        }

        public virtual XdmStream<T> Stream()
        {
            Stream<T> @base = StreamSupport.Stream<T>(Spliterators.SpliteratorUnknownSize<T>(this, Spliterator.ORDERED), false);
            @base = @base.OnClose(() => this.Dispose());
            return new XdmStream<T>(@base);
        }
        bool System.Collections.IEnumerator.MoveNext() => false;
        void System.Collections.IEnumerator.Reset() { }
    }
}