////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Functions
{

    // Count_1 (fn:count#1) -- REAL elaborator-free impl ported from the excluded Count.cs:69-74 + SteppingCount:57-66.
    // The real Count.cs is excluded (csproj) AND its class name 'Count' is occupied here by a hollow static-helper
    // stub (not a SystemFunction), so this is registered as a distinct class on the compiled SystemFunction base.
    // GetElaborator() (CountFnElaborator) intentionally omitted (String_1/Tokenize_1 pattern): correctness from Call
    // (interpreter path); the optimizer elaborator is deferred. The grounded fast-path uses IGroundedValue.GetLength();
    // otherwise count by iterating (faithful to the real SteppingCount loop). UO/INS upstream flags are non-correctness.
    public class Count_1 : SystemFunction
    {
        public Count_1() { }
        public static Func<Count_1> New() => () => new Count_1();
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            ISequence arg = arguments[0];
            int size;
            if (arg is IGroundedValue)
            {
                size = ((IGroundedValue)arg).GetLength();
            }
            else
            {
                var __it = arg.Iterate();
                // Java's Count.count(iter): a LAST_POSITION_FINDER iterator yields its length without
                // iterating. This makes count(reverse(X)), count(subsequence(...)) etc. O(1)/O(base-length)
                // instead of walking every item. Byte-identical: GetLength() is the item count.
                if (__it is ILastPositionFinder __lpf && __lpf.SupportsGetLength())
                {
                    size = __lpf.GetLength();
                }
                else
                {
                    size = 0;
                    IItem __c;
                    while ((__c = __it.Next()) != null)
                    {
                        size++;
                    }
                }
            }
            return Int64Value.MakeIntegerValue(size);
        }
    }
}
