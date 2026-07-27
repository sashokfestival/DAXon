////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Collections.Trie;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Expressions.Instructions
{
    public class BreakInstr : Instruction, TailCallLoop.ITailCallInfo
    {

        /// <summary>
        /// Create the instruction
        /// </summary>
        public override int InstructionNameCode => StandardNames.XSL_BREAK;

        /// <summary>
        /// Create the instruction
        /// </summary>
        public override string ExpressionName => "xsl:break";
        /// <summary>
        /// Create the instruction
        /// </summary>
        public BreakInstr()
        {
        }

        /// <summary>
        /// Create the instruction
        /// </summary>
        public override IEnumerable<Operand> Operands()
        {
            return new List<Operand>();
        }

        /// <summary>
        /// Create the instruction
        /// </summary>
        public override Expression Copy(RebindingMap rebindings)
        {
            BreakInstr b2 = new BreakInstr();
            ExpressionTool.CopyLocationInfo(this, b2);
            return b2;
        }

        /// <summary>
        /// Create the instruction
        /// </summary>
        public override bool MayCreateNewNodes()
        {

            // this is a fiction, but it prevents the instruction being moved to a global variable,
            // which would be pointless and possibly harmful
            return true;
        }

        /// <summary>
        /// Create the instruction
        /// </summary>
        public override bool IsLiftable(bool forStreaming)
        {
            return false;
        }

        /// <summary>
        /// Create the instruction
        /// </summary>
        public virtual void MarkContext(IXPathContext context)
        {
            context.MajorContext.RequestTailCall(this, null);
        }

        /// <summary>
        /// Create the instruction
        /// </summary>
        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("break", this);
            @out.EndElement();
        }

        /// <summary>
        /// Create the instruction
        /// </summary>
        public override Elaborator GetElaborator()
        {
            return new BreakElaborator();
        }

        /// <summary>
        /// Create the instruction
        /// </summary>
        public class BreakElaborator : PushElaborator
        {
            public override IPushEvaluator ElaborateForPush()
            {
                BreakInstr expr = (BreakInstr)GetExpression();
                return (output, context) =>
                {
                    expr.MarkContext(context);
                    return null;
                };
            }

            public override IItemEvaluator ElaborateForItem()
            {
                BreakInstr expr = (BreakInstr)GetExpression();
                return (context) =>
                {
                    expr.MarkContext(context);
                    return null;
                };
            }

            public override IPullEvaluator ElaborateForPull()
            {
                BreakInstr expr = (BreakInstr)GetExpression();
                return (context) =>
                {
                    expr.MarkContext(context);
                    return EmptyIterator.GetInstance();
                };
            }
        }
    }
}
