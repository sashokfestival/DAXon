////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Accumulators;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Tracing
{
    /// <summary>
    /// A Simple trace listener for XSLT that writes messages (by default) to System.Console.Error
    /// </summary>
    public class XSLTTraceListener : AbstractTraceListener
    {

        /// <summary>
        /// Generate attributes to be included in the opening trace element
        /// </summary>
        protected override string OpeningAttributes => "xmlns:xsl=\"" + NamespaceConstant.XSLT + '"';
        protected override bool IsApplicable(ITraceable info)
        {
            if (!base.IsApplicable(info))
            {
                return false;
            }

            if (Level(info) == TraceLevel.LOW)
            {
                return detail != TraceLevel.NONE;
            }

            if (detail == TraceLevel.HIGH)
            {
                return true;
            }

            if (info is Expression)
            {
                return XSLTTraceCodeInjector.IsTraceableExpression((Expression)info);
            }

            return false;
        }

        /// <summary>
        /// Generate attributes to be included in the opening trace element
        /// </summary>
        protected override string Tag(ITraceable info)
        {
            return TagName(info);
        }

        /// <summary>
        /// Generate attributes to be included in the opening trace element
        /// </summary>
        public static string TagName(ITraceable info)
        {
            if (info is Expression)
            {
                Expression expr = (Expression)info;
                if (expr is FixedElement)
                {
                    return "LRE";
                }
                else if (expr is FixedAttribute)
                {
                    return "ATTR";
                }
                else if (expr is LetExpression)
                {
                    return "xsl:variable";
                }
                else if (expr.IsCallOn(typeof(Functions.Trace)))
                {
                    return "fn-trace";
                }
                else if (expr is SystemFunctionCall)
                {
                    return "call";
                }
                else
                {
                    return expr.ExpressionName;
                }
            }
            else if (info is AccumulatorRule)
            {
                return "xsl:accumulator";
            }
            else if (info is UserFunction)
            {
                return "xsl:function";
            }
            else if (info is TemplateRule)
            {
                return "xsl:template";
            }
            else if (info is NamedTemplate)
            {
                return "xsl:template";
            }
            else if (info is GlobalParam)
            {
                return "xsl:param";
            }
            else if (info is GlobalVariable)
            {
                return "xsl:variable";
            }
            else if (info is Functions.Trace)
            {
                return "fn-trace";
            }
            else
            {
                return "misc";
            }
        }
    }
}