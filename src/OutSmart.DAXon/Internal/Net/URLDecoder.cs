////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;

namespace OutSmart.DAXon.Internal.Net
{

    // Phase 5: URLDecoder — Java's URLDecoder.decode(s, charset).
    public static class URLDecoder
    {
        public static string Decode(string s, string enc) => global::System.Uri.UnescapeDataString(s ?? "");
        public static string Decode(string s) => global::System.Uri.UnescapeDataString(s ?? "");
    }
}
