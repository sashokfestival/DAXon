////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Regex;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Transformation.Rules;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Expressions
{
    public class EarlyEvaluationContext : IXPathContext
    {
        private readonly Configuration config;

        /// <summary>
        /// Get the value of a local variable, identified by its slot number
        /// </summary>
        public virtual XPathContextMajor MajorContext => null;

        /// <summary>
        /// Get the value of a local variable, identified by its slot number
        /// </summary>
        /// <summary>
        /// Set the calling IXPathContext
        /// </summary>
        // no-op
        /// <summary>
        /// Set the value of a local variable, identified by its slot number
        /// </summary>
        public virtual int TemporaryOutputState
        {
            get => 0; set
            {
            }
        }

        /// <summary>
        /// Get the value of a local variable, identified by its slot number
        /// </summary>
        /// <summary>
        /// Set the calling IXPathContext
        /// </summary>
        // no-op
        /// <summary>
        /// Set the value of a local variable, identified by its slot number
        /// </summary>
        public virtual string CurrentOutputUri
        {
            get => null; set
            {
            }
        }
        public EarlyEvaluationContext(Configuration config)
        {
            this.config = config;
        }

        /// <summary>
        /// Get the value of a local variable, identified by its slot number
        /// </summary>
        public virtual ISequence EvaluateLocalVariable(int slotnumber)
        {
            NotAllowed();
            return null;
        }

        /// <summary>
        /// Get the value of a local variable, identified by its slot number
        /// </summary>
        public virtual IXPathContext GetCaller()
        {
            return null;
        }

        /// <summary>
        /// Get the value of a local variable, identified by its slot number
        /// </summary>
        public virtual IResourceResolver GetResourceResolver()
        {
            return config.GetResourceResolver();
        }

        /// <summary>
        /// Get the value of a local variable, identified by its slot number
        /// </summary>
        public virtual IErrorReporter GetErrorReporter()
        {
            return config.MakeErrorReporter();
        }

        /// <summary>
        /// Get the value of a local variable, identified by its slot number
        /// </summary>
        /// <summary>
        /// Get the current component
        /// </summary>
        public virtual Component GetCurrentComponent()
        {
            NotAllowed();
            return null;
        }

        /// <summary>
        /// Get the value of a local variable, identified by its slot number
        /// </summary>
        /// <summary>
        /// Get the Configuration
        /// </summary>
        public virtual Configuration GetConfiguration()
        {
            return config;
        }

        /// <summary>
        /// Get the value of a local variable, identified by its slot number
        /// </summary>
        /// <summary>
        /// Get the Configuration
        /// </summary>
        public virtual IItem GetContextItem()
        {
            return null;
        }

        /// <summary>
        /// Get the value of a local variable, identified by its slot number
        /// </summary>
        /// <summary>
        /// Get the Controller. May return null when running outside XSLT or XQuery
        /// </summary>
        public virtual Controller GetController()
        {
            return null;
        }

        /// <summary>
        /// Get the value of a local variable, identified by its slot number
        /// </summary>
        /// <summary>
        /// Get the Controller. May return null when running outside XSLT or XQuery
        /// </summary>
        public virtual IGroupIterator GetCurrentGroupIterator()
        {
            NotAllowed();
            return null;
        }

        /// <summary>
        /// Get the value of a local variable, identified by its slot number
        /// </summary>
        /// <summary>
        /// Get the Controller. May return null when running outside XSLT or XQuery
        /// </summary>
        public virtual IGroupIterator GetCurrentMergeGroupIterator()
        {
            NotAllowed();
            return null;
        }

        /// <summary>
        /// Get the value of a local variable, identified by its slot number
        /// </summary>
        /// <summary>
        /// Get the Controller. May return null when running outside XSLT or XQuery
        /// </summary>
        public virtual FocusTrackingIterator GetCurrentIterator()
        {
            return null;
        }

        /// <summary>
        /// Get the value of a local variable, identified by its slot number
        /// </summary>
        /// <summary>
        /// Get the Controller. May return null when running outside XSLT or XQuery
        /// </summary>
        public virtual Component.M GetCurrentMode()
        {
            NotAllowed();
            return null;
        }

        /// <summary>
        /// Get the value of a local variable, identified by its slot number
        /// </summary>
        /// <summary>
        /// Get the Controller. May return null when running outside XSLT or XQuery
        /// </summary>
        public virtual IRegexIterator GetCurrentRegexIterator()
        {
            return null;
        }

        /// <summary>
        /// Get the value of a local variable, identified by its slot number
        /// </summary>
        /// <summary>
        /// Get the Controller. May return null when running outside XSLT or XQuery
        /// </summary>
        public virtual Rule GetCurrentTemplateRule()
        {
            return null;
        }

        /// <summary>
        /// Get the value of a local variable, identified by its slot number
        /// </summary>
        /// <summary>
        /// Get the Controller. May return null when running outside XSLT or XQuery
        /// </summary>
        public virtual int GetLast()
        {
            XPathException err = new XPathException("The context item is absent", "XPDY0002");
            throw new UncheckedXPathException(err);
        }

        /// <summary>
        /// Get the value of a local variable, identified by its slot number
        /// </summary>
        /// <summary>
        /// Get the Controller. May return null when running outside XSLT or XQuery
        /// </summary>
        public virtual ParameterSet GetLocalParameters()
        {
            NotAllowed();
            return null;
        }

        /// <summary>
        /// Get the value of a local variable, identified by its slot number
        /// </summary>
        /// <summary>
        /// Get the Name Pool
        /// </summary>
        public virtual NamePool GetNamePool()
        {
            return config.GetNamePool();
        }

        /// <summary>
        /// Get the value of a local variable, identified by its slot number
        /// </summary>
        /// <summary>
        /// Get the Name Pool
        /// </summary>
        public virtual StackFrame GetStackFrame()
        {
            NotAllowed();
            return null;
        }

        /// <summary>
        /// Get the value of a local variable, identified by its slot number
        /// </summary>
        /// <summary>
        /// Get the Name Pool
        /// </summary>
        public virtual ParameterSet GetTunnelParameters()
        {
            NotAllowed();
            return null;
        }

        /// <summary>
        /// Get the value of a local variable, identified by its slot number
        /// </summary>
        /// <summary>
        /// Get the Name Pool
        /// </summary>
        public virtual bool IsAtLast()
        {
            XPathException err = new XPathException("The context item is absent");
            err.SetErrorCode("XPDY0002");
            throw err;
        }

        /// <summary>
        /// Get the value of a local variable, identified by its slot number
        /// </summary>
        /// <summary>
        /// Construct a new context without copying (used for the context in a function call)
        /// </summary>
        public virtual XPathContextMajor NewCleanContext()
        {
            NotAllowed();
            return null;
        }

        /// <summary>
        /// Get the value of a local variable, identified by its slot number
        /// </summary>
        /// <summary>
        /// Construct a new context without copying (used for the context in a function call)
        /// </summary>
        public virtual XPathContextMajor NewContext()
        {
            Controller controller = new Controller(config);
            return controller.NewXPathContext();
        }

        /// <summary>
        /// Get the value of a local variable, identified by its slot number
        /// </summary>
        /// <summary>
        /// Construct a new context without copying (used for the context in a function call)
        /// </summary>
        public virtual XPathContextMinor NewMinorContext()
        {
            return NewContext().NewMinorContext();
        }

        /// <summary>
        /// Get the value of a local variable, identified by its slot number
        /// </summary>
        /// <summary>
        /// Set the calling IXPathContext
        /// </summary>
        public virtual void SetCaller(IXPathContext caller)
        {
        }

        /// <summary>
        /// Get the value of a local variable, identified by its slot number
        /// </summary>
        /// <summary>
        /// Set the calling IXPathContext
        /// </summary>
        // no-op
        /// <summary>
        /// Set a new sequence iterator.
        /// </summary>
        public virtual void SetCurrentIterator(IFocusIterator iter)
        {
            NotAllowed();
        }

        /// <summary>
        /// Get the value of a local variable, identified by its slot number
        /// </summary>
        /// <summary>
        /// Set the calling IXPathContext
        /// </summary>
        // no-op
        /// <summary>
        /// Set a new sequence iterator.
        /// </summary>
        public virtual IFocusIterator TrackFocus(ISequenceIterator iter)
        {
            NotAllowed();
            return null;
        }

        /// <summary>
        /// Get the value of a local variable, identified by its slot number
        /// </summary>
        /// <summary>
        /// Set the calling IXPathContext
        /// </summary>
        // no-op
        /// <summary>
        /// Set the value of a local variable, identified by its slot number
        /// </summary>
        public virtual void SetLocalVariable(int slotNumber, ISequence value)
        {
            NotAllowed();
        }

        /// <summary>
        /// Get the value of a local variable, identified by its slot number
        /// </summary>
        /// <summary>
        /// Set the calling IXPathContext
        /// </summary>
        // no-op
        /// <summary>
        /// Set the value of a local variable, identified by its slot number
        /// </summary>
        public virtual int UseLocalParameter(StructuredQName parameterId, int slotNumber, bool isTunnel)
        {
            return ParameterSet.NOT_SUPPLIED;
        }

        /// <summary>
        /// Get the value of a local variable, identified by its slot number
        /// </summary>
        /// <summary>
        /// Set the calling IXPathContext
        /// </summary>
        // no-op
        /// <summary>
        /// Set the value of a local variable, identified by its slot number
        /// </summary>
        public virtual DateTimeValue GetCurrentDateTime()
        {
            throw new NoDynamicContextException("current-dateTime");
        }

        /// <summary>
        /// Get the value of a local variable, identified by its slot number
        /// </summary>
        /// <summary>
        /// Set the calling IXPathContext
        /// </summary>
        // no-op
        /// <summary>
        /// Set the value of a local variable, identified by its slot number
        /// </summary>
        public virtual int GetImplicitTimezone()
        {
            return CalendarValue.MISSING_TIMEZONE;
        }

        /// <summary>
        /// Get the value of a local variable, identified by its slot number
        /// </summary>
        /// <summary>
        /// Set the calling IXPathContext
        /// </summary>
        // no-op
        /// <summary>
        /// Set the value of a local variable, identified by its slot number
        /// </summary>
        public virtual IEnumerator<ContextStackFrame> IterateStackFrames()
        {
            return System.Linq.Enumerable.Empty<ContextStackFrame>().GetEnumerator();
        }

        /// <summary>
        /// Get the value of a local variable, identified by its slot number
        /// </summary>
        /// <summary>
        /// Set the calling IXPathContext
        /// </summary>
        // no-op
        /// <summary>
        /// Set the value of a local variable, identified by its slot number
        /// </summary>
        public virtual XPathException GetCurrentException()
        {
            return null;
        }

        /// <summary>
        /// Get the value of a local variable, identified by its slot number
        /// </summary>
        /// <summary>
        /// Set the calling IXPathContext
        /// </summary>
        // no-op
        /// <summary>
        /// Set the value of a local variable, identified by its slot number
        /// </summary>
        public virtual void WaitForChildThreads()
        {
            GetCaller().WaitForChildThreads();
        }

        /// <summary>
        /// Get the value of a local variable, identified by its slot number
        /// </summary>
        /// <summary>
        /// Set the calling IXPathContext
        /// </summary>
        // no-op
        /// <summary>
        /// Set the value of a local variable, identified by its slot number
        /// </summary>
        private void NotAllowed()
        {
            throw new NotSupportedException((new NoDynamicContextException("Internal error: early evaluation of subexpression with no context")).ToString());
        }

        /// <summary>
        /// Get the value of a local variable, identified by its slot number
        /// </summary>
        /// <summary>
        /// Set the calling IXPathContext
        /// </summary>
        // no-op
        /// <summary>
        /// Set the value of a local variable, identified by its slot number
        /// </summary>
        public virtual XPathContextMajor.ThreadManager GetThreadManager()
        {
            return null;
        }

        /// <summary>
        /// Get the value of a local variable, identified by its slot number
        /// </summary>
        /// <summary>
        /// Set the calling IXPathContext
        /// </summary>
        // no-op
        /// <summary>
        /// Set the value of a local variable, identified by its slot number
        /// </summary>
        public virtual Component GetTargetComponent(int bindingSlot)
        {
            return null;
        }
        IFocusIterator IXPathContext.GetCurrentIterator() => default;
    }
}
