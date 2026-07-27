////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Resources;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal.Net;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
using OutSmart.DAXon.Internal.Jaxp.Transform;
using OutSmart.DAXon.Internal.Jaxp.Transform.Stream;
using OutSmart.DAXon.Internal.Streams;
using System.IO;
namespace OutSmart.DAXon.Lib
{
    public class DirectResourceResolver : IResourceResolver
    {
        private readonly Configuration config;
        public DirectResourceResolver(Configuration config)
        {
            this.config = config;
        }

        public virtual ResolvedResource Resolve(ResourceRequest request)
        {
            if (request.uriIsNamespace)
            {
                return null; // bug 5266
            }

            ProtocolRestrictor restrictor = config.GetProtocolRestrictor();
            if (!"all".Equals(restrictor.ToString()))
            {
                try
                {
                    URI u = new URI(request.uri);
                    if (!restrictor.Test(u))
                    {
                        throw new XPathException("URIs using protocol " + u.Scheme + " are not permitted");
                    }
                }
                catch (URISyntaxException err)
                {
                    throw new XPathException("Unknown URI scheme requested " + request.uri);
                }
            }

            System.IO.Stream stream;
            if (ResourceRequest.BINARY_NATURE.Equals(request.nature))
            {

                // This path is used by the unparsed-text resolver when no encoding is supplied;
                // it returns (where possible) the binary input stream together with the content type
                // from the HTTP headers.
                string uri;
                try
                {
                    if (request.baseUri == null)
                    {
                        uri = request.uri;
                    }
                    else
                    {
                        uri = new URI(request.baseUri).Resolve(request.uri).ToString();
                    }

                    return ResourceLoader.TypedResource(config, uri);
                }
                catch (IOException e)
                {
                    throw new XPathException("Cannot read " + request.uri, e);
                }
                catch (URISyntaxException e)
                {
                    throw new XPathException("Cannot read " + request.uri, e);
                }
            }

            try
            {

                // Get an input stream from the request URI
                stream = ResourceLoader.UrlStream(config, request.uri);
            }
            catch (IOException e)
            {
                stream = null; // Carry on, the XML parser might know what to do with it.
            }

            if (ResourceRequest.TEXT_NATURE.Equals(request.nature))
            {

                // Typically happens when using unparsed-text() with an explicit encoding
                return new ResolvedResource { Stream = stream, SystemId = request.uri };
            }

            // Default: an XML resource. Return the raw byte stream + systemId. It is delivered through the
            // direct System.Xml.XmlReader path (StreamSource -> ActiveStreamSource -> XmlReaderToReceiver),
            // no longer via a SAXSource + SAX round-trip. We opened the stream, so it is closed after the parse.
            ResolvedResource xml = new ResolvedResource();
            xml.Stream = stream;
            xml.SystemId = request.uri;
            xml.PleaseCloseAfterUse = stream != null;
            return xml;
        }
    }
}