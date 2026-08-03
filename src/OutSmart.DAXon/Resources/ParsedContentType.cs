////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Resources
{
    internal class ParsedContentType
    {
        public bool isXmlMediaType;
        public string encoding;
        public ParsedContentType(string contentType)
        {
            string mediaType;
            int pos = contentType.IndexOf(';');
            if (pos >= 0)
            {
                mediaType = contentType.Substring(0, pos);
            }
            else
            {
                mediaType = contentType;
            }

            mediaType = mediaType.Trim();
            isXmlMediaType = (mediaType.StartsWith("application/", StringComparison.Ordinal) || mediaType.StartsWith("text/", StringComparison.Ordinal)) && (mediaType.EndsWith("/xml", StringComparison.Ordinal) || mediaType.EndsWith("+xml", StringComparison.Ordinal));
            string charset = "";
            pos = contentType.ToLowerInvariant().IndexOf("charset", StringComparison.Ordinal);
            if (pos >= 0)
            {
                pos = contentType.IndexOf('=', pos + 7);
                if (pos >= 0)
                {
                    charset = contentType.Substring(pos + 1);
                }

                if ((pos = charset.IndexOf(';')) > 0)
                {
                    charset = charset.Substring(0, pos);
                }


                // attributes can have comment fields (RFC 822)
                if ((pos = charset.IndexOf('(')) > 0)
                {
                    charset = charset.Substring(0, pos);
                }


                // ... and values may be quoted
                if ((pos = charset.IndexOf('"')) > 0)
                {
                    charset = charset.Substring(pos + 1, charset.IndexOf('"', pos + 2) - pos - 1) /*Java substring(begin,END) -> C# (start,LENGTH)*/;
                }

                encoding = charset.Trim();
            }
        }
    }
}