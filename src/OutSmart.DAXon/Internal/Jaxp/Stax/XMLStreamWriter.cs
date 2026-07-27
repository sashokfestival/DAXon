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
    public interface XMLStreamWriter
    {
        void Close();
        void Flush();
        void WriteStartDocument();
        void WriteEndDocument();
        void WriteStartElement(string localName);
        void WriteEndElement();
        void WriteCharacters(string text);
        void WriteAttribute(string localName, string value);
    }
}
