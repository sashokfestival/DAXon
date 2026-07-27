////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions
{
    public sealed class TailCallLoop : UnaryExpression
    {
        UserFunction containingFunction;

        public UserFunction ContainingFunction => containingFunction;

        /// <summary>
        /// Type-check the expression
        /// </summary>
        public override int ImplementationMethod => BaseExpression.ImplementationMethod;

        /// <summary>
        /// Type-check the expression
        /// </summary>
        /*TailCallLoop e2 = new TailCallLoop(containingFunction);
        e2.setBaseExpression(getBaseExpression().copy());
        return e2;*/
        /// <summary>
        /// Iterate over the sequence of values
        /// </summary>
        /// <summary>
        /// Evaluate as an IItem.
        /// </summary>
        /// <summary>
        /// Determine the data type of the items returned by the expression
        /// </summary>
        public override string ExpressionName => "tailCallLoop";
        public TailCallLoop(UserFunction function, Expression body) : base(body)
        {
            containingFunction = function;
        }

        /// <summary>
        /// Type-check the expression
        /// </summary>
        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            GetOperand().TypeCheck(visitor, contextInfo);
            return this;
        }

        /// <summary>
        /// Type-check the expression
        /// </summary>
        protected override OperandRole GetOperandRole()
        {
            return OperandRole.SAME_FOCUS_ACTION;
        }

        /// <summary>
        /// Type-check the expression
        /// </summary>
        public override Expression Copy(RebindingMap rebindings)
        {
            throw new NotSupportedException("TailCallLoop.copy()"); /*TailCallLoop e2 = new TailCallLoop(containingFunction);
        e2.setBaseExpression(getBaseExpression().copy());
        return e2;*/
        }

        /// <summary>
        /// Type-check the expression
        /// </summary>
        /*TailCallLoop e2 = new TailCallLoop(containingFunction);
        e2.setBaseExpression(getBaseExpression().copy());
        return e2;*/
        /// <summary>
        /// Iterate over the sequence of values
        /// </summary>
        public override ISequenceIterator Iterate(IXPathContext context)
        {
            return MakeElaborator().ElaborateForPull().Iterate(context);
        }

        /// <summary>
        /// Type-check the expression
        /// </summary>
        /*TailCallLoop e2 = new TailCallLoop(containingFunction);
        e2.setBaseExpression(getBaseExpression().copy());
        return e2;*/
        /// <summary>
        /// Iterate over the sequence of values
        /// </summary>
        /// <summary>
        /// Evaluate as an IItem.
        /// </summary>
        public override IItem EvaluateItem(IXPathContext context)
        {
            return MakeElaborator().ElaborateForItem().Eval(context);
        }

        /// <summary>
        /// Type-check the expression
        /// </summary>
        /*TailCallLoop e2 = new TailCallLoop(containingFunction);
        e2.setBaseExpression(getBaseExpression().copy());
        return e2;*/
        /// <summary>
        /// Iterate over the sequence of values
        /// </summary>
        /// <summary>
        /// Evaluate as an IItem.
        /// </summary>
        private UserFunction EstablishTargetFunction(ITailCallInfo tail, XPathContextMajor cm)
        {
            if (tail is TailCallFunction)
            {
                return ((TailCallFunction)tail).function;
            }
            else if (tail is TailCallComponent)
            {
                Component targetComponent = ((TailCallComponent)tail).component;
                cm.SetCurrentComponent(targetComponent);
                return (UserFunction)targetComponent.GetActor();
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        /// <summary>
        /// Type-check the expression
        /// </summary>
        /*TailCallLoop e2 = new TailCallLoop(containingFunction);
        e2.setBaseExpression(getBaseExpression().copy());
        return e2;*/
        /// <summary>
        /// Iterate over the sequence of values
        /// </summary>
        /// <summary>
        /// Evaluate as an IItem.
        /// </summary>
        public override void Process(Outputter output, IXPathContext context)
        {
            ITailCall tc = MakeElaborator().ElaborateForPush().ProcessLeavingTail(output, context);
        }

        /// <summary>
        /// Type-check the expression
        /// </summary>
        /*TailCallLoop e2 = new TailCallLoop(containingFunction);
        e2.setBaseExpression(getBaseExpression().copy());
        return e2;*/
        /// <summary>
        /// Iterate over the sequence of values
        /// </summary>
        /// <summary>
        /// Evaluate as an IItem.
        /// </summary>
        private ISequence TailCallDifferentFunction(UserFunction userFunction, XPathContextMajor cm)
        {
            cm.ResetStackFrameMap(userFunction.GetStackFrameMap(), userFunction.GetArity());
            try
            {
                return userFunction.BodyEvaluator.Evaluate(cm);
            }
            catch (XPathException err)
            {
                throw err.MaybeWithLocation(GetLocation()).MaybeWithContext(cm);
            }
        }

        /// <summary>
        /// Type-check the expression
        /// </summary>
        /*TailCallLoop e2 = new TailCallLoop(containingFunction);
        e2.setBaseExpression(getBaseExpression().copy());
        return e2;*/
        /// <summary>
        /// Iterate over the sequence of values
        /// </summary>
        /// <summary>
        /// Evaluate as an IItem.
        /// </summary>
        /// <summary>
        /// Determine the data type of the items returned by the expression
        /// </summary>
        public override ItemType GetItemType()
        {
            return BaseExpression.GetItemType();
        }

        /// <summary>
        /// Type-check the expression
        /// </summary>
        /*TailCallLoop e2 = new TailCallLoop(containingFunction);
        e2.setBaseExpression(getBaseExpression().copy());
        return e2;*/
        /// <summary>
        /// Iterate over the sequence of values
        /// </summary>
        /// <summary>
        /// Evaluate as an IItem.
        /// </summary>
        /// <summary>
        /// Determine the data type of the items returned by the expression
        /// </summary>
        public override Elaborator GetElaborator()
        {
            return new TailCallLoopElaborator();
        }

        /// <summary>
        /// Type-check the expression
        /// </summary>
        /*TailCallLoop e2 = new TailCallLoop(containingFunction);
        e2.setBaseExpression(getBaseExpression().copy());
        return e2;*/
        /// <summary>
        /// Iterate over the sequence of values
        /// </summary>
        /// <summary>
        /// Evaluate as an IItem.
        /// </summary>
        /// <summary>
        /// Determine the data type of the items returned by the expression
        /// </summary>
        public interface ITailCallInfo
        {
        }

        /// <summary>
        /// Type-check the expression
        /// </summary>
        /*TailCallLoop e2 = new TailCallLoop(containingFunction);
        e2.setBaseExpression(getBaseExpression().copy());
        return e2;*/
        /// <summary>
        /// Iterate over the sequence of values
        /// </summary>
        /// <summary>
        /// Evaluate as an IItem.
        /// </summary>
        /// <summary>
        /// Determine the data type of the items returned by the expression
        /// </summary>
        public class TailCallComponent : ITailCallInfo
        {
            public Component component;
            public UserFunction function;
        }

        /// <summary>
        /// Type-check the expression
        /// </summary>
        /*TailCallLoop e2 = new TailCallLoop(containingFunction);
        e2.setBaseExpression(getBaseExpression().copy());
        return e2;*/
        /// <summary>
        /// Iterate over the sequence of values
        /// </summary>
        /// <summary>
        /// Evaluate as an IItem.
        /// </summary>
        /// <summary>
        /// Determine the data type of the items returned by the expression
        /// </summary>
        public class TailCallFunction : ITailCallInfo
        {
            public UserFunction function;
        }

        /// <summary>
        /// Type-check the expression
        /// </summary>
        /*TailCallLoop e2 = new TailCallLoop(containingFunction);
        e2.setBaseExpression(getBaseExpression().copy());
        return e2;*/
        /// <summary>
        /// Iterate over the sequence of values
        /// </summary>
        /// <summary>
        /// Evaluate as an IItem.
        /// </summary>
        /// <summary>
        /// Determine the data type of the items returned by the expression
        /// </summary>
        private class TailCallLoopElaborator : PullElaborator
        {
            public override IPullEvaluator ElaborateForPull()
            {
                TailCallLoop expr = (TailCallLoop)GetExpression();
                IPullEvaluator contentEval = expr.BaseExpression.MakeElaborator().ElaborateForPull();
                return (context) =>
                {
                    try
                    {
                        XPathContextMajor cm = (XPathContextMajor)context;
                        Controller controller = cm.GetController();
                        while (true)
                        {
                            controller.CheckTimeout();
                            ISequenceIterator iter = contentEval.Iterate(context);
                            IGroundedValue extent = SequenceTool.ToGroundedValue(iter);
                            ITailCallInfo tail = cm.TailCallInfo;
                            if (tail == null)
                            {
                                return extent.Iterate();
                            }
                            else
                            {
                                UserFunction target = expr.EstablishTargetFunction(tail, cm);
                                if (target != expr.containingFunction)
                                {
                                    return expr.TailCallDifferentFunction(target, cm).Iterate();
                                } // otherwise, loop round to execute the tail call
                            }
                        }
                    }
                    catch (UncheckedXPathException e)
                    {
                        throw e.GetXPathException().MaybeWithContext(context).MaybeWithLocation(expr.GetLocation());
                    }
                };
            }

            public override IPushEvaluator ElaborateForPush()
            {
                TailCallLoop expr = (TailCallLoop)GetExpression();
                IPushEvaluator contentPush = expr.BaseExpression.MakeElaborator().ElaborateForPush();
                return (output, context) =>
                {
                    XPathContextMajor cm = (XPathContextMajor)context;
                    Controller controller = cm.GetController();
                    while (true)
                    {
                        controller.CheckTimeout();
                        ITailCall tc = contentPush.ProcessLeavingTail(output, context);
                        ITailCallInfo tail = cm.TailCallInfo;
                        if (tail == null)
                        {
                            return null;
                        }
                        else
                        {
                            UserFunction target = expr.EstablishTargetFunction(tail, cm);
                            if (target != expr.containingFunction)
                            {
                                SequenceTool.Process(expr.TailCallDifferentFunction(target, cm), output, expr.GetLocation());
                                return null;
                            } // otherwise, loop round to execute the tail call
                        }
                    }
                };
            }

            public override IItemEvaluator ElaborateForItem()
            {
                TailCallLoop expr = (TailCallLoop)GetExpression();
                IItemEvaluator contentEval = expr.BaseExpression.MakeElaborator().ElaborateForItem();
                return (context) =>
                {
                    XPathContextMajor cm = (XPathContextMajor)context;
                    Controller controller = cm.GetController();
                    while (true)
                    {
                        controller.CheckTimeout();
                        IItem item = contentEval.Eval(context);
                        ITailCallInfo tail = cm.TailCallInfo;
                        if (tail == null)
                        {
                            return item;
                        }
                        else
                        {
                            UserFunction target = expr.EstablishTargetFunction(tail, cm);
                            if (target != expr.containingFunction)
                            {
                                return expr.TailCallDifferentFunction(target, cm).Head();
                            } // otherwise, loop round to execute the tail call
                        }
                    }
                };
            }
        }
    }
}