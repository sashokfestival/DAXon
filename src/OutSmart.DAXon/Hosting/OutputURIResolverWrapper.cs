////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Serialization;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Jaxp.Transform;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Core;
namespace OutSmart.DAXon.Lib
{
    public class OutputURIResolverWrapper : IResultDocumentResolver
    {
        private readonly IOutputURIResolver outputURIResolver;
        public OutputURIResolverWrapper(IOutputURIResolver resolver)
        {
            this.outputURIResolver = resolver;
        }

        public virtual IReceiver Resolve(IXPathContext context, string href, string baseUri, SerializationProperties properties)
        {
            IOutputURIResolver r2 = outputURIResolver.NewInstance();
            try
            {
                Result result = r2.Resolve(href, baseUri);
                IAction onClose = () =>
                {
                    try
                    {
                        r2.Dispose(result);
                    }
                    catch (TransformerException te)
                    {
                        throw new UncheckedXPathException(XPathException.MakeXPathException(te));
                    }
                };
                IReceiver @out;
                if (result is IReceiver)
                {
                    @out = (IReceiver)result;
                }
                else
                {
                    SerializerFactory factory = context.GetConfiguration().SerializerFactory;
                    PipelineConfiguration pipe = context.GetController().MakePipelineConfiguration();
                    pipe.XPathContext = context;
                    @out = factory.GetReceiver(result, properties, pipe);
                }

                IList<IAction> actions = new List<IAction>();
                actions.Add(onClose);
                return new CloseNotifier(@out, actions);
            }
            catch (TransformerException e)
            {
                throw XPathException.MakeXPathException(e);
            }
        }

        public virtual IOutputURIResolver GetOutputURIResolver()
        {
            return outputURIResolver;
        }
    }
}