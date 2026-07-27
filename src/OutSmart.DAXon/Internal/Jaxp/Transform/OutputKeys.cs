////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// JAXP / javax.xml.transform stubs. Minimal interface/class shapes so transpiled Saxon code
// type-resolves. NOT functional — Saxon's TrAX/SAX bridging will be reworked in Phase 3.2 to
// use System.Xml.* natively.

using System;

namespace OutSmart.DAXon.Internal.Jaxp.Transform
{
    public class OutputKeys
    {
        public const string METHOD = "method";
        public const string VERSION = "version";
        public const string ENCODING = "encoding";
        public const string OMIT_XML_DECLARATION = "omit-xml-declaration";
        public const string STANDALONE = "standalone";
        public const string DOCTYPE_PUBLIC = "doctype-public";
        public const string DOCTYPE_SYSTEM = "doctype-system";
        public const string CDATA_SECTION_ELEMENTS = "cdata-section-elements";
        public const string INDENT = "indent";
        public const string MEDIA_TYPE = "media-type";
    }
}
