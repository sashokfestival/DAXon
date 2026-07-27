////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Parsing;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using System.IO;
namespace OutSmart.DAXon.Expressions
{
    /// <summary>
    /// Information about a sub-expression and its relationship to the parent expression
    /// </summary>
    public sealed class Operand : IEnumerable<Operand>, IExpressionOwner
    {

        private static readonly bool DEBUG = false;
        private readonly Expression parentExpression;
        private Expression childExpression;
        private OperandRole role;

        public Expression ParentExpression => parentExpression;

        public OperandRole OperandRole { get => role; set => this.role = value; }

        public OperandUsage Usage
        {
            get => role.Usage; set
            {
                role = new OperandRole(role.properties, value, role.GetRequiredType());
            }
        }
        public Operand(Expression parentExpression, Expression childExpression, OperandRole role)
        {
            this.parentExpression = parentExpression;
            this.role = role;
            SetChildExpression(childExpression);
        }

        public Expression GetChildExpression()
        {
            return childExpression;
        }

        public void SetChildExpression(Expression childExpression)
        {
            if (childExpression != this.childExpression)
            {
                if (role.IsConstrainedClass())
                {
                    if (role.Constraint != null)
                    {
                        if (!role.Constraint.Invoke(childExpression))
                        {
                            throw new InvalidOperationException();
                        }
                    }
                    else if (this.childExpression != null && childExpression.GetType() != this.childExpression.GetType())
                    {
                        throw new InvalidOperationException();
                    }
                }

                this.childExpression = childExpression;
                parentExpression.AdoptChildExpression(childExpression);
                parentExpression.ResetLocalStaticProperties(); //childExpression.verifyParentPointers();
            }
        }
        public void DetachChild()
        {
            if (DEBUG)
            {
                childExpression.ParentExpression = null;
                StringWriter sw = new StringWriter();
                sw.WriteLine(new XPathException("dummy").ToString());
                childExpression = new ErrorExpression("child expression has been detached: " + sw.ToString(), "ZZZ", false);
                ExpressionTool.CopyLocationInfo(parentExpression, childExpression);
            }
        }

        public bool SetsNewFocus()
        {
            return role.SetsNewFocus();
        }

        public bool HasSpecialFocusRules()
        {
            return role.HasSpecialFocusRules();
        }

        public bool HasSameFocus()
        {
            return role.HasSameFocus();
        }

        public bool IsHigherOrder()
        {
            return role.IsHigherOrder();
        }

        public bool IsEvaluatedRepeatedly()
        {
            return role.IsEvaluatedRepeatedly();
        }

        public SequenceType GetRequiredType()
        {
            return role.GetRequiredType();
        }

        public bool IsInChoiceGroup()
        {
            return role.IsInChoiceGroup();
        }

        public IEnumerator<Operand> IIterator()
        {
            yield return this;
        }

        public void TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            // Bound tree-descent depth (see ExpressionVisitor.EnterStaticDescent). Must precede the try:
            // it self-decrements before throwing, so the paired LeaveStaticDescent runs only when we entered.
            visitor.EnterStaticDescent();
            try
            {
                SetChildExpression(GetChildExpression().TypeCheck(visitor, contextInfo));
            }
            catch (XPathException e)
            {
                e.MaybeSetLocation(GetChildExpression().GetLocation());
                if (!e.IsReportableStatically())
                {
                    visitor.StaticContext.IssueWarning("Evaluation will always throw a dynamic error: " + e.GetMessage(), DAXonErrorCode.SXWN9027, GetChildExpression().GetLocation());
                    SetChildExpression(new ErrorExpression(new XmlProcessingException(e)));
                }
                else
                {
                    throw e;
                }
            }
            finally
            {
                visitor.LeaveStaticDescent();
            }
        }

        public void Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            // Bound tree-descent depth (see ExpressionVisitor.EnterStaticDescent). Must precede the try:
            // it self-decrements before throwing, so the paired LeaveStaticDescent runs only when we entered.
            visitor.EnterStaticDescent();
            try
            {
                SetChildExpression(GetChildExpression().Optimize(visitor, contextInfo));
            }
            catch (XPathException e)
            {
                e.MaybeSetLocation(GetChildExpression().GetLocation());
                if (!e.IsReportableStatically())
                {
                    visitor.StaticContext.IssueWarning("Evaluation will always throw a dynamic error: " + e.GetMessage(), DAXonErrorCode.SXWN9027, GetChildExpression().GetLocation());
                    SetChildExpression(new ErrorExpression(new XmlProcessingException(e)));
                }
                else
                {
                    throw e;
                }
            }
            finally
            {
                visitor.LeaveStaticDescent();
            }
        }

        public static OperandUsage TypeDeterminedUsage(ItemType type)
        {
            if (type.IsPlainType())
            {
                return OperandUsage.ABSORPTION;
            }
            else if (type is NodeTest || type == AnyItemType.GetInstance())
            {
                return OperandUsage.NAVIGATION;
            }
            else
            {
                return OperandUsage.INSPECTION;
            }
        }
        public IEnumerator<Operand> GetEnumerator() { yield return this; }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
