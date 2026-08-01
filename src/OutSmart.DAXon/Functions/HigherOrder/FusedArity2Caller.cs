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
    /// Two-argument sibling of <see cref="FusedArity1Caller"/> for fn:fold-left's per-item call
    /// (accumulator, item). Same reuse contract: the clean context and the argument array are
    /// allocated once and rebound per call, and every result is materialized before the next call.
    /// One extra gate beyond the arity-1 shape checks: a MATERIALIZED sequence can still contain a
    /// function item whose closure captures the (reused) stack frame, and fold-left — unlike
    /// filter/sort-keys — returns the call result to the caller. So TryMake additionally requires
    /// the function body's static item type to be plain (atomic/union): then no function item can
    /// appear in a result, and nothing can reference the frame after Materialize(). Any other
    /// shape keeps the general DynamicCall path.
    /// </summary>
    internal sealed class FusedArity2Caller
    {
        private readonly CoercedFunction coerced;   // null for the bare BoundUserFunction shape
        private readonly UserFunctionReference.BoundUserFunction bound;
        private readonly UserFunction target;
        private readonly XPathContextMajor c2;
        private readonly ISequence[] args;
        private readonly bool direct;
        private readonly SequenceType argType0;
        private readonly SequenceType argType1;
        private readonly SequenceType resultType;
        private readonly TypeHierarchy th;

        private FusedArity2Caller(CoercedFunction coerced, UserFunctionReference.BoundUserFunction bound,
            UserFunction target, IXPathContext context)
        {
            this.coerced = coerced;
            this.bound = bound;
            this.target = target;
            if (coerced != null)
            {
                this.argType0 = bound.FunctionItemType.ArgumentTypes[0];
                this.argType1 = bound.FunctionItemType.ArgumentTypes[1];
                this.resultType = ((SpecificFunctionType)coerced.FunctionItemType).ResultType;
                this.th = context.GetConfiguration().GetTypeHierarchy();
            }
            this.c2 = target.MakeNewContext(context, bound);
            if (bound.BoundComponent != null)
            {
                c2.SetCurrentComponent(bound.BoundComponent);
            }

            // Exact UserFunction (a subclass, e.g. MemoFunction, may override Call): install one
            // stack frame up front and per call just rebind the two argument slots and evaluate
            // the body — Call would rebuild a StackFrame per invocation. Local let-slots beyond
            // the two params are always assigned before they are read, so stale values from the
            // previous call are unobservable. Tail-call bodies are excluded: the TailCallLoop
            // machinery replaces the context's frame, breaking the slots-array identity.
            this.direct = target.GetType() == typeof(UserFunction)
                && !target.ContainsTailCalls()
                && !target.IsTailRecursive()
                && target.DeclaredStreamability == FunctionStreamability.UNCLASSIFIED;
            if (direct)
            {
                Expressions.Instructions.SlotManager map = target.GetStackFrameMap();
                this.args = new ISequence[map.NumberOfVariables];
                c2.SetStackFrame(map, args);
            }
            else
            {
                this.args = new ISequence[2];
            }
        }

        public static FusedArity2Caller TryMake(IFunctionItem f, IXPathContext context)
        {
            // Bare BoundUserFunction: an inline function whose signature already matches the
            // expected function type gets no CoercedFunction wrapper, and BoundUserFunction.Call
            // applies no conversions — the fused call is just (reused context, args, Materialize).
            if (f is UserFunctionReference.BoundUserFunction buf0
                && buf0.GetType() == typeof(UserFunctionReference.BoundUserFunction)
                && buf0.TargetFunction is UserFunction uf0
                && uf0.GetArity() == 2
                && buf0.GetArity() == 2
                && uf0.GetBody() != null
                && uf0.GetBody().GetItemType() is IPlainType)
            {
                return new FusedArity2Caller(null, buf0, uf0, context);
            }

            if (f is CoercedFunction cf
                && cf.GetType() == typeof(CoercedFunction)
                && cf.TargetFunction is UserFunctionReference.BoundUserFunction buf
                && buf.TargetFunction is UserFunction uf
                && uf.GetArity() == 2
                && cf.FunctionItemType is SpecificFunctionType sft
                && sft.GetArity() == 2
                && buf.FunctionItemType.ArgumentTypes.Length == 2
                && uf.GetBody() != null
                && uf.GetBody().GetItemType() is IPlainType)
            {
                return new FusedArity2Caller(cf, buf, uf, context);
            }

            return null;
        }

        /// <summary>
        /// Invoke the function on (accumulator, item), applying exactly the CoercedFunction
        /// argument/result conversion rules (none for the bare shape, matching
        /// BoundUserFunction.Call). Returns a grounded (materialized) value.
        /// </summary>
        // Same description the classic UserFunctionCall elaborators apply — this fused path
        // bypasses them, so the stack-guard signal must be described here.
        private static Internal.RecursionDepthError StackOverflowError(Internal.RecursionDepthError e)
        {
            return e.Describe("Too many nested function calls. May be due to infinite recursion", DAXonErrorCode.SXLM0001, Loc.NONE);
        }

        public ISequence CallTwo(ISequence acc, IItem item)
        {
            IGroundedValue g0 = acc.Materialize();
            IGroundedValue g1 = (IGroundedValue)item;
            if (coerced == null)
            {
                args[0] = g0;
                args[1] = g1;
                try
                {
                    if (direct)
                    {
                        return target.EvaluateBodyDirect(c2).Materialize();
                    }

                    return target.Call(c2, args).Materialize();
                }
                catch (Internal.RecursionDepthError e) when (!e.Described)
                {
                    throw StackOverflowError(e);
                }
            }

            if (argType0.Matches(g0, th))
            {
                args[0] = g0;
            }
            else
            {
                Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.FUNCTION, bound.Description, 0);
                args[0] = th.ApplyFunctionConversionRules(g0, argType0, role, Loc.NONE);
            }

            if (argType1.Matches(g1, th))
            {
                args[1] = g1;
            }
            else
            {
                Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.FUNCTION, bound.Description, 1);
                args[1] = th.ApplyFunctionConversionRules(g1, argType1, role, Loc.NONE);
            }

            IGroundedValue rawResult;
            try
            {
                rawResult = (direct ? target.EvaluateBodyDirect(c2) : target.Call(c2, args)).Materialize();
            }
            catch (Internal.RecursionDepthError e) when (!e.Described)
            {
                throw StackOverflowError(e);
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
