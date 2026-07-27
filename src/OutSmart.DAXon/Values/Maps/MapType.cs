////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Core;
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
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Values.Maps
{
    public class MapType : AnyFunctionType
    {
        public static readonly MapType ANY_MAP_TYPE = new MapType(BuiltInAtomicType.ANY_ATOMIC, SequenceType.ANY_SEQUENCE);
        public static readonly MapType EMPTY_MAP_TYPE = new MapType(BuiltInAtomicType.ANY_ATOMIC, SequenceType.ANY_SEQUENCE, true);
        /// <summary>
        /// A type that allows a sequence of zero or one map items
        /// </summary>
        public static readonly SequenceType OPTIONAL_MAP_ITEM = SequenceType.MakeSequenceType(ANY_MAP_TYPE, StaticProperty.ALLOWS_ZERO_OR_ONE);
        /// <summary>
        /// A type that allows a sequence of zero or one map items
        /// </summary>
        public static readonly SequenceType SINGLE_MAP_ITEM = SequenceType.MakeSequenceType(ANY_MAP_TYPE, StaticProperty.ALLOWS_ONE);
        /// <summary>
        /// A type that allows a sequence of zero or one map items
        /// </summary>
        public static readonly SequenceType SEQUENCE_OF_MAPS = SequenceType.MakeSequenceType(ANY_MAP_TYPE, StaticProperty.ALLOWS_ZERO_OR_MORE);
        /// <summary>
        /// A type that allows a sequence of zero or one map items
        /// </summary>
        private readonly IPlainType keyType;
        /// <summary>
        /// A type that allows a sequence of zero or one map items
        /// </summary>
        private readonly SequenceType valueType;
        /// <summary>
        /// A type that allows a sequence of zero or one map items
        /// </summary>
        private readonly bool mustBeEmpty;

        /// <summary>
        /// A type that allows a sequence of zero or one map items
        /// </summary>
        public virtual IPlainType KeyType => keyType;

        /// <summary>
        /// A type that allows a sequence of zero or one map items
        /// </summary>
        public virtual SequenceType ValueType => valueType;

        /// <summary>
        /// A type that allows a sequence of zero or one map items
        /// </summary>
        public override string BasicAlphaCode => "FM";

        /// <summary>
        /// A type that allows a sequence of zero or one map items
        /// </summary>
        public override double DefaultPriority => keyType.GetNormalizedDefaultPriority() * valueType.PrimaryType.GetNormalizedDefaultPriority();

        /// <summary>
        /// Test whether a given item conforms to this type
        /// </summary>
        public override SequenceType[] ArgumentTypes => new SequenceType[]
            {
                SequenceType.MakeSequenceType(BuiltInAtomicType.ANY_ATOMIC, StaticProperty.EXACTLY_ONE)
            };

        /// <summary>
        /// Test whether a given item conforms to this type
        /// </summary>
        public override SequenceType ResultType
        {
            get
            {

                // a function call on this map can always return ()
                if (Cardinality.AllowsZero(valueType.GetCardinality()))
                {
                    return valueType;
                }
                else
                {
                    return SequenceType.MakeSequenceType(valueType.PrimaryType, Cardinality.Union(valueType.GetCardinality(), StaticProperty.ALLOWS_ZERO));
                }
            }
        }
        /// <summary>
        /// A type that allows a sequence of zero or one map items
        /// </summary>
        public MapType(IPlainType keyType, SequenceType valueType)
        {
            this.keyType = keyType;
            this.valueType = valueType;
            this.mustBeEmpty = false;
        }

        /// <summary>
        /// A type that allows a sequence of zero or one map items
        /// </summary>
        public MapType(IAtomicType keyType, SequenceType valueType, bool mustBeEmpty)
        {
            this.keyType = keyType;
            this.valueType = valueType;
            this.mustBeEmpty = mustBeEmpty;
        }

        /// <summary>
        /// A type that allows a sequence of zero or one map items
        /// </summary>
        public override Genre GetGenre()
        {
            return Genre.MAP;
        }

        /// <summary>
        /// A type that allows a sequence of zero or one map items
        /// </summary>
        public override bool IsMapType()
        {
            return true;
        }

        /// <summary>
        /// A type that allows a sequence of zero or one map items
        /// </summary>
        public override bool IsArrayType()
        {
            return false;
        }

        /// <summary>
        /// A type that allows a sequence of zero or one map items
        /// </summary>
        public override bool IsAtomizable(TypeHierarchy th)
        {
            return false;
        }

        /// <summary>
        /// Test whether a given item conforms to this type
        /// </summary>
        public override bool Matches(IItem item, TypeHierarchy th)
        {
            if (!(item is MapItem))
            {
                return false;
            }

            if (((MapItem)item).IsEmpty())
            {
                return true;
            }
            else if (mustBeEmpty)
            {
                return false;
            }

            if (this == ANY_MAP_TYPE)
            {
                return true;
            }
            else
            {
                return ((MapItem)item).Conforms(keyType, valueType, th);
            }
        }

        /// <summary>
        /// Test whether a given item conforms to this type
        /// </summary>
        public virtual int GetArity()
        {
            return 1;
        }

        /// <summary>
        /// Test whether a given item conforms to this type
        /// </summary>
        public override string ToString()
        {
            if (this == ANY_MAP_TYPE)
            {
                return "map(*)";
            }
            else if (this == EMPTY_MAP_TYPE)
            {
                return "map{}";
            }
            else
            {
                StringBuilder sb = new StringBuilder(100);
                sb.Append("map(");
                sb.Append(keyType.ToString());
                sb.Append(", ");
                sb.Append(valueType.ToString());
                sb.Append(")");
                return sb.ToString();
            }
        }

        /// <summary>
        /// Test whether a given item conforms to this type
        /// </summary>
        public string ToExportString()
        {
            if (this == ANY_MAP_TYPE)
            {
                return "map(*)";
            }
            else if (this == EMPTY_MAP_TYPE)
            {
                return "map{}";
            }
            else
            {
                StringBuilder sb = new StringBuilder(100);
                sb.Append("map(");
                sb.Append(keyType.ToExportString());
                sb.Append(", ");
                sb.Append(valueType.ToExportString());
                sb.Append(")");
                return sb.ToString();
            }
        }

        /// <summary>
        /// Test whether a given item conforms to this type
        /// </summary>
        /// <summary>
        /// Test whether this function type equals another function type
        /// </summary>
        public override bool Equals(object other)
        {
            if (this == other)
            {
                return true;
            }

            if (other is MapType)
            {
                MapType f2 = (MapType)other;
                return keyType.Equals(f2.keyType) && valueType.Equals(f2.valueType) && mustBeEmpty == f2.mustBeEmpty;
            }

            return false;
        }

        /// <summary>
        /// Test whether a given item conforms to this type
        /// </summary>
        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        public override int GetHashCode()
        {
            return keyType.GetHashCode() ^ valueType.GetHashCode();
        }

        /// <summary>
        /// Test whether a given item conforms to this type
        /// </summary>
        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        public override Affinity Relationship(IFunctionItemType other, TypeHierarchy th)
        {
            if (other == AnyFunctionType.GetInstance())
            {
                return Affinity.SUBSUMED_BY;
            }
            else if (Equals(other))
            {
                return Affinity.SAME_TYPE;
            }
            else if (other == MapType.ANY_MAP_TYPE)
            {
                return Affinity.SUBSUMED_BY;
            }
            else if (other.IsArrayType())
            {
                return Affinity.DISJOINT;
            }
            else if (other is RecordTest)
            {
                return TypeHierarchy.InverseRelationship(other.Relationship(this, th));
            }
            else if (other is MapType)
            {

                // See bug 3720. Two map types can never be disjoint because the empty
                // map is an instance of every map type
                MapType f2 = (MapType)other;
                Affinity keyRel = th.Relationship(keyType, f2.keyType);
                if (keyRel == Affinity.DISJOINT)
                {
                    return Affinity.OVERLAPS;
                }

                Affinity valueRel = th.SequenceTypeRelationship(valueType, f2.valueType);
                if (valueRel == Affinity.DISJOINT)
                {
                    return Affinity.OVERLAPS;
                }

                if (keyRel == valueRel)
                {
                    return keyRel;
                }

                if ((keyRel == Affinity.SAME_TYPE || keyRel == Affinity.SUBSUMES) && (valueRel == Affinity.SAME_TYPE || valueRel == Affinity.SUBSUMES))
                {
                    return Affinity.SUBSUMES;
                }

                if ((keyRel == Affinity.SAME_TYPE || keyRel == Affinity.SUBSUMED_BY) && (valueRel == Affinity.SAME_TYPE || valueRel == Affinity.SUBSUMED_BY))
                {
                    return Affinity.SUBSUMED_BY;
                }

                return Affinity.OVERLAPS;
            }
            else
            {

                // see Bug #4692
                SequenceType st = ResultType;
                if (!Cardinality.AllowsZero(st.GetCardinality()))
                {
                    st = SequenceType.MakeSequenceType(st.PrimaryType, Cardinality.Union(st.GetCardinality(), StaticProperty.ALLOWS_ZERO));
                }

                return new SpecificFunctionType(new SequenceType[] { SequenceType.ATOMIC_SEQUENCE }, st).Relationship(other, th);
            }
        }

        /// <summary>
        /// Test whether a given item conforms to this type
        /// </summary>
        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        public string ExplainMismatch(IItem item, TypeHierarchy th)
        {
            if (item is MapItem)
            {
                foreach (KeyValuePair kvp in ((MapItem)item).KeyValuePairs())
                {
                    if (!keyType.Matches(kvp.key, th))
                    {
                        string s = "The map contains a key (" + kvp.key.Show() + ") of type " + kvp.key.GetItemType() + " that is not an instance of the required type " + keyType;
                        return (s);
                    }

                    if (!valueType.Matches(kvp.value, th))
                    {
                        string s = "The map contains an entry with key (" + kvp.key.Show() + ") whose corresponding value (" + Err.DepictSequence(kvp.value) + ") is not an instance of the required type " + valueType;
                        string more = valueType.ExplainMismatch(kvp.value, th);
                        if (more != null)
                        {
                            s = s + ". " + more;
                        }

                        return (s);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Test whether a given item conforms to this type
        /// </summary>
        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        public override Expression MakeFunctionSequenceCoercer(Expression exp, Func<RoleDiagnostic> role, bool allow40)
        {
            return new SpecificFunctionType(ArgumentTypes, ResultType).MakeFunctionSequenceCoercer(exp, role, false);
        }
    }
}
