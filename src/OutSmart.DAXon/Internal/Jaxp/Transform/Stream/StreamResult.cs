////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// JAXP / javax.xml.transform stubs. Minimal interface/class shapes so transpiled Saxon code
// type-resolves. NOT functional — Saxon's TrAX/SAX bridging will be reworked in Phase 3.2 to
// use System.Xml.* natively.

using System;

namespace OutSmart.DAXon.Internal.Jaxp.Transform.Stream
{
    using global::OutSmart.DAXon.Internal.Jaxp.Transform;
    public class StreamResult : Result
    {
        public string SystemId { get; set; }
        public global::System.IO.Stream OutputStream { get; set; }
        public global::System.IO.TextWriter Writer { get; set; }
        public StreamResult() { }
        public StreamResult(string systemId) { SystemId = systemId; }
        public StreamResult(global::System.IO.Stream s) { OutputStream = s; }
        public StreamResult(global::System.IO.TextWriter w) { Writer = w; }
        public string GetSystemId() => SystemId;
        public void SetSystemId(string s) { SystemId = s; }
        public global::System.IO.Stream GetOutputStream() => OutputStream;
        public global::System.IO.TextWriter GetWriter() => Writer;
        // Phase 5: SetWriter / SetOutputStream setters used by SerializerFactory etc.
        public void SetWriter(global::System.IO.TextWriter w) { Writer = w; }
        public void SetOutputStream(global::System.IO.Stream s) { OutputStream = s; }
    }
}
