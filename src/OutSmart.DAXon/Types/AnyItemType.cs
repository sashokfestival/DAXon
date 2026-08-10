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
namespace OutSmart.DAXon.Types
{
    /// <summary>
    /// An implementation of ItemType that matches any item (node or atomic value)
    /// </summary>
    internal class AnyItemType : IItemTypeWithSequenceTypeCache
    {

        private static readonly AnyItemType theInstance = new AnyItemType();
        private SequenceType _one;
        private SequenceType _oneOrMore;
        private SequenceType _zeroOrOne;
        private SequenceType _zeroOrMore;

        public virtual string BasicAlphaCode => "";

        public virtual int PrimitiveType => Type.ITEM;
        private AnyItemType()
        {
        }
        public static AnyItemType GetInstance()
        {
            return theInstance;
        }

        public virtual Genre GetGenre()
        {
            return Genre.ANY;
        }

        public virtual UType GetUType()
        {
            return UType.ANY;
        }

        public virtual bool IsAtomicType()
        {
            return false;
        }

        public virtual bool IsPlainType()
        {
            return false;
        }

        public virtual bool Matches(IItem item, TypeHierarchy th)
        {
            return true;
        }

        public virtual IAtomicType GetAtomizedItemType()
        {
            return BuiltInAtomicType.ANY_ATOMIC;
        }

        IPlainType IItemTypeWithSequenceTypeCache.GetAtomizedItemType() => GetAtomizedItemType();

        public override string ToString()
        {
            return "item()";
        }

        public override int GetHashCode()
        {
            return "AnyItemType".GetHashCode();
        }

        public virtual SequenceType One()
        {
            if (_one == null)
            {
                _one = new SequenceType(this, StaticProperty.EXACTLY_ONE);
            }

            return _one;
        }

        public virtual SequenceType ZeroOrOne()
        {
            if (_zeroOrOne == null)
            {
                _zeroOrOne = new SequenceType(this, StaticProperty.ALLOWS_ZERO_OR_ONE);
            }

            return _zeroOrOne;
        }

        public virtual SequenceType OneOrMore()
        {
            if (_oneOrMore == null)
            {
                _oneOrMore = new SequenceType(this, StaticProperty.ALLOWS_ONE_OR_MORE);
            }

            return _oneOrMore;
        }

        public virtual SequenceType ZeroOrMore()
        {
            if (_zeroOrMore == null)
            {
                _zeroOrMore = new SequenceType(this, StaticProperty.ALLOWS_ZERO_OR_MORE);
            }

            return _zeroOrMore;
        }
    }
}