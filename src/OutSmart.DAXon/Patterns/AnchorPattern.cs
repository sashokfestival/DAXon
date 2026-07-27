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
    public class AnchorPattern : Pattern
    {
        private static readonly AnchorPattern _instance = new AnchorPattern();
        public static AnchorPattern GetInstance() => _instance;
        public override Expression Copy(RebindingMap rebindings) => this;
        public override void Export(ExpressionPresenter ep) { }
        public override ItemType GetItemType() => throw new NotImplementedException("STUB: AnchorPattern.GetItemType not ported (excluded stub)");
        public override UType GetUType() => throw new NotImplementedException("STUB: AnchorPattern.GetUType not ported (excluded stub)");
        public override bool Matches(IItem item, IXPathContext context) => false;
    }
}
