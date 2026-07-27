////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Lib;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Types
{
    public interface IAtomicType : ISimpleType, IPlainType, ICastingTarget
    {
        Genre GetGenre()
;



        ValidationFailure Validate(AtomicValue primValue, UnicodeString lexicalValue, ConversionRules rules);
        bool IsOrdered(bool optimistic);
        bool IsAbstract();
        bool IsPrimitiveType();
        BuiltInAtomicType PrimitiveAtomicType { get; }



        bool IsIdType();
        bool IsIdRefType();
        bool IsBuiltInType();
        StructuredQName TypeName { get; }
        StringConverter GetStringConverter(ConversionRules rules);
        string ExplainMismatch(IItem item, TypeHierarchy th)
;










        double DefaultPriority { get; }















    }
}
