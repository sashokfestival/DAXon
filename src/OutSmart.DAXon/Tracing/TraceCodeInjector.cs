////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Flwor;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Transformation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Tracing
{
    public class TraceCodeInjector : ICodeInjector
    {
        protected int traceLevel = TraceLevel.NORMAL;
        public virtual void SetTraceLevel(int traceLevel)
        {
            this.traceLevel = traceLevel;
        }

        public static int LevelValue(string level)
        {
            switch (level)
            {
                case "none":
                    return TraceLevel.NONE;
                case "low":
                    return TraceLevel.LOW;
                case "normal":
                    return TraceLevel.NORMAL;
                case "high":
                    return TraceLevel.HIGH;
                default:
                    throw new ArgumentException("Trace level " + level);
            }
        }

        public virtual Expression Inject(Expression exp)
        {
            if (exp is FLWORExpression)
            {
                ((FLWORExpression)exp).InjectCode(this);
                return exp;
            }
            else if (!(exp is TraceExpression) && IsApplicable(exp))
            {
                return new TraceExpression(exp);
            }
            else
            {
                return exp;
            }
        }

        protected virtual bool IsApplicable(Expression exp)
        {
            return false;
        }

        public virtual void Process(ITraceableComponent component)
        {
            if (!(component.GetBody() is ComponentTracer))
            {
                Expression newBody = ExpressionTool.InjectCode(component.GetBody(), this);
                component.SetBody(newBody);
                ComponentTracer trace = new ComponentTracer(component);
                component.SetBody(trace);
            }
        }

        public virtual Clause InjectClause(FLWORExpression expression, Clause clause)
        {
            try
            {
                clause.ProcessOperands((operand) => operand.SetChildExpression(ExpressionTool.InjectCode(operand.GetChildExpression(), this)));
            }
            catch (XPathException e)
            {
                throw new UncheckedXPathException(e);
            }

            return new TraceClause(expression, clause);
        }
    }
}