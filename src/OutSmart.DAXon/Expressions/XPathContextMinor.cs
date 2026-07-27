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
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Expressions
{
    public class XPathContextMinor : IXPathContext
    {
        public Controller controller;
        public IFocusIterator currentIterator;
        public LastValue last = null;
        public IXPathContext caller = null;
        public StackFrame stackFrame;
        public string currentDestination = "";
        public int temporaryOutputState = 0;

        /// <summary>
        /// Private Constructor
        /// </summary>
        /// <summary>
        /// Get the Name Pool
        /// </summary>
        public XPathContextMajor MajorContext
        {
            get
            {
                IXPathContext c = this;
                while (c != null && !(c is XPathContextMajor))
                {
                    c = c.GetCaller();
                }

                return (XPathContextMajor)c;
            }
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        /// <summary>
        /// Set a new sequence iterator.
        /// </summary>
        /// <summary>
        /// Set the value of a local variable, identified by its slot number
        /// </summary>
        public virtual int TemporaryOutputState
        {
            get => temporaryOutputState; set
            {
                temporaryOutputState = value;
            }
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        /// <summary>
        /// Set a new sequence iterator.
        /// </summary>
        /// <summary>
        /// Set the value of a local variable, identified by its slot number
        /// </summary>
        public virtual string CurrentOutputUri
        {
            get => currentDestination; set
            {
                currentDestination = value;
            }
        }
        /// <summary>
        /// Private Constructor
        /// </summary>
        protected XPathContextMinor()
        {
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        public virtual XPathContextMajor NewContext()
        {
            return XPathContextMajor.NewContext(this);
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        public virtual XPathContextMinor NewMinorContext()
        {
            XPathContextMinor c = new XPathContextMinor();

            c.controller = controller;
            c.caller = this;
            c.currentIterator = currentIterator;
            c.last = last;
            c.stackFrame = stackFrame;
            c.currentDestination = currentDestination;
            c.temporaryOutputState = temporaryOutputState;
            return c;
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        /// <summary>
        /// Set the calling IXPathContext
        /// </summary>
        public virtual void SetCaller(IXPathContext caller)
        {
            this.caller = caller;
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        /// <summary>
        /// Construct a new context without copying (used for the context in a function call)
        /// </summary>
        public virtual XPathContextMajor NewCleanContext()
        {
            XPathContextMajor c = new XPathContextMajor(GetController());
            c.SetCaller(this);
            return c;
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        /// <summary>
        /// Construct a new context without copying (used for the context in a function call)
        /// </summary>
        public virtual ParameterSet GetLocalParameters()
        {
            return MajorContext.GetLocalParameters();
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        /// <summary>
        /// Construct a new context without copying (used for the context in a function call)
        /// </summary>
        public virtual ParameterSet GetTunnelParameters()
        {
            return MajorContext.GetTunnelParameters();
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        /// <summary>
        /// Get the Controller. May return null when running outside XSLT or XQuery
        /// </summary>
        public Controller GetController()
        {
            return controller;
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        /// <summary>
        /// Get the Configuration
        /// </summary>
        public Configuration GetConfiguration()
        {
            return controller.GetConfiguration();
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        /// <summary>
        /// Get the Name Pool
        /// </summary>
        public NamePool GetNamePool()
        {
            return controller.GetConfiguration().GetNamePool();
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        /// <summary>
        /// Get the Name Pool
        /// </summary>
        public IXPathContext GetCaller()
        {
            return caller;
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        /// <summary>
        /// Set a new sequence iterator.
        /// </summary>
        public virtual void SetCurrentIterator(IFocusIterator iter)
        {
            currentIterator = iter;
            last = null;
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        /// <summary>
        /// Set a new sequence iterator.
        /// </summary>
        public virtual IFocusIterator TrackFocus(ISequenceIterator iter)
        {
            IFocusIterator fit = controller.MakeFocusTracker(iter, false);
            SetCurrentIterator(fit);
            return fit;
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        /// <summary>
        /// Set a new sequence iterator.
        /// </summary>
        public virtual IFocusIterator TrackFocusMultithreaded(ISequenceIterator iter)
        {
            IFocusIterator fit = controller.MakeFocusTracker(iter, true);
            SetCurrentIterator(fit);
            return fit;
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        /// <summary>
        /// Set a new sequence iterator.
        /// </summary>
        public IFocusIterator GetCurrentIterator()
        {
            return currentIterator;
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        /// <summary>
        /// Set a new sequence iterator.
        /// </summary>
        public IItem GetContextItem()
        {
            if (currentIterator == null)
            {
                return null;
            }

            if (currentIterator is FocusTrackingIterator)
            {

                // Common case extracted to reduce overhead of megamorphism
                return ((FocusTrackingIterator)currentIterator).Current();
            }
            else
            {
                return currentIterator.Current();
            }
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        /// <summary>
        /// Set a new sequence iterator.
        /// </summary>
        public int GetLast()
        {
            if (currentIterator == null)
            {
                throw new UncheckedXPathException(new XPathException("The context item is absent, so last() is undefined").WithXPathContext(this).WithErrorCode("XPDY0002"));
            }

            if (last != null)
            {
                return last.value;
            }

            try
            {
                int length = currentIterator.GetLength();
                last = new LastValue(length);
                return length;
            }
            catch (XPathException err)
            {
                throw new UncheckedXPathException(err);
            }
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        /// <summary>
        /// Set a new sequence iterator.
        /// </summary>
        public bool IsAtLast()
        {
            if (currentIterator == null)
            {
                throw new XPathException("Cannot evaluate position()=last() because there is no context item", "XPDY0002");
            }

            if (currentIterator is ILookaheadIterator && ((ILookaheadIterator)currentIterator).SupportsHasNext())
            {
                return !((ILookaheadIterator)currentIterator).HasNext;
            }

            try
            {
                return currentIterator.Position() == GetLast();
            }
            catch (UncheckedXPathException e)
            {
                throw XPathException.MakeXPathException(e);
            }
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        /// <summary>
        /// Set a new sequence iterator.
        /// </summary>
        public virtual IResourceResolver GetResourceResolver()
        {
            return caller.GetResourceResolver();
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        /// <summary>
        /// Set a new sequence iterator.
        /// </summary>
        public virtual IErrorReporter GetErrorReporter()
        {
            return caller.GetErrorReporter();
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        /// <summary>
        /// Set a new sequence iterator.
        /// </summary>
        public virtual XPathException GetCurrentException()
        {
            return caller.GetCurrentException();
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        /// <summary>
        /// Set a new sequence iterator.
        /// </summary>
        public virtual XPathContextMajor.ThreadManager GetThreadManager()
        {
            return caller.GetThreadManager();
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        /// <summary>
        /// Set a new sequence iterator.
        /// </summary>
        /// <summary>
        /// Get the current component
        /// </summary>
        public virtual Component GetCurrentComponent()
        {
            return caller.GetCurrentComponent();
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        /// <summary>
        /// Set a new sequence iterator.
        /// </summary>
        /// <summary>
        /// Get the current component
        /// </summary>
        public virtual StackFrame GetStackFrame()
        {
            return stackFrame;
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        /// <summary>
        /// Set a new sequence iterator.
        /// </summary>
        /// <summary>
        /// Get the current component
        /// </summary>
        public virtual void MakeStackFrameMutable()
        {
            if (stackFrame == StackFrame.EMPTY)
            {
                stackFrame = new StackFrame(null, SequenceTool.MakeSequenceArray(0));
            }
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        /// <summary>
        /// Set a new sequence iterator.
        /// </summary>
        /// <summary>
        /// Get the value of a local variable, identified by its slot number
        /// </summary>
        public ISequence EvaluateLocalVariable(int slot)
        {
            return stackFrame.slots[slot];
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        /// <summary>
        /// Set a new sequence iterator.
        /// </summary>
        /// <summary>
        /// Set the value of a local variable, identified by its slot number
        /// </summary>
        public void SetLocalVariable(int slotNumber, ISequence value)
        {

            // The following code is deep defence against attempting to store a non-memo Closure in a variable.
            // This should not happen, and if it does, it means that the evaluation mode has been miscalculated.
            // But if it does happen, we recover by wrapping the Closure in a MemoSequence which remembers the
            // value as it is calculated.
            value = value.MakeRepeatable();
            try
            {
                stackFrame.slots[slotNumber] = value;
            }
            catch (IndexOutOfRangeException e)
            {
                if (slotNumber == -999)
                {
                    throw new InvalidOperationException("Internal error: Cannot set local variable: no slot allocated");
                }
                else
                {
                    throw new InvalidOperationException("Internal error: Cannot set local variable (slot " + slotNumber + " of " + GetStackFrame().StackFrameValues.Length + ")");
                }
            }
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        /// <summary>
        /// Set a new sequence iterator.
        /// </summary>
        /// <summary>
        /// Set the value of a local variable, identified by its slot number
        /// </summary>
        public virtual void WaitForChildThreads()
        {
            lock (this)
            {
                MajorContext.WaitForChildThreads();
            }
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        /// <summary>
        /// Set a new sequence iterator.
        /// </summary>
        /// <summary>
        /// Set the value of a local variable, identified by its slot number
        /// </summary>
        public virtual int UseLocalParameter(StructuredQName parameterId, int slotNumber, bool isTunnel)
        {
            return MajorContext.UseLocalParameter(parameterId, slotNumber, isTunnel);
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        /// <summary>
        /// Set a new sequence iterator.
        /// </summary>
        /// <summary>
        /// Set the value of a local variable, identified by its slot number
        /// </summary>
        public virtual Component.M GetCurrentMode()
        {
            return MajorContext.GetCurrentMode();
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        /// <summary>
        /// Set a new sequence iterator.
        /// </summary>
        /// <summary>
        /// Set the value of a local variable, identified by its slot number
        /// </summary>
        public virtual Rule GetCurrentTemplateRule()
        {

            // In a minor context, the current template rule is always null. This is a consequence
            // of the way they are used.
            return null; //return getCaller().getCurrentTemplateRule();
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        /// <summary>
        /// Set a new sequence iterator.
        /// </summary>
        /// <summary>
        /// Set the value of a local variable, identified by its slot number
        /// </summary>
        public virtual IGroupIterator GetCurrentGroupIterator()
        {
            return MajorContext.GetCurrentGroupIterator();
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        /// <summary>
        /// Set a new sequence iterator.
        /// </summary>
        /// <summary>
        /// Set the value of a local variable, identified by its slot number
        /// </summary>
        public virtual IGroupIterator GetCurrentMergeGroupIterator()
        {
            return MajorContext.GetCurrentMergeGroupIterator();
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        /// <summary>
        /// Set a new sequence iterator.
        /// </summary>
        /// <summary>
        /// Set the value of a local variable, identified by its slot number
        /// </summary>
        public virtual IRegexIterator GetCurrentRegexIterator()
        {
            return MajorContext.GetCurrentRegexIterator();
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        /// <summary>
        /// Set a new sequence iterator.
        /// </summary>
        /// <summary>
        /// Set the value of a local variable, identified by its slot number
        /// </summary>
        public virtual DateTimeValue GetCurrentDateTime()
        {
            return controller.GetCurrentDateTime();
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        /// <summary>
        /// Set a new sequence iterator.
        /// </summary>
        /// <summary>
        /// Set the value of a local variable, identified by its slot number
        /// </summary>
        public int GetImplicitTimezone()
        {
            return controller.GetImplicitTimezone();
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        /// <summary>
        /// Set a new sequence iterator.
        /// </summary>
        /// <summary>
        /// Set the value of a local variable, identified by its slot number
        /// </summary>
        public virtual IEnumerator<ContextStackFrame> IterateStackFrames()
        {
            return (IEnumerator<ContextStackFrame>)(new ContextStackIterator(this));
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        /// <summary>
        /// Set a new sequence iterator.
        /// </summary>
        /// <summary>
        /// Set the value of a local variable, identified by its slot number
        /// </summary>
        public virtual Component GetTargetComponent(int bindingSlot)
        {
            return MajorContext.GetTargetComponent(bindingSlot);
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        /// <summary>
        /// Set a new sequence iterator.
        /// </summary>
        /// <summary>
        /// Set the value of a local variable, identified by its slot number
        /// </summary>
        // Note: consider eliminating this class. A new XPathContextMinor is created under two circumstances,
        // (a) when the focus changes (i.e., a new current iterator), and (b) when the current
        // receiver changes. We could handle these by maintaining a stack of iterators and a stack of
        // receivers in the XPathContextMajor object. Adding a new iterator or receiver to the stack would
        // generally be cheaper than creating the new XPathContextMinor object. The main difficulty (in the
        // case of iterators) is knowing when to pop the stack: currently we rely on the garbage collector.
        // We can only really do this when the iterator comes to its end, which is difficult to detect.
        // Perhaps we should try to do static allocation, so that fixed slots are allocated for different
        // minor-contexts within a Procedure, and a compiled expression that uses the focus knows which
        // slot to look in.
        // Investigated the above Sept 2008. On xmark, with a 100Mb input, the path expression
        // count(site/people/person/watches/watch) takes just 13ms to execute (compared with 6500ms for building
        // the tree). Only 6 context objects are created while doing this. This doesn't appear to be a productive
        // area to look for new optimizations.
        public class LastValue
        {
            public readonly int value;
            public LastValue(int count)
            {

                value = count;
            }
        }
    }
}
