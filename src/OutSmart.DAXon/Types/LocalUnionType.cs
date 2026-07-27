////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2020 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using OutSmart.DAXon.Internal.Functional;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Types
{
    public class LocalUnionType : IPlainType, IUnionType, IItemTypeWithSequenceTypeCache
    {
        private IList<IAtomicType> memberTypes;
        private SequenceType _one, _zeroOrOne, _oneOrMore, _zeroOrMore;

        public virtual StructuredQName TypeName => new StructuredQName("", NamespaceUri.ANONYMOUS, "U" + GetHashCode());

        public virtual string Description
        {
            get
            {
                StringBuilder builder = new StringBuilder("union(");
                foreach (IAtomicType at in memberTypes)
                {
                    builder.Append(at.Description);
                    builder.Append(", ");
                }

                builder.SetLength(builder.Length - 2);
                builder.Append(")");
                return builder.ToString();
            }
        }

        public virtual IList<IAtomicType> MemberTypes => memberTypes;

        /// <summary>
        /// Ask whether this union type includes any list types among its members
        /// </summary>
        public virtual SequenceType ResultTypeOfCast => SequenceType.MakeSequenceType(this, StaticProperty.ALLOWS_ZERO_OR_ONE);

        /// <summary>
        /// Determine whether this is a list type
        /// </summary>
        public virtual string BasicAlphaCode => "A";

        /// <summary>
        /// Determine whether this is a list type
        /// </summary>
        public virtual int PrimitiveType => StandardNames.XS_ANY_ATOMIC_TYPE;

        /// <summary>
        /// Determine whether this is a list type
        /// </summary>
        public virtual IList<IPlainType> PlainMemberTypes => new List<IPlainType>(memberTypes);

        /// <summary>
        /// Determine whether this is a list type
        /// </summary>
        public virtual double DefaultPriority
        {
            get
            {
                double result = 1;
                foreach (IAtomicType t in memberTypes)
                {
                    result *= t.DefaultPriority;
                }

                return result;
            }
        }

        public LocalUnionType(IList<IAtomicType> memberTypes)
        {
            this.memberTypes = memberTypes;
        }

        public LocalUnionType(params IAtomicType[] memberTypes)
        {
            this.memberTypes = new List<IAtomicType>();
            this.memberTypes.AddAll(memberTypes.ToList());
        }
        public virtual Genre GetGenre()
        {
            return Genre.ATOMIC;
        }

        // Implement IItemTypeWithSequenceTypeCache so the DAXonItemTypeUTypeExt.GetUType(this ItemType)
        // extension routes to this type's real GetUType() (and GetGenre()) instead of falling back to
        // UType.VOID. Without this, a union/xs:numeric static type reached through an ItemType reference
        // reported UType.VOID, which made TypeChecker's promotion test raise a spurious XTTE0780/XPTY0004
        // (e.g. a closure-captured integer added inside an inline function typed `as xs:integer`).
        public virtual SequenceType One()
        {
            return _one ?? (_one = new SequenceType(this, StaticProperty.EXACTLY_ONE));
        }

        public virtual SequenceType ZeroOrOne()
        {
            return _zeroOrOne ?? (_zeroOrOne = new SequenceType(this, StaticProperty.ALLOWS_ZERO_OR_ONE));
        }

        public virtual SequenceType OneOrMore()
        {
            return _oneOrMore ?? (_oneOrMore = new SequenceType(this, StaticProperty.ALLOWS_ONE_OR_MORE));
        }

        public virtual SequenceType ZeroOrMore()
        {
            return _zeroOrMore ?? (_zeroOrMore = new SequenceType(this, StaticProperty.ALLOWS_ZERO_OR_MORE));
        }

        // upstream UnionType interface DEFAULT methods (no DIM in C# 7.3)
        public virtual StructuredQName GetStructuredQName()
        {
            return TypeName;
        }

        public virtual string ExplainMismatch(IItem item, TypeHierarchy th)
        {
            if (item.GetGenre() == Genre.ATOMIC)
            {
                string message = "This is a union type, and the supplied value " + Err.Depict(item) + " of type " + ((AtomicValue)item).GetItemType().Description + " does not match any of its member types";
                return (message);
            }
            else
            {
                return null;
            }
        }

        public virtual bool IsAtomicType()
        {
            return false;
        }

        /// <summary>
        /// Ask whether this union type includes any list types among its members
        /// </summary>
        public virtual bool ContainsListType()
        {
            return false;
        }

        /// <summary>
        /// Ask whether this union type includes any list types among its members
        /// </summary>
        public virtual bool IsPlainType()
        {
            return true;
        }

        /// <summary>
        /// Ask whether this union type includes any list types among its members
        /// </summary>
        private bool SomeMemberTypeSatisfies(Func<IAtomicType, bool> condition)
        {
            foreach (IAtomicType member in memberTypes)
            {
                if (condition(member))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Ask whether this union type includes any list types among its members
        /// </summary>
        public virtual bool IsIdType()
        {
            return SomeMemberTypeSatisfies((t) => t.IsIdType());
        }

        /// <summary>
        /// Ask whether this union type includes any list types among its members
        /// </summary>
        public virtual bool IsIdRefType()
        {
            return SomeMemberTypeSatisfies((t) => t.IsIdRefType());
        }

        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        public virtual bool IsBuiltInType()
        {
            return false;
        }

        /// <summary>
        /// Determine whether this is a list type
        /// </summary>
        public virtual bool IsListType()
        {
            return false;
        }

        /// <summary>
        /// Determine whether this is a list type
        /// </summary>
        public virtual bool IsUnionType()
        {
            return true;
        }

        /// <summary>
        /// Determine whether this is a list type
        /// </summary>
        public virtual UType GetUType()
        {
            UType u = UType.VOID;
            foreach (IAtomicType at in memberTypes)
            {
                u = u.Union(at.GetUType());
            }

            return u;
        }

        /// <summary>
        /// Determine whether this is a list type
        /// </summary>
        public virtual bool IsNamespaceSensitive()
        {
            return SomeMemberTypeSatisfies((t) => t.IsNamespaceSensitive());
        }

        /// <summary>
        /// Determine whether this is a list type
        /// </summary>
        public virtual ValidationFailure ValidateContent(UnicodeString value, INamespaceResolver nsResolver, ConversionRules rules)
        {
            foreach (IAtomicType at in memberTypes)
            {
                ValidationFailure err = at.ValidateContent(value, nsResolver, rules);
                if (err == null)
                {
                    return null;
                }
            }

            return new ValidationFailure("Value " + Err.Wrap(value, Err.VALUE) + " does not match any member of union type " + ToString());
        }

        /// <summary>
        /// Determine whether this is a list type
        /// </summary>
        public virtual ValidationFailure CheckAgainstFacets(AtomicValue value, ConversionRules rules)
        {
            return null;
        }

        /// <summary>
        /// Determine whether this is a list type
        /// </summary>
        public virtual IAtomicSequence GetTypedValue(UnicodeString value, INamespaceResolver resolver, ConversionRules rules) /*Java covariant AtomicValue widened (C# 7.3)*/
        {
            foreach (IAtomicType type in memberTypes)
            {
                StringConverter converter = rules.MakeStringConverter(type);
                converter.SetNamespaceResolver(resolver);
                IConversionResult outcome = converter.ConvertString(value);
                if (outcome is AtomicValue)
                {
                    return (AtomicValue)outcome;
                }
            }

            ValidationFailure ve = new ValidationFailure("Value " + Err.Wrap(value, Err.VALUE) + " does not match any member of union type " + ToString());

            throw ve.MakeException();
        }

        /// <summary>
        /// Determine whether this is a list type
        /// </summary>
        public virtual bool Matches(IItem item, TypeHierarchy th)
        {
            if (item is AtomicValue)
            {
                return SomeMemberTypeSatisfies((at) => at.Matches(item, th));
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Determine whether this is a list type
        /// </summary>
        public virtual IAtomicType GetPrimitiveItemType()
        {
            return BuiltInAtomicType.ANY_ATOMIC;
        }

        /// <summary>
        /// Determine whether this is a list type
        /// </summary>
        public virtual IPlainType GetAtomizedItemType()
        {
            return this;
        }

        /// <summary>
        /// Determine whether this is a list type
        /// </summary>
        public virtual bool IsAtomizable(TypeHierarchy th)
        {
            return true;
        }

        /// <summary>
        /// Determine whether this is a list type
        /// </summary>
        public override string ToString()
        {
            StringBuilder fsb = new StringBuilder(256);
            fsb.Append("union(");
            foreach (IAtomicType at in memberTypes)
            {
                string member = at.DisplayName;
                fsb.Append(member);
                fsb.Append(", ");
            }

            fsb.SetLength(fsb.Length - 2);
            fsb.Append(")");
            return fsb.ToString();
        }

        /// <summary>
        /// Determine whether this is a list type
        /// </summary>
        public virtual string ToExportString()
        {
            StringBuilder fsb = new StringBuilder(256);
            fsb.Append("union(");
            foreach (IAtomicType at in memberTypes)
            {
                fsb.Append(at.ToExportString());
                fsb.Append(", ");
            }

            fsb.SetLength(fsb.Length - 2);
            fsb.Append(")");
            return fsb.ToString();
        }
        IAtomicSequence IUnionType.GetTypedValue(UnicodeString arg0, INamespaceResolver arg1, ConversionRules arg2) => GetTypedValue(arg0, arg1, arg2); // covariant bridge

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
    }
}
