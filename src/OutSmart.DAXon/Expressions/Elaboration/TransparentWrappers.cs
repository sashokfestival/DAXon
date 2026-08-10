////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using OutSmart.DAXon.Types;

namespace OutSmart.DAXon.Expressions.Elaboration
{
    /// <summary>
    /// Wrapper kinds a fused-lane matcher may peel off an operand before probing its shape.
    /// Each site names exactly the wrappers that are value-transparent FOR ITS lane — e.g. a
    /// string-typed converter is identity only when the lane itself reads the string value.
    /// </summary>
    [Flags]
    internal enum Peel
    {
        StringFn = 1,           // fn:string(x) — the type checker's wrapper around a bare `.`
        Converter = 2,          // AtomicSequenceConverter, any target type
        StringConverter = 4,    // AtomicSequenceConverter to xs:string only
        Atomizer = 8,
        SingletonAtomizer = 16,
        CardinalityChecker = 32,
    }

    internal static class TransparentWrappers
    {
        internal static Expression Unwrap(Expression e, Peel accept)
        {
            while (true)
            {
                if ((accept & Peel.StringFn) != 0 && e is SystemFunctionCall sfc
                    && sfc.TargetFunction is Functions.String_1)
                {
                    e = sfc.GetArg(0);
                }
                else if (e is AtomicSequenceConverter asc
                    && ((accept & Peel.Converter) != 0
                        || ((accept & Peel.StringConverter) != 0 && BuiltInAtomicType.STRING.Equals(asc.RequiredItemType))))
                {
                    e = asc.BaseExpression;
                }
                else if ((accept & Peel.Atomizer) != 0 && e is Atomizer at)
                {
                    e = at.BaseExpression;
                }
                else if ((accept & Peel.SingletonAtomizer) != 0 && e is SingletonAtomizer sa)
                {
                    e = sa.BaseExpression;
                }
                else if ((accept & Peel.CardinalityChecker) != 0 && e is CardinalityChecker cc)
                {
                    e = cc.BaseExpression;
                }
                else
                {
                    return e;
                }
            }
        }
    }
}
