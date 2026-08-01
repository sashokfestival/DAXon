////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// fn:string-join(string* $sequence, string $separator)
    /// </summary>
    public class StringJoin : FoldingFunction, IPushableFunction
    {
        private bool returnEmptyIfEmpty;

        public static Func<StringJoin> New() => () => new StringJoin();
        public virtual void SetReturnEmptyIfEmpty(bool option)
        {
            returnEmptyIfEmpty = option;
        }

        public virtual bool IsReturnEmptyIfEmpty()
        {
            return returnEmptyIfEmpty;
        }

        /// <summary>
        /// Determine the cardinality of the function.
        /// </summary>
        public override int GetCardinality(Expression[] arguments)
        {
            if (returnEmptyIfEmpty)
            {
                return StaticProperty.ALLOWS_ZERO_OR_ONE;
            }
            else
            {
                return StaticProperty.EXACTLY_ONE;
            }
        }

        public override bool Equals(object o)
        {
            return (o is StringJoin) && base.Equals(o) && returnEmptyIfEmpty == ((StringJoin)o).returnEmptyIfEmpty;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode() | (returnEmptyIfEmpty ? 0x05000000 : 0);
        }

        public override Expression MakeOptimizedFunctionCall(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo, params Expression[] arguments)
        {
            Expression e2 = base.MakeOptimizedFunctionCall(visitor, contextInfo, arguments);
            if (e2 != null)
            {
                return e2;
            }

            int card = arguments[0].GetCardinality();
            if (!Cardinality.AllowsMany(card))
            {
                if (Cardinality.AllowsZero(card) || arguments[0].GetItemType().GetPrimitiveItemType() != BuiltInAtomicType.STRING)
                {
                    if (returnEmptyIfEmpty)
                    {
                        return new CastExpression(arguments[0], BuiltInAtomicType.STRING, true);
                    }
                    else
                    {
                        return SystemFunction.MakeCall("string", GetRetainedStaticContext(), arguments[0]);
                    }
                }
                else
                {
                    return arguments[0];
                }
            }

            return null;
        }

        public override IFold GetFold(IXPathContext context, params ISequence[] additionalArguments)
        {
            UnicodeString separator = EmptyUnicodeString.GetInstance();
            if (additionalArguments.Length > 0)
            {
                separator = ((IGroundedValue)additionalArguments[0].Head()).UnicodeStringValue;
            }

            return new StringJoinFold(separator, returnEmptyIfEmpty);
        }

        public void Process(Outputter destination, IXPathContext context, ISequence[] arguments)
        {
            UnicodeString separator = arguments.Length > 1 ? ((IGroundedValue)arguments[1].Head()).UnicodeStringValue : EmptyUnicodeString.GetInstance();
            IUniStringConsumer output = destination.GetStringReceiver(false, Loc.NONE);
            output.Open();
            bool first = true;
            ISequenceIterator iter = arguments[0].Iterate();
            IItem it;
            try
            {
                while ((it = iter.Next()) != null)
                {
                    if (first)
                    {
                        first = false;
                    }
                    else
                    {
                        output.Accept(separator);
                    }

                    output.Accept(it.UnicodeStringValue);
                }
            }
            catch (UncheckedXPathException e)
            {
                throw e.GetXPathException();
            }

            output.Close();
        }

        private class StringJoinFold : IFold
        {
            private int position = 0;
            private readonly UnicodeString separator;
            private readonly UniStringCollector data;
            private readonly bool returnEmptyIfEmpty;
            public StringJoinFold(UnicodeString separator, bool returnEmptyIfEmpty)
            {
                this.separator = separator;
                // Byte (Latin1) collector, not an int[] UnicodeBuilder: a large join (string-join of
                // millions of tokens into one big string) held 4 bytes per codepoint plus a doubling
                // ladder; the collector keeps Latin1 on the byte path and switches to a char buffer only
                // on the first codepoint > 0xFF. Same string value, so byte-identical output.
                this.data = new UniStringCollector();
                this.returnEmptyIfEmpty = returnEmptyIfEmpty;
            }

            public virtual void ProcessItem(IItem item)
            {
                if (position == 0)
                {
                    data.Accept(item.UnicodeStringValue);
                    position = 1;
                }
                else
                {
                    data.Accept(separator).Accept(item.UnicodeStringValue);
                }
            }

            public virtual bool IsFinished()
            {
                return false;
            }

            public virtual ISequence Result()
            {
                if (position == 0 && returnEmptyIfEmpty)
                {
                    return EmptySequence.GetInstance();
                }
                else
                {
                    return new StringValue(data.ToUnicodeString());
                }
            }
        }
    }
}
