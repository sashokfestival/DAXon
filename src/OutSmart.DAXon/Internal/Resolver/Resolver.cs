////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace OutSmart.DAXon.Internal.Resolver
{
    /// <summary>Stub for org.xmlresolver.Resolver (XML Catalog resolver).
    /// CatalogResourceResolver.cs references this; it's an optional
    /// catalog-based URI resolution feature. The full library is not ported.</summary>
    public class Resolver
    {
        public Resolver() { }
        public Resolver(object config) { }
        public T GetFeature<T>(object feature) => default;
        public void SetFeature(object feature, object value) { }
    }
}
