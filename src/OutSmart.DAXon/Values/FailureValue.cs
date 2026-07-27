////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using System.Linq;

namespace OutSmart.DAXon.Values
{
    public class FailureValue : IGroundedValue
    {
        public UnicodeString UnicodeStringValue => throw new NotImplementedException("STUB: FailureValue.GetUnicodeStringValue not ported (excluded stub)");
        public FailureValue() { }
        public FailureValue(string err) { }
        public FailureValue(Exception err) { }
        public FailureValue(XPathException err) { }
        public IItem Head() => throw new NotImplementedException("STUB: FailureValue.Head not ported (excluded stub)");
        public ISequenceIterator Iterate() => throw new NotImplementedException("STUB: FailureValue.Iterate not ported (excluded stub)");
        public bool IsEmpty() => true;
        public IItem ItemAt(int n) => throw new NotImplementedException("STUB: FailureValue.ItemAt not ported (excluded stub)");
        public int GetLength() => 0;
        public string GetStringValue() => "";
        public bool EffectiveBooleanValue() => false;
        public IGroundedValue Materialize() => this;
        public IGroundedValue Reduce() => this;
        public IGroundedValue Subsequence(int start, int length) => this;
        public string ToShortString() => "";
        public IEnumerable<IItem> AsIterable() => Enumerable.Empty<IItem>();
        public bool ContainsNode(NodeInfo node) => false;
        public IGroundedValue Concatenate(params IGroundedValue[] others) => this;
        public ISequence MakeRepeatable() => this;
    }
}
