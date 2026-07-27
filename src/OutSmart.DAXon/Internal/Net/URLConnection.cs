////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;

namespace OutSmart.DAXon.Internal.Net
{

    // Phase 5: URLConnection -- Java's URLConnection wrapper.
    // Runtime 2026-06-10: was hollow (GetInputStream=>null killed fn:doc for EVERY scheme incl. file:).
    // Real semantics: file: opens the local file; other schemes go through WebRequest/WebResponse.
    public class URLConnection
    {
        protected readonly URL _url;
        protected global::System.Net.WebResponse _resp;
        // Runtime 2026-06-10 (2): translate native I/O failures to the OutSmart.DAXon.Internal.IO family — transpiled callers
        // catch IOException per the Java contract (DirectResourceResolver "carry on", UnparsedTextFunction
        // HandleIOError -> FOUT1170); a native System.IO exception flies past those catches and kills the transform.
        public virtual global::System.IO.Stream InputStream
        {
            get
            {
                if (_url == null)
                    return null;
                string u = _url.ToString();
                try
                {
                    if (u.StartsWith("file:", global::System.StringComparison.OrdinalIgnoreCase))
                    {
                        return (global::System.IO.Stream)(global::System.IO.Stream)global::System.IO.File.OpenRead(new global::System.Uri(u).LocalPath);
                    }
                    return (global::System.IO.Stream)Response().GetResponseStream();
                }
                catch (global::System.IO.IOException) { throw; }
                // IO-removal: compat IO.IOException eliminated -> System.IO.IOException. Native I/O failures
                // (System.IO.FileNotFoundException/DirectoryNotFoundException) ARE subtypes of System.IO.IOException, so
                // the catch above now propagates them as IOExceptions to the resource-resolution consumers
                // (ResourceLoader/DirectResourceResolver) which "carry on" per the Java IOException contract
                // (unparsed-text-available false / FOUT1170). Non-IO failures still wrap into System.IO.IOException.
                catch (global::System.Exception e) { throw new global::System.IO.IOException(e.Message); }
            }
        }
        public virtual string ContentType { get { try { return _url == null || _url.ToString().StartsWith("file:", global::System.StringComparison.Ordinal) ? null : Response().ContentType; } catch { return null; } } }
        public virtual long ContentLength { get { try { return Response()?.ContentLength ?? 0; } catch { return 0; } } }
        public virtual long LastModified => 0;
        public virtual string ContentEncoding { get { try { if (_url != null && _url.ToString().StartsWith("file:", global::System.StringComparison.OrdinalIgnoreCase)) return null; return (Response() as global::System.Net.HttpWebResponse)?.ContentEncoding; } catch { return null; } } }
        public URLConnection() { }
        public URLConnection(URL url) { _url = url; }
        protected global::System.Net.WebResponse Response()
        {
            if (_resp == null && _url != null)
            {
                try { _resp = global::System.Net.WebRequest.Create(_url.ToString()).GetResponse(); }
                catch (global::System.Net.WebException we)
                {
                    // Java's URLConnection surfaces HTTP retrieval failures (including 4xx/5xx, which .NET raises
                    // as WebException from GetResponse) as java.io.IOException. Translate so the callers' existing
                    // IOException handlers fire — unparsed-text() -> FOUT1170, doc()/unparsed-text-available() ->
                    // not-available — instead of a raw WebException escaping as a code-less error and killing the query.
                    throw new global::System.IO.IOException(we.Message, we);
                }
            }

            return _resp;
        }
        public virtual void Connect() { Response(); }
        // Phase 7.8: static helpers used by AbstractResourceCollection.
        public static string GuessContentTypeFromName(string name) => null;
        public static string GuessContentTypeFromStream(global::System.IO.Stream stream) => null;
    }
}
