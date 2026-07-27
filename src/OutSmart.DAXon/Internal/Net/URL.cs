////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;

namespace OutSmart.DAXon.Internal.Net
{

    public sealed class URL
    {
        public Uri Inner { get; }
        public string Protocol => Inner.Scheme;
        public string Host => Inner.Host;
        public int Port => Inner.Port;
        public URL(string spec) { Inner = new Uri(spec); }
        public URL(URL context, string spec) { Inner = new Uri(context.Inner, spec); }
        public string GetPath() => Inner.AbsolutePath;
        public string GetFile() => Inner.PathAndQuery;
        public URI ToURI() => new URI(Inner);
        public override string ToString() => Inner.OriginalString;
        // Phase 5: OpenConnection — Java's URL.openConnection() returns URLConnection.
        // Runtime 2026-06-10: pass the URL through (was a blind parameterless ctor -> GetInputStream had no target); http(s) gets the Http flavor.
        public URLConnection OpenConnection() { var pr = Protocol; return pr == "http" || pr == "https" ? new HttpURLConnection(this) : new URLConnection(this); }
    }
}
