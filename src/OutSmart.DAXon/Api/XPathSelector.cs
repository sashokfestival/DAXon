////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Api.Streams;
using OutSmart.DAXon.XPath;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Jaxp.Transform;
using OutSmart.DAXon.Internal.Streams;
namespace OutSmart.DAXon.Api
{
    //@CSharpInjectMembers(code = {
    //        "    public void setErrorReporter(global::System.Action<OutSmart.DAXon.Api.IXmlProcessingError> reporter) {"
    //                + "        setErrorReporter(new Saxon.Impl.Helpers.ErrorReportingAction(reporter));"
    //                + "    }"
    //})
    public class XPathSelector : IEnumerable<XdmItem>
    {
        private readonly XPathExpression exp;
        private readonly XPathDynamicContext dynamicContext;
        private readonly Dictionary<StructuredQName, XPathVariable> declaredVariables;

        public virtual XPathDynamicContext UnderlyingXPathContext => dynamicContext;
        public XPathSelector(XPathExpression exp, Dictionary<StructuredQName, XPathVariable> declaredVariables)
        {
            this.exp = exp;
            this.declaredVariables = declaredVariables;
            dynamicContext = exp.CreateDynamicContext();
        }

        public virtual void SetContextItem(XdmItem item)
        {
            if (item == null)
            {
                throw new NullReferenceException("contextItem");
            }

            if (!exp.InternalExpression.GetPackageData().IsSchemaAware())
            {
                IItem it = item.UnderlyingValue.Head();
                if (it is NodeInfo && ((NodeInfo)it).GetTreeInfo().IsTyped())
                {
                    throw new DAXonApiException("The supplied node has been schema-validated, but the XPath expression was compiled without schema-awareness");
                }
            }

            try
            {
                dynamicContext.ContextItem = item.UnderlyingValue;
            }
            catch (XPathException e)
            {
                throw new DAXonApiException(e);
            }
        }

        public virtual XdmItem GetContextItem()
        {
            return XdmItem.WrapItem(dynamicContext.ContextItem);
        }

        public virtual void SetVariable(QName name, XdmValue value)
        {
            if (name == null)
                throw new NullReferenceException("name");
            if (value == null)
                throw new NullReferenceException("value");
            StructuredQName qn = name.GetStructuredQName();
            XPathVariable var = declaredVariables.Get(qn);
            if (var == null)
            {
                throw new DAXonApiException(new XPathException("Variable has not been declared: " + name));
            }

            try
            {
                dynamicContext.SetVariable(var, (ISequence)(value.UnderlyingValue));
            }
            catch (XPathException e)
            {
                throw new DAXonApiException(e);
            }
            catch (UncheckedXPathException e)
            {
                throw new DAXonApiException(e);
            }
        }

        public virtual void SetResourceResolver(IResourceResolver resolver)
        {
            dynamicContext.ResourceResolver = resolver;
        }

        public virtual IResourceResolver GetResourceResolver()
        {
            return dynamicContext.ResourceResolver;
        }

        public virtual void SetURIResolver(URIResolver resolver)
        {
            dynamicContext.ResourceResolver = new ResourceResolverWrappingURIResolver(resolver);
        }

        public virtual URIResolver GetURIResolver()
        {
            if (dynamicContext.ResourceResolver is ResourceResolverWrappingURIResolver)
            {
                return ((ResourceResolverWrappingURIResolver)dynamicContext.ResourceResolver).WrappedURIResolver;
            }
            else
            {
                return null;
            }
        }

        public virtual void SetUnparsedTextResolver(IUnparsedTextURIResolver resolver)
        {
            dynamicContext.SetUnparsedTextURIResolver(resolver);
        }

        public virtual IUnparsedTextURIResolver GetUnparsedTextURIResolver()
        {
            return dynamicContext.GetUnparsedTextURIResolver();
        }

        public virtual void SetErrorReporter(IErrorReporter reporter)
        {
            dynamicContext.ErrorReporter = reporter;
        }

        public virtual XdmValue Evaluate()
        {
            ISequence value;
            try
            {
                value = SequenceTool.ToGroundedValue(exp.Iterate(dynamicContext));
            }
            catch (UncheckedXPathException uxe)
            {
                throw new DAXonApiException(uxe);
            }
            catch (XPathException e)
            {
                throw new DAXonApiException(e);
            }

            return XdmValue.Wrap(value);
        }

        public virtual XdmItem EvaluateSingle()
        {
            try
            {
                IItem i = exp.EvaluateSingle(dynamicContext);
                if (i == null)
                {
                    return null;
                }

                return (XdmItem)XdmValue.Wrap(i);
            }
            catch (XPathException e)
            {
                throw new DAXonApiException(e);
            }
        }

        public virtual XdmSequenceIterator<XdmItem> IIterator()
        {
            try
            {
                return new XdmSequenceIterator<XdmItem>(exp.Iterate(dynamicContext));
            }
            catch (XPathException e)
            {
                throw new DAXonApiUncheckedException(e);
            }
        }

        public virtual XdmStream<XdmItem> Stream()
        {
            return IIterator().Stream();
        }

        public virtual bool EffectiveBooleanValue()
        {
            try
            {
                return exp.EffectiveBooleanValue(dynamicContext);
            }
            catch (XPathException e)
            {
                throw new DAXonApiException(e);
            }
        }
        public IEnumerator<XdmItem> GetEnumerator() => throw new NotImplementedException();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => throw new NotImplementedException();
    }
}