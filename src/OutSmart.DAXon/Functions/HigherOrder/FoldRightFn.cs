////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2020 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Functions.HigherOrder
{
    /// <summary>
    /// This class implements the function fn:fold-right(), which is a standard function in XQuery 1.1
    /// </summary>
    internal class FoldRightFn : SystemFunction
    {

        public static Func<FoldRightFn> New() => () => new FoldRightFn();
        public override ItemType GetResultItemType(Expression[] args)
        {

            // IItem type of the result is the same as the result item type of the function
            ItemType functionArgType = args[2].GetItemType();
            if (functionArgType is AnyFunctionType)
            {

                // will always be true once the query has been successfully type-checked
                return ((AnyFunctionType)functionArgType).ResultType.PrimaryType;
            }
            else
            {
                return AnyItemType.GetInstance();
            }
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            return EvalFoldRight((IFunctionItem)arguments[2].Head(), arguments[1].Materialize(), arguments[0].Iterate(), context);
        }

        private ISequence EvalFoldRight(IFunctionItem function, ISequence zero, ISequenceIterator @base, IXPathContext context)
        {
            ISequenceIterator reverseBase = Reverse.GetReverseIterator(@base);
            ISequence[] args = new ISequence[2];
            IItem item;
            while ((item = reverseBase.Next()) != null)
            {
                args[0] = item;
                args[1] = zero.Materialize();
                try
                {
                    zero = DynamicCall(function, context, args);
                }
                catch (XPathException e)
                {
                    e.MaybeSetContext(context);
                    throw e;
                }
            }

            return zero;
        }
    }
}
