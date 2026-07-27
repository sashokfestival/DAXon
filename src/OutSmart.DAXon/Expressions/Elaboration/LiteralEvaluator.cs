////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;

namespace OutSmart.DAXon.Expressions.Elaboration
{
    // Runtime 2026-06-07: real OutSmart.DAXon.Expressions.Elaboration.LiteralEvaluator.cs is excluded; this stub had
    // `Evaluate(context) => null` and discarded the ctor value -> EVERY eagerly-evaluated literal returned null
    // (e.g. map:entry's StringLiteral key 'doc_id' -> null -> MapEntry.Call NRE). Faithful: store the grounded
    // value and return it (matches the real LiteralEvaluator: readonly value; Evaluate => value).
    public class LiteralEvaluator : ISequenceEvaluator
    {
        private readonly IGroundedValue value;
        public LiteralEvaluator() { }
        public LiteralEvaluator(object value) { this.value = value as IGroundedValue; }
        public ISequence Evaluate(IXPathContext context) => value;
    }
}
