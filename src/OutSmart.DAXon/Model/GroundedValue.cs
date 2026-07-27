////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Collections.Zeno;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Model
{
    public interface IGroundedValue : ISequence
    {
        ISequenceIterator Iterate();
        IItem ItemAt(int n);
        IItem Head();
        IGroundedValue Subsequence(int start, int length);
        int GetLength();
        bool EffectiveBooleanValue()
;



        UnicodeString UnicodeStringValue { get; }
        string GetStringValue();
        IGroundedValue Reduce()
;



        IGroundedValue Materialize()
;



        string ToShortString()
;



        IEnumerable<IItem> AsIterable()
;



        bool ContainsNode(NodeInfo sought)
;



        IGroundedValue Concatenate(params IGroundedValue[] others)
;









    }
}
