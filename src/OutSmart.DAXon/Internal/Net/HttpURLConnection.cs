////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;

namespace OutSmart.DAXon.Internal.Net
{

    // Runtime 2026-06-10: HTTP flavor for ResourceLoader's redirect/gzip loop. .NET WebRequest handles
    // redirects itself when follow=true; with the loop's follow=false the first response is already the
    // final one (auto-redirected), so GetResponseCode reports the terminal 200 and the loop exits.
    public class HttpURLConnection : URLConnection
    {
        public const int HTTP_MOVED_PERM = 301; public const int HTTP_MOVED_TEMP = 302; public const int HTTP_SEE_OTHER = 303; public const int HTTP_OK = 200;
        private readonly global::System.Collections.Generic.Dictionary<string, string> _headers = new global::System.Collections.Generic.Dictionary<string, string>();
        public virtual int ResponseCode { get { var r = Response() as global::System.Net.HttpWebResponse; return r == null ? 200 : (int)r.StatusCode; } }
        public HttpURLConnection() { }
        public HttpURLConnection(URL url) : base(url) { }
        public virtual void SetInstanceFollowRedirects(bool follow) { }
        public virtual void SetRequestProperty(string key, string value) { _headers[key] = value; }
        public virtual string GetHeaderField(string name) { try { return Response()?.Headers?[name]; } catch { return null; } }
    }
}
