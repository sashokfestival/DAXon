////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// Implements the XSLT function current-group()
    /// </summary>
    public class CurrentGroupCall : Expression, ICallable
    {
        private bool inHigherOrderOperand = false;
        private ItemType itemType = AnyItemType.GetInstance();
        private ForEachGroup controllingInstruction = null; // may be unknown, when current group has dynamic scope
        public override Expression ScopingExpression => ControllingInstruction;

        public virtual ForEachGroup ControllingInstruction
        {
            get
            {
                if (controllingInstruction == null)
                {
                    controllingInstruction = FindControllingInstruction(this);
                }

                return controllingInstruction;
            }
        }

        /// <summary>
        /// Determine the dependencies
        /// </summary>
        public override int IntrinsicDependencies => StaticProperty.DEPENDS_ON_CURRENT_GROUP;

        /// <summary>
        /// Determine the dependencies
        /// </summary>
        public override int ImplementationMethod => ITERATE_METHOD;

        /// <summary>
        /// Return an iteration over the result sequence
        /// </summary>
        public override string StreamerName => "CurrentGroup";

        public virtual void SetControllingInstruction(ForEachGroup instruction, ItemType itemType, bool isHigherOrder)
        {
            ResetLocalStaticProperties();
            this.controllingInstruction = instruction;
            this.inHigherOrderOperand = isHigherOrder;
            this.itemType = itemType;
        }

        public override void ResetLocalStaticProperties()
        {
            base.ResetLocalStaticProperties();
            this.controllingInstruction = null;
            this.itemType = AnyItemType.GetInstance();
        }

        public static ForEachGroup FindControllingInstruction(Expression exp)
        {
            Expression child = exp;
            Expression parent = exp.ParentExpression;
            while (parent != null)
            {
                if (parent is ForEachGroup && (child == ((ForEachGroup)parent).GetActionExpression() || child == ((ForEachGroup)parent).GetSortKeyDefinitionList()))
                {
                    return (ForEachGroup)parent;
                }

                child = parent;
                parent = parent.ParentExpression;
            }

            return null;
        }

        public virtual bool IsInHigherOrderOperand()
        {
            return inHigherOrderOperand;
        }

        /// <summary>
        /// Determine the item type of the value returned by the function
        /// </summary>
        public override ItemType GetItemType()
        {
            if (itemType == AnyItemType.GetInstance() && controllingInstruction != null)
            {
                itemType = controllingInstruction.GetSelectExpression().GetItemType();
            }

            return itemType;
        }

        /// <summary>
        /// Determine the dependencies
        /// </summary>
        protected override int ComputeCardinality()
        {
            return StaticProperty.ALLOWS_ZERO_OR_MORE;
        }

        /// <summary>
        /// Determine the dependencies
        /// </summary>
        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("currentGroup");
            @out.EndElement();
        }

        /// <summary>
        /// Determine the dependencies
        /// </summary>
        protected override int ComputeSpecialProperties()
        {
            if (ControllingInstruction == null)
            {
                return 0;
            }
            else
            {
                return controllingInstruction.GetSelectExpression().GetSpecialProperties();
            }
        }

        /// <summary>
        /// Determine the dependencies
        /// </summary>
        public override Expression Copy(RebindingMap rebindings)
        {
            CurrentGroupCall cg = new CurrentGroupCall();
            cg.inHigherOrderOperand = inHigherOrderOperand;
            cg.itemType = itemType;
            cg.controllingInstruction = controllingInstruction;
            return cg;
        }

        /// <summary>
        /// Return an iteration over the result sequence
        /// </summary>
        public override ISequenceIterator Iterate(IXPathContext context)
        {
            return MakeElaborator().ElaborateForPull().Iterate(context);
        }

        /// <summary>
        /// Return an iteration over the result sequence
        /// </summary>
        public ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            IGroupIterator gi = context.GetCurrentGroupIterator();
            if (gi == null)
            {
                throw new XPathException("There is no current group", "XTDE1061").WithLocation(GetLocation());
            }

            return gi.CurrentGroup();
        }

        /// <summary>
        /// Return an iteration over the result sequence
        /// </summary>
        public override string ToString()
        {
            return "current-group()";
        }

        /// <summary>
        /// Return an iteration over the result sequence
        /// </summary>
        public override string ToShortString()
        {
            return ToString();
        }

        /// <summary>
        /// Return an iteration over the result sequence
        /// </summary>
        public override Elaborator GetElaborator()
        {
            return new CurrentGroupCallElaborator();
        }

        /// <summary>
        /// Return an iteration over the result sequence
        /// </summary>
        private class CurrentGroupCallElaborator : PullElaborator
        {
            public override IPullEvaluator ElaborateForPull()
            {
                CurrentGroupCall expr = (CurrentGroupCall)GetExpression();
                return (context) =>
                {
                    IGroupIterator gi = context.GetCurrentGroupIterator();
                    if (gi == null)
                    {
                        throw new XPathException("There is no current group", "XTDE1061").WithLocation(expr.GetLocation());
                    }

                    return gi.CurrentGroup().Iterate();
                };
            }
        }
    }
}
