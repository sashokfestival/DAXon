////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Values.Maps;

namespace OutSmart.DAXon.Api
{

    // upstream: XdmMap extends XdmFunctionItem (a map IS an item) — as XdmValue it broke
    // XdmValue.Wrap's singleton dispatch contract (callers cast Wrap(item) to XdmItem)
    public class XdmMap : XdmFunctionItem
    {
        public XdmMap() { }
        public XdmMap(OutSmart.DAXon.Model.IItem map) : base(map) { }

        // upstream s9api XdmMap.put: functional add — returns a NEW map, the receiver is unchanged.
        public virtual XdmMap Put(XdmAtomicValue key, XdmValue value)
        {
            MapItem map = UnderlyingValue as MapItem ?? new HashTrieMap();
            return new XdmMap(map.AddEntry((AtomicValue)key.UnderlyingValue, (IGroundedValue)value.UnderlyingValue));
        }
    }
}
