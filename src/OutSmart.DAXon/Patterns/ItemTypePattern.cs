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
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Types;

namespace OutSmart.DAXon.Patterns
{
    public class ItemTypePattern : Pattern
    {
        public override int ImplementationMethod => 0;
        public override double DefaultPriority => 0;
        public ItemTypePattern() { }
        public ItemTypePattern(object itemType) { }
        public override Expression Copy(RebindingMap r) => this;
        public override void Export(ExpressionPresenter @out) { }
        public override ItemType GetItemType() => throw new NotImplementedException("STUB: ItemTypePattern.GetItemType not ported (excluded stub)");
        protected override int ComputeCardinality() => 0;
        public override bool Matches(IItem item, IXPathContext context) => false;
        public override UType GetUType() => UType.VOID;
    }
}
