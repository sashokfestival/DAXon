////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;

namespace OutSmart.DAXon.Functions.HigherOrder
{
    /// <summary>
    /// Reusable single-argument invoker for the CoercedFunction -> BoundUserFunction -> UserFunction
    /// chain that HOF system functions call once per item (fn:filter predicates, fn:sort keys). The
    /// general path pays, per call: a fresh clean XPathContextMajor, two argument arrays, and the
    /// coercion's TypeHierarchy lookup. All of that is per-evaluation constant, and the coercion
    /// contract MATERIALIZES the result, so no lazy value can hold the stack frame past a call:
    /// the clean context and the argument array can be allocated once and rebound per call. The
    /// caller must consume each result before the next CallOne (both call sites do: EBV / atomize),
    /// and inline-function bodies are pure XPath (no instructions), so no other context state
    /// survives a call. Any other function-item shape returns null from TryMake and the call site
    /// keeps the general DynamicCall path.
    /// </summary>
    internal sealed class FusedArity1Caller
    {
        private readonly CoercedFunction coerced;
        private readonly UserFunctionReference.BoundUserFunction bound;
        private readonly UserFunction target;
        private readonly XPathContextMajor c2;
        private readonly ISequence[] args;
        private readonly bool direct;
        private readonly SequenceType argType;
        private readonly SequenceType resultType;
        private readonly TypeHierarchy th;

        private FusedArity1Caller(CoercedFunction coerced, UserFunctionReference.BoundUserFunction bound,
            UserFunction target, IXPathContext context)
        {
            this.coerced = coerced;
            this.bound = bound;
            this.target = target;
            this.argType = bound.FunctionItemType.ArgumentTypes[0];
            this.resultType = ((SpecificFunctionType)coerced.FunctionItemType).ResultType;
            this.th = context.GetConfiguration().GetTypeHierarchy();

            // What BoundUserFunction.Call does per call, done once: a clean major context with the
            // component made current.
            this.c2 = target.MakeNewContext(context, bound);
            if (bound.BoundComponent != null)
            {
                c2.SetCurrentComponent(bound.BoundComponent);
            }

            // Same direct-body contract as FusedArity2Caller: one reused frame, slot rebound per
            // call. Excluded for Call overrides (MemoFunction) and tail-call bodies (TailCallLoop
            // replaces the context's frame, breaking the slots-array identity).
            this.direct = target.GetType() == typeof(UserFunction)
                && !target.ContainsTailCalls()
                && !target.IsTailRecursive()
                && target.DeclaredStreamability == FunctionStreamability.UNCLASSIFIED;
            if (direct)
            {
                SlotManager map = target.GetStackFrameMap();
                this.args = new ISequence[map.NumberOfVariables];
                c2.SetStackFrame(map, args);
            }
            else
            {
                this.args = new ISequence[1];
            }
        }

        public static FusedArity1Caller TryMake(IFunctionItem f, IXPathContext context)
        {
            if (f is CoercedFunction cf
                && cf.GetType() == typeof(CoercedFunction)
                && cf.TargetFunction is UserFunctionReference.BoundUserFunction buf
                && buf.TargetFunction is UserFunction uf
                && uf.GetArity() == 1
                && cf.FunctionItemType is SpecificFunctionType sft
                && sft.GetArity() == 1
                && buf.FunctionItemType.ArgumentTypes.Length == 1)
            {
                return new FusedArity1Caller(cf, buf, uf, context);
            }

            return null;
        }

        /// <summary>
        /// Invoke the function on one item, applying exactly the CoercedFunction argument/result
        /// conversion rules (same checks, same RoleDiagnostic messages). Returns a grounded value.
        /// </summary>
        public ISequence CallOne(IItem item)
        {
            IGroundedValue gVal = (IGroundedValue)item;
            if (argType.Matches(gVal, th))
            {
                args[0] = gVal;
            }
            else
            {
                Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.FUNCTION, bound.Description, 0);
                args[0] = th.ApplyFunctionConversionRules(gVal, argType, role, Loc.NONE);
            }

            IGroundedValue rawResult;
            try
            {
                rawResult = (direct ? target.EvaluateBodyDirect(c2) : target.Call(c2, args)).Materialize();
            }
            catch (Internal.RecursionDepthError)
            {
                // Same conversion the classic UserFunctionCall elaborators apply — this fused
                // path bypasses them, so the stack-guard signal must be converted here.
                throw new XPathException.StackOverflow("Too many nested function calls. May be due to infinite recursion", DAXonErrorCode.SXLM0001, Loc.NONE);
            }

            if (resultType.Matches(rawResult, th))
            {
                return rawResult;
            }
            else
            {
                Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.FUNCTION_RESULT, coerced.Description, 0);
                return th.ApplyFunctionConversionRules(rawResult, resultType, role, Loc.NONE);
            }
        }
    }
}
