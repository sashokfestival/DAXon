////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using System.Linq;

namespace OutSmart.DAXon.Patterns
{
    // Runtime 2026-06-10: LastItemExpression hollow stub REMOVED (implicit operator => null silently NULLED the
    // a[last()] rewrite in FilterExpression:558 and the max() optimization in Minimax:150). Real class re-included (csproj).
    public class NamespaceResolverWithDefault : INamespaceResolver
    {
        public NamespaceResolverWithDefault(object resolver, object defaultNs) { }
        public NamespaceUri GetURIForPrefix(string prefix, bool useDefault) => throw new NotImplementedException("STUB: NamespaceResolverWithDefault.GetURIForPrefix not ported (excluded stub)");
        public IEnumerator<string> IteratePrefixes() => Enumerable.Empty<string>().GetEnumerator();
    }
}
