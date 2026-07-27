////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// java.time stubs.
using System;

namespace OutSmart.DAXon.Internal.Jaxp.Stax
{
    using global::System;
    public class XMLStreamException : Exception
    {
        public XMLStreamException(string m) : base(m) { }
        public XMLStreamException(Exception cause) : base(cause?.Message, cause) { }
        public XMLStreamException(string m, Exception cause) : base(m, cause) { }
    }
}
