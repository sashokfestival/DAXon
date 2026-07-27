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
    // Phase 7.20: PI constants on a sibling static class (different name to
    // avoid interface/class collision). Callers using `Result.PI_*` still
    // fail; addressed by a separate codemod patch.
    public static class ResultConsts
    {
        public const string PI_DISABLE_OUTPUT_ESCAPING = "javax.xml.transform.disable-output-escaping";
        public const string PI_ENABLE_OUTPUT_ESCAPING = "javax.xml.transform.enable-output-escaping";
    }
}
