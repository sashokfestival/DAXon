////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Trees.Iterators;

namespace OutSmart.DAXon.Trees.Linked
{
    public class NamespaceNode
    {
        public NamespaceNode() { }
        public NamespaceNode(object parent, object name, int index) { }
        public static IAxisIterator MakeIterator(object parent, object map) => throw new NotImplementedException("STUB: NamespaceNode.MakeIterator not ported (excluded stub)");
    }
}
