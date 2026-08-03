////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Api;
using System;
// DAXonPhaseBHelpers.cs — real, reusable adapters added during Phase B (un-stub the
// ProcessXml critical path). Compiled into OutSmart.DAXon (project-dir *.cs is auto-included).
// These are NOT stubs: they implement genuine behaviour missing only because paulirwin cannot
// convert a C# lambda/method-group to a Java @FunctionalInterface (which is not a C# delegate).

namespace OutSmart.DAXon.Lib
{
    /// <summary>
    /// Bridges a delegate to the single-method <see cref="IErrorReporter"/> functional
    /// interface. Java code uses method references such as <c>errorList::add</c> as an
    /// ErrorReporter; C# cannot convert a lambda to a non-delegate interface, so the
    /// transpiled <c>SetErrorReporter(err =&gt; errorList.Add(err))</c> fails CS1660.
    /// Used by XsltCompiler.SetErrorList and XQueryCompiler.SetErrorList.
    /// </summary>
    internal sealed class DelegateErrorReporter : IErrorReporter
    {
        private readonly Action<IXmlProcessingError> _action;

        public DelegateErrorReporter(Action<IXmlProcessingError> action)
        {
            _action = action;
        }

        public void Report(IXmlProcessingError error)
        {
            if (_action != null)
            {
                _action(error);
            }
        }
    }
}
