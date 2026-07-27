////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// java.time stubs.
using System;

namespace OutSmart.DAXon.Internal.Jaxp.Namespace
{
    public interface NamespaceContext
    {
        string GetNamespaceURI(string prefix);
        string GetPrefix(string namespaceURI);
        global::System.Collections.IEnumerator GetPrefixes(string namespaceURI);
    }
}
