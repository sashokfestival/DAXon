////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//
// Runtime 2026-06-10: HAND-PORTED from upstream/saxon12-9-src/net/sf/saxon/expr/instruct/WithParam.java (338
// lines). The JavaToCSharp converter CRASHES on this file (ArgumentException: identifier — a `params`-named
// Java parameter), so poc/output/full has only WithParam.error and the class previously existed solely as a
// HOLLOW compat stub (JavaInternals.cs, namespace OutSmart.DAXon.Transformation): GetSelectValue=>null, GetSlotNumber=>0,
// all statics no-ops. Consequences: xsl:next-iteration set slots to null (NRE in SetLocalVariable via
// MakeRepeatable), and every xsl:with-param value for apply-templates/call-template silently evaporated.
// This is a faithful 1:1 port; member shapes match the transpiled call sites exactly (verified by grep:
// SetSelectExpression(parent,select), Copy(parent,params,rebindings), ExportParameters(params,out,tunnel),
// TypeCheck(params,visitor,info), Optimize(visitor,params,info), GatherOperands(parent,params,list),
// GetSelectValue(context), Get/SetSlotNumber, Get/SetVariableQName, Is/SetTypeChecked, GetRequiredType).

using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Values;
using System.Collections.Generic;

namespace OutSmart.DAXon.Expressions.Instructions
{
    /// <summary>
    /// An object derived from a xsl:with-param element in the stylesheet.
    /// </summary>
    public class WithParam
    {
        public static WithParam[] EMPTY_ARRAY = new WithParam[0];

        private Operand selectOp;
        private bool typeChecked = false;
        private int slotNumber = -1;
        private SequenceType requiredType;
        private StructuredQName variableQName;
        private ISequenceEvaluator evaluator = null;

        public virtual Operand SelectOperand => selectOp;

        public virtual SequenceType RequiredType
        {
            get => requiredType; set
            {
                requiredType = value;
            }
        }

        public virtual int SlotNumber
        {
            get => slotNumber; set
            {
                slotNumber = value;
            }
        }

        public virtual StructuredQName VariableQName
        {
            get => variableQName; set
            {
                variableQName = value;
            }
        }

        public virtual int InstructionNameCode => StandardNames.XSL_WITH_PARAM;

        public WithParam()
        {
        }

        public virtual void SetSelectExpression(Expression parent, Expression select)
        {
            selectOp = new Operand(parent, select, OperandRole.NAVIGATE);
        }

        public virtual Expression GetSelectExpression()
        {
            return selectOp.GetChildExpression();
        }

        public virtual void SetTypeChecked(bool @checked)
        {
            typeChecked = @checked;
        }

        public static void Simplify(WithParam[] @params)
        {
            if (@params != null)
            {
                foreach (WithParam param in @params)
                {
                    param.selectOp.SetChildExpression(param.selectOp.GetChildExpression().Simplify());
                }
            }
        }

        public static void TypeCheck(WithParam[] @params, ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            if (@params != null)
            {
                foreach (WithParam param in @params)
                {
                    param.selectOp.TypeCheck(visitor, contextItemType);
                }
            }
        }

        public static void Optimize(ExpressionVisitor visitor, WithParam[] @params, ContextItemStaticInfo contextItemType)
        {
            if (@params != null)
            {
                foreach (WithParam param in @params)
                {
                    param.selectOp.Optimize(visitor, contextItemType);
                }
            }
        }

        public virtual ISequenceEvaluator GetEvaluator()
        {
            if (evaluator == null)
            {
                MakeEvaluator();
            }

            return evaluator;
        }

        private void MakeEvaluator()
        {
            Expression select = selectOp.GetChildExpression();
            evaluator = new LearningEvaluator(select, select.MakeElaborator().Lazily(true, false));
        }

        public static WithParam[] Copy(Expression parent, WithParam[] @params, RebindingMap rebindings)
        {
            if (@params == null)
            {
                return null;
            }

            WithParam[] result = new WithParam[@params.Length];
            for (int i = 0; i < @params.Length; i++)
            {
                result[i] = new WithParam();
                result[i].slotNumber = @params[i].slotNumber;
                result[i].typeChecked = @params[i].typeChecked;
                result[i].selectOp = new Operand(parent, @params[i].selectOp.GetChildExpression().Copy(rebindings), OperandRole.NAVIGATE);
                result[i].requiredType = @params[i].requiredType;
                result[i].variableQName = @params[i].variableQName;
            }

            return result;
        }

        public static void GatherOperands(Expression parent, WithParam[] @params, IList<Operand> list)
        {
            if (@params != null)
            {
                foreach (WithParam param in @params)
                {
                    list.Add(param.selectOp);
                }
            }
        }

        public static void ExportParameters(WithParam[] @params, ExpressionPresenter @out, bool tunnel)
        {
            if (@params != null)
            {
                foreach (WithParam param in @params)
                {
                    @out.StartElement("withParam");
                    @out.EmitAttribute("name", param.variableQName);
                    string flags = "";
                    if (tunnel)
                    {
                        flags += "t";
                    }

                    if (param.IsTypeChecked())
                    {
                        flags += "c";
                    }

                    if (!(flags.Length == 0))
                    {
                        @out.EmitAttribute("flags", flags);
                    }

                    if (param.RequiredType != SequenceType.ANY_SEQUENCE)
                    {
                        @out.EmitAttribute("as", param.RequiredType.ToAlphaCode());
                    }

                    if (param.SlotNumber != -1)
                    {
                        @out.EmitAttribute("slot", param.SlotNumber + "");
                    }

                    param.selectOp.GetChildExpression().Export(@out);
                    @out.EndElement();
                }
            }
        }

        public virtual ISequence GetSelectValue(IXPathContext context)
        {
            // There is a select attribute: do a lazy evaluation of the expression,
            // which will already contain any code to force conversion to the required type.
            if (evaluator == null)
            {
                MakeEvaluator();
            }

            int savedOutputState = context.TemporaryOutputState;
            context.TemporaryOutputState = StandardNames.XSL_WITH_PARAM;
            ISequence result = evaluator.Evaluate(context);
            context.TemporaryOutputState = savedOutputState;
            return result;
        }

        public virtual bool IsTypeChecked()
        {
            return typeChecked;
        }
    }
}
