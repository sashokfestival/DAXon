////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Expressions.Instructions
{
    /// <summary>
    /// The compiled form of an xsl:attribute-set element in the stylesheet.
    /// </summary>
    public class AttributeSet : Actor
    {
        StructuredQName attributeSetName;
        private bool declaredStreamable;
        private IPushEvaluator bodyEvaluator;

        /// <summary>
        /// Create an empty attribute set
        /// </summary>
        public virtual int FocusDependencies => body.Dependencies & StaticProperty.DEPENDS_ON_FOCUS;
        /// <summary>
        /// Create an empty attribute set
        /// </summary>
        public AttributeSet()
        {
        }

        /// <summary>
        /// Create an empty attribute set
        /// </summary>
        public override SymbolicName GetSymbolicName()
        {
            return new SymbolicName(StandardNames.XSL_ATTRIBUTE_SET, attributeSetName);
        }

        /// <summary>
        /// Create an empty attribute set
        /// </summary>
        public virtual void SetName(StructuredQName attributeSetName)
        {
            this.attributeSetName = attributeSetName;
        }

        /// <summary>
        /// Create an empty attribute set
        /// </summary>
        public virtual void SetDeclaredStreamable(bool value)
        {
            this.declaredStreamable = value;
        }

        /// <summary>
        /// Create an empty attribute set
        /// </summary>
        public virtual bool IsDeclaredStreamable()
        {
            return this.declaredStreamable;
        }

        /// <summary>
        /// Create an empty attribute set
        /// </summary>
        public override void SetStackFrameMap(SlotManager stackFrameMap)
        {
            if (stackFrameMap != null)
            {
                base.SetStackFrameMap(stackFrameMap);
            }
        }

        /// <summary>
        /// Create an empty attribute set
        /// </summary>
        public virtual void Expand(Outputter output, IXPathContext context)
        {
            lock (this)
            {
                if (bodyEvaluator == null)
                {
                    bodyEvaluator = GetBody().MakeElaborator().ElaborateForPush();
                }
            }

            Stack<AttributeSet> stack = ((XsltController)context.GetController()).AttributeSetEvaluationStack;
            if (stack.Contains(this))
            {
                throw new XPathException("Attribute set " + GetObjectName().EQName + " invokes itself recursively", "XTDE0640");
            }

            stack.Push(this);
            Expression.DispatchTailCall(bodyEvaluator.ProcessLeavingTail(output, context));
            stack.Pop();
            if (stack.IsEmpty())
            {
                ((XsltController)context.GetController()).ReleaseAttributeSetEvaluationStack();
            }
        }

        /// <summary>
        /// Create an empty attribute set
        /// </summary>
        public virtual StructuredQName GetObjectName()
        {
            return attributeSetName;
        }

        /// <summary>
        /// Create an empty attribute set
        /// </summary>
        public override void Export(ExpressionPresenter presenter)
        {
            presenter.StartElement("attributeSet");
            presenter.EmitAttribute("name", GetObjectName());
            presenter.EmitAttribute("line", GetLineNumber() + "");
            presenter.EmitAttribute("module", GetSystemId());
            presenter.EmitAttribute("slots", GetStackFrameMap().NumberOfVariables + "");
            presenter.EmitAttribute("binds", DeclaringComponent.ComponentBindings.Count + "");
            if (IsDeclaredStreamable())
            {
                presenter.EmitAttribute("flags", "s");
            }

            GetBody().Export(presenter);
            presenter.EndElement();
        }
    }
}