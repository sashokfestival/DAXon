////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Jaxp.Transform;
using OutSmart.DAXon.Internal.Jaxp.Transform.Stream;
using OutSmart.DAXon.Internal.Streams;
using OutSmart.DAXon.Core;
using System.IO;
namespace OutSmart.DAXon.Lib
{
    /// <summary>
    /// A {@link IResourceResolver} implemented by wrapping a supplied {@link URIResolver}
    /// </summary>
    public class ResourceResolverWrappingURIResolver : IResourceResolver
    {
        private readonly URIResolver uriResolver;

        public virtual URIResolver WrappedURIResolver => uriResolver;
        public ResourceResolverWrappingURIResolver(URIResolver uriResolver)
        {
            if (uriResolver == null)
                throw new NullReferenceException();
            this.uriResolver = uriResolver;
        }

        public virtual ResolvedResource Resolve(ResourceRequest request)
        {
            string href;
            if (request.relativeUri != null && request.baseUri != null)
            {
                href = request.relativeUri;
            }
            else if (request.uri != null)
            {
                href = request.uri;
            }
            else
            {
                return null;
            }

            try
            {
                // The URIResolver boundary is now .NET-native (ResolvedResource), so no Source/SAXSource
                // bridging is needed — hand the resolved resource straight through.
                return uriResolver.Resolve(href, request.baseUri);
            }
            catch (TransformerException e)
            {
                throw XPathException.MakeXPathException(e);
            }
        }
    }
}