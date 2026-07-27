////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Regex;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Transformation.Rules;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Expressions
{
    public class XPathContextMajor : XPathContextMinor
    {
        private ParameterSet localParameters;
        private ParameterSet tunnelParameters;
        private TailCallLoop.ITailCallInfo tailCallInfo;
        private Component.M currentMode;
        private Rule currentTemplate;
        private IGroupIterator currentGroupIterator;
        private IGroupIterator currentMergeGroupIterator;
        private IRegexIterator currentRegexIterator;
        private IContextOriginator origin;
        private ThreadManager threadManager = null;
        private IResourceResolver resourceResolver;
        private IErrorReporter errorReporter;
        private Component currentComponent;
        XPathException currentException;

        /// <summary>
        /// Private Constructor
        /// </summary>
        public virtual IContextOriginator Origin
        {
            get => origin; set
            {
                origin = value;
            }
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        public virtual ISequence[] AllVariableValues => stackFrame.StackFrameValues;

        /// <summary>
        /// Private Constructor
        /// </summary>
        public virtual TailCallLoop.ITailCallInfo TailCallInfo
        {
            get
            {
                TailCallLoop.ITailCallInfo fn = tailCallInfo;
                tailCallInfo = null;
                return fn;
            }
        }
        public XPathContextMajor(Controller controller)
        {
            this.controller = controller;
            stackFrame = StackFrame.EMPTY;
            origin = controller;
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        private XPathContextMajor()
        {
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        public XPathContextMajor(IItem item, Executable exec)
        {
            controller = exec is PreparedStylesheet ? new XsltController(exec.GetConfiguration(), (PreparedStylesheet)exec) : new Controller(exec.GetConfiguration(), exec);
            if (item != null)
            {
                ISequenceIterator iter = SingletonIterator.MakeIterator(item);
                currentIterator = SequenceTool.FocusTracker(iter);
                currentIterator.Next();
                last = new LastValue(1);
            }

            origin = controller;
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        public override XPathContextMajor NewContext()
        {
            XPathContextMajor c = new XPathContextMajor();
            c.controller = controller;
            c.currentIterator = currentIterator;
            c.stackFrame = stackFrame;
            c.localParameters = localParameters;
            c.tunnelParameters = tunnelParameters;
            c.last = last;
            c.currentDestination = currentDestination;
            c.currentMode = currentMode;
            c.currentTemplate = currentTemplate;
            c.currentRegexIterator = currentRegexIterator;
            c.currentGroupIterator = currentGroupIterator;
            c.currentMergeGroupIterator = currentMergeGroupIterator;
            c.currentException = currentException;
            c.caller = this;
            c.tailCallInfo = null;
            c.temporaryOutputState = temporaryOutputState;
            c.threadManager = threadManager;
            c.currentComponent = currentComponent;
            c.errorReporter = errorReporter;
            c.resourceResolver = resourceResolver;
            return c;
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        public static XPathContextMajor NewContext(XPathContextMinor prev)
        {
            XPathContextMajor c = new XPathContextMajor();
            XPathContextMajor p = prev.MajorContext;
            c.controller = p.GetController();
            c.currentIterator = prev.GetCurrentIterator();
            c.stackFrame = prev.GetStackFrame();
            c.localParameters = p.GetLocalParameters();
            c.tunnelParameters = p.GetTunnelParameters();
            c.last = prev.last;
            c.currentDestination = prev.currentDestination;
            c.currentMode = p.GetCurrentMode();
            c.currentTemplate = p.GetCurrentTemplateRule();
            c.currentRegexIterator = p.GetCurrentRegexIterator();
            c.currentGroupIterator = p.GetCurrentGroupIterator();
            c.currentMergeGroupIterator = p.GetCurrentMergeGroupIterator();
            c.caller = prev;
            c.tailCallInfo = null;
            c.threadManager = p.threadManager;
            c.currentComponent = p.currentComponent;
            c.errorReporter = p.errorReporter;
            c.currentException = p.currentException;
            c.resourceResolver = p.resourceResolver;
            c.temporaryOutputState = prev.temporaryOutputState;
            return c;
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        public static XPathContextMajor NewThreadContext(XPathContextMinor prev)
        {
            XPathContextMajor c = NewContext(prev);
            c.stackFrame = prev.stackFrame.Copy();
            return c;
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        public override ThreadManager GetThreadManager()
        {
            return threadManager;
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        public virtual void CreateThreadManager()
        {
            threadManager = GetConfiguration().MakeThreadManager();
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        public override void WaitForChildThreads()
        {
            if (threadManager != null)
            {
                threadManager.WaitForChildThreads();
            }
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        public override ParameterSet GetLocalParameters()
        {
            if (localParameters == null)
            {
                localParameters = new ParameterSet();
            }

            return localParameters;
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        public virtual void SetLocalParameters(ParameterSet localParameters)
        {
            this.localParameters = localParameters;
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        public override ParameterSet GetTunnelParameters()
        {
            return tunnelParameters;
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        public virtual void SetTunnelParameters(ParameterSet tunnelParameters)
        {
            this.tunnelParameters = tunnelParameters;
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        public virtual void SetStackFrame(SlotManager map, ISequence[] variables)
        {
            stackFrame = new StackFrame(map, variables);
            if (map != null && variables.Length != map.NumberOfVariables)
            {
                if (variables.Length > map.NumberOfVariables)
                {
                    throw new InvalidOperationException("Attempting to set more local variables (" + variables.Length + ") than the stackframe can accommodate (" + map.NumberOfVariables + ")");
                }

                stackFrame.slots = new ISequence[map.NumberOfVariables];
                Array.Copy(variables, 0, stackFrame.slots, 0, variables.Length);
            }
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        public virtual void ResetStackFrameMap(SlotManager map, int numberOfParams)
        {
            stackFrame.map = map;
            if (stackFrame.slots.Length != map.NumberOfVariables)
            {
                ISequence[] v2 = new ISequence[map.NumberOfVariables];
                Array.Copy(stackFrame.slots, 0, v2, 0, numberOfParams);
                stackFrame.slots = v2;
            }
            else
            {

                // not strictly necessary
                ArrayTools.Fill(stackFrame.slots, numberOfParams, stackFrame.slots.Length, null);
            }
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        public virtual void ResetAllVariableValues(ISequence[] values)
        {
            stackFrame.StackFrameValues = values;
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        public virtual void ResetParameterValues(ISequence[] values)
        {
            Array.Copy(values, 0, stackFrame.slots, 0, values.Length);
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        public virtual void RequestTailCall(TailCallLoop.ITailCallInfo targetFn, ISequence[] variables)
        {
            if (variables != null)
            {
                if (variables.Length > stackFrame.slots.Length)
                {
                    stackFrame.slots = ArrayTools.CopyOf(variables, variables.Length);
                }
                else
                {
                    Array.Copy(variables, 0, stackFrame.slots, 0, variables.Length);
                }
            }

            tailCallInfo = targetFn;
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        public virtual void OpenStackFrame(SlotManager map)
        {
            int numberOfSlots = map.NumberOfVariables;
            if (numberOfSlots == 0)
            {
                stackFrame = StackFrame.EMPTY;
            }
            else
            {
                stackFrame = new StackFrame(map, new ISequence[numberOfSlots]);
            }
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        public virtual void OpenStackFrame(int numberOfVariables)
        {
            stackFrame = new StackFrame(new SlotManager(numberOfVariables), SequenceTool.MakeSequenceArray(numberOfVariables));
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        public virtual void SetCurrentMode(Component.M mode)
        {
            currentMode = mode;
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        public override Component.M GetCurrentMode()
        {
            Component.M m = currentMode;
            if (m == null)
            {
                RuleManager rm = GetController().GetRuleManager();
                if (rm != null)
                {
                    return rm.UnnamedMode.GetDeclaringComponent();
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return m;
            }
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        public virtual void SetCurrentTemplateRule(Rule rule)
        {
            currentTemplate = rule;
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        public override Rule GetCurrentTemplateRule()
        {
            return currentTemplate;
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        public virtual void SetCurrentGroupIterator(IGroupIterator iterator)
        {
            currentGroupIterator = iterator;
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        public override IGroupIterator GetCurrentGroupIterator()
        {
            return currentGroupIterator;
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        public virtual void SetCurrentMergeGroupIterator(IGroupIterator iterator)
        {
            currentMergeGroupIterator = iterator;
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        public override IGroupIterator GetCurrentMergeGroupIterator()
        {
            return currentMergeGroupIterator;
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        public virtual void SetCurrentRegexIterator(IRegexIterator iterator)
        {
            currentRegexIterator = iterator;
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        public override IRegexIterator GetCurrentRegexIterator()
        {
            return currentRegexIterator;
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        public override int UseLocalParameter(StructuredQName paramName, int slotNumber, bool isTunnel)
        {
            ParameterSet @params = isTunnel ? GetTunnelParameters() : localParameters;
            if (@params == null)
            {
                return ParameterSet.NOT_SUPPLIED;
            }

            int index = @params.GetIndex(paramName);
            if (index < 0)
            {
                return ParameterSet.NOT_SUPPLIED;
            }

            ISequence val = @params.GetValue(index);
            stackFrame.slots[slotNumber] = val;
            bool @checked = @params.IsTypeChecked(index);
            return @checked ? ParameterSet.SUPPLIED_AND_CHECKED : ParameterSet.SUPPLIED;
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        public virtual void SetResourceResolver(IResourceResolver resolver)
        {
            resourceResolver = resolver;
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        public override IResourceResolver GetResourceResolver()
        {
            return resourceResolver == null ? controller.ResourceResolver : resourceResolver;
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        public virtual void SetErrorReporter(IErrorReporter reporter)
        {
            errorReporter = reporter;
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        public override IErrorReporter GetErrorReporter()
        {
            return errorReporter == null ? controller.ErrorReporter : errorReporter;
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        public virtual void SetCurrentException(XPathException exception)
        {
            currentException = exception;
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        public override XPathException GetCurrentException()
        {
            return currentException;
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        /// <summary>
        /// Get the current component
        /// </summary>
        public override Component GetCurrentComponent()
        {
            return currentComponent;
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        /// <summary>
        /// Get the current component
        /// </summary>
        public virtual void SetCurrentComponent(Component component)
        {

            currentComponent = component;
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        /// <summary>
        /// Get the current component
        /// </summary>
        public override Component GetTargetComponent(int bindingSlot)
        {
            try
            {
                ComponentBinding binding = currentComponent.ComponentBindings[bindingSlot];
                return binding.GetTarget();
            }
            catch (NullReferenceException e)
            {

                // Suggests that the current component is null, which would be a bug
                e.ToString();
                throw e;
            }
            catch (IndexOutOfRangeException e)
            {

                // Suggests that the current component's binding vector is the wrong size, which would be a bug.
                e.ToString();
                throw e;
            }
        }

        /// <summary>
        /// Private Constructor
        /// </summary>
        public abstract class ThreadManager
        {
            public abstract void WaitForChildThreads();
        }
    }
}