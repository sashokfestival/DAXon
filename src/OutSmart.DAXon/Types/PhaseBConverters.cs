////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Types
{
    /// <summary>
    /// Faithful replacement for the hollow OutSmart.DAXon.Internal Converter stubs (compat/.../JavaInternals.cs:650
    /// `Convert(object) => null`). The real OutSmart.DAXon.Types.Converter.cs is excluded from the build, so the
    /// compiled Converter base + its nested converters (ToStringConverter/IdentityConverter/
    /// ToUntypedAtomicConverter/...) are stub-only and return null. ConversionRules.GetConverter short-circuits
    /// target==STRING -> ToStringConverter and source==target -> IdentityConverter, so integer->string and
    /// anyAtomic->string (e.g. an @id or count() AVT) hit a null-returning stub -> NRE at
    /// AtomicSequenceConverter.ConvertItem (result.AsAtomic() on null). Bodies are upstream-faithful
    /// (Converter.java: IdentityConverter -> input; ToStringConverter -> new StringValue(input.tidy());
    /// ToUntypedAtomicConverter -> makeUntypedAtomic). Real subclasses (StringConverter.StringToString etc.)
    /// still dispatch via the dynamic call and take the early return -> their behaviour is unchanged.
    /// </summary>
    public static class PhaseBConverters
    {
        public static IConversionResult Convert(Converter conv, AtomicValue item)
        {
            object r = conv.Convert(item);
            if (r != null)
            {
                return (IConversionResult)r;
            }
            switch (conv.GetType().Name)
            {
                case "IdentityConverter":
                    return item;
                case "ToUntypedAtomicConverter":
                    return StringValue.MakeUntypedAtomic(item.UnicodeStringValue);
                case "ToStringConverter":
                case "NumericToString":
                case "IntegerToString":
                case "DecimalToString":
                case "FloatToString":
                case "DoubleToString":
                case "BooleanToString":
                    return new StringValue(item.UnicodeStringValue.Tidy());
                default:
                    if (System.Environment.GetEnvironmentVariable("SAXON_DBG_CONV") != null)
                        System.Console.WriteLine("[conv-null] " + conv.GetType().FullName + " item=" + item.GetType().Name);
                    return null;
            }
        }
    }
}
