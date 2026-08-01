////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
namespace OutSmart.DAXon.Serialization
{
    // A place transformation/serialization output can be sent (ex-JAXP Result): a
    // StreamResult (stream/writer holder), a UnicodeWriterResult, or a Receiver itself
    // (IReceiver extends this interface).
    public interface IResultTarget
    {
        string GetSystemId();
        void SetSystemId(string systemId);
    }
}
