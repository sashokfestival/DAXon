////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Functions.Registry;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Xslt;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using OutSmart.DAXon.Internal.Functional;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Trees.Iterators;
namespace OutSmart.DAXon.Expressions.Instructions
{
    public class UserFunction : Actor, IFunctionItem, IFunctionDefinition, IContextOriginator, ITraceableComponent
    {

        private const int MAX_INLININGS = 100;

        /// <summary>
        /// Create a user-defined function (the body must be added later)
        /// </summary>
        private static string saxonDotEqName = "Q{" + NamespaceUri.SAXON + "}dot";
        private StructuredQName functionName; // null for an anonymous function
        private bool tailCalls = false;
        private bool tailRecursive = false;
        private UserFunctionParameter[] parameterDefinitions;
        private SequenceType resultType;
        private SequenceType declaredResultType;
        protected volatile ISequenceEvaluator bodyEvaluator = null; // set once (fully built), then read lock-free per call
        protected volatile IPushEvaluator pushEvaluator = null;     // ditto
        private bool updating = false;
        private bool ixslUpdating = false;
        private int inlineable = -1; // 0:no 1:yes -1:don't know
        private int inliningCount = 0;
        private bool overrideExtensionFunction = true;
        private AnnotationList annotations = AnnotationList.EMPTY;
        private FunctionStreamability declaredStreamability = FunctionStreamability.UNCLASSIFIED;
        private Determinism determinism = Determinism.PROACTIVE;
        private int refCount = 0;
        private int minimumArity = 0;

        /// <summary>
        /// Create a user-defined function (the body must be added later)
        /// </summary>
        public string Description
        {
            get
            {
                StructuredQName name = GetFunctionName();
                if (name.HasURI(NamespaceUri.ANONYMOUS))
                {
                    bool first = true;
                    StringBuilder sb = new StringBuilder("function");
                    if (GetParameterDefinitions().Length != 1 || !GetParameterDefinitions()[0].GetVariableQName().EQName.Equals(saxonDotEqName))
                    {
                        sb.Append("(");
                        foreach (UserFunctionParameter param in GetParameterDefinitions())
                        {
                            if (first)
                            {
                                first = false;
                            }
                            else
                            {
                                sb.Append(", ");
                            }

                            sb.Append("$").Append(param.GetVariableQName().DisplayName);
                        }

                        sb.Append(")");
                    }

                    sb.Append("{");
                    Expression body = GetBody();
                    if (body == null)
                    {
                        sb.Append("...");
                    }
                    else
                    {
                        string bodyText = body.ToShortString().Replace("$saxon:dot!", "");
                        sb.Append(bodyText);
                    }

                    sb.Append("}");
                    return sb.ToString();
                }
                else
                {
                    return name.DisplayName;
                }
            }
        }
        /// <summary>
        /// Create a user-defined function (the body must be added later)
        /// </summary>
        public override string TracingTag => "xsl:function";

        /// <summary>
        /// Create a user-defined function (the body must be added later)
        /// </summary>
        public IFunctionItemType FunctionItemType
        {
            get
            {
                SequenceType[] argTypes = new SequenceType[parameterDefinitions.Length];
                for (int i = 0; i < parameterDefinitions.Length; i++)
                {
                    UserFunctionParameter ufp = parameterDefinitions[i];
                    argTypes[i] = ufp.GetRequiredType();
                }

                return new SpecificFunctionType(argTypes, resultType, annotations);
            }
        }

        /// <summary>
        /// Create a user-defined function (the body must be added later)
        /// </summary>
        public OperandRole[] OperandRoles
        {
            get
            {
                OperandRole[] roles = new OperandRole[GetArity()];
                OperandUsage first = OperandUsage.TRANSMISSION;
                switch (declaredStreamability)
                {
                    case FunctionStreamability.UNCLASSIFIED:
                        SequenceType required = GetArgumentType(0);
                        first = OperandRole.GetTypeDeterminedUsage(required.PrimaryType);
                        break;
                    case FunctionStreamability.ABSORBING:
                        first = OperandUsage.ABSORPTION;
                        break;
                    case FunctionStreamability.INSPECTION:
                        first = OperandUsage.INSPECTION;
                        break;
                    case FunctionStreamability.FILTER:
                        first = OperandUsage.TRANSMISSION;
                        break;
                    case FunctionStreamability.SHALLOW_DESCENT:
                        first = OperandUsage.TRANSMISSION;
                        break;
                    case FunctionStreamability.DEEP_DESCENT:
                        first = OperandUsage.TRANSMISSION;
                        break;
                    case FunctionStreamability.ASCENT:
                        first = OperandUsage.TRANSMISSION;
                        break;
                }

                roles[0] = new OperandRole(0, first, GetArgumentType(0));
                for (int i = 1; i < roles.Length; i++)
                {
                    SequenceType required = GetArgumentType(i);
                    roles[i] = new OperandRole(0, OperandRole.GetTypeDeterminedUsage(required.PrimaryType), required);
                }

                return roles;
            }
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public virtual FunctionStreamability DeclaredStreamability
        {
            get => this.declaredStreamability; set
            {
                this.declaredStreamability = value == null ? FunctionStreamability.UNCLASSIFIED : value;
            }
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public virtual SequenceType ResultType
        {
            get
            {
                if (resultType == SequenceType.ANY_SEQUENCE && GetBody() != null)
                {

                    // see if we can infer a more precise result type. We don't do this if the function contains
                    // calls on further functions, to prevent infinite regress.
                    if (!ContainsUserFunctionCalls(GetBody()))
                    {
                        resultType = SequenceType.MakeSequenceType(GetBody().GetItemType(), GetBody().GetCardinality());
                    }
                }

                return resultType;
            }
            set
            {
                this.declaredResultType = value;
                this.resultType = value;
            }
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public virtual SequenceType DeclaredResultType => declaredResultType;

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public virtual ISequenceEvaluator BodyEvaluator
        {
            get
            {
                if (bodyEvaluator == null)
                {
                    ComputeEvaluationMode();
                }

                return bodyEvaluator;
            }
            set
            {
                bodyEvaluator = value;
            }
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public UnicodeString UnicodeStringValue
        {
            get
            {
                throw new NotSupportedException("A function has no string value");
            }
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public virtual int ReferenceCount => refCount;

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public int NumberOfParameters => GetArity();
        /// <summary>
        /// Create a user-defined function (the body must be added later)
        /// </summary>
        public UserFunction()
        {
        }

        /// <summary>
        /// Create a user-defined function (the body must be added later)
        /// </summary>
        public virtual void SetFunctionName(StructuredQName name)
        {
            functionName = name;
        }

        /// <summary>
        /// Create a user-defined function (the body must be added later)
        /// </summary>
        public StructuredQName GetFunctionName()
        {
            return functionName;
        }

        /// <summary>
        /// Create a user-defined function (the body must be added later)
        /// </summary>
        public void GatherProperties(Action<string, object> consumer)
        {
            consumer.Accept("name", GetFunctionName());
            consumer.Accept("arity", GetArity());
        }

        /// <summary>
        /// Create a user-defined function (the body must be added later)
        /// </summary>
        public StructuredQName GetObjectName()
        {
            return functionName;
        }

        /// <summary>
        /// Create a user-defined function (the body must be added later)
        /// </summary>
        public override SymbolicName GetSymbolicName()
        {
            return new SymbolicName.F(functionName, GetArity());
        }

        /// <summary>
        /// Create a user-defined function (the body must be added later)
        /// </summary>
        public virtual IFunctionItemType GetFunctionItemType(int arity)
        {
            SequenceType[] argTypes = new SequenceType[arity];
            for (int i = 0; i < arity; i++)
            {
                UserFunctionParameter ufp = parameterDefinitions[i];
                argTypes[i] = ufp.GetRequiredType();
            }

            return new SpecificFunctionType(argTypes, resultType, annotations);
        }

        /// <summary>
        /// Create a user-defined function (the body must be added later)
        /// </summary>
        public virtual bool AcceptsNodesWithoutAtomization()
        {
            for (int i = 0; i < GetArity(); i++)
            {
                ItemType type = GetArgumentType(i).PrimaryType;
                if (type is NodeTest || type == AnyItemType.GetInstance())
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Create a user-defined function (the body must be added later)
        /// </summary>
        public virtual bool IsOverrideExtensionFunction()
        {
            return overrideExtensionFunction;
        }

        /// <summary>
        /// Create a user-defined function (the body must be added later)
        /// </summary>
        public virtual void SetOverrideExtensionFunction(bool overrideExtensionFunction)
        {
            this.overrideExtensionFunction = overrideExtensionFunction;
        }

        /// <summary>
        /// Create a user-defined function (the body must be added later)
        /// </summary>
        public virtual void SetAnnotations(AnnotationList list)
        {
            this.annotations = list ?? throw new NullReferenceException();
        }

        /// <summary>
        /// Create a user-defined function (the body must be added later)
        /// </summary>
        public AnnotationList GetAnnotations()
        {
            return annotations;
        }

        /// <summary>
        /// Create a user-defined function (the body must be added later)
        /// </summary>
        public virtual void SetDeterminism(Determinism determinism)
        {
            this.determinism = determinism;
        }

        /// <summary>
        /// Create a user-defined function (the body must be added later)
        /// </summary>
        public virtual Determinism GetDeterminism()
        {
            return determinism;
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public virtual void ComputeEvaluationMode()
        {
            if (tailRecursive || declaredStreamability != FunctionStreamability.UNCLASSIFIED)
            {

                // If this function contains tail calls, we evaluate it eagerly, because
                // the caller needs to know whether a tail call was returned or not: if we
                // return a Closure, the tail call escapes into the wild and can reappear anywhere...
                bodyEvaluator = GetBody().MakeElaborator().Eagerly();
            }
            else
            {
                bodyEvaluator = GetBody().MakeElaborator().Lazily(true, false);
            }
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public virtual bool? IsInlineable()
        {
            if (inlineable != -1)
            {
                return (inlineable > 0 && inliningCount < MAX_INLININGS);
            }

            if (body == null)
            {

                // bug 2226
                return null;
            }

            if (body.HasSpecialProperty(StaticProperty.HAS_SIDE_EFFECTS) || tailCalls)
            {

                // This is mainly to handle current-output-uri()
                return (false);
            }

            Component component = DeclaringComponent;
            if (component != null)
            {
                Visibility visibility = DeclaringComponent.GetVisibility();
                if (visibility == Visibility.PRIVATE || visibility == Visibility.FINAL)
                {
                    if (inlineable < 0)
                    {
                        return null;
                    }
                    else
                    {
                        return (inlineable > 0);
                    }
                }
                else
                {
                    return (false);
                }
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public virtual void SetInlineable(bool inlineable)
        {
            this.inlineable = inlineable ? 1 : 0;
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public virtual void MarkAsInlined()
        {
            inliningCount++;
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public virtual void SetParameterDefinitions(UserFunctionParameter[] @params)
        {
            parameterDefinitions = @params;
            minimumArity = 0;
            foreach (UserFunctionParameter param in @params)
            {
                if (param.IsRequired())
                {
                    minimumArity++;
                }
            }
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public virtual void SetMinimumArity(int minimumArity)
        {
            this.minimumArity = minimumArity;
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public virtual void SetArityRange(int min, int max)
        {
            this.minimumArity = min;
            this.parameterDefinitions = new UserFunctionParameter[max];
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public virtual UserFunctionParameter[] GetParameterDefinitions()
        {
            return parameterDefinitions;
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public virtual int GetMinimumArity()
        {
            return minimumArity;
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public virtual void SetTailRecursive(bool tailCalls, bool recursiveTailCalls)
        {
            this.tailCalls = tailCalls;
            tailRecursive = recursiveTailCalls;
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public virtual bool ContainsTailCalls()
        {
            return tailCalls;
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public virtual bool IsTailRecursive()
        {
            return tailRecursive;
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public virtual void SetUpdating(bool isUpdating)
        {
            this.updating = isUpdating;
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public virtual bool IsUpdating()
        {
            return updating;
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public virtual void SetIxslUpdating(bool isUpdating)
        {
            this.ixslUpdating = isUpdating;
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        private static bool ContainsUserFunctionCalls(Expression exp)
        {
            if (exp is UserFunctionCall)
            {
                return true;
            }

            foreach (Operand o in exp.Operands())
            {
                if (ContainsUserFunctionCalls(o.GetChildExpression()))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public virtual SequenceType GetArgumentType(int n)
        {
            return parameterDefinitions[n].GetRequiredType();
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public int GetArity()
        {
            return parameterDefinitions.Length;
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public virtual bool IsMemoFunction()
        {
            return false;
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public virtual void TypeCheck(ExpressionVisitor visitor)
        {
            Expression exp = GetBody();
            if (exp is ValueOf && ((ValueOf)exp).Select.GetItemType().IsAtomicType() && declaredResultType.PrimaryType.IsAtomicType() && declaredResultType.PrimaryType != BuiltInAtomicType.STRING)
            {
                visitor.StaticContext.IssueWarning("A function that computes atomic values should use xsl:sequence rather than xsl:value-of", DAXonErrorCode.SXWN9032, GetLocation());
            }

            ExpressionTool.ResetPropertiesWithinSubtree(exp);
            Expression exp2 = exp;
            try
            {

                // We've already done the typecheck of each XPath expression, but it's worth doing again at this
                // level because we have more information now.
                ContextItemStaticInfo info = ContextItemStaticInfo.ABSENT;
                exp2 = exp.TypeCheck(visitor, info);
                if (resultType != null)
                {
                    // XTTE0780 is reserved for the result of a *named* xsl:function; an inline (anonymous)
                    // function's result type error is XPTY0004 even in XSLT. Inline functions carry a synthetic
                    // name in the anonymous namespace, so treat that as "no name" (Java leaves it null here).
                    bool namedXsltFn = GetPackageData().IsXSLT() && functionName != null && !functionName.HasURI(NamespaceUri.ANONYMOUS);
                    Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.FUNCTION_RESULT, functionName == null ? "" : functionName.DisplayName + "#" + GetArity(), 0, namedXsltFn ? "XTTE0780" : "XPTY0004");
                    exp2 = visitor.GetConfiguration().GetTypeChecker(false).StaticTypeCheck(exp2, resultType, role, visitor);
                }
            }
            catch (XPathException err)
            {
                throw err.MaybeWithLocation(GetLocation());
            }

            if (exp2 != exp)
            {
                SetBody(exp2);
            }
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public XPathContextMajor MakeNewContext(IXPathContext oldContext, IContextOriginator originator)
        {
            XPathContextMajor c2 = oldContext.NewCleanContext();

            c2.TemporaryOutputState = StandardNames.XSL_FUNCTION;
            c2.CurrentOutputUri = null;
            c2.SetCurrentComponent(DeclaringComponent); // default value for the caller to override if necessary
            c2.Origin = originator;
            return c2;
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public virtual ISequence Call(IXPathContext context, ISequence[] actualArgs)
        {
            XPathContextMajor c2 = (XPathContextMajor)context;
            c2.SetStackFrame(GetStackFrameMap(), actualArgs);
            return EvaluateBodyDirect(c2);
        }

        /// <summary>
        /// The body-evaluation half of Call: assumes the caller has already installed the stack
        /// frame. FusedArity(1|2)Caller installs one reused frame and comes here directly.
        /// </summary>
        internal ISequence EvaluateBodyDirect(XPathContextMajor c2)
        {
            // Every pull/item function invocation (classic and fused) enters here, so this one
            // probe bounds user-function recursion depth (RecursionDepthError -> SXLM0001).
            StackGuard.Probe();

            // Lock-free after first call (this is the per-function-call hot path); the volatile
            // field is assigned exactly one fully-built evaluator by ComputeEvaluationMode.
            ISequenceEvaluator body = bodyEvaluator;
            if (body == null)
            {
                lock (this)
                {
                    if (bodyEvaluator == null)
                    {

                        // first time through
                        ComputeEvaluationMode();
                    }
                }

                body = bodyEvaluator;
            }

            ISequence result;
            try
            {
                result = body.Evaluate(c2);
            }
            catch (RecursionDepthError)
            {
                // A deeper recursion level tripped the stack guard; convert here so dynamic and
                // fused call paths (no call-site catch) report SXLM0001 instead of the generic
                // internal-error wrap below.
                throw RecursionOverflow();
            }
            catch (XPathException err) when (!(err is XPathException.StackOverflow))
            {
                // StackOverflow flies through decorating rethrows untouched — see the
                // DocumentInstr elaborator note (deep-unwind stack discipline, round AQ).
                throw err.MaybeWithLocation(GetLocation()).MaybeWithContext(c2);
            }
            catch (UncheckedXPathException uxe)
            {
                throw uxe.GetXPathException().MaybeWithLocation(GetLocation()).MaybeWithContext(c2);
            }
            catch (Exception err2) when (!(err2 is XPathException))
            {
                string message = "Internal error evaluating function " + (functionName == null ? "(unnamed)" : functionName.DisplayName) + (GetLineNumber() > 0 ? " at line " + GetLineNumber() : "") + (GetSystemId() != null ? " in module " + GetSystemId() : "");
                throw new Exception(message, err2);
            }

            return result;
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public virtual void Process(XPathContextMajor context, ISequence[] actualArgs, Outputter output)
        {
            StackGuard.Probe();
            context.SetStackFrame(GetStackFrameMap(), actualArgs);

            // Lock-free after first call, same discipline as Call above.
            IPushEvaluator push = pushEvaluator;
            if (push == null)
            {
                lock (this)
                {
                    if (pushEvaluator == null)
                    {
                        pushEvaluator = GetBody().MakeElaborator().ElaborateForPush();
                    }
                }

                push = pushEvaluator;
            }

            try
            {
                ITailCall tc = push.ProcessLeavingTail(output, context);
                Expression.DispatchTailCall(tc);
            }
            catch (RecursionDepthError)
            {
                // A deeper recursion level tripped the stack guard; convert at the nearest body
                // so call sites without their own catch still report SXLM0001.
                throw RecursionOverflow();
            }
        }

        private XPathException.StackOverflow RecursionOverflow()
        {
            return new XPathException.StackOverflow("Too many nested function calls. May be due to infinite recursion", DAXonErrorCode.SXLM0001, GetLocation());
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public virtual ISequence Call(ISequence[] actualArgs, Controller controller)
        {
            return Call(controller.NewXPathContext(), actualArgs);
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public virtual void CallUpdating(ISequence[] actualArgs, XPathContextMajor context, IPendingUpdateList pul)
        {
            context.SetStackFrame(GetStackFrameMap(), actualArgs);
            try
            {
                GetBody().MakeElaborator().ElaborateForUpdate().RegisterUpdates(context, pul);
            }
            catch (XPathException err)
            {
                throw err.MaybeWithLocation(GetLocation()).MaybeWithContext(context);
            }
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public override void Export(ExpressionPresenter presenter)
        {
            presenter.StartElement("function");
            if (GetFunctionName() != null)
            {
                presenter.EmitAttribute("name", GetFunctionName());
                presenter.EmitAttribute("line", GetLineNumber() + "");
                presenter.EmitAttribute("module", GetSystemId()); //presenter.emitAttribute("eval", getEvaluator().getCode() + "");
            }

            string flags = "";
            if (determinism == Determinism.PROACTIVE)
            {
                flags += "p";
            }
            else if (determinism == Determinism.ELIDABLE)
            {
                flags += "e";
            }
            else
            {
                flags += "d";
            }

            if (IsMemoFunction())
            {
                flags += "m";
            }

            if (ixslUpdating)
            {
                flags += "u";
            }

            switch (declaredStreamability)
            {
                case FunctionStreamability.UNCLASSIFIED:
                    flags += "U";
                    break;
                case FunctionStreamability.ABSORBING:
                    flags += "A";
                    break;
                case FunctionStreamability.INSPECTION:
                    flags += "I";
                    break;
                case FunctionStreamability.FILTER:
                    flags += "F";
                    break;
                case FunctionStreamability.SHALLOW_DESCENT:
                    flags += "S";
                    break;
                case FunctionStreamability.DEEP_DESCENT:
                    flags += "D";
                    break;
                case FunctionStreamability.ASCENT:
                    flags += "C";
                    break;
            }

            presenter.EmitAttribute("flags", flags);
            presenter.EmitAttribute("as", DeclaredResultType.ToAlphaCode());
            presenter.EmitAttribute("slots", GetStackFrameMap().NumberOfVariables + "");
            foreach (UserFunctionParameter p in parameterDefinitions)
            {
                presenter.StartElement("arg");
                presenter.EmitAttribute("name", p.GetVariableQName());
                presenter.EmitAttribute("as", p.GetRequiredType().ToAlphaCode());
                presenter.EndElement();
            }

            presenter.SetChildRole("body");
            GetBody().Export(presenter);
            presenter.EndElement();
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public override bool IsExportable()
        {
            return refCount > 0 || (DeclaredVisibility != Visibility.UNDEFINED && DeclaredVisibility != Visibility.PRIVATE) || ((StylesheetPackage)GetPackageData()).IsRetainUnusedFunctions();
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public bool IsTrustedResultType()
        {
            return false;
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public bool IsMap()
        {
            return false;
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public bool IsArray()
        {
            return false;
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public bool DeepEquals(IFunctionItem other, IXPathContext context, IAtomicComparer comparer, int flags)
        {
            throw new XPathException("Cannot compare functions using deep-equal", "FOTY0015").AsTypeError().WithXPathContext(context);
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public bool DeepEqual40(IFunctionItem other, IXPathContext context, DeepEqual.DeepEqualOptions options)
        {
            throw new XPathException("Cannot compare functions using deep-equal", "FOTY0015").AsTypeError().WithXPathContext(context);
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public IFunctionItem ItemAt(int n)
        {
            return n == 0 ? this : null;
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public IGroundedValue Subsequence(int start, int length)
        {

            return start <= 0 && (start + length) > 0 ? (IGroundedValue)this : (IGroundedValue)EmptySequence.GetInstance();
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public int GetLength()
        {
            return 1;
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public bool EffectiveBooleanValue()
        {
            return ExpressionTool.EffectiveBooleanValue(this);
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public UserFunction Reduce()
        {
            return this;
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public UserFunction Head()
        {
            return this;
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public IAtomicSequence Atomize()
        {
            throw new XPathException("Functions cannot be atomized", "FOTY0013");
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public virtual void IncrementReferenceCount()
        {
            refCount++;
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public virtual void PrepareForStreaming()
        {
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public StructuredQName GetParameterName(int i)
        {
            return GetParameterDefinitions()[i].GetVariableQName();
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public Expression GetDefaultValueExpression(int i)
        {
            return GetParameterDefinitions()[i].DefaultValueExpression;
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public int GetPositionOfParameter(StructuredQName name)
        {
            for (int i = 0; i < parameterDefinitions.Length; i++)
            {
                if (parameterDefinitions[i].GetVariableQName().Equals(name))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Determine the preferred evaluation mode for this function
        /// </summary>
        public static Expression[] MakeExpandedArgumentArray(Expression[] arguments, Dictionary<StructuredQName, int> keywords, IFunctionDefinition fd)
        {

            // 4.0: handle keyword arguments and default arguments
            Expression[] expandedArgs;
            int maxArity = fd.NumberOfParameters;

            // If there are keyword arguments, reposition them to the correct position in the argument sequence
            if (keywords != null)
            {
                expandedArgs = new Expression[maxArity];
                int positionalArgs = arguments.Length - keywords.Count;
                Array.Copy(arguments, 0, expandedArgs, 0, positionalArgs);
                foreach (KeyValuePair<StructuredQName, int> entry in keywords.EntrySet())
                {
                    StructuredQName key = entry.Key;
                    int argPos = entry.Value;
                    int paramPos = fd.GetPositionOfParameter(key);
                    if (paramPos < 0)
                    {
                        throw new XPathException("Keyword " + key + " does not match the name of any declared parameter of function " + fd.GetFunctionName(), "XPST0142");
                    }

                    if (paramPos < positionalArgs)
                    {
                        throw new XPathException("Parameter " + key + " of function " + fd.GetFunctionName() + " is supplied both by position and by keyword", "XPST0141");
                    }

                    Expression supplied = arguments[argPos];
                    expandedArgs[paramPos] = supplied;
                }
            }
            else
            {
                expandedArgs = ArrayTools.CopyOf(arguments, maxArity);
            }

            for (int a = 0; a < maxArity; a++)
            {
                if (expandedArgs[a] == null)
                {
                    Expression defaultVal = new DefaultedArgumentExpression(); // to be fixed up later
                    expandedArgs[a] = defaultVal; //                Expression defaultVal = fd.getDefaultValueExpression(a);
                    //                if (defaultVal == null) {
                    //                    defaultVal = new DefaultedArgumentExpression(); // to be fixed up later
                    //                }
                    //                expandedArgs[a] = defaultVal.copy(new RebindingMap());
                    //expandedArgs[a] = new ErrorExpression("UseDefault", "UseDefault", false); // for now
                }
            }

            return expandedArgs;
        }
        IXPathContext IFunctionItem.MakeNewContext(IXPathContext arg0, IContextOriginator arg1) => MakeNewContext(arg0, arg1); // covariant bridge
        IItem IGroundedValue.ItemAt(int arg0) => ItemAt(arg0);
        IItem IGroundedValue.Head() => Head();
        IItem ISequence.Head() => Head();
        public virtual Genre GetGenre() => throw new NotImplementedException();
        public virtual ISequenceIterator Iterate() => throw new NotImplementedException();
        public virtual string GetStringValue() => throw new NotImplementedException();
        IItem IItem.Head() => Head();
        IItem IItem.ItemAt(int arg0) => ItemAt(arg0);
        SingletonIterator IItem.Iterate() => new SingletonIterator(this);
        IGroundedValue IItem.Reduce() => Reduce();
        IGroundedValue IGroundedValue.Reduce() => Reduce();

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
        // A user-declared function is never sequence-variadic (that's a 4.0 concept for fn:concat-style
        // built-ins; upstream Function.isSequenceVariadic defaults to false). Was a throwing stub that
        // FunctionCall.CheckFunctionCall hits for EVERY call to an XQuery `declare function` — blocked all
        // user-function calls under XQuery (app-FunctxFunctx, prod-FunctionDecl, helpers everywhere).
        public virtual bool IsSequenceVariadic() => false;
        public virtual string ToShortString() => ToString(); // upstream Item default
        public virtual bool IsStreamed() => false; // upstream NodeInfo/Item default
        public virtual IGroundedValue Materialize() => this; // upstream GroundedValue default method
        public virtual IEnumerable<IItem> AsIterable() => new IItem[] { this }; // singleton grounded value (upstream GroundedValue default for an Item)
        public virtual bool ContainsNode(NodeInfo sought) => OutSmart.DAXon.Expressions.SingletonIntersectExpression.ContainsNode(((OutSmart.DAXon.Model.ISequence)this).Iterate(), sought); // upstream GroundedValue default
        public virtual IGroundedValue Concatenate(IGroundedValue[] others)
        {
            // upstream GroundedValue default: chain this value's items with the others
            var __chain = new OutSmart.DAXon.Collections.Zeno.ZenoChain<OutSmart.DAXon.Model.IItem>().AddAll(((OutSmart.DAXon.Model.IGroundedValue)this).AsIterable());
            foreach (OutSmart.DAXon.Model.IGroundedValue __v in others)
                __chain = __chain.AddAll(__v.AsIterable());
            return new OutSmart.DAXon.Collections.Zeno.ZenoSequence(__chain);
        }
        public virtual ISequence MakeRepeatable() => this; // upstream Sequence.makeRepeatable default
        public enum Determinism
        {
            DETERMINISTIC,
            PROACTIVE,
            ELIDABLE
        }
    }
}
