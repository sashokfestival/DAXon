////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Misc Java stdlib types Saxon references but we don't yet shim individually.

using System;
using System.Collections.Generic;
using System.IO;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Values;

// Saxon-internal stub namespaces — sub-packages permanently excluded for now.
// Stubs only what's needed for top-level references to resolve.

namespace OutSmart.DAXon.Types
{
    // Stub for excluded Converter (cascade-pulled-many-errors when re-included). Most subclasses
    // (StringConverter, JPConverter etc) just need a base type.
    public abstract class Converter
    {
        // ConversionRules backing field + ctor + getter — many subclasses use
        // `base(rules)` and then call `GetConversionRules()` in method bodies.
        // Use `object` for the field so OutSmart.DAXon.Internal doesn't need to know the Saxon
        // ConversionRules type. Subclasses cast on use.
        protected object conversionRules;
        // Phase 5: GetTargetType — callers chain .Preprocess(...) on the target type.
        public virtual ISimpleType TargetType => null;
        protected Converter() { }
        protected Converter(object rules) { this.conversionRules = rules; }
        // Cast the stored ConversionRules back to its real type — callers in Saxon code use it as `ConversionRules`.
        public OutSmart.DAXon.Lib.ConversionRules GetConversionRules() => (OutSmart.DAXon.Lib.ConversionRules)conversionRules;
        public void SetConversionRules(object rules) { this.conversionRules = rules; }
        // Phase 5: Convert/ConvertString return IConversionResult; callers assign directly.
        // (14 sites in Cast/CastableExpression/UnionConstructorFunction etc.)
        public virtual IConversionResult Convert(object value) => null;
        public virtual IConversionResult ConvertString(object input) => null;
        // Static 3-arg overload `Convert(value, targetType, rules)`. Was a hollow stub `=> value` that returned
        // the value UNCONVERTED, so callers like fn:min/fn:max's final numeric promotion (Minimax converts the
        // retained min/max to the widest encountered type) silently kept the original type — min((5,5.0e0)) came
        // back xs:integer instead of xs:double. Route through the real converter (the same GetConverter path
        // xs:double() uses); fall back to the value unchanged only if the args aren't the expected types or no
        // converter exists (so it is never worse than the old no-op).
        public static object Convert(object value, object targetType, object rules)
        {
            if (value is AtomicValue av && targetType is IAtomicType tt && rules is OutSmart.DAXon.Lib.ConversionRules cr)
            {
                Converter conv = cr.GetConverter(av.GetItemType(), tt);
                IConversionResult res = conv == null ? null : conv.Convert(av);
                if (res != null)
                {
                    return res.AsAtomic();
                }
            }
            return value;
        }
        // Phase 7.8: 2-arg instance variant for callers like phaseTwo.Convert(value, in).
        public virtual IConversionResult Convert(object value, object @in) => (IConversionResult)value;
        public virtual bool IsAlwaysSuccessful() => false;
        public virtual bool IsPromoter() => false; // Saxon default; only PromotingConverter overrides to true (ASC.Export reads this)
        public virtual Converter SetNamespaceResolver(object resolver) => this;
        // Cast a finite double/float to xs:integer (unbounded). Out-of-Int64-range values must become an
        // arbitrary-precision BigIntegerValue, not wrap to Int64.MinValue via (long)d. BigDecimal(d).ToBigInteger()
        // truncates the fractional part toward zero, matching the cast-to-integer rule (Saxon: DoubleValue).
        internal static OutSmart.DAXon.Values.IntegerValue DoubleToIntegerValue(double d)
        {
            if (d > long.MaxValue || d < long.MinValue)
                return new OutSmart.DAXon.Values.BigIntegerValue(new OutSmart.DAXon.Internal.Numerics.BigDecimal(d).ToBigInteger());
            return new Int64Value((long)d);
        }
        // Runtime 2026-06-10: the *ToInteger family was HOLLOW (inherited Convert=>null) -> xs:integer(7.9)
        // / xs:integer(true()) NRE-d CastExpression.PreEvaluate via MakeLiteral(null) (probe_conv2).
        // Java semantics: cast to xs:integer truncates toward zero = NumericValue.longValue().
        public class FloatToInteger : Converter
        {
            public static readonly FloatToInteger INSTANCE = new FloatToInteger();
            public override IConversionResult Convert(object value) { double d = ((NumericValue)value).GetDoubleValue(); if (double.IsNaN(d)) return new ValidationFailure("Cannot convert float NaN to an integer", "FOCA0002"); if (double.IsInfinity(d)) return new ValidationFailure("Cannot convert float infinity to an integer", "FOCA0002"); return (IConversionResult)DoubleToIntegerValue(d); }
        }
        public class BooleanToInteger : Converter
        {
            public static readonly BooleanToInteger INSTANCE = new BooleanToInteger();
            public override IConversionResult Convert(object value) => (IConversionResult)new Int64Value(((BooleanValue)value).GetBooleanValue() ? 1L : 0L);
        }
        public class DoubleToInteger : Converter
        {
            public static readonly DoubleToInteger INSTANCE = new DoubleToInteger();
            public override IConversionResult Convert(object value) { double d = ((NumericValue)value).GetDoubleValue(); if (double.IsNaN(d)) return new ValidationFailure("Cannot convert double NaN to an integer", "FOCA0002"); if (double.IsInfinity(d)) return new ValidationFailure("Cannot convert double infinity to an integer", "FOCA0002"); return (IConversionResult)DoubleToIntegerValue(d); }
        }
        // xs:integer(xs:decimal) truncates toward zero and MUST stay exact: routing through LongValue()
        // (which goes via double) loses precision above 2^53, e.g. xs:integer(12345678901234567.3) gave
        // ...568 not ...567. BigDecimal.ToBigInteger() drops the fraction exactly; MakeIntegerValue then
        // picks Int64Value or BigIntegerValue by magnitude (matches Saxon's BigIntegerValue(...toBigInteger)).
        public class DecimalToInteger : Converter
        {
            public static readonly DecimalToInteger INSTANCE = new DecimalToInteger();
            public override IConversionResult Convert(object value) => (IConversionResult)OutSmart.DAXon.Values.IntegerValue.MakeIntegerValue(((NumericValue)value).GetDecimalValue().ToBigInteger());
        }
        public class IntegerToFloat : Converter
        {
            public static readonly IntegerToFloat INSTANCE = new IntegerToFloat();
            public override IConversionResult Convert(object value) => (IConversionResult)new FloatValue((float)((NumericValue)value).GetDoubleValue());
        }
        public class IntegerToDouble : Converter
        {
            public static readonly IntegerToDouble INSTANCE = new IntegerToDouble();
        }
        // REMOVED 2026-05-26: nested Converter.StringToDouble conflicts with the real
        // OutSmart.DAXon.Types.StringToDouble class (poc/output/full/StringToDouble.cs).
        // The bare `StringToDouble` reference in Configuration.cs and ItemType.cs was
        // resolving to this nested stub instead of the real class, causing CS1503.
        // public class StringToDouble : Converter { public static readonly StringToDouble INSTANCE = new StringToDouble(); }
        // Runtime 2026-06-10: StringToFloat was hollow -> xs:float(string) NRE (probe_conv2). Java: parse with INF/NaN forms.
        public class StringToFloat : Converter
        {
            public static readonly StringToFloat INSTANCE = new StringToFloat();
            public override IConversionResult Convert(object value) { string s = ((AtomicValue)value).GetStringValue().Trim(); float f; switch (s) { case "INF": case "+INF": f = float.PositiveInfinity; break; case "-INF": f = float.NegativeInfinity; break; case "NaN": f = float.NaN; break; default: f = float.Parse(s, System.Globalization.CultureInfo.InvariantCulture); break; } return (IConversionResult)new FloatValue(f); }
        }
        public class StringToInteger : Converter
        {
            public static readonly StringToInteger INSTANCE = new StringToInteger();
        }
        public class StringToBoolean : Converter
        {
            public static readonly StringToBoolean INSTANCE = new StringToBoolean();
        }
        public class StringToDecimal : Converter
        {
            public static readonly StringToDecimal INSTANCE = new StringToDecimal();
        }

        // Runtime 2026-06-18: TwoPhaseConverter was a HOLLOW stub (discarded its phases; Convert=>null) -> every cast
        // routed through it NRE-d at CastExpression.PreEvaluate. ConversionRules dispatches date->gYear/gYearMonth/gMonth/
        // gDay/gMonthDay as a two-phase DATE->DATE_TIME->gXxx, plus subtype-via-primitive casts. Faithful Java
        // (net.sf.saxon.type.Converter.TwoPhaseConverter): run phaseOne, then phaseTwo on the intermediate result.
        public class TwoPhaseConverter : Converter
        {
            private readonly Converter phaseOne;
            private readonly Converter phaseTwo;
            public TwoPhaseConverter() { }
            public TwoPhaseConverter(Converter a, Converter b) { phaseOne = a; phaseTwo = b; }
            public static TwoPhaseConverter MakeTwoPhaseConverter(Converter a, Converter b) => new TwoPhaseConverter(a, b);
            public static Converter MakeTwoPhaseConverter(object a, object b, object c) => new TwoPhaseConverter();
            // 4-arg (inputType, viaType, outputType, rules): resolve each phase from the ConversionRules, exactly like the
            // upstream factory `new TwoPhaseConverter(rules.getConverter(in,via), rules.getConverter(via,out))`.
            public static Converter MakeTwoPhaseConverter(object inputType, object viaType, object outputType, object rules)
            {
                // Reflective call (NOT dynamic): GetConverter's params are IAtomicType; dynamic binding of object-typed
                // BuiltInAtomicType args fails ("invalid arguments"), but Reflection.Invoke binds them by assignability.
                System.Reflection.MethodInfo gc = null;
                foreach (var mi in rules.GetType().GetMethods())
                {
                    if (mi.Name == "GetConverter" && mi.GetParameters().Length == 2) { gc = mi; break; }
                }
                Converter p1 = (Converter)gc.Invoke(rules, new object[] { inputType, viaType });
                Converter p2 = (Converter)gc.Invoke(rules, new object[] { viaType, outputType });
                return new TwoPhaseConverter(p1, p2);
            }
            public override IConversionResult Convert(object value)
            {
                if (phaseOne == null || phaseTwo == null) { return null; }
                object temp = phaseOne.Convert(value);
                if (temp == null) { return null; }
                // Short-circuit a ValidationFailure from phase one (don't feed it to phase two).
                if (temp.GetType().Name == "ValidationFailure") { return (IConversionResult)temp; }
                return phaseTwo.Convert(temp);
            }
            public override Converter SetNamespaceResolver(object resolver)
            {
                if (phaseOne == null || phaseTwo == null) { return this; }
                return new TwoPhaseConverter(phaseOne.SetNamespaceResolver(resolver), phaseTwo.SetNamespaceResolver(resolver));
            }
        }
        // Runtime 2026-06-10: IdentityConverter inherited the hollow base Convert(object)=>null -> every
        // identity conversion (e.g. xsl:attribute string content through ASC) returned null -> .AsAtomic() NRE.
        // Java IdentityConverter.convert returns the input unchanged.
        public class IdentityConverter : Converter
        {
            public static readonly IdentityConverter INSTANCE = new IdentityConverter();
            public override IConversionResult Convert(object value) => (IConversionResult)value;
        }
        // Phase 5: nested-class form of converter pairs (some Saxon code references
        // `Converter.FloatToDecimal.INSTANCE` — the nested type with an INSTANCE field).
        // Cast float/double -> decimal must give the EXACT value (upstream: new BigDecimalValue(floatValue)),
        // not GetDecimalValue()'s BigDecimal.ValueOf (shortest, ~17 sig digits). The rounded form broke map
        // "same key": (D cast as xs:decimal) rounded away from D's exact value, so the decimal lookup key no
        // longer compared equal to the stored float/double key (same-key-008). BigDecimalValue(double) uses
        // new BigDecimal(d) — the full-precision constructor.
        public class FloatToDecimal : Converter
        {
            public static readonly FloatToDecimal INSTANCE = new FloatToDecimal();
            public override IConversionResult Convert(object value) { double d = ((NumericValue)value).GetDoubleValue(); if (double.IsNaN(d)) return new ValidationFailure("Cannot convert float NaN to a decimal", "FOCA0002"); if (double.IsInfinity(d)) return new ValidationFailure("Cannot convert float infinity to a decimal", "FOCA0002"); return new BigDecimalValue(d); }
        }
        public class DoubleToDecimal : Converter
        {
            public static readonly DoubleToDecimal INSTANCE = new DoubleToDecimal();
            public override IConversionResult Convert(object value) { double d = ((NumericValue)value).GetDoubleValue(); if (double.IsNaN(d)) return new ValidationFailure("Cannot convert double NaN to a decimal", "FOCA0002"); if (double.IsInfinity(d)) return new ValidationFailure("Cannot convert double infinity to a decimal", "FOCA0002"); return new BigDecimalValue(d); }
        }
        public class IntegerToDecimal : Converter
        {
            public static readonly IntegerToDecimal INSTANCE = new IntegerToDecimal();
            public override IConversionResult Convert(object value) => new BigDecimalValue(((NumericValue)value).GetDecimalValue());
        }
        public class NumericToDecimal : Converter
        {
            public static readonly NumericToDecimal INSTANCE = new NumericToDecimal();
            public override IConversionResult Convert(object value) => new BigDecimalValue(((NumericValue)value).GetDecimalValue());
        }
        public class BooleanToDecimal : Converter
        {
            public static readonly BooleanToDecimal INSTANCE = new BooleanToDecimal();
            public override IConversionResult Convert(object value) => new BigDecimalValue(((BooleanValue)value).GetBooleanValue() ? 1.0 : 0.0);
        }
        public class DecimalToFloat : Converter
        {
            public static readonly DecimalToFloat INSTANCE = new DecimalToFloat();
        }
        public class DecimalToDouble : Converter
        {
            public static readonly DecimalToDouble INSTANCE = new DecimalToDouble();
        }
        public class FloatToDouble : Converter { public static readonly FloatToDouble INSTANCE = new FloatToDouble(); }
        public class DoubleToFloat : Converter { public static readonly DoubleToFloat INSTANCE = new DoubleToFloat(); }
        public class IntegerToString : Converter
        {
            public static readonly IntegerToString INSTANCE = new IntegerToString();
        }
        public class DecimalToString : Converter
        {
            public static readonly DecimalToString INSTANCE = new DecimalToString();
        }
        public class FloatToString : Converter { public static readonly FloatToString INSTANCE = new FloatToString(); }
        public class DoubleToString : Converter
        {
            public static readonly DoubleToString INSTANCE = new DoubleToString();
        }
        public class BooleanToString : Converter
        {
            public static readonly BooleanToString INSTANCE = new BooleanToString();
        }
        public class BooleanToFloat : Converter
        {
            public static readonly BooleanToFloat INSTANCE = new BooleanToFloat();
            public override IConversionResult Convert(object value) => (IConversionResult)new FloatValue(((BooleanValue)value).GetBooleanValue() ? 1.0f : 0.0f);
        } // was a hollow stub (no Convert override) -> base `=> null` -> NRE on `xs:boolean cast as xs:float`
        public class BooleanToDouble : Converter
        {
            public static readonly BooleanToDouble INSTANCE = new BooleanToDouble();
            public override IConversionResult Convert(object value) => (IConversionResult)new DoubleValue(((BooleanValue)value).GetBooleanValue() ? 1.0 : 0.0);
        }
        // Runtime 2026-06-18: the temporal-cast converters below were HOLLOW (inherited Convert=>null) -> casts like
        // xs:dateTime(xs:date(..)), xs:date/xs:time/xs:gYearMonth/xs:gYear(xs:dateTime(..)) NRE-d CastExpression.PreEvaluate
        // via Literal.MakeLiteral(null). Faithful Java (net.sf.saxon.type.Converter): the value is rebuilt in the target
        // temporal type from the source components (constructed directly via the engine value ctors).
        public class DateToDateTime : Converter
        {
            public static readonly DateToDateTime INSTANCE = new DateToDateTime();
            public override IConversionResult Convert(object value) => (IConversionResult)((DateValue)value).ToDateTime();
        }
        public class DateTimeToTime : Converter
        {
            public static readonly DateTimeToTime INSTANCE = new DateTimeToTime();
            public override IConversionResult Convert(object value) { var dt = (DateTimeValue)value; byte hour = dt.Hour, minute = dt.Minute, second = dt.Second; int nano = dt.Nanosecond, tz = dt.TimezoneInMinutes; return (IConversionResult)new TimeValue(hour, minute, second, nano, tz, BuiltInAtomicType.TIME); }
        }
        public class DateTimeToDate : Converter
        {
            public static readonly DateTimeToDate INSTANCE = new DateTimeToDate();
            public override IConversionResult Convert(object value) { var dt = (DateTimeValue)value; int year = dt.Year; byte month = dt.Month, day = dt.Day; int tz = dt.TimezoneInMinutes; bool xsd10 = dt.IsXsd10Rules(); return (IConversionResult)new DateValue(year, month, day, tz, xsd10); }
        }
        public class DateTimeToGYearMonth : Converter
        {
            public static readonly DateTimeToGYearMonth INSTANCE = new DateTimeToGYearMonth();
            public override IConversionResult Convert(object value) { var dt = (DateTimeValue)value; int year = dt.Year; byte month = dt.Month; int tz = dt.TimezoneInMinutes; bool xsd10 = dt.IsXsd10Rules(); return (IConversionResult)new GYearMonthValue(year, month, tz, xsd10); }
        }
        public class DateTimeToGYear : Converter
        {
            public static readonly DateTimeToGYear INSTANCE = new DateTimeToGYear();
            public override IConversionResult Convert(object value) { var dt = (DateTimeValue)value; int year = dt.Year; int tz = dt.TimezoneInMinutes; bool xsd10 = dt.IsXsd10Rules(); return (IConversionResult)new GYearValue(year, tz, xsd10); }
        }
        // Runtime 2026-06-18: was HOLLOW (Convert=>null) -> xs:gMonthDay(xs:dateTime(..)) NRE'd via MakeLiteral(null)
        // once GMonthDayValue was re-included. Faithful Java Converter.DateTimeToGMonthDay: new GMonthDayValue(month,day,tz).
        // (The sibling DateTime/Date -> gYear/gMonth/gDay/gYearMonth converters remain hollow -- separate latent gap.)
        public class DateTimeToGMonthDay : Converter
        {
            public static readonly DateTimeToGMonthDay INSTANCE = new DateTimeToGMonthDay();
            public override IConversionResult Convert(object value) { var dt = (DateTimeValue)value; byte month = dt.Month; byte day = dt.Day; int tz = dt.TimezoneInMinutes; return (IConversionResult)new GMonthDayValue(month, day, tz); }
        }
        public class DateTimeToGMonth : Converter
        {
            public static readonly DateTimeToGMonth INSTANCE = new DateTimeToGMonth();
            public override IConversionResult Convert(object value) { var dt = (DateTimeValue)value; byte month = dt.Month; int tz = dt.TimezoneInMinutes; return (IConversionResult)new GMonthValue(month, tz); }
        }
        public class DateTimeToGDay : Converter
        {
            public static readonly DateTimeToGDay INSTANCE = new DateTimeToGDay();
            public override IConversionResult Convert(object value) { var dt = (DateTimeValue)value; byte day = dt.Day; int tz = dt.TimezoneInMinutes; return (IConversionResult)new GDayValue(day, tz); }
        }
        // PHANTOM stubs (kept hollow ON PURPOSE): Saxon 12.9 has NO dedicated Date->gXxx converter class. Casting xs:date
        // to a gXxx type is dispatched as a TwoPhaseConverter(DateToDateTime, DateTimeToGXxx), so these names are never
        // instantiated by ConversionRules.GetConverter (verified: zero refs outside this file). Implementing DateToDateTime
        // + the DateTimeToGXxx family above makes the real xs:gYear(xs:date(..)) etc. casts work through the two-phase path.
        public class DateToGYearMonth : Converter
        {
            public static readonly DateToGYearMonth INSTANCE = new DateToGYearMonth();
        }
        public class DateToGYear : Converter { public static readonly DateToGYear INSTANCE = new DateToGYear(); }
        public class DateToGMonthDay : Converter
        {
            public static readonly DateToGMonthDay INSTANCE = new DateToGMonthDay();
        }
        public class DateToGMonth : Converter { public static readonly DateToGMonth INSTANCE = new DateToGMonth(); }
        public class DateToGDay : Converter { public static readonly DateToGDay INSTANCE = new DateToGDay(); }
        // Runtime 2026-06-18: binary cross-casts were HOLLOW -> xs:hexBinary(xs:base64Binary(..)) / reverse NRE-d.
        // Faithful Java: new HexBinaryValue(base64.getBinaryValue()) / new Base64BinaryValue(hex.getBinaryValue()).
        public class Base64BinaryToHexBinary : Converter
        {
            public static readonly Base64BinaryToHexBinary INSTANCE = new Base64BinaryToHexBinary();
            public override IConversionResult Convert(object value) { byte[] b = ((Base64BinaryValue)value).BinaryValue; return (IConversionResult)new HexBinaryValue(b); }
        }
        public class HexBinaryToBase64Binary : Converter
        {
            public static readonly HexBinaryToBase64Binary INSTANCE = new HexBinaryToBase64Binary();
            public override IConversionResult Convert(object value) { byte[] b = ((HexBinaryValue)value).BinaryValue; return (IConversionResult)new Base64BinaryValue(b); }
        }
        // Runtime 2026-06-18: NotationToQName was HOLLOW. Faithful Java: new QNameValue(notation.getStructuredQName(), QNAME).
        public class NotationToQName : Converter
        {
            public static readonly NotationToQName INSTANCE = new NotationToQName();
            public override IConversionResult Convert(object value) { var sqn = ((QualifiedNameValue)value).GetStructuredQName(); return (IConversionResult)new QNameValue((StructuredQName)sqn, BuiltInAtomicType.QNAME); }
        }
        // Runtime 2026-06-18: NumericToBoolean was HOLLOW -> xs:boolean(<numeric>) NRE-d. Faithful Java: BooleanValue.get(input.effectiveBooleanValue()).
        public class NumericToBoolean : Converter
        {
            public static readonly NumericToBoolean INSTANCE = new NumericToBoolean();
            public override IConversionResult Convert(object value) => (IConversionResult)BooleanValue.Get(((AtomicValue)value).EffectiveBooleanValue());
        }
        // PHANTOM stubs: no NumericToString / NumericToBigDecimal class exists in Saxon 12.9. numeric->string routes through
        // ToStringConverter (and the engine PhaseBConverters wrapper also name-covers the *ToString family); numeric->decimal
        // routes through NumericToDecimal. Never instantiated -> safe to leave hollow.
        public class NumericToString : Converter
        {
            public static readonly NumericToString INSTANCE = new NumericToString();
        }
        public class NumericToBigDecimal : Converter
        {
            public static readonly NumericToBigDecimal INSTANCE = new NumericToBigDecimal();
        }
        // Runtime 2026-06-18: ToUntypedAtomicConverter / ToStringConverter were HOLLOW. The engine PhaseBConverters wrapper has a
        // name-based fallback for them, but direct Convert() dispatch sites still NRE-d -> implement faithfully (matches the
        // fallback byte-for-byte). Java: ToUntyped -> StringValue.makeUntypedAtomic(input.getUnicodeStringValue());
        // ToString -> new StringValue(input.getUnicodeStringValue().tidy()).
        // ToStringConverter is exercised and byte-verified (xs:string(..) casts go via CastExpression.PreEvaluate / TO_STRING_MAPPER).
        // KNOWN ENGINE-SIDE GAP (NOT this converter): xs:untypedAtomic(..) casts compile to an AtomicSequenceConverter whose
        // MapItem (poc/output/full/expr/AtomicSequenceConverter.cs:~500) does `IConversionResult r = converter.Convert(item)` --
        // a dynamic->IConversionResult assignment that yields null for the (otherwise-perfect: engine StringValue, implements
        // IConversionResult, AsAtomic OK -- verified) value, NRE-ing at MapItem result.AsAtomic(). The PhaseBConverters fix that
        // routes around this NRE was never wired into MapItem line 500. Fix belongs engine-side (route MapItem through
        // PhaseBConverters.Convert, like the other sites) -- a separate Fix-PhaseB patch, out of scope for the compat un-stub.
        public class ToUntypedAtomicConverter : Converter
        {
            public static readonly ToUntypedAtomicConverter INSTANCE = new ToUntypedAtomicConverter();
            public override IConversionResult Convert(object value) { var us = ((AtomicValue)value).UnicodeStringValue; return (IConversionResult)new StringValue((UnicodeString)us, BuiltInAtomicType.UNTYPED_ATOMIC); }
        }
        public class ToStringConverter : Converter
        {
            public static readonly ToStringConverter INSTANCE = new ToStringConverter();
            public override IConversionResult Convert(object value) { var us = ((AtomicValue)value).UnicodeStringValue.Tidy(); return (IConversionResult)new StringValue((UnicodeString)us); }
        }
        // Runtime 2026-06-18: both were HOLLOW (inherited Convert=>null) -> xs:dayTimeDuration('PT1H30M') /
        // xs:duration / xs:yearMonthDuration string-casts NRE-d CastExpression.PreEvaluate via MakeLiteral(null).
        // Faithful Java (Converter.DurationToDayTimeDuration / DurationToYearMonthDuration): rebuild the duration
        // value in the narrower type from the parsed components (constructed directly via the engine value ctors).
        public class DurationToDayTimeDuration : Converter
        {
            public static readonly DurationToDayTimeDuration INSTANCE = new DurationToDayTimeDuration();
            public override IConversionResult Convert(object value)
            {
                var d = (DurationValue)value;
                int days = d.Days, hours = d.Hours, minutes = d.Minutes, seconds = d.Seconds, nanos = d.Nanoseconds;
                if (d.Signum() < 0) { return (IConversionResult)new DayTimeDurationValue(-days, -hours, -minutes, -(long)seconds, -nanos); }
                return (IConversionResult)new DayTimeDurationValue(days, hours, minutes, (long)seconds, nanos);
            }
        }
        public class DurationToYearMonthDuration : Converter
        {
            public static readonly DurationToYearMonthDuration INSTANCE = new DurationToYearMonthDuration();
            public override IConversionResult Convert(object value) { int months = ((DurationValue)value).TotalMonths; return (IConversionResult)YearMonthDurationValue.FromMonths(months); }
        }
        // Runtime 2026-06-18: QNameToNotation was HOLLOW. Faithful Java: new NotationValue(qname.getStructuredQName(), NOTATION).
        public class QNameToNotation : Converter
        {
            public static readonly QNameToNotation INSTANCE = new QNameToNotation();
            public override IConversionResult Convert(object value) { var sqn = ((QualifiedNameValue)value).GetStructuredQName(); return (IConversionResult)new NotationValue((StructuredQName)sqn, BuiltInAtomicType.NOTATION); }
        }
        // Runtime 2026-06-10: NumericToInteger was hollow (probe_conv2 xs:integer(7.9)). Truncate toward zero.
        public class NumericToInteger : Converter
        {
            public static readonly NumericToInteger INSTANCE = new NumericToInteger();
            public override IConversionResult Convert(object value) => (IConversionResult)new Int64Value((long)((NumericValue)value).LongValue());
        }
        // Runtime 2026-06-18: NumericToFloat was HOLLOW -> xs:float(<decimal/double>) NRE-d (integer->float uses IntegerToFloat).
        // Faithful Java: new FloatValue(((NumericValue)input).getFloatValue()).
        public class NumericToFloat : Converter
        {
            public static readonly NumericToFloat INSTANCE = new NumericToFloat();
            public override IConversionResult Convert(object value) => (IConversionResult)new FloatValue(((NumericValue)value).GetFloatValue());
        }
        // Runtime 2026-06-10: NumericToDouble inherited the hollow base Convert=>null -> every numeric->double
        // cast/promotion returned null (CastExpression.PreEvaluate of xs:double(2) -> MakeLiteral(null) NRE).
        // Faithful Java NumericToDouble.convert: DoubleValue passes through; otherwise new DoubleValue(getDoubleValue()).
        public class NumericToDouble : Converter
        {
            public static readonly NumericToDouble INSTANCE = new NumericToDouble();
            public override IConversionResult Convert(object value)
            {
                if (value is DoubleValue) { return (IConversionResult)value; }
                return (IConversionResult)new DoubleValue(((NumericValue)value).GetDoubleValue());
            }
        }
    }
}
