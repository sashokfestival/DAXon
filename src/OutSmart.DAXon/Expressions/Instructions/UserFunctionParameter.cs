////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions.Instructions
{
    /// <summary>
    /// Run-time object representing a formal argument to a user-defined function
    /// </summary>
    public class UserFunctionParameter : ILocalBinding
    {
        private SequenceType requiredType;
        private StructuredQName variableQName;
        private int slotNumber;
        private readonly int referenceCount = 999;
        private bool isIndexed = false;
        private bool isRequiredParam = true;
        private FunctionStreamability functionStreamability = FunctionStreamability.UNCLASSIFIED;
        private Expression defaultValue; // In 4.0, function parameters can have a default value

        /// <summary>
        /// Create a UserFunctionParameter
        /// </summary>
        public virtual int LocalSlotNumber => slotNumber;

        /// <summary>
        /// Create a UserFunctionParameter
        /// </summary>
        public virtual IntegerValue[] IntegerBoundsForVariable => null;

        /// <summary>
        /// Create a UserFunctionParameter
        /// </summary>
        public virtual Expression DefaultValueExpression
        {
            get => defaultValue; set
            {
                this.defaultValue = value;
            }
        }

        /// <summary>
        /// Create a UserFunctionParameter
        /// </summary>
        public virtual int ReferenceCount => referenceCount;

        /// <summary>
        /// Create a UserFunctionParameter
        /// </summary>
        public virtual FunctionStreamability FunctionStreamability
        {
            get => functionStreamability; set
            {
                this.functionStreamability = value;
            }
        }
        /// <summary>
        /// Create a UserFunctionParameter
        /// </summary>
        public UserFunctionParameter()
        {
        }

        /// <summary>
        /// Create a UserFunctionParameter
        /// </summary>
        public bool IsGlobal()
        {
            return false;
        }

        /// <summary>
        /// Create a UserFunctionParameter
        /// </summary>
        public bool IsAssignable()
        {
            return false;
        }

        /// <summary>
        /// Create a UserFunctionParameter
        /// </summary>
        public virtual void SetRequired(bool required)
        {
            isRequiredParam = required;
        }

        /// <summary>
        /// Create a UserFunctionParameter
        /// </summary>
        public bool IsRequired()
        {
            return isRequiredParam;
        }

        /// <summary>
        /// Create a UserFunctionParameter
        /// </summary>
        public virtual void SetSlotNumber(int slot)
        {
            slotNumber = slot;
        }

        /// <summary>
        /// Create a UserFunctionParameter
        /// </summary>
        public virtual void SetRequiredType(SequenceType type)
        {
            requiredType = type;
        }

        /// <summary>
        /// Create a UserFunctionParameter
        /// </summary>
        public virtual SequenceType GetRequiredType()
        {
            return requiredType;
        }

        /// <summary>
        /// Create a UserFunctionParameter
        /// </summary>
        public virtual void SetVariableQName(StructuredQName name)
        {
            variableQName = name;
        }

        /// <summary>
        /// Create a UserFunctionParameter
        /// </summary>
        public virtual StructuredQName GetVariableQName()
        {
            return variableQName;
        }

        /// <summary>
        /// Create a UserFunctionParameter
        /// </summary>
        public virtual void AddReference(VariableReference @ref, bool isLoopingReference)
        {
        }

        /// <summary>
        /// Create a UserFunctionParameter
        /// </summary>
        public virtual void SetIndexedVariable(bool indexed)
        {
            isIndexed = indexed;
        }

        /// <summary>
        /// Create a UserFunctionParameter
        /// </summary>
        public virtual void SetIndexedVariable()
        {
            SetIndexedVariable(true);
        }

        /// <summary>
        /// Create a UserFunctionParameter
        /// </summary>
        public virtual bool IsIndexedVariable()
        {
            return isIndexed;
        }

        /// <summary>
        /// Create a UserFunctionParameter
        /// </summary>
        public virtual ISequence EvaluateVariable(IXPathContext context)
        {
            return context.EvaluateLocalVariable(slotNumber);
        }
    }
}