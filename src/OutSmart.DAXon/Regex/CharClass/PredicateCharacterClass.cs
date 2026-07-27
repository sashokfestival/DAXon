////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Collections;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Internal.Functional;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Regex.CharClass
{
    public class PredicateCharacterClass : ICharacterClass
    {
        private readonly Func<int, bool> predicate;
        public PredicateCharacterClass(Func<int, bool> predicate)
        {
            this.predicate = predicate;
        }

        public virtual bool Test(int value)
        {
            return predicate.Test(value);
        }

        public virtual bool IsDisjoint(ICharacterClass other)
        {
            return other is InverseCharacterClass && other.IsDisjoint(this);
        }

        public virtual IntSet GetIntSet()
        {
            return null; // Not known
        }

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
        public virtual IIntPredicateProxy Union(IIntPredicateProxy other) => throw new NotImplementedException();
    }
}