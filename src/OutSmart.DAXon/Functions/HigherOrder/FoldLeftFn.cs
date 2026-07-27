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
    /// This class implements the function fn:fold-left(), which is a standard function in XPath 3.0
    /// </summary>
    public class FoldLeftFn : FoldingFunction
    {

        public static Func<FoldLeftFn> New() => () => new FoldLeftFn();
        public override IFold GetFold(IXPathContext context, params ISequence[] arguments)
        {
            ISequence arg0 = arguments[0];
            return new FoldLeftFold(context, arg0.Materialize(), (IFunctionItem)arguments[1].Head());
        }

        public override ItemType GetResultItemType(Expression[] args)
        {

            // IItem type of the result is the same as the result item type of the argument function
            ItemType functionArgType = args[2].GetItemType();
            if (functionArgType is AnyFunctionType)
            {

                // will always be true once the query has been successfully type-checked
                return ((AnyFunctionType)args[2].GetItemType()).ResultType.PrimaryType;
            }
            else
            {
                return AnyItemType.GetInstance();
            }
        }

        public class FoldLeftFold : IFold
        {
            private readonly IXPathContext context;
            private readonly IFunctionItem function;
            private readonly FusedArity2Caller fused;
            private ISequence data;
            private int counter;
            public FoldLeftFold(IXPathContext context, IGroundedValue zero, IFunctionItem function)
            {
                this.context = context;
                this.function = function;
                this.fused = FusedArity2Caller.TryMake(function, context);
                this.data = zero;
                this.counter = 0;
            }

            public virtual void ProcessItem(IItem item)
            {
                if (fused != null)
                {
                    // Reused-frame invoker; results come back materialized, so no memo wrapping.
                    data = fused.CallTwo(data, item);
                    return;
                }

                ISequence[] args = new ISequence[2];
                args[0] = data;
                args[1] = item;

                // The result can be returned as a LazySequence. Since we are passing it to a user-defined
                // function which can read it repeatedly, we need at the very least to wrap it in a MemoSequence.
                // But wrapping MemoSequences too deeply can cause a StackOverflow when the unwrapping finally
                // takes place; so to avoid this, we periodically ground the value as a real in-memory concrete
                // sequence. We don't want to do this every time because it involves allocating memory.
                ISequence result = DynamicCall(function, context, args);
                if (counter++ % 32 == 0)
                {
                    data = result.Materialize();
                }
                else
                {
                    data = result;
                }
            }

            public virtual bool IsFinished()
            {
                return false;
            }

            public virtual ISequence Result()
            {
                return data;
            }
        }
    }
}