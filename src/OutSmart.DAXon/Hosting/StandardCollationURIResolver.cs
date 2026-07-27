////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Net;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Lib
{
    public class StandardCollationURIResolver : ICollationURIResolver
    {
        private static readonly StandardCollationURIResolver theInstance = new StandardCollationURIResolver();
        public StandardCollationURIResolver()
        {
        }

        public static StandardCollationURIResolver GetInstance()
        {
            return theInstance;
        }

        public virtual IStringCollator Resolve(string uri, Configuration config)
        {
            if (uri.Equals("http://saxon.sf.net/collation"))
            {
                return Core.Version.platform.MakeCollation(config, new Properties(), uri);
            }
            else if (uri.StartsWith("http://saxon.sf.net/collation?", StringComparison.Ordinal))
            {
                URI uuri;
                try
                {
                    uuri = new URI(uri);
                }
                catch (URISyntaxException err)
                {
                    throw new XPathException(err?.Message);
                }

                Properties props = new Properties();
                string query = uuri.RawQuery;
                if (query.StartsWith("?", StringComparison.Ordinal))
                {

                    // Happens on .NET
                    query = query.Substring(1);
                }

                foreach (string param in query.Split(new[] { ';', '&' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    int eq = param.IndexOf('=');
                    if (eq > 0 && eq < param.Length - 1)
                    {
                        string kw = param.Substring(0, eq);
                        string val = AnyURIValue.Decode(param.Substring(eq + 1));
                        props.SetProperty(kw, val);
                    }
                }

                return Core.Version.platform.MakeCollation(config, props, uri);
            }
            else if (uri.StartsWith("http://www.w3.org/2013/collation/UCA", StringComparison.Ordinal))
            {
                IStringCollator uca = Core.Version.platform.MakeUcaCollator(uri, config);
                if (uca != null)
                {
                    return uca;
                }

                // No exact UCA collator on this platform (MakeUcaCollator is a stub). `fallback=no` demands the
                // exact UCA collation with no substitution, which we cannot guarantee — signal FOCH0002 (the
                // spec's "collation not supported" error, which fn:compare/collation-key surface and the
                // fallback=no tests expect). Without fallback=no we may substitute the closest CompareInfo
                // locale/strength approximation below.
                if (uri.Contains("fallback=no"))
                {
                    throw new XPathException("The UCA collation with fallback=no is not supported (no exact Unicode Collation Algorithm implementation)", "FOCH0002");
                }

                URI uuri;
                try
                {
                    uuri = new URI(uri);
                }
                catch (URISyntaxException err)
                {
                    throw new XPathException(err?.Message);
                }

                Properties props = new Properties();
                string query = AnyURIValue.Decode(uuri.RawQuery);
                if (query != null)
                {
                    if (query.StartsWith("?", StringComparison.Ordinal))
                        query = query.Substring(1);
                    foreach (string param in query.Split(new[] { ';', '&' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        int eq = param.IndexOf('=');
                        if (eq <= 0 || eq >= param.Length - 1)
                            continue;
                        string kw = param.Substring(0, eq);
                        string val = param.Substring(eq + 1);
                        if (kw.Equals("fallback")) continue; // always satisfied: we provide the CompareInfo collation
                        // Fallback is LAX (F&O §5.3.3): an unrecognized value for a known keyword is ignored,
                        // not an error — the strict validation in MakeCollation/CaseFirstCollator is for
                        // saxon:-URI collations only (UCA-collation-012/017/019).
                        switch (kw)
                        {
                            case "strength":
                                switch (val)
                                {
                                    case "1": val = "primary"; break;
                                    case "2": val = "secondary"; break;
                                    case "3": val = "tertiary"; break;
                                    case "quaternary":
                                    case "4":
                                    case "5": val = "identical"; break;
                                }

                                if (val != "primary" && val != "secondary" && val != "tertiary" && val != "identical") continue;
                                break;
                            case "caseFirst":
                                if (val != "upper" && val != "lower") continue;
                                kw = "case-order";
                                val += "-first";
                                break;
                            case "numeric":
                                if (val != "yes" && val != "no") continue;
                                kw = "alphanumeric";
                                break;
                        }

                        props.SetProperty(kw, val);
                    }
                }

                return Core.Version.platform.MakeCollation(config, props, uri);
            }
            else
            {
                return null;
            }
        }
    }
}