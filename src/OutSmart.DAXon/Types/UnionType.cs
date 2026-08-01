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
using static OutSmart.DAXon.Model.Genre;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Types
{
    public interface IUnionType : ItemType, ICastingTarget
    {
        StructuredQName TypeName { get; }
        StructuredQName GetStructuredQName();



        bool ContainsListType();
        IList<IPlainType> PlainMemberTypes { get; }
        SequenceType ResultTypeOfCast { get; }
        IAtomicSequence GetTypedValue(UnicodeString value, INamespaceResolver resolver, ConversionRules rules);
        ValidationFailure CheckAgainstFacets(AtomicValue value, ConversionRules rules);
        string ExplainMismatch(IItem item, TypeHierarchy th);











        string Description { get; }









    }
}
