////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Functions.HigherOrder;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Transformation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// Implementation of the fn:sum function
    /// </summary>
    internal class Sum : FoldingFunction
    {

        public static Func<Sum> New() => () => new Sum();
        public override Expression MakeFunctionCall(params Expression[] arguments)
        {
            Expression[] newArgs = new Expression[2];
            newArgs[0] = arguments[0];
            if (arguments.Length < 2 || arguments[1] is DefaultedArgumentExpression)
            {
                newArgs[1] = FunctionLiteral.MakeLiteral(Int64Value.ZERO);
                SetArity(2);
            }
            else
            {
                newArgs[1] = arguments[1];
            }

            return base.MakeFunctionCall(newArgs);
        }

        // The elaborated path short-circuits sum(m to n) into a closed form; this interpreted
        // entry is what compile-time pre-evaluation of all-literal arguments and dynamic sum#1
        // calls use. Without the same short-circuit the argument wrapper materialized a literal
        // range before folding — sum(1 to 1000000000) exhausted memory at COMPILE time.
        // Overflow falls through to the generic fold (BigInteger promotion); the zero argument
        // only matters for empty input, which a constructed IntegerRange never is.
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            if (arguments[0] is IntegerRange ir && IntegerRange.TrySum(ir.start, ir.step, ir.end, out long ranged))
            {
                return Int64Value.MakeIntegerValue(ranged);
            }

            ISequenceIterator iter = arguments[0].Iterate();
            if (iter is Expressions.AscendingRangeIterator ari && ari.TryComputeTotal(out long asc))
            {
                return Int64Value.MakeIntegerValue(asc);
            }

            if (iter is Expressions.DescendingRangeIterator dri && dri.TryComputeTotal(out long desc))
            {
                return Int64Value.MakeIntegerValue(desc);
            }

            return RunFold(GetFold(context, TailArguments(arguments)), iter);
        }

        public override Types.ItemType GetResultItemType(Expression[] args)
        {
            TypeHierarchy th = GetRetainedStaticContext().GetConfiguration().GetTypeHierarchy();
            Types.ItemType @base = Atomizer.GetAtomizedItemType(args[0], false, th);
            if (@base.Equals(BuiltInAtomicType.UNTYPED_ATOMIC))
            {
                @base = BuiltInAtomicType.DOUBLE;
            }

            if (Cardinality.AllowsZero(args[0].GetCardinality()))
            {
                if (GetArity() == 1)
                {
                    return Types.Type.GetCommonSuperType(@base, BuiltInAtomicType.INTEGER, th);
                }
                else
                {
                    return Types.Type.GetCommonSuperType(@base, args[1].GetItemType(), th);
                }
            }
            else
            {
                return @base.GetPrimitiveItemType();
            }
        }

        public override int GetCardinality(Expression[] arguments)
        {
            if (GetArity() == 1 || arguments[1].GetCardinality() == 1)
            {
                return StaticProperty.EXACTLY_ONE;
            }
            else
            {
                return StaticProperty.ALLOWS_ZERO_OR_ONE;
            }
        }

        public override IFold GetFold(IXPathContext context, params ISequence[] additionalArguments)
        {
            if (additionalArguments.Length > 0)
            {
                AtomicValue z = (AtomicValue)additionalArguments[0].Head();
                return new SumFold(context, z);
            }
            else
            {
                return new SumFold(context, Int64Value.ZERO);
            }
        }

        public static AtomicValue Total(ISequenceIterator @in, IXPathContext context, ILocation locator)
        {
            try
            {
                SumFold fold = new SumFold(context, null);
                SequenceTool.Supply(@in, (IItemConsumer<IItem>)fold.ProcessItem);
                return (AtomicValue)fold.Result().Head();
            }
            catch (XPathException e)
            {
                throw e.MaybeWithLocation(locator).MaybeWithContext(context);
            }
        }

        public override Elaborator GetElaborator()
        {
            return new SumFnElaborator();
        }

        internal class SumFold : IFold
        {
            private readonly IXPathContext context;
            private readonly AtomicValue zeroValue; // null means empty sequence
            private AtomicValue data;
            private bool atStart = true;
            private readonly ConversionRules rules;
            private readonly StringConverter toDouble;
            // Unboxed accumulators: a run of xs:integer (long), xs:double, or compact xs:decimal
            // values is summed without the per-step Calculator dispatch + boxed result. Any other
            // type (or overflow) materializes the total into `data` and continues on the generic
            // path. The decimal accumulator is a (mantissa, scale) pair; stripping trailing zeros
            // only at the end (BigDecimalValue's constructor) yields the same canonical value the
            // per-step DecimalPlusDecimal + strip sequence produces.
            private long longTotal;
            private bool onLongPath;
            private double dblTotal;
            private bool onDoublePath;
            private long decUnscaled;
            private int decScale;
            private bool onDecimalPath;
            public SumFold(IXPathContext context, AtomicValue zeroValue)
            {
                this.context = context;
                this.zeroValue = zeroValue;
                this.rules = context.GetConfiguration().GetConversionRules();
                this.toDouble = BuiltInAtomicType.DOUBLE.GetStringConverter(rules);
                {
                }
            }

            public virtual void ProcessItem(IItem item)
            {
                AtomicValue next = (AtomicValue)item;
                if (atStart)
                {
                    // A single-item sum returns the item unchanged (derived type annotations
                    // included), so the unboxed paths only engage from the SECOND value on --
                    // arithmetic on two values yields the primitive type anyway.
                    atStart = false;
                    if (next.IsUntypedAtomic())
                    {
                        data = toDouble.Convert(next).AsAtomic();
                        return;
                    }
                    else if (next is NumericValue || next is DayTimeDurationValue || next is YearMonthDurationValue)
                    {
                        data = next;
                        return;
                    }
                    else
                    {
                        throw new XPathException("Input to sum() contains a value of type " + next.PrimitiveType.DisplayName + " which is neither numeric, nor a duration").WithXPathContext(context).WithErrorCode("FORG0006");
                    }
                }

                if (onLongPath)
                {
                    if (next is Int64Value iv)
                    {
                        try
                        {
                            longTotal = checked(longTotal + iv.LongValue());
                            return;
                        }
                        catch (OverflowException)
                        {
                            // fall through to the generic path (promotes to big integer)
                        }
                    }

                    onLongPath = false;
                    data = Int64Value.MakeIntegerValue(longTotal);
                }
                else if (onDoublePath)
                {
                    if (next.IsUntypedAtomic())
                    {
                        dblTotal += ((DoubleValue)toDouble.Convert(next).AsAtomic()).GetDoubleValue();
                        return;
                    }

                    if (next is DoubleValue dv1)
                    {
                        dblTotal += dv1.GetDoubleValue();
                        return;
                    }

                    onDoublePath = false;
                    data = new DoubleValue(dblTotal);
                }
                else if (onDecimalPath)
                {
                    if (next is BigDecimalValue nbv
                        && nbv.GetDecimalValue().TryGetCompactParts(out long nu, out int ns)
                        && Internal.Numerics.BigDecimal.TryAddCompactParts(decUnscaled, decScale, nu, ns, out long dru, out int drs))
                    {
                        decUnscaled = dru;
                        decScale = drs;
                        return;
                    }

                    onDecimalPath = false;
                    data = new BigDecimalValue(Internal.Numerics.BigDecimal.FromCompact(decUnscaled, decScale));
                }
                else if (data is Int64Value d0 && next is Int64Value n0)
                {
                    try
                    {
                        longTotal = checked(d0.LongValue() + n0.LongValue());
                        onLongPath = true;
                        data = null;
                        return;
                    }
                    catch (OverflowException)
                    {
                        // generic path below
                    }
                }
                else if (data is DoubleValue dd)
                {
                    if (next.IsUntypedAtomic())
                    {
                        dblTotal = dd.GetDoubleValue() + ((DoubleValue)toDouble.Convert(next).AsAtomic()).GetDoubleValue();
                        onDoublePath = true;
                        data = null;
                        return;
                    }

                    if (next is DoubleValue nn)
                    {
                        dblTotal = dd.GetDoubleValue() + nn.GetDoubleValue();
                        onDoublePath = true;
                        data = null;
                        return;
                    }
                }
                else if (data is BigDecimalValue db && next is BigDecimalValue nb
                    && db.GetDecimalValue().TryGetCompactParts(out long du, out int ds)
                    && nb.GetDecimalValue().TryGetCompactParts(out long nu0, out int ns0)
                    && Internal.Numerics.BigDecimal.TryAddCompactParts(du, ds, nu0, ns0, out long ru0, out int rs0))
                {
                    decUnscaled = ru0;
                    decScale = rs0;
                    onDecimalPath = true;
                    data = null;
                    return;
                }

                if (data is NumericValue)
                {
                    if (next.IsUntypedAtomic())
                    {
                        next = toDouble.Convert(next).AsAtomic();
                    }
                    else if (!(next is NumericValue))
                    {
                        throw new XPathException("Input to sum() contains a mix of numeric and non-numeric values").WithXPathContext(context).WithErrorCode("FORG0006");
                    }

                    data = ArithmeticExpression.Compute(data, Calculator.PLUS, next, context);
                }
                else if (data is DurationValue)
                {
                    if (!((data is DayTimeDurationValue) || (data is YearMonthDurationValue)))
                    {
                        throw new XPathException("Input to sum() contains a duration that is neither a dayTimeDuration nor a yearMonthDuration").WithXPathContext(context).WithErrorCode("FORG0006");
                    }

                    if (!(next is DurationValue))
                    {
                        throw new XPathException("Input to sum() contains a mix of duration and non-duration values").WithXPathContext(context).WithErrorCode("FORG0006");
                    }

                    data = ((DurationValue)data).Add((DurationValue)next);
                }
                else
                {
                    throw new XPathException("Input to sum() contains a value of type " + data.PrimitiveType.DisplayName + " which is neither numeric, nor a duration").WithXPathContext(context).WithErrorCode("FORG0006");
                }
            }

            public virtual bool IsFinished()
            {
                if (onDoublePath)
                {
                    return double.IsNaN(dblTotal);
                }

                return data is DoubleValue && data.IsNaN();
            }

            public virtual ISequence Result()
            {
                if (atStart)
                {
                    return SequenceTool.ItemOrEmpty(zeroValue);
                }
                else if (onLongPath)
                {
                    return Int64Value.MakeIntegerValue(longTotal);
                }
                else if (onDoublePath)
                {
                    return new DoubleValue(dblTotal);
                }
                else if (onDecimalPath)
                {
                    return new BigDecimalValue(Internal.Numerics.BigDecimal.FromCompact(decUnscaled, decScale));
                }
                else
                {
                    return data;
                }
            }
        }

        internal class SumFnElaborator : ItemElaborator
        {
            public override IItemEvaluator ElaborateForItem()
            {
                SystemFunctionCall fnc = (SystemFunctionCall)GetExpression();
                IPullEvaluator puller = fnc.GetArg(0).MakeElaborator().ElaborateForPull();
                bool defaultSecondArg = fnc.GetArity() < 2 || fnc.GetArg(1) is DefaultedArgumentExpression;
                IItemEvaluator zero = defaultSecondArg ? (context) => Int64Value.ZERO : fnc.GetArg(1).MakeElaborator().ElaborateForItem();

                // sum(m to n) with a statically visible range: evaluate the two bounds as single
                // items and run the closed form (IntegerRange.TrySum) — without constructing the
                // range iterator per call at all. Empty bound or m > n is the empty range (the
                // zero argument); non-Int64 bounds or overflow fall back to the generic fold
                // below, which reproduces the BigInteger promotion.
                if (fnc.GetArg(0) is RangeExpression range
                    && !ErrorExpression.IsContainedIn(range.StartExpression)
                    && !ErrorExpression.IsContainedIn(range.EndExpression))
                {
                    IItemEvaluator loEval = range.StartExpression.MakeElaborator().ElaborateForItem();
                    IItemEvaluator hiEval = range.EndExpression.MakeElaborator().ElaborateForItem();
                    IItemEvaluator genericSum = BuildGenericSum(puller, zero);
                    return (context) =>
                    {
                        IItem lo = loEval.Eval(context);
                        IItem hi = hiEval.Eval(context);
                        if (lo == null || hi == null)
                        {
                            return zero.Eval(context);
                        }

                        if (lo is Int64Value l64 && hi is Int64Value h64)
                        {
                            long m = l64.LongValue();
                            long n = h64.LongValue();
                            if (m > n)
                            {
                                return zero.Eval(context);
                            }

                            if (IntegerRange.TrySum(m, 1, n, out long total))
                            {
                                return Int64Value.MakeIntegerValue(total);
                            }
                        }

                        return genericSum.Eval(context);
                    };
                }

                return BuildGenericSum(puller, zero);
            }

            private static IItemEvaluator BuildGenericSum(IPullEvaluator puller, IItemEvaluator zero)
            {
                return (context) =>
                {
                    ISequenceIterator iter = puller.Iterate(context);

                    if (iter is Text.CodepointIterator cpi)
                    {
                        // sum(string-to-codepoints(...)): fold the raw ints — no Int64Value per
                        // character. Total stays far below long overflow (codepoints <= 0x10FFFF,
                        // count < 2^31), and a sum of xs:integer is the same Int64Value the
                        // generic fold would produce; an empty stream returns the zero argument.
                        Collections.IIntIterator cps = cpi.RawCodepoints;
                        long total = 0;
                        bool any = false;
                        while (cps.MoveNext())
                        {
                            total += cps.Current;
                            any = true;
                        }

                        return any ? Int64Value.MakeIntegerValue(total) : zero.Eval(context);
                    }

                    // sum(m to n): closed-form arithmetic series — no per-step Int64Value.
                    // Overflow (or a partially-consumed iterator) falls through to the generic
                    // fold, which reproduces the BigInteger promotion.
                    if (iter is Expressions.AscendingRangeIterator ari && ari.TryComputeTotal(out long rangeTotal))
                    {
                        return Int64Value.MakeIntegerValue(rangeTotal);
                    }

                    if (iter is Expressions.DescendingRangeIterator dri && dri.TryComputeTotal(out long rangeTotal2))
                    {
                        return Int64Value.MakeIntegerValue(rangeTotal2);
                    }

                    SumFold fold = new SumFold(context, (AtomicValue)zero.Eval(context));
                    for (IItem it; (it = iter.Next()) != null;)
                    {
                        fold.ProcessItem(it);
                    }

                    return fold.Result().Head();
                };
            }
        }
    }
}