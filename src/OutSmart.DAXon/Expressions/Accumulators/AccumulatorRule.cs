////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation.Rules;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal.Functional;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions.Accumulators
{
    /// <summary>
    /// This class represents one of the rules making up the definition of an accumulator
    /// </summary>
    public class AccumulatorRule : IRuleTarget, ITraceableComponent
    {
        private Expression newValueExpression;
        private readonly SlotManager stackFrameMap;
        private readonly bool postDescent;
        private bool capturing;
        private ILocation location;
        private StructuredQName accumulatorName;

        public virtual Expression NewValueExpression => newValueExpression;

        public virtual string TracingTag => "xsl:accumulator-rule";
        public AccumulatorRule(Expression newValueExpression, SlotManager stackFrameMap, bool postDescent)
        {
            this.newValueExpression = newValueExpression;
            this.stackFrameMap = stackFrameMap;
            this.postDescent = postDescent;
        }

        public virtual void Export(ExpressionPresenter @out)
        {
            newValueExpression.Export(@out);
        }

        public virtual SlotManager GetStackFrameMap()
        {
            return stackFrameMap;
        }

        public virtual void RegisterRule(Rule rule)
        {
        }

        public virtual void SetCapturing(bool capturing)
        {
            this.capturing = capturing;
        }

        public virtual bool IsCapturing()
        {
            return capturing;
        }

        public virtual bool IsPostDescent()
        {
            return postDescent;
        }

        // ITraceableComponent interface
        public virtual Expression GetBody()
        {
            return newValueExpression;
        }

        public virtual void SetLocation(ILocation loc)
        {
            this.location = loc;
        }

        public virtual ILocation GetLocation()
        {
            return location;
        }

        public virtual StructuredQName GetObjectName()
        {
            return null;
        }

        public virtual void SetBody(Expression expression)
        {
            newValueExpression = expression;
        }

        public virtual void SetAccumulatorName(StructuredQName name)
        {
            this.accumulatorName = name;
        }

        public virtual void GatherProperties(Action<string, object> consumer)
        {
            if (accumulatorName != null)
            {
                consumer.Accept("name", accumulatorName.DisplayName);
            }

            consumer.Accept("phase", IsPostDescent() ? "end" : "start");
        }
    }
}