////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;using OutSmart.DAXon.Functions;

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions.Parsing
{
    /// <summary>
    /// Constants for different mechanisms of expression evaluation
    /// </summary>
    internal class Evaluators
    {
        // These numeric constants must be stable as they are held in the SEF file
        public const int UNDECIDED = -1;
        public const int EVALUATE_LITERAL = 0;
        public const int EVALUATE_VARIABLE = 1;
        public const int EAGER_SEQUENCE = 2;
        public const int MAKE_CLOSURE = 3;
        public const int MAKE_MEMO_CLOSURE = 4;
        public const int RETURN_EMPTY_SEQUENCE = 5;
        public const int EVALUATE_AND_MATERIALIZE_VARIABLE = 6;
        public const int CALL_EVALUATE_OPTIONAL_ITEM = 7;
        public const int ITERATE_AND_MATERIALIZE = 8;
        public const int PROCESS = 9;
        public const int LAZY_TAIL_EXPRESSION = 10;
        public const int SHARED_APPEND_EXPRESSION = 11;
        public const int MAKE_INDEXED_VARIABLE = 12;
        public const int MAKE_SINGLETON_CLOSURE = 13;
        public const int EVALUATE_SUPPLIED_PARAMETER = 14;
        public const int STREAMING_ARGUMENT = 15;
        public const int CALL_EVALUATE_SINGLE_ITEM = 16;
    }
}