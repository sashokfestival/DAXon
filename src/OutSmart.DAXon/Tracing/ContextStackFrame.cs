////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Values.Arrays;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Tracing
{
    public abstract class ContextStackFrame
    {
        protected IXPathContext context;
        private ILocation location;
        private IItem contextItem;
        private object container;

        public virtual object Container => container;

        public virtual IXPathContext Context
        {
            get => context; set
            {
                this.context = value;
            }
        }

        public virtual IItem ContextItem
        {
            get => contextItem; set
            {
                this.contextItem = value;
            }
        }
        public virtual void SetLocation(ILocation loc)
        {
            this.location = loc;
        }

        public virtual string GetSystemId()
        {
            return location.GetSystemId();
        }

        public virtual int GetLineNumber()
        {
            return location.GetLineNumber();
        }

        public virtual void SetComponent(object container)
        {
            this.container = container;
        }

        public abstract void Print(Logger @out);
        protected virtual string ShowLocation()
        {
            if (GetSystemId() == null)
            {
                return "";
            }

            int line = GetLineNumber();
            if (line == -1 || line == 0xfffff)
            {
                return "(" + GetSystemId() + ")";
            }
            else
            {
                return "(" + GetSystemId() + "#" + GetLineNumber() + ")";
            }
        }

        private static string DisplayContainer(object container)
        {
            if (container is Actor)
            {
                StructuredQName name = ((Actor)container).ComponentName;
                string objectName = name == null ? "" : name.DisplayName;
                if (container is NamedTemplate)
                {
                    return "template name=\"" + objectName + "\"";
                }
                else if (container is UserFunction)
                {
                    return "function " + objectName + "()";
                }
                else if (container is AttributeSet)
                {
                    return "attribute-set " + objectName;
                }
                else if (container is KeyDefinition)
                {
                    return "key " + objectName;
                }
                else if (container is GlobalVariable)
                {
                    StructuredQName qName = ((GlobalVariable)container).GetVariableQName();
                    if (qName.HasURI(NamespaceUri.SAXON_GENERATED_VARIABLE))
                    {
                        return "optimizer-created global variable";
                    }
                    else
                    {
                        return "global variable $" + qName.DisplayName;
                    }
                }
            }
            else if (container is TemplateRule)
            {
                return "template match=\"" + ((TemplateRule)container).MatchPattern.ToString() + "\"";
            }

            return "";
        }

        internal class CallingApplication : ContextStackFrame
        {
            public override void Print(Logger @out)
            {
            }
        }

        /// <summary>
        /// Subclass of ContextStackFrame representing a built-in template rule in XSLT
        /// </summary>
        internal class BuiltInTemplateRule : ContextStackFrame
        {
            public BuiltInTemplateRule(IXPathContext context)
            {
                this.context = context;
            }

            public override void Print(Logger @out)
            {
                IItem contextItem = context.GetContextItem();
                string diag;
                if (contextItem is NodeInfo)
                {
                    diag = Navigator.GetPath((NodeInfo)contextItem);
                }
                else if (contextItem is AtomicValue)
                {
                    diag = "value " + contextItem.ToString();
                }
                else if (contextItem is MapItem)
                {
                    diag = "map";
                }
                else if (contextItem is ArrayItem)
                {
                    diag = "array";
                }
                else if (contextItem is IFunctionItem)
                {
                    diag = "function";
                }
                else
                {
                    diag = "item";
                }

                @out.Error("  in built-in template rule for " + diag + " in " + context.GetCurrentMode().GetActor().GetModeTitle(false));
            }
        }

        internal class FunctionCall : ContextStackFrame
        {
            StructuredQName functionName;
            public virtual StructuredQName GetFunctionName()
            {
                return functionName;
            }

            public virtual void SetFunctionName(StructuredQName functionName)
            {
                this.functionName = functionName;
            }

            public override void Print(Logger @out)
            {
                @out.Error("  at " + (functionName == null ? "(anonymous)" : functionName.DisplayName) + "() " + ShowLocation());
            }
        }

        /// <summary>
        /// Subclass of ContextStackFrame representing an xsl:apply-templates call in XSLT
        /// </summary>
        internal class ApplyTemplates : ContextStackFrame
        {
            public override void Print(Logger @out)
            {
                @out.Error("  at xsl:apply-templates " + ShowLocation());
                IItem node = ContextItem;
                if (node is NodeInfo)
                {
                    @out.Error("     processing " + Navigator.GetPath((NodeInfo)node));
                }
            }
        }

        /// <summary>
        /// Subclass of ContextStackFrame representing an xsl:call-template instruction in XSLT
        /// </summary>
        internal class CallTemplate : ContextStackFrame
        {

            StructuredQName templateName;
            public virtual StructuredQName TemplateName
            {
                get => templateName; set
                {
                    this.templateName = value;
                }
            }
            public override void Print(Logger @out)
            {
                string name = templateName == null ? "??" : templateName.DisplayName;
                @out.Error("  at xsl:call-template name=\"" + name + "\" " + ShowLocation());
            }
        }

        /// <summary>
        /// Subclass of ContextStackFrame representing the evaluation of a variable (typically a global variable)
        /// </summary>
        internal class VariableEvaluation : ContextStackFrame
        {

            StructuredQName variableName;
            public virtual StructuredQName VariableName
            {
                get => variableName; set
                {
                    this.variableName = value;
                }
            }
            public override void Print(Logger @out)
            {
                @out.Error("  in " + DisplayContainer(Container) + " " + ShowLocation());
            }
        }
    }
}