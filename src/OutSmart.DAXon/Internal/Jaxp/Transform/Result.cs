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
    // Phase 5: Java's Result has these PI constants. C# interfaces can't have
    // const fields (with net472/C# 7.3), and changing to class breaks Receiver/
    // ComplexContentOutputter which use Result as interface. Keep as interface;
    // PI_* constants delegated to a static class `ResultConsts` below.
    public interface Result
    {
        string GetSystemId();
        void SetSystemId(string systemId);
    }
}
