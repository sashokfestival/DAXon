////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Functions
{
    // NormalizeSpace_1 hollow stub removed 2026-06-03: fleshed real impl (ScalarSystemFunction + Evaluate,
    // no GetElaborator) lives in excluded stubs.cs to register normalize-space#1. See String_1, same pattern.
    // Runtime 2026-06-10: Minimax hollow stub REMOVED (SetIgnoreNaN was a no-op; real Minimax.cs re-included for fn:min/fn:max, core function batch 2).
    // Runtime 2026-06-10: LocalName_1 hollow stub REMOVED (real file re-included for fn:local-name, batch 3).
    // IsIdRef extends ExtensionFunctionDefinition in Saxon (KeyManager passes new IsIdRef()
    // to IntegratedFunctionLibrary.makeFunctionCall); reflect the real base so the upcast is valid.
    // Runtime: real port of net.sf.saxon.functions.IsIdRef (excluded source had a BooleanValue[...] transpile
    // residual). The internal saxon:is-idref predicate; registered for the built-in idref key on every compile
    // (KeyManager.RegisterIdrefKey). Was hollow (=>null), NRE'ing IntegratedFunctionLibrary.MakeFunctionCall.
    public class IsIdRef : ExtensionFunctionDefinition
    {
        private static readonly StructuredQName _qName =
            new StructuredQName("", NamespaceUri.SAXON, "is-idref");
        public override StructuredQName FunctionQName => _qName;
        public override int MinimumNumberOfArguments => 0;
        public override int MaximumNumberOfArguments => 0;
        public override SequenceType[] ArgumentTypes => new SequenceType[] { };
        public IsIdRef() { }
        public override SequenceType GetResultType(SequenceType[] suppliedArgumentTypes) => SequenceType.SINGLE_BOOLEAN;
        public override ExtensionFunctionCall MakeCallExpression() => new IsIdRefCall();
        private sealed class IsIdRefCall : ExtensionFunctionCall
        {
            public override ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                var ci = context.GetContextItem();
                return BooleanValue.Get(ci is NodeInfo && ((NodeInfo)ci).IsIdref());
            }
            public override bool EffectiveBooleanValue(IXPathContext context, ISequence[] arguments)
            {
                var ci = context.GetContextItem();
                return ci is NodeInfo && ((NodeInfo)ci).IsIdref();
            }
        }
    }
}
