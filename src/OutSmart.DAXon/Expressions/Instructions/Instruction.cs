////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
using OutSmart.DAXon.Internal.Jaxp.Transform;
using OutSmart.DAXon.Core;
namespace OutSmart.DAXon.Expressions.Instructions
{
    public abstract class Instruction : Expression
    {

        /// <summary>
        /// Constructor
        /// </summary>
        public override int ImplementationMethod => Expression.PROCESS_METHOD;

        /// <summary>
        /// Constructor
        /// </summary>
        public virtual int InstructionNameCode => -1;

        /// <summary>
        /// Constructor
        /// </summary>
        public override string ExpressionName
        {
            get
            {
                int code = InstructionNameCode;
                if (code >= 0 & code < 1024)
                {
                    return StandardNames.GetDisplayName(code);
                }
                else
                {
                    return base.ExpressionName;
                }
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public override int NetCost => 20;
        /// <summary>
        /// Constructor
        /// </summary>
        public Instruction()
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public override bool IsInstruction()
        {
            return true;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public override Types.ItemType GetItemType()
        {
            return Types.Type.ITEM_TYPE;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        protected override int ComputeCardinality()
        {
            return StaticProperty.ALLOWS_ZERO_OR_MORE;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public abstract override IEnumerable<Operand> Operands();
        /// <summary>
        /// Constructor
        /// </summary>
        public override void Process(Outputter output, IXPathContext context)
        {
            try
            {
                ITailCall tc = MakeElaborator().ElaborateForPush().ProcessLeavingTail(output, context);
                DispatchTailCall(tc);
            }
            catch (XPathException err)
            {
                throw err.MaybeWithFailingExpression(this).MaybeWithContext(context);
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public virtual ILocation GetSourceLocator()
        {
            return GetLocation();
        }

        /// <summary>
        /// Constructor
        /// </summary>
        protected static XPathException DynamicError(ILocation loc, XPathException error, IXPathContext context)
        {
            if (error is TerminationException)
            {
                return error;
            }

            return error.MaybeWithLocation(loc).MaybeWithContext(context);
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public static ParameterSet AssembleParams(IXPathContext context, WithParam[] actualParams)
        {
            if (actualParams == null || actualParams.Length == 0)
            {
                return null;
            }

            ParameterSet @params = new ParameterSet(actualParams.Length);
            foreach (WithParam actualParam in actualParams)
            {
                @params.Put(actualParam.VariableQName, actualParam.GetSelectValue(context), actualParam.IsTypeChecked());
            }

            return @params;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public static ParameterSet AssembleTunnelParams(IXPathContext context, WithParam[] actualParams)
        {
            ParameterSet existingParams = context.GetTunnelParameters();
            if (existingParams == null)
            {
                return AssembleParams(context, actualParams);
            }

            if (actualParams == null || actualParams.Length == 0)
            {
                return existingParams;
            }

            ParameterSet newParams = new ParameterSet(existingParams, actualParams.Length);
            foreach (WithParam actualParam in actualParams)
            {
                newParams.Put(actualParam.VariableQName, actualParam.GetSelectValue(context), false);
            }

            return newParams;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        protected override int ComputeSpecialProperties()
        {
            int p = base.ComputeSpecialProperties();
            if (AlwaysCreatesNewNodes())
            {
                p |= StaticProperty.ALL_NODES_NEWLY_CREATED;
            }

            if (MayCreateNewNodes())
            {
                return p;
            }
            else
            {
                return p | StaticProperty.NO_NODES_NEWLY_CREATED;
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public virtual bool MayCreateNewNodes()
        {
            return false;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public virtual bool AlwaysCreatesNewNodes()
        {
            return false;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        protected bool SomeOperandCreatesNewNodes()
        {
            foreach (Operand o in Operands())
            {
                Expression child = o.GetChildExpression();
                int props = child.GetSpecialProperties();
                if ((props & StaticProperty.NO_NODES_NEWLY_CREATED) == 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public override IItem EvaluateItem(IXPathContext context)
        {
            return MakeElaborator().ElaborateForItem().Eval(context); //        int m = getImplementationMethod();
            //        if ((m & EVALUATE_METHOD) != 0) {
            //            throw new AssertionError(
            //                    "evaluateItem() is not implemented in the subclass " + getClass());
            //        } else if ((m & ITERATE_METHOD) != 0) {
            //            return iterate(context).next();
            //        } else {
            //        }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public override ISequenceIterator Iterate(IXPathContext context)
        {
            Elaborator elaborator = MakeElaborator();
            if (elaborator is FallbackElaborator)
            {
                if ((ImplementationMethod & PROCESS_METHOD) != 0)
                {
                    return ExpressionTool.GetIteratorFromProcessMethod(this, context);
                }

                throw new InvalidOperationException("No iterate() method available for expression " + ToShortString());
            }

            return elaborator.ElaborateForPull().Iterate(context); //        int m = getImplementationMethod();
            //        if ((m & EVALUATE_METHOD) != 0) {
            //            IItem item = evaluateItem(context);
            //            if (item == null) {
            //                return EmptyIterator.emptyIterator();
            //            } else {
            //            }
            //        } else if ((m & ITERATE_METHOD) != 0) {
            //            throw new AssertionError("iterate() is not implemented in the subclass " + getClass());
            //        } else {
            //        }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public override UnicodeString EvaluateAsString(IXPathContext context)
        {
            IItem item = EvaluateItem(context);
            if (item == null)
            {
                return EmptyUnicodeString.GetInstance();
            }
            else
            {
                return item.UnicodeStringValue;
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public virtual bool IsXSLT()
        {
            return GetPackageData().IsXSLT();
        }
    }
}