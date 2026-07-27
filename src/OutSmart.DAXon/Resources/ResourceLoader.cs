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
using OutSmart.DAXon.Internal.Jaxp.Transform.Stream;
using OutSmart.DAXon.Internal.Streams;
using System.IO;
using System.IO.Compression;
namespace OutSmart.DAXon.Resources
{
    public class ResourceLoader
    {
        public static int MAX_REDIRECTS = 20;
        public static URLConnection UrlConnection(URL url)
        {
            if ("http".Equals(url.Protocol) || "https".Equals(url.Protocol))
            {
                var visited = new HashSet<string>();
                string cookies = null;
                int count = MAX_REDIRECTS;
                for (; ; )
                {
                    HttpURLConnection conn = (HttpURLConnection)url.OpenConnection();
                    conn.SetInstanceFollowRedirects(false);
                    conn.SetRequestProperty("Accept-Encoding", "gzip");
                    if (cookies != null)
                    {
                        conn.SetRequestProperty("Cookie", cookies);
                    }

                    int status = conn.ResponseCode;
                    if (status == HttpURLConnection.HTTP_MOVED_PERM || status == HttpURLConnection.HTTP_MOVED_TEMP)
                    {
                        string location = conn.GetHeaderField("ILocation");
                        url = new URL(url, location);
                        cookies = conn.GetHeaderField("Set-Cookie");
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
                        return conn;
                    }
                }
            }
            else
            {
                return url.OpenConnection();
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

                return config.DynamicLoader.GetResourceAsStream(path);
            }
            else
            {
                URLConnection conn = ResourceLoader.UrlConnection(new URL(url));
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
                URLConnection conn = ResourceLoader.UrlConnection(new URL(url));
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

        public static TextReader UrlReader(Configuration config, string url, string requestedEncoding)
        {
            string resourceEncoding = null;

            // Get any external (HTTP) requestedEncoding label.
            bool isXmlMediaType = false;
            URLConnection conn = null;
            System.IO.Stream inputStream = null;
            if (config != null && url.StartsWith("classpath:", StringComparison.Ordinal))
            {
                inputStream = ResourceLoader.UrlStream(config, url);
            }
            else
            {
                conn = ResourceLoader.UrlConnection(new URL(url));
                inputStream = conn.InputStream;
                string contentEncoding = conn.ContentEncoding;
                if ("gzip".Equals(contentEncoding))
                {
                    inputStream = new GZipStream(inputStream, CompressionMode.Decompress);
                }
            }

            if (true)
            {
                inputStream = new BufferedStream(inputStream);
            }


            // If conn was used and the url isn't a file: URI, try to get encoding information from it
            if (conn != null && !url.StartsWith("file:", StringComparison.Ordinal))
            {

                // Use the contentType from the HTTP header if available, and parse it
                string contentType = conn.ContentType;
                if (contentType != null)
                {
                    ParsedContentType parsedContentType = new ParsedContentType(contentType);
                    isXmlMediaType = parsedContentType.isXmlMediaType;
                    resourceEncoding = parsedContentType.encoding;
                }
            }

            try
            {
                if (requestedEncoding == null)
                {
                    requestedEncoding = "UTF-8";
                }

                if (resourceEncoding == null || isXmlMediaType)
                {
                    resourceEncoding = InferStreamEncoding(inputStream, requestedEncoding, null);
                }
            }
            catch (IOException e)
            {
                resourceEncoding = "UTF-8";
            }

            return GetReaderFromStream(inputStream, resourceEncoding);
        }

        public static TextReader GetReaderFromStream(System.IO.Stream inputStream, string resourceEncoding)
        {
            try
            {
                if (inputStream == null)
                    throw new NullReferenceException();
                if (resourceEncoding == null)
                    throw new NullReferenceException();
                Charset charset2 = Charset.ForName(resourceEncoding);

                // ensure that encoding errors are not recovered
                // decoder line removed (System.IO.TextReader cluster -> BCL StreamReader)
                return new StreamReader((System.IO.Stream)inputStream, charset2.Inner);
            }
            catch (Exception e)
            {
                throw new ArgumentException("Unable to get reader with encoding: " + resourceEncoding);
            }
        }
    }
}
