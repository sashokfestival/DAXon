////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Transformation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Api
{
    /// <summary>
    /// An exception thrown by the Saxon s9api API. This is always a wrapper for some other underlying exception
    /// </summary>
    public class DAXonApiException : Exception
    {
        public DAXonApiException(Exception cause) : base(cause?.Message, cause)
        {
        }

        public DAXonApiException(UncheckedXPathException cause) : base(cause.GetXPathException()?.Message, cause.GetXPathException())
        {
        }

        public DAXonApiException(string message) : base(message, new XPathException(message))
        {
        }

        public DAXonApiException(string message, Exception cause) : base(message, new XPathException(message, cause))
        {
        }

        public string GetMessage()
        {
            return InnerException.GetMessage();
        }

        public virtual QName GetErrorCode()
        {
            Exception cause = (Exception)InnerException;
            if (cause is XPathException)
            {
                StructuredQName code = ((XPathException)cause).ErrorCodeQName;
                return code == null ? null : new QName(code);
            }
            else
            {
                return null;
            }
        }

        public virtual int GetLineNumber()
        {
            Exception cause = (Exception)InnerException;
            if (cause is XPathException)
            {
                ILocation loc = ((XPathException)cause).GetLocator();
                return loc == null ? -1 : loc.GetLineNumber();
            }
            else
            {
                return -1;
            }
        }

        public virtual string GetSystemId()
        {
            Exception cause = (Exception)InnerException;
            if (cause is XPathException)
            {
                ILocation loc = ((XPathException)cause).GetLocator();
                return loc == null ? null : loc.GetSystemId();
            }
            else
            {
                return null;
            }
        }
    }
}