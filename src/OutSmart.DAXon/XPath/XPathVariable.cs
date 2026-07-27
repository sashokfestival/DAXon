////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.XPath
{
    public sealed class XPathVariable : ILocalBinding
    {
        private StructuredQName name;
        private SequenceType requiredType = SequenceType.ANY_SEQUENCE;
        private ISequence defaultValue;
        private int slotNumber;

        /// <summary>
        /// Private constructor: for use only by the protected factory method make()
        /// </summary>
        public IntegerValue[] IntegerBoundsForVariable => null;

        /// <summary>
        /// Private constructor: for use only by the protected factory method make()
        /// </summary>
        public int LocalSlotNumber => slotNumber;

        /// <summary>
        /// Private constructor: for use only by the protected factory method make()
        /// </summary>
        public ISequence DefaultValue { get => defaultValue; set => this.defaultValue = value; }
        /// <summary>
        /// Private constructor: for use only by the protected factory method make()
        /// </summary>
        private XPathVariable()
        {
        }

        /// <summary>
        /// Private constructor: for use only by the protected factory method make()
        /// </summary>
        public static XPathVariable Make(StructuredQName name)
        {
            XPathVariable v = new XPathVariable();
            v.name = name;
            return v;
        }

        /// <summary>
        /// Private constructor: for use only by the protected factory method make()
        /// </summary>
        public bool IsGlobal()
        {
            return false;
        }

        /// <summary>
        /// Private constructor: for use only by the protected factory method make()
        /// </summary>
        public bool IsAssignable()
        {
            return false;
        }

        /// <summary>
        /// Private constructor: for use only by the protected factory method make()
        /// </summary>
        public void SetRequiredType(SequenceType requiredType)
        {
            this.requiredType = requiredType;
        }

        /// <summary>
        /// Private constructor: for use only by the protected factory method make()
        /// </summary>
        public SequenceType GetRequiredType()
        {
            return requiredType;
        }

        /// <summary>
        /// Private constructor: for use only by the protected factory method make()
        /// </summary>
        public void SetSlotNumber(int slotNumber)
        {
            this.slotNumber = slotNumber;
        }

        /// <summary>
        /// Private constructor: for use only by the protected factory method make()
        /// </summary>
        public StructuredQName GetVariableQName()
        {
            return name;
        }

        /// <summary>
        /// Private constructor: for use only by the protected factory method make()
        /// </summary>
        public void AddReference(VariableReference @ref, bool isLoopingReference)
        {
        }

        /// <summary>
        /// Private constructor: for use only by the protected factory method make()
        /// </summary>
        public ISequence EvaluateVariable(IXPathContext context)
        {
            return context.EvaluateLocalVariable(slotNumber);
        }

        /// <summary>
        /// Private constructor: for use only by the protected factory method make()
        /// </summary>
        /// <summary>
        /// Say that the bound value has the potential to be indexed
        /// </summary>
        public void SetIndexedVariable()
        {
        }

        /// <summary>
        /// Private constructor: for use only by the protected factory method make()
        /// </summary>
        /// <summary>
        /// Say that the bound value has the potential to be indexed
        /// </summary>
        public bool IsIndexedVariable()
        {
            return false;
        }
    }
}