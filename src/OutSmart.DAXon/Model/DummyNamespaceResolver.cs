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

namespace OutSmart.DAXon.Model
{
    // Phase 5: DummyNamespaceResolver implements INamespaceResolver (8 callers).
    public class DummyNamespaceResolver : INamespaceResolver
    {
        private static readonly DummyNamespaceResolver _instance = new DummyNamespaceResolver();
        public DummyNamespaceResolver() { }
        public static DummyNamespaceResolver GetInstance() => _instance;
        public NamespaceUri GetURIForPrefix(string prefix, bool useDefault) => throw new NotImplementedException("STUB: DummyNamespaceResolver.GetURIForPrefix not ported (excluded stub)");
        public IEnumerator<string> IteratePrefixes() => Enumerable.Empty<string>().GetEnumerator();
    }
}
