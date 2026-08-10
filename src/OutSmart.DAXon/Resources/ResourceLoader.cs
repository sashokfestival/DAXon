////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Internal.Net;
using OutSmart.DAXon.Internal.Charsets;
using OutSmart.DAXon.Internal.Collections;
using static OutSmart.DAXon.Resources.EncodingDetector;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Streams;
using System.IO;
using System.IO.Compression;
namespace OutSmart.DAXon.Resources
{
    internal class ResourceLoader
    {
        public static int MAX_REDIRECTS = 20;
        public static URLConnection UrlConnection(Uri url)
        {
            if (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps)
            {
                var visited = new HashSet<string>();
                string cookies = null;
                int count = MAX_REDIRECTS;
                for (; ; )
                {
                    HttpURLConnection conn = new HttpURLConnection(url);
                    conn.SetInstanceFollowRedirects(false);
                    conn.SetRequestProperty("Accept-Encoding", "gzip");
                    if (cookies != null)
                    {
                        conn.SetRequestProperty("Cookie", cookies);
                    }

                    int status = conn.ResponseCode;
                    if (status == HttpURLConnection.HTTP_MOVED_PERM || status == HttpURLConnection.HTTP_MOVED_TEMP)
                    {
                        // Normally dormant on .NET: HttpWebRequest auto-follows redirects inside
                        // GetResponse (SetInstanceFollowRedirects is a no-op here), so this manual
                        // loop only runs if a host disables that. The header name had been mangled
                        // to "ILocation" by the ILocation-type rename sweep - a silent artifact
                        // precisely because the loop is dormant.
                        string location = conn.GetHeaderField("Location");
                        url = new Uri(url, location);
                        cookies = conn.GetHeaderField("Set-Cookie");

                        // Every header read above needs the response, so release it only now - but
                        // release it on EVERY exit from this hop, including the two throws below.
                        // Nobody will ever read this hop's body, and an abandoned response keeps its
                        // socket checked out of the pool until finalization.
                        conn.Disconnect();
                        if (visited.Contains(location))
                        {
                            throw new IOException("HTTP redirect loop through " + location);
                        }

                        visited.Add(location);
                        count -= 1;
                        if (count < 0)
                        {
                            throw new IOException("HTTP redirects more than " + MAX_REDIRECTS + " times");
                        }
                    }
                    else
                    {
                        // The caller reads this one's body, so it stays open: closing it is the
                        // caller's job, via the stream it obtains from InputStream.
                        return conn;
                    }
                }
            }
            else
            {
                return new URLConnection(url);
            }
        }

        public static System.IO.Stream UrlStream(Configuration config, string url)
        {
            if (config != null && url.StartsWith("classpath:", StringComparison.Ordinal))
            {
                string path;
                if (url.Length > 10 && url[10] == '/')
                {
                    path = url.Substring(11);
                }
                else
                {
                    path = url.Substring(10);
                }

                // DynamicLoader has no default implementation; without one, classpath: URIs are unresolvable.
                if (config.DynamicLoader == null)
                {
                    throw new IOException("Cannot resolve classpath: URI (no dynamic loader configured): " + url);
                }

                return config.DynamicLoader.GetResourceAsStream(path);
            }
            else
            {
                URLConnection conn = ResourceLoader.UrlConnection(new Uri(url));
                System.IO.Stream inputStream = conn.InputStream;
                string contentEncoding = conn.ContentEncoding;
                if ("gzip".Equals(contentEncoding))
                {
                    inputStream = new GZipStream(inputStream, CompressionMode.Decompress);
                }

                return inputStream;
            }
        }

        public static ResolvedResource TypedResource(Configuration config, string url)
        {
            if (config != null && url.StartsWith("classpath:", StringComparison.Ordinal))
            {
                return new ResolvedResource { Stream = UrlStream(config, url), SystemId = url };
            }
            else
            {
                URLConnection conn = ResourceLoader.UrlConnection(new Uri(url));
                System.IO.Stream inputStream = conn.InputStream;
                if ("gzip".Equals(conn.ContentEncoding))
                {
                    inputStream = new GZipStream(inputStream, CompressionMode.Decompress);
                }

                if (true)
                {
                    inputStream = new BufferedStream(inputStream);
                }

                return new ResolvedResource { Stream = inputStream, ContentType = conn.ContentType, SystemId = url };
            }
        }
    }
}
