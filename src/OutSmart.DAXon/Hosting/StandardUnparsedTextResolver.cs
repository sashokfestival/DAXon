////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Core;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal.Net;
using OutSmart.DAXon.Internal.Jaxp.Transform.Stream;
using OutSmart.DAXon.Transformation;
using System.IO;
using System.Net;
using System.Text;

namespace OutSmart.DAXon.Lib
{
    public class StandardUnparsedTextResolver : IUnparsedTextURIResolver
    {
        public StandardUnparsedTextResolver() { }
        // Phase C 2026-06-09: real resolver (was => null, which made json-doc()/unparsed-text() fail with
        // "Unable to resolve URI"). Opens an absolute file:// or http(s):// URI and returns a Reader over its
        // content (the real StandardUnparsedTextResolver.cs is excluded). Errors -> null -> caller's FOUT1170.
        // Read a file as text honouring the F&O unparsed-text encoding rules: an explicit encoding wins;
        // otherwise a byte-order mark; otherwise, for a resource carrying an XML declaration, the encoding it
        // names; otherwise UTF-8. Without this, an iso-8859-1 .xml file (no BOM) was decoded as UTF-8 and its
        // é/ü turned into U+FFFD (unparsed-text-2001).
        private static string ReadTextFile(string path, string encoding)
        {
            byte[] bytes = File.ReadAllBytes(path);
            Encoding enc = !string.IsNullOrEmpty(encoding) ? Encoding.GetEncoding(encoding) : InferEncoding(bytes);
            using (var ms = new MemoryStream(bytes))
            using (var sr = new StreamReader(ms, enc, detectEncodingFromByteOrderMarks: true))
                return sr.ReadToEnd();
        }

        private static Encoding InferEncoding(byte[] bytes)
        {
            // BOMs are handled by StreamReader; here infer from an XML declaration (its bytes are
            // ASCII-compatible in every XML encoding, so a Latin-1 decode of the prefix reads it).
            int n = Math.Min(bytes.Length, 256);
            string head = Encoding.GetEncoding("ISO-8859-1").GetString(bytes, 0, n);
            var m = System.Text.RegularExpressions.Regex.Match(head, "^\\s*<\\?xml\\b[^>]*?encoding\\s*=\\s*[\"']([^\"']+)[\"']");
            if (m.Success)
            {
                try { return Encoding.GetEncoding(m.Groups[1].Value); }
                catch { }
            }

            return new UTF8Encoding(false);
        }

        public TextReader Resolve(URI absoluteURI, string encoding, Configuration config)
        {
            try
            {
                var sysUri = new Uri(absoluteURI.ToString());
                string text;
                if (sysUri.IsFile)
                {
                    text = ReadTextFile(sysUri.LocalPath, encoding);
                }
                else
                {
                    using (var wc = new WebClient()) { text = wc.DownloadString(sysUri); }
                }
                return new StringReader(text);
            }
            catch (Exception) { return null; }
        }
        // 2026-06-10: real UnparsedTextFunction.ReadFile falls back to this static when the resolver returns
        // null (ResourceRequest -> DirectResourceResolver -> StreamSource). Materializes via StringReader
        // (Java -1 EOF semantics), honoring whichever of reader/stream/systemId the source carries.
        public static TextReader GetReaderFromResolvedResource(ResolvedResource src, string encoding, Configuration config, bool isXml)
        {
            try
            {
                var tr = src.TextReader;
                if (tr != null)
                    return new StringReader(tr.ReadToEnd());
                var ins = src.Stream;
                if (ins != null)
                {
                    using (var sr = string.IsNullOrEmpty(encoding) ? new StreamReader(ins) : new StreamReader(ins, Encoding.GetEncoding(encoding)))
                    {
                        return new StringReader(sr.ReadToEnd());
                    }
                }
                var sysId = src.SystemId;
                if (sysId != null)
                {
                    var u = new Uri(sysId);
                    if (u.IsFile)
                    {
                        return new StringReader(ReadTextFile(u.LocalPath, encoding));
                    }
                    using (var wc = new WebClient()) { return new StringReader(wc.DownloadString(u)); }
                }
            }
            // Upstream contract is `throws XPathException` (StandardUnparsedTextResolver.java:157) and the
            // UnparsedTextFunction.ReadFile call site sits OUTSIDE its IOException try - so translate all
            // native failures here: missing/unreadable resource -> FOUT1170, unknown encoding -> FOUT1190.
            catch (XPathException) { throw; }
            catch (ArgumentException e) { throw new XPathException("unparsed-text(): unknown encoding " + encoding + " (" + e.Message + ")", "FOUT1190"); }
            catch (Exception e) { throw new XPathException("unparsed-text(): cannot read " + (src.SystemId ?? "(anonymous source)") + ": " + e.Message, "FOUT1170"); }
            throw new XPathException("unparsed-text(): resource has no reader, stream or system ID", "FOUT1170");
        }
    }
}
