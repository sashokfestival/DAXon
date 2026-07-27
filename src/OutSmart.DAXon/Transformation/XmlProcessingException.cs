////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Trees;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Transformation
{
    public class XmlProcessingException : IXmlProcessingError
    {
        private readonly XPathException exception;
        private bool _isWarning;
        private string fatalErrorMessage;

        public virtual string TerminationMessage
        {
            get => this.fatalErrorMessage; set
            {
                this.fatalErrorMessage = value;
            }
        }

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
        public virtual string ModuleUri => throw new NotImplementedException();
        public XmlProcessingException(XPathException exception)
        {
            this.exception = exception;
        }

        public virtual XPathException GetXPathException()
        {
            return exception;
        }

        public virtual HostLanguage GetHostLanguage()
        {
            ILocation loc = GetLocation();
            if (loc is Instruction || loc is AttributeLocation)
            {
                return HostLanguage.XSLT;
            }
            else
            {
                return HostLanguage.XPATH;
            }
        }

        public virtual bool IsStaticError()
        {
            return exception.IsStaticError();
        }

        public virtual bool IsTypeError()
        {
            return exception.IsTypeError();
        }

        public virtual QName GetErrorCode()
        {
            StructuredQName errorCodeQName = exception.ErrorCodeQName;
            return errorCodeQName == null ? null : new QName(errorCodeQName);
        }

        public virtual string GetMessage()
        {
            return exception.GetMessage();
        }

        public virtual ILocation GetLocation()
        {
            return exception.GetLocator() == null ? Loc.NONE : exception.GetLocator();
        }

        public virtual bool IsWarning()
        {
            return _isWarning;
        }

        public virtual string GetPath()
        {
            return null;
        }

        public virtual Exception GetCause()
        {
            return (Exception)exception.GetCause();
        }

        public virtual Expression GetFailingExpression()
        {
            return exception.GetFailingExpression();
        }

        public virtual void SetWarning(bool warning)
        {
            _isWarning = warning;
        }

        public virtual XmlProcessingException AsWarning()
        {
            XmlProcessingException e2 = new XmlProcessingException(exception);
            e2.SetWarning(true);
            return e2;
        }

        public virtual bool IsAlreadyReported()
        {
            return exception.HasBeenReported();
        }

        public virtual void SetAlreadyReported(bool reported)
        {
            exception.SetHasBeenReported(reported);
        }
        IXmlProcessingError IXmlProcessingError.AsWarning() => AsWarning();
    }
}