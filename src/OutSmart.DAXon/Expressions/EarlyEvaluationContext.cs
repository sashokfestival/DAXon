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
    internal class EarlyEvaluationContext : IXPathContext
    {
        private readonly Configuration config;

        public virtual XPathContextMajor MajorContext => null;

        // no-op
        public virtual int TemporaryOutputState
        {
            get => 0; set
            {
            }
        }

        // no-op
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

        public virtual ISequence EvaluateLocalVariable(int slotnumber)
        {
            NotAllowed();
            return null;
        }

        public virtual IXPathContext GetCaller()
        {
            return null;
        }

        public virtual IResourceResolver GetResourceResolver()
        {
            return config.GetResourceResolver();
        }

        public virtual IErrorReporter GetErrorReporter()
        {
            return config.MakeErrorReporter();
        }

        /// <summary>
        /// Get the current component
        /// </summary>
        public virtual Component GetCurrentComponent()
        {
            NotAllowed();
            return null;
        }

        /// <summary>
        /// Get the Configuration
        /// </summary>
        public virtual Configuration GetConfiguration()
        {
            return config;
        }

        /// <summary>
        /// Get the Configuration
        /// </summary>
        public virtual IItem GetContextItem()
        {
            return null;
        }

        public virtual Controller GetController()
        {
            return null;
        }

        public virtual IGroupIterator GetCurrentGroupIterator()
        {
            NotAllowed();
            return null;
        }

        public virtual IGroupIterator GetCurrentMergeGroupIterator()
        {
            NotAllowed();
            return null;
        }

        public virtual FocusTrackingIterator GetCurrentIterator()
        {
            return null;
        }

        public virtual Component.M GetCurrentMode()
        {
            NotAllowed();
            return null;
        }

        public virtual IRegexIterator GetCurrentRegexIterator()
        {
            return null;
        }

        public virtual Rule GetCurrentTemplateRule()
        {
            return null;
        }

        public virtual int GetLast()
        {
            XPathException err = new XPathException("The context item is absent", "XPDY0002");
            throw new UncheckedXPathException(err);
        }

        public virtual ParameterSet GetLocalParameters()
        {
            NotAllowed();
            return null;
        }

        public virtual NamePool GetNamePool()
        {
            return config.GetNamePool();
        }

        public virtual StackFrame GetStackFrame()
        {
            NotAllowed();
            return null;
        }

        public virtual ParameterSet GetTunnelParameters()
        {
            NotAllowed();
            return null;
        }

        public virtual bool IsAtLast()
        {
            XPathException err = new XPathException("The context item is absent");
            err.SetErrorCode("XPDY0002");
            throw err;
        }

        public virtual XPathContextMajor NewCleanContext()
        {
            NotAllowed();
            return null;
        }

        public virtual XPathContextMajor NewContext()
        {
            Controller controller = new Controller(config);
            return controller.NewXPathContext();
        }

        public virtual XPathContextMinor NewMinorContext()
        {
            return NewContext().NewMinorContext();
        }

        public virtual void SetCaller(IXPathContext caller)
        {
        }

        // no-op
        /// <summary>
        /// Set a new sequence iterator.
        /// </summary>
        public virtual void SetCurrentIterator(IFocusIterator iter)
        {
            NotAllowed();
        }

        // no-op
        /// <summary>
        /// Set a new sequence iterator.
        /// </summary>
        public virtual IFocusIterator TrackFocus(ISequenceIterator iter)
        {
            NotAllowed();
            return null;
        }

        // no-op
        public virtual void SetLocalVariable(int slotNumber, ISequence value)
        {
            NotAllowed();
        }

        // no-op
        public virtual int UseLocalParameter(StructuredQName parameterId, int slotNumber, bool isTunnel)
        {
            return ParameterSet.NOT_SUPPLIED;
        }

        // no-op
        public virtual DateTimeValue GetCurrentDateTime()
        {
            throw new NoDynamicContextException("current-dateTime");
        }

        // no-op
        public virtual int GetImplicitTimezone()
        {
            return CalendarValue.MISSING_TIMEZONE;
        }

        // no-op
        public virtual IEnumerator<ContextStackFrame> IterateStackFrames()
        {
            return System.Linq.Enumerable.Empty<ContextStackFrame>().GetEnumerator();
        }

        // no-op
        public virtual XPathException GetCurrentException()
        {
            return null;
        }

        // no-op
        public virtual void WaitForChildThreads()
        {
            GetCaller().WaitForChildThreads();
        }

        // no-op
        private void NotAllowed()
        {
            throw new NotSupportedException((new NoDynamicContextException("Internal error: early evaluation of subexpression with no context")).ToString());
        }

        // no-op
        public virtual XPathContextMajor.ThreadManager GetThreadManager()
        {
            return null;
        }

        // no-op
        public virtual Component GetTargetComponent(int bindingSlot)
        {
            return null;
        }
        IFocusIterator IXPathContext.GetCurrentIterator() => default;
    }
}
