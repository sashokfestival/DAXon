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
        private IAtomicType matched0;   // last atomic item type that PASSED the slot's Matches
        private IAtomicType matched1;
        private IAtomicType matchedResult;

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
            return Invoke(acc.Materialize(), (IGroundedValue)item);
        }

        /// <summary>
        /// Same invocation with both arguments as sequences — fn:fold-right's per-item call
        /// is (item, accumulator), so the accumulator lands in slot 1.
        /// </summary>
        public ISequence CallTwoSeq(ISequence a0, ISequence a1)
        {
            return Invoke(a0.Materialize(), a1.Materialize());
        }

        private ISequence Invoke(IGroundedValue g0, IGroundedValue g1)
        {
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

            if (MatchesMemo(argType0, g0, ref matched0))
            {
                args[0] = g0;
            }
            else
            {
                Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.FUNCTION, bound.Description, 0);
                args[0] = th.ApplyFunctionConversionRules(g0, argType0, role, Loc.NONE);
            }

            if (MatchesMemo(argType1, g1, ref matched1))
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

            if (MatchesMemo(resultType, rawResult, ref matchedResult))
            {
                return rawResult;
            }
            else
            {
                Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.FUNCTION_RESULT, coerced.Description, 0);
                return th.ApplyFunctionConversionRules(rawResult, resultType, role, Loc.NONE);
            }
        }

        /// <summary>
        /// Long-accumulator lane for integer fold bodies, compiled once from a closed expression
        /// set: integer literal, bare parameter reference, integer +,-,*,idiv,mod, and transparent
        /// atomize/check wrappers. Each step mirrors the Int64Value promotion guards verbatim and
        /// reports false instead of deviating — the driver replays that step on the boxed path, so
        /// BigInteger promotion and error semantics stay byte-identical. Callers must feed only
        /// plain xs:integer values (no subtypes: a leaf that returns its input unchanged would
        /// otherwise drop the subtype label the boxed path preserves).
        /// </summary>
        internal delegate bool LongBody(long p0, long p1, out long result);

        private LongBody longLane;
        private bool longLaneResolved;

        internal LongBody TryLongLane()
        {
            if (!longLaneResolved)
            {
                longLaneResolved = true;
                Expressions.Instructions.UserFunctionParameter[] defs = target.GetParameterDefinitions();
                if (direct
                    && defs != null && defs.Length == 2
                    && (coerced == null
                        || (IntegerAlwaysMatches(argType0) && IntegerAlwaysMatches(argType1) && IntegerAlwaysMatches(resultType))))
                {
                    longLane = CompileLong(target.GetBody(), defs[0], defs[1]);
                }
            }

            return longLane;
        }

        // A plain single xs:integer either always passes this slot's Matches or never does — the
        // verdict is type-based only (see MatchesMemo) — so one probe value decides for the lane.
        private bool IntegerAlwaysMatches(SequenceType t)
        {
            return t.Matches(Int64Value.MakeIntegerValue(0), th);
        }

        // Int64Value.IsLong() verbatim — the Times/Idiv/Mod promotion trigger. Sign-extension of
        // an in-range negative is all ones, so -1 passes as "fits in 32 bits" too.
        private static bool OutsideInt(long v)
        {
            long top = v >> 31;
            return top != 0 && top != -1;
        }

        // Top-4-bits outside {0,0xF} is the Plus/Minus promotion trigger verbatim. Internal:
        // aggregate fusions (fn:sum) use the same conservative bail-before-overflow bound.
        internal static bool NearOverflow(long v)
        {
            long top = (v >> 60) & 0xf;
            return top != 0 && top != 0xf;
        }

        /// <summary>
        /// One-parameter entry for aggregate fusions (fn:sum over a mapped range): p0 is the
        /// given binding's value, p1 is unused.
        /// </summary>
        internal static LongBody CompileLongFor(Expression body, object binding0)
        {
            return CompileLong(body, binding0, null);
        }

        // Marker binding: a ContextItemExpression in the body reads p0. Only the simple-map
        // entry below passes it, so function-body lanes still reject `.` (their context is
        // the caller's, not a lane parameter).
        private static readonly object ContextBinding = new object();

        /// <summary>
        /// One-parameter entry for the simple-map form (`RANGE ! body`): the body sees each
        /// mapped value as the context item, delivered as p0.
        /// </summary>
        internal static LongBody CompileLongForContext(Expression body)
        {
            return CompileLong(body, ContextBinding, null);
        }

        private static LongBody CompileLong(Expression e, object binding0, object binding1)
        {
            // Transparent wrappers: atomizers are identity for the atomic values the lane feeds;
            // an untyped-converter/rejector only acts on untypedAtomic, which the lane never feeds;
            // a checker that a plain single xs:integer always satisfies can never fire.
            while (true)
            {
                if (e is Atomizer atomizer)
                {
                    e = atomizer.BaseExpression;
                }
                else if (e is SingletonAtomizer sa)
                {
                    e = sa.BaseExpression;
                }
                else if (e is UntypedSequenceConverter usc)
                {
                    e = usc.BaseExpression;
                }
                else if (e is ItemChecker ic
                         && (ReferenceEquals(ic.GetRequiredType(), BuiltInAtomicType.INTEGER)
                             || ReferenceEquals(ic.GetRequiredType(), BuiltInAtomicType.ANY_ATOMIC)
                             || ic.GetRequiredType() is NumericType))
                {
                    e = ic.BaseExpression;
                }
                else if (e is CardinalityChecker cc && cc.RequiredCardinality != StaticProperty.EMPTY)
                {
                    e = cc.BaseExpression;
                }
                else
                {
                    break;
                }
            }

            if (e is Literal lit)
            {
                if (lit.GroundedValue is Int64Value c && ReferenceEquals(c.GetItemType(), BuiltInAtomicType.INTEGER))
                {
                    long cv = c.LongValue();
                    return (long p0, long p1, out long r) => { r = cv; return true; };
                }

                return null;
            }

            if (e is LocalVariableReference lvr)
            {
                object b = lvr.GetBinding();
                if (b != null && ReferenceEquals(b, binding0))
                {
                    return (long p0, long p1, out long r) => { r = p0; return true; };
                }

                if (b != null && ReferenceEquals(b, binding1))
                {
                    return (long p0, long p1, out long r) => { r = p1; return true; };
                }

                return null;
            }

            if (e is ContextItemExpression)
            {
                if (ReferenceEquals(binding0, ContextBinding))
                {
                    return (long p0, long p1, out long r) => { r = p0; return true; };
                }

                return null;
            }

            if (e is ArithmeticExpression ae)
            {
                return CompileLongArithmetic(ae, binding0, binding1);
            }

            return null;
        }

        private static LongBody CompileLongArithmetic(ArithmeticExpression ae, object binding0, object binding1)
        {
            LongBody lhs = CompileLong(ae.GetLhsExpression(), binding0, binding1);
            if (lhs == null)
            {
                return null;
            }

            LongBody rhs = CompileLong(ae.GetRhsExpression(), binding0, binding1);
            if (rhs == null)
            {
                return null;
            }

            // Any* calculators dispatch on runtime types; the lane only ever feeds plain
            // integers, for which they resolve to the Integer*Integer twins.
            Calculator calc = ae.GetCalculator();
            if (calc is Calculator.IntegerPlusInteger || calc is Calculator.AnyPlusAny)
            {
                return (long p0, long p1, out long r) =>
                {
                    r = 0;
                    if (!lhs(p0, p1, out long a) || !rhs(p0, p1, out long b)
                        || NearOverflow(a) || NearOverflow(b))
                    {
                        return false;
                    }

                    r = a + b;
                    return true;
                };
            }

            if (calc is Calculator.IntegerMinusInteger || calc is Calculator.AnyMinusAny)
            {
                return (long p0, long p1, out long r) =>
                {
                    r = 0;
                    if (!lhs(p0, p1, out long a) || !rhs(p0, p1, out long b)
                        || NearOverflow(a) || NearOverflow(b))
                    {
                        return false;
                    }

                    r = a - b;
                    return true;
                };
            }

            if (calc is Calculator.IntegerTimesInteger || calc is Calculator.AnyTimesAny)
            {
                return (long p0, long p1, out long r) =>
                {
                    r = 0;
                    if (!lhs(p0, p1, out long a) || !rhs(p0, p1, out long b)
                        || OutsideInt(a) || OutsideInt(b))
                    {
                        return false;
                    }

                    r = a * b;
                    return true;
                };
            }

            if (calc is Calculator.IntegerIdivInteger || calc is Calculator.AnyIdivAny)
            {
                return (long p0, long p1, out long r) =>
                {
                    r = 0;
                    if (!lhs(p0, p1, out long a) || !rhs(p0, p1, out long b)
                        || b == 0 || OutsideInt(a) || OutsideInt(b))
                    {
                        return false;
                    }

                    r = a / b;
                    return true;
                };
            }

            if (calc is Calculator.IntegerModInteger || calc is Calculator.AnyModAny)
            {
                return (long p0, long p1, out long r) =>
                {
                    r = 0;
                    if (!lhs(p0, p1, out long a) || !rhs(p0, p1, out long b)
                        || b == 0 || OutsideInt(a) || OutsideInt(b))
                    {
                        return false;
                    }

                    r = a % b;
                    return true;
                };
            }

            return null;
        }

        // Verdict memo per slot: for a singleton atomic the Matches verdict depends only on the
        // value's item type (plain-type matching is subtyping on GetItemType(); node/function tests
        // never match an atomic, so a pass is never memoized for them), so the last PASSING type
        // short-circuits the SequenceType.Matches walk on every further iteration.
        private bool MatchesMemo(SequenceType req, IGroundedValue g, ref IAtomicType memo)
        {
            if (g is AtomicValue atomic)
            {
                IAtomicType t = atomic.GetItemType();
                if (ReferenceEquals(t, memo))
                {
                    return true;
                }

                if (req.Matches(g, th))
                {
                    memo = t;
                    return true;
                }

                return false;
            }

            return req.Matches(g, th);
        }
    }
}
