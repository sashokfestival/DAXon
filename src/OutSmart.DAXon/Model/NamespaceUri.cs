////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Model
{
    public class NamespaceUri
    {
        // A map from strings to NamespaceUris. Concurrent (as in the Java original): Of() is called
        // while parsing source documents, and two threads first meeting new URIs at once would race
        // an unsynchronized Dictionary's resize. Grows with distinct URIs for the process lifetime.
        //
        // NOT a cache, and deliberately NOT bounded (round A3). NamespaceUri overrides neither
        // Equals nor GetHashCode nor ==, so every comparison in the engine - IsEmpty() against NULL,
        // IsReserved() against XSLT/FN/..., the built-in function-set dispatch, ~15 explicit
        // `== NamespaceUri.X` sites - is REFERENCE equality. This table is what makes that sound.
        // Evicting an entry would let the same URI be interned twice and silently compare unequal
        // to itself: the null namespace stops being empty, XSLT stops being reserved, built-ins
        // stop resolving. Scoping it per-Configuration fails for the same reason (the well-known
        // singletons are static). Bounding becomes possible only after equality is made value-based,
        // which would put a string compare on the hottest path in the engine. Watch InternedCount
        // instead; see docs/HOSTING.md.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, NamespaceUri> stringToNamespaceUri
            = new System.Collections.Concurrent.ConcurrentDictionary<string, NamespaceUri>();

        /// <summary>
        /// Distinct namespace URIs interned process-wide. Only ever grows, and is NOT released by
        /// disposing a Processor - this table outlives every Configuration. Diagnostic.
        /// </summary>
        public static int InternedCount => stringToNamespaceUri.Count;

        /// <summary>
        /// A URI representing the null namespace (actually, an empty string)
        /// </summary>
        public static readonly NamespaceUri NULL = NamespaceUri.Of("");
        /// <summary>
        /// Fixed namespace name for XML: "http://www.w3.org/XML/1998/namespace".
        /// </summary>
        public static readonly NamespaceUri XML = NamespaceUri.Of("http://www.w3.org/XML/1998/namespace");
        /// <summary>
        /// Fixed namespace name for XSLT: "http://www.w3.org/1999/XSL/Transform"
        /// </summary>
        public static readonly NamespaceUri XSLT = NamespaceUri.Of("http://www.w3.org/1999/XSL/Transform");
        /// <summary>
        /// Current namespace name for SAXON (from 7.0 onwards): "http://saxon.sf.net/"
        /// </summary>
        public static readonly NamespaceUri SAXON = NamespaceUri.Of("http://saxon.sf.net/");
        /// <summary>
        /// Old namespace name for SAXON6: "http://icl.com/saxon"
        /// </summary>
        public static readonly NamespaceUri SAXON6 = NamespaceUri.Of("http://icl.com/saxon");
        /// <summary>
        /// Fixed namespace name for the export of a Saxon stylesheet package
        /// </summary>
        public static readonly NamespaceUri SAXON_XSLT_EXPORT = NamespaceUri.Of("http://ns.saxonica.com/xslt/export");
        /// <summary>
        /// Namespace name for XML Schema: "http://www.w3.org/2001/XMLSchema"
        /// </summary>
        public static readonly NamespaceUri SCHEMA = NamespaceUri.Of("http://www.w3.org/2001/XMLSchema");
        /// <summary>
        /// XML-schema-defined namespace for use in instance documents ("xsi")
        /// </summary>
        public static readonly NamespaceUri SCHEMA_INSTANCE = NamespaceUri.Of("http://www.w3.org/2001/XMLSchema-instance");
        /// <summary>
        /// Namespace defined in XSD 1.1 for schema versioning
        /// </summary>
        public static readonly NamespaceUri SCHEMA_VERSIONING = NamespaceUri.Of("http://www.w3.org/2007/XMLSchema-versioning");
        /// <summary>
        /// Fixed namespace name for SAXON SQL extension: "http://saxon.sf.net/sql"
        /// </summary>
        public static readonly NamespaceUri SQL = NamespaceUri.Of("http://saxon.sf.net/sql");
        /// <summary>
        /// Fixed namespace name for EXSLT/Common: "http://exslt.org/common"
        /// </summary>
        public static readonly NamespaceUri EXSLT_COMMON = NamespaceUri.Of("http://exslt.org/common");
        /// <summary>
        /// Fixed namespace name for EXSLT/math: "http://exslt.org/math"
        /// </summary>
        public static readonly NamespaceUri EXSLT_MATH = NamespaceUri.Of("http://exslt.org/math");
        /// <summary>
        /// Fixed namespace name for EXSLT/sets: "http://exslt.org/sets"
        /// </summary>
        public static readonly NamespaceUri EXSLT_SETS = NamespaceUri.Of("http://exslt.org/sets");
        /// <summary>
        /// Fixed namespace name for EXSLT/date: "http://exslt.org/dates-and-times"
        /// </summary>
        public static readonly NamespaceUri EXSLT_DATES_AND_TIMES = NamespaceUri.Of("http://exslt.org/dates-and-times");
        /// <summary>
        /// Fixed namespace name for EXSLT/random: "http://exslt.org/random"
        /// </summary>
        public static readonly NamespaceUri EXSLT_RANDOM = NamespaceUri.Of("http://exslt.org/random");
        /// <summary>
        /// The standard namespace for functions and operators
        /// </summary>
        public static readonly NamespaceUri FN = NamespaceUri.Of("http://www.w3.org/2005/xpath-functions");
        /// <summary>
        /// The standard namespace for XQuery output declarations
        /// </summary>
        public static readonly NamespaceUri OUTPUT = NamespaceUri.Of("http://www.w3.org/2010/xslt-xquery-serialization");
        /// <summary>
        /// The standard namespace for system error codes
        /// </summary>
        public static readonly NamespaceUri ERR = NamespaceUri.Of("http://www.w3.org/2005/xqt-errors");
        /// <summary>
        /// Predefined XQuery namespace for local functions
        /// </summary>
        public static readonly NamespaceUri LOCAL = NamespaceUri.Of("http://www.w3.org/2005/xquery-local-functions");
        /// <summary>
        /// Math namespace for the XPath 3.0 math functions
        /// </summary>
        public static readonly NamespaceUri MATH = NamespaceUri.Of("http://www.w3.org/2005/xpath-functions/math");
        /// <summary>
        /// Namespace URI for XPath 3.0 functions associated with maps
        /// </summary>
        public static readonly NamespaceUri MAP_FUNCTIONS = NamespaceUri.Of("http://www.w3.org/2005/xpath-functions/map");
        /// <summary>
        /// Namespace URI for XPath 3.1 functions associated with arrays
        /// </summary>
        public static readonly NamespaceUri ARRAY_FUNCTIONS = NamespaceUri.Of("http://www.w3.org/2005/xpath-functions/array");
        /// <summary>
        /// Namespace URI for the EXPath Binary module
        /// </summary>
        public static readonly NamespaceUri EXPATH_BINARY = NamespaceUri.Of("http://expath.org/ns/binary");
        /// <summary>
        /// Namespace URI for the EXPath string module
        /// </summary>
        public static readonly NamespaceUri EXPATH_FILE = NamespaceUri.Of("http://expath.org/ns/file");
        /// <summary>
        /// The XHTML namespace http://www.w3.org/1999/xhtml
        /// </summary>
        public static readonly NamespaceUri XHTML = NamespaceUri.Of("http://www.w3.org/1999/xhtml");
        /// <summary>
        /// The SVG namespace http://www.w3.org/2000/svg
        /// </summary>
        public static readonly NamespaceUri SVG = NamespaceUri.Of("http://www.w3.org/2000/svg");
        /// <summary>
        /// The MathML namespace http://www.w3.org/1998/Math/MathML
        /// </summary>
        public static readonly NamespaceUri MATHML = NamespaceUri.Of("http://www.w3.org/1998/Math/MathML");
        /// <summary>
        /// The XMLNS namespace http://www.w3.org/2000/xmlns/ (used in DOM)
        /// </summary>
        public static readonly NamespaceUri XMLNS = NamespaceUri.Of("http://www.w3.org/2000/xmlns/");
        /// <summary>
        /// The XLink namespace http://www.w3.org/1999/xlink
        /// </summary>
        public static readonly NamespaceUri XLINK = NamespaceUri.Of("http://www.w3.org/1999/xlink");
        /// <summary>
        /// The xquery namespace http://www.w3.org/2012/xquery for the XQuery 3.0 declare option
        /// </summary>
        public static readonly NamespaceUri XQUERY = NamespaceUri.Of("http://www.w3.org/2012/xquery");
        /// <summary>
        /// Namespace for types representing external Java objects: http://saxon.sf.net/java-type
        /// </summary>
        public static readonly NamespaceUri JAVA_TYPE = NamespaceUri.Of("http://saxon.sf.net/java-type");
        /// <summary>
        /// Namespace for types representing external .NET objects
        /// </summary>
        public static readonly NamespaceUri DOT_NET_TYPE = NamespaceUri.Of("http://saxon.sf.net/clitype");
        public static readonly NamespaceUri ANONYMOUS = NamespaceUri.Of("http://ns.saxonica.com/anonymous-type");
        /// <summary>
        /// Namespace for the Saxon serialization of the schema component model
        /// </summary>
        public static readonly NamespaceUri SCM = NamespaceUri.Of("http://ns.saxonica.com/schema-component-model");
        /// <summary>
        /// URI identifying the Saxon object model for use in the JAXP 1.3 XPath API
        /// </summary>
        public static readonly NamespaceUri OBJECT_MODEL_SAXON = NamespaceUri.Of("http://saxon.sf.net/jaxp/xpath/om");
        /// <summary>
        /// URI identifying the XOM object model for use in the JAXP 1.3 XPath API
        /// </summary>
        public static readonly NamespaceUri OBJECT_MODEL_XOM = NamespaceUri.Of("http://www.xom.nu/jaxp/xpath/xom");
        /// <summary>
        /// URI identifying the JDOM object model for use in the JAXP 1.3 XPath API
        /// </summary>
        public static readonly NamespaceUri OBJECT_MODEL_JDOM = NamespaceUri.Of("http://jdom.org/jaxp/xpath/jdom");
        /// <summary>
        /// URI identifying the AXIOM object model for use in the JAXP 1.3 XPath API
        /// </summary>
        // Note: this URI is a Saxon invention
        public static readonly NamespaceUri OBJECT_MODEL_AXIOM = NamespaceUri.Of("http://ws.apache.org/jaxp/xpath/axiom");
        /// <summary>
        /// URI identifying the DOM4J object model for use in the JAXP 1.3 XPath API
        /// </summary>
        public static readonly NamespaceUri OBJECT_MODEL_DOM4J = NamespaceUri.Of("http://www.dom4j.org/jaxp/xpath/dom4j");
        /// <summary>
        /// URI identifying the .NET DOM object model (not used, but needed for consistency)
        /// </summary>
        public static readonly NamespaceUri OBJECT_MODEL_DOT_NET_DOM = NamespaceUri.Of("http://saxon.sf.net/object-model/dotnet/dom");
        /// <summary>
        /// URI identifying the DOMINO object model (not used, but needed for consistency)
        /// </summary>
        public static readonly NamespaceUri OBJECT_MODEL_DOMINO = NamespaceUri.Of("http://saxon.sf.net/object-model/domino");
        /// <summary>
        /// URI for the names of generated variables
        /// </summary>
        public static readonly NamespaceUri SAXON_GENERATED_VARIABLE = NamespaceUri.Of("http://saxon.sf.net/generated-variable");
        /// <summary>
        /// URI for the Saxon configuration file
        /// </summary>
        public static readonly NamespaceUri SAXON_CONFIGURATION = NamespaceUri.Of("http://saxon.sf.net/ns/configuration");
        /// <summary>
        /// URI for the EXPath zip module
        /// </summary>
        public static readonly NamespaceUri EXPATH_ZIP = NamespaceUri.Of("http://expath.org/ns/zip");
        /// <summary>
        /// URI for the user extension calls in SaxonJS
        /// </summary>
        public static readonly NamespaceUri GLOBAL_JS = NamespaceUri.Of("http://saxonica.com/ns/globalJS");
        /// <summary>
        /// URI for the user extension calls in SaxonC for C++ and PHP
        /// </summary>
        public static readonly NamespaceUri PHP = NamespaceUri.Of("http://php.net/xsl");
        /// <summary>
        /// URI for interactive XSLT extensions in Saxon-CE and SaxonJS
        /// </summary>
        public static readonly NamespaceUri IXSL = NamespaceUri.Of("http://saxonica.com/ns/interactiveXSLT");

        private readonly string stringContent;
        private readonly UnicodeString unicodeStringContent;
        private NamespaceUri(string content)
        {
            this.stringContent = content;
            this.unicodeStringContent = StringTool.FromCharSequence(content);
        }
        public static NamespaceUri Of(string content)
        {
            if (content == null)
            {
                content = "";
            }

            return stringToNamespaceUri.GetOrAdd(Whitespace.Trim(content), k => new NamespaceUri(k));
        }

        public static implicit operator string(NamespaceUri u) => u?.stringContent;
        public override string ToString()
        {
            return stringContent;
        }

        public virtual UnicodeString ToUnicodeString()
        {
            return unicodeStringContent;
        }

        public virtual bool IsEmpty()
        {
            return this == NamespaceUri.NULL;
        }

        public virtual StructuredQName QName(string localName)
        {
            return new StructuredQName("", this, localName);
        }
        public static NamespaceUri GetUriForConventionalPrefix(string prefix)
        {
            switch (prefix)
            {
                case "xsl":
                    return XSLT;
                case "fn":
                    return FN;
                case "xml":
                    return XML;
                case "xs":
                    return SCHEMA;
                case "xsi":
                    return SCHEMA_INSTANCE;
                case "err":
                    return ERR;
                case "ixsl":
                    return IXSL;
                case "js":
                    return GLOBAL_JS;
                case "saxon":
                    return SAXON;
                case "vv":
                    return SAXON_GENERATED_VARIABLE;
                case "math":
                    return MATH;
                case "map":
                    return MAP_FUNCTIONS;
                case "array":
                    return ARRAY_FUNCTIONS;
                default:
                    return null;
            }
        }

        public static bool IsReserved(NamespaceUri uri)
        {
            return uri != null && (uri.Equals(XSLT) || uri.Equals(FN) || uri.Equals(MATH) || uri.Equals(MAP_FUNCTIONS) || uri.Equals(ARRAY_FUNCTIONS) || uri.Equals(XML) || uri.Equals(SCHEMA) || uri.Equals(SCHEMA_INSTANCE) || uri.Equals(ERR) || uri.Equals(XMLNS));
        }

        public static bool IsReservedInQuery31(NamespaceUri uri)
        {
            return uri.Equals(FN) || uri.Equals(XML) || uri.Equals(SCHEMA) || uri.Equals(SCHEMA_INSTANCE) || uri.Equals(MATH) || uri.Equals(XQUERY) || uri.Equals(MAP_FUNCTIONS) || uri.Equals(ARRAY_FUNCTIONS);
        }
    }
}