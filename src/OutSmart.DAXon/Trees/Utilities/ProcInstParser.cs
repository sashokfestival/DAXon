////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Transformation;
using System;
using System.IO;

namespace OutSmart.DAXon.Trees.Utilities
{
    // Faithful port of net.sf.saxon.tree.util.ProcInstParser (Saxon 12.9). Parses pseudo-attributes
    // within processing instructions per "Associating Style Sheets with XML Documents".
    // Java wraps the PI data as "<e ...data.../>" and parses it with a SAX reader; the port does the
    // same with System.Xml.XmlReader (quotes and character references handled by the XML parser).
    internal static class ProcInstParser
    {
        /// <summary>
        /// Get a pseudo-attribute value from PI content, or null if not present.
        /// Throws XPathException (SXCH0005) if the pseudo-attribute syntax is invalid.
        /// </summary>
        public static string GetPseudoAttribute(string content, string name)
        {
            try
            {
                var settings = new System.Xml.XmlReaderSettings
                {
                    DtdProcessing = System.Xml.DtdProcessing.Prohibit,
                    XmlResolver = null,
                };
                using (var reader = System.Xml.XmlReader.Create(new StringReader("<e " + content + "/>"), settings))
                {
                    reader.MoveToContent();
                    return reader.GetAttribute(name);
                }
            }
            catch (Exception)
            {
                throw new XPathException("Invalid syntax for pseudo-attributes: '" + content + "'. ", "SXCH0005");
            }
        }
    }
}
