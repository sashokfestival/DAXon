////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;

namespace OutSmart.DAXon.Api
{
    // Phase 5: ConstructedItemType extends ItemType (s9api) for 6 callers expecting ItemType.
    public class ConstructedItemType : ItemType
    {
        public ConstructedItemType() : base(null) { }
        public ConstructedItemType(Types.ItemType underlying, object processor) : base(underlying) { }
        public override bool Matches(XdmItem item) => false;
        public override bool Subsumes(ItemType other) => false;
    }
}
