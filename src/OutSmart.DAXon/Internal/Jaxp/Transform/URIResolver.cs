////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// A legacy JAXP-shaped URI resolver, nativized in Phase 5: it now hands back the .NET-typed
// OutSmart.DAXon.Lib.ResolvedResource (byte Stream / char TextReader / already-built NodeInfo) instead of a
// JAXP Source. Kept as a convenience two-string-argument entry point (href/base) over the fuller
// IResourceResolver(ResourceRequest); Configuration wraps a supplied URIResolver as a
// ResourceResolverWrappingURIResolver.

using System;
using OutSmart.DAXon.Lib;

namespace OutSmart.DAXon.Internal.Jaxp.Transform
{
    public interface URIResolver { ResolvedResource Resolve(string href, string @base); }
}
