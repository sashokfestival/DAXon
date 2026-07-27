////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
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
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// Implement the XPath string-length() function
    /// </summary>
    public class StringLength_1 : ScalarSystemFunction
    {
        public override IntegerValue[] IntegerBounds => new IntegerValue[]
            {
                Int64Value.ZERO,
                Expression.MAX_STRING_LENGTH
            };

        public static Func<StringLength_1> New() => () => new StringLength_1();

        public override ISequence ResultWhenEmpty()
        {
            return Int64Value.ZERO;
        }

        public override AtomicValue Evaluate(IItem arg, IXPathContext context)
        {
            if (arg is StringValue)
            {
                return Int64Value.MakeIntegerValue(((StringValue)arg).Length());
            }
            else
            {
                UnicodeString s;
                try
                {
                    s = arg.UnicodeStringValue;
                }
                catch (NotSupportedException e)
                {
                    throw new XPathException("Cannot get the string value of a function item", "FOTY0013");
                }

                return Int64Value.MakeIntegerValue(s.Length());
            }
        }

        public override Elaborator GetElaborator()
        {
            return new StringLengthFnElaborator();
        }

        public class StringLengthFnElaborator : ItemElaborator
        {
            public override IItemEvaluator ElaborateForItem()
            {
                SystemFunctionCall fnc = (SystemFunctionCall)GetExpression();
                Expression arg = fnc.GetArg(0);
                IUnicodeStringEvaluator argEval = arg.MakeElaborator().ElaborateForUnicodeString(true);
                return (context) =>
                {
                    UnicodeString str = argEval.Eval(context);
                    return Int64Value.MakeIntegerValue(str.Length());
                };
            }
        }
    }
}