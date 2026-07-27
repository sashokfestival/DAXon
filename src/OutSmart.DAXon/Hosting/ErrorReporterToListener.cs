////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Api;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Jaxp.Transform;
using OutSmart.DAXon.Core;
namespace OutSmart.DAXon.Lib
{
    public class ErrorReporterToListener : IErrorReporter
    {
        private readonly ErrorListener listener;
        public ErrorReporterToListener(ErrorListener listener)
        {
            this.listener = listener ?? throw new NullReferenceException();
        }

        public virtual ErrorListener GetErrorListener()
        {
            return listener;
        }

        public virtual void Report(IXmlProcessingError error)
        {
            if (!error.IsAlreadyReported())
            {
                try
                {
                    XPathException err = XPathException.FromXmlProcessingError(error);
                    if (error.IsWarning())
                    {
                        listener.Warning(err);
                    }
                    else
                    {
                        listener.FatalError(err);
                    }

                    error.SetAlreadyReported(true);
                }
                catch (TransformerException e)
                {
                    error.TerminationMessage = e.GetMessage();
                }
            }
        }
    }
}