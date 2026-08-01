////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Stubs for Saxon types whose source files are EXCLUDED from build (too many compile errors).
// These types exist only to satisfy CS0246 references in other Saxon files. Calling them throws.
// Add more here as we exclude more files.

using System;

namespace OutSmart.DAXon.Api
{

    public class XdmStream<T>
    {
        public XdmStream() { }
        // 1-arg ctor wrapping an iterator/sequence.
        public XdmStream(object source) { }
        public XdmStream<T> Filter(object predicate) => throw new NotImplementedException("XdmStream excluded from build");
        public XdmStream<TR> Map<TR>(object mapper) => throw new NotImplementedException("XdmStream excluded from build");
        public T First() => throw new NotImplementedException("XdmStream excluded from build");
        public object Collect(object collector) => throw new NotImplementedException("XdmStream excluded from build");
    }
}
