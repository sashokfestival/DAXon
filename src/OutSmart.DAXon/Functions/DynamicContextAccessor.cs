////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
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
    public abstract class DynamicContextAccessor : SystemFunction
    {
        private AtomicValue boundValue;
        public virtual void BindContext(IXPathContext context)
        {
            boundValue = Evaluate(context);
        }

        public abstract AtomicValue Evaluate(IXPathContext context);
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            if (boundValue != null)
            {
                return boundValue;
            }
            else
            {
                return Evaluate(context);
            }
        }

        public override Expression MakeFunctionCall(Expression[] arguments)
        {
            return new AnonymousSystemFunctionCall(this, arguments);
        }

        private sealed class AnonymousSystemFunctionCall : SystemFunctionCall
        {

            private readonly DynamicContextAccessor parent;

            public override int IntrinsicDependencies => StaticProperty.DEPENDS_ON_RUNTIME_ENVIRONMENT;
            public AnonymousSystemFunctionCall(DynamicContextAccessor parent, Expression[] arguments) : base(parent, arguments)
            {
                this.parent = parent;
            }
            public override IItem EvaluateItem(IXPathContext context)
            {

                // Cut some of the call overhead
                return parent.Evaluate(context);
            }
        }

        public class ImplicitTimezone : DynamicContextAccessor
        {
            public override AtomicValue Evaluate(IXPathContext context)
            {
                DateTimeValue now = DateTimeValue.GetCurrentDateTime(context);
                return now.GetComponent(AccessorFn.Component.TIMEZONE);
            }
        }

        public class CurrentDateTime : DynamicContextAccessor
        {
            public override AtomicValue Evaluate(IXPathContext context)
            {
                return DateTimeValue.GetCurrentDateTime(context);
            }
        }

        public class CurrentDate : DynamicContextAccessor
        {
            public override AtomicValue Evaluate(IXPathContext context)
            {
                DateTimeValue now = DateTimeValue.GetCurrentDateTime(context);
                return now.ToDateValue();
            }
        }

        public class CurrentTime : DynamicContextAccessor
        {
            public override AtomicValue Evaluate(IXPathContext context)
            {
                DateTimeValue now = DateTimeValue.GetCurrentDateTime(context);
                return now.ToTimeValue();
            }
        }

        public class DefaultLanguage : DynamicContextAccessor
        {

            public static Func<DefaultLanguage> New() => () => new DefaultLanguage();
            public override AtomicValue Evaluate(IXPathContext context)
            {
                string lang = context.GetConfiguration().GetDefaultLanguage();
                return new StringValue(lang, BuiltInAtomicType.LANGUAGE);
            }
        }
    }
}
