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

    public abstract class Transformer
    {
        public virtual void SetParameter(string name, object value) { }
        public virtual object GetParameter(string name) => null;
        public virtual void ClearParameters() { }
        public virtual void SetURIResolver(URIResolver r) { }
        public virtual URIResolver GetURIResolver() => null;
        public virtual void SetErrorListener(ErrorListener l) { }
        public virtual ErrorListener GetErrorListener() => null;
    }
}
