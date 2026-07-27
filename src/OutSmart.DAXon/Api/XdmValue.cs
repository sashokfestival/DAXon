////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;
using System.Linq;

namespace OutSmart.DAXon.Api
{
    // Minimal XdmValue stub with the static Wrap factory used by ~40 sites.
    public class XdmValue
    {
        // Runtime: the real XdmValue.cs is excluded (it pulls in XdmStream/Step). This stub must still faithfully
        // hold the wrapped GroundedValue so XdmNode/XdmItem (real subclasses that base() into here) round-trip the
        // underlying NodeInfo/value. Previously the ctor discarded the arg and GetUnderlyingValue()=>null, so
        // Xslt30Transformer.ApplyTemplates passed a null source into XsltController -> NRE at source.Iterate().
        private readonly object _value;
        public virtual object UnderlyingValue => _value;
        public XdmValue() { }
        public XdmValue(object value) { _value = value; }
        // Runtime 2026-06-11: type-dispatching Wrap like the real one - a NodeInfo must wrap as XdmNode
        // (MessageInstr.MakeMessage casts (XdmNode)XdmNode.Wrap(content)). AtomicValue -> XdmAtomicValue.
        public static XdmValue Wrap(object value)
        {
            if (value is NodeInfo __n)
                return new XdmNode(__n);
            if (value is AtomicValue __a)
                return new XdmAtomicValue(__a);
            // upstream singleton dispatch: a lone map/array/function wraps as its XdmItem subclass
            // (callers cast Wrap(singleItem) to XdmItem, e.g. XPathCompiler.EvaluateSingle)
            if (value is OutSmart.DAXon.Values.Maps.MapItem __m)
                return new XdmMap(__m);
            if (value is OutSmart.DAXon.Values.Arrays.ArrayItem __arr)
                return new XdmArray(__arr);
            if (value is IFunctionItem __f)
                return new XdmFunctionItem(__f);
            return new XdmValue(value);
        }
        public bool Matches(object t) => false;
        // Enumerate the wrapped value's items as XdmItems (was an always-empty stub — any foreach
        // over an XdmValue silently saw nothing, e.g. the driver's context-select narrowing).
        public IEnumerator<XdmItem> GetEnumerator()
        {
            if (this is XdmItem selfItem) { yield return selfItem; yield break; }
            if (_value is OutSmart.DAXon.Model.ISequence seq)
            {
                var iter = seq.Iterate();
                for (OutSmart.DAXon.Model.IItem it; (it = iter.Next()) != null;)
                {
                    yield return (XdmItem)Wrap(it);
                }
            }
        }
        // Phase 7.17: Select used by XdmNode.Children() etc. — returns object
        // (callers chain .AsListOfNodes(); the result type doesn't matter for
        // compile gating since XdmStream/Step are excluded).
        public virtual object Select(object step) => throw new NotImplementedException("STUB: XdmValue.Select not ported (excluded stub)");
    }
}
