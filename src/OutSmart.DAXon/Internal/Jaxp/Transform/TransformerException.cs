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

    public class TransformerException : global::System.Exception
    {
        public SourceLocator Locator { get => null; set { } }
        public string MessageAndLocation => Message;
        public TransformerException() : base("") { }
        public TransformerException(string message) : base(message) { }
        public TransformerException(string message, Exception cause) : base(message, cause) { }
        public TransformerException(Exception cause) : base("", cause) { }
        // Phase 5: GetException — Java's TransformerException.getException() returns the cause.
        public Exception GetException() => InnerException;
    }
}
