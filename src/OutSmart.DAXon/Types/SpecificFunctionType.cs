////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions.HigherOrder;
using OutSmart.DAXon.Values.Arrays;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Types
{
    public class SpecificFunctionType : AnyFunctionType
    {
        public static readonly IFunctionItemType COMPONENT_FUNCTION_TYPE = new SpecificFunctionType(new SequenceType[] { SequenceType.SINGLE_STRING }, SequenceType.ANY_SEQUENCE);
        private readonly SequenceType[] argTypes;
        private readonly SequenceType resultType;
        private readonly AnnotationList annotations;

        public override SequenceType[] ArgumentTypes => argTypes;

        public override SequenceType ResultType => resultType;

        public override AnnotationList AnnotationAssertions => annotations;

        public override double DefaultPriority
        {
            get
            {
                double prio = 1;
                foreach (SequenceType st in ArgumentTypes)
                {
                    prio *= st.PrimaryType.GetNormalizedDefaultPriority();
                }

                return prio;
            }
        }
        public SpecificFunctionType(SequenceType[] argTypes, SequenceType resultType)
        {
            this.argTypes = argTypes ?? throw new NullReferenceException();
            this.resultType = resultType ?? throw new NullReferenceException();
            this.annotations = AnnotationList.EMPTY;
        }

        public SpecificFunctionType(SequenceType[] argTypes, SequenceType resultType, AnnotationList annotations)
        {
            this.argTypes = argTypes ?? throw new NullReferenceException();
            this.resultType = resultType ?? throw new NullReferenceException();
            this.annotations = annotations ?? throw new NullReferenceException();
        }

        public virtual int GetArity()
        {
            return argTypes.Length;
        }

        public override bool IsAtomizable(TypeHierarchy th)
        {

            // An instance of a specific function type can be atomized only if it is an array, which
            // means there must be a single argument and it must be of type xs:integer or a supertype.
            if (GetArity() != 1)
            {
                return false;
            }

            ItemType argType = ArgumentTypes[0].PrimaryType;
            return th.IsSubType(BuiltInAtomicType.INTEGER, argType);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(100);
            sb.Append("(function(");
            for (int i = 0; i < argTypes.Length; i++)
            {
                sb.Append(argTypes[i].ToString());
                if (i < argTypes.Length - 1)
                {
                    sb.Append(", ");
                }
            }

            sb.Append(") as ");
            sb.Append(resultType.ToString());
            sb.Append(')');
            return sb.ToString();
        }

        public string ToExportString()
        {
            StringBuilder sb = new StringBuilder(100);
            sb.Append("(function(");
            for (int i = 0; i < argTypes.Length; i++)
            {
                sb.Append(argTypes[i].ToExportString());
                if (i < argTypes.Length - 1)
                {
                    sb.Append(", ");
                }
            }

            sb.Append(") as ");
            sb.Append(resultType.ToExportString());
            sb.Append(')');
            return sb.ToString();
        }

        /// <summary>
        /// Test whether this function type equals another function type
        /// </summary>
        public override bool Equals(object other)
        {
            if (other is SpecificFunctionType)
            {
                SpecificFunctionType f2 = (SpecificFunctionType)other;
                if (!resultType.Equals(f2.resultType))
                {
                    return false;
                }

                if (argTypes.Length != f2.argTypes.Length)
                {
                    return false;
                }

                for (int i = 0; i < argTypes.Length; i++)
                {
                    if (!argTypes[i].Equals(f2.argTypes[i]))
                    {
                        return false;
                    }
                }


                // Compare the annotations
                if (!AnnotationAssertions.Equals(f2.AnnotationAssertions))
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        public override int GetHashCode()
        {
            int h = resultType.GetHashCode() ^ argTypes.Length;
            foreach (SequenceType argType in argTypes)
            {
                h ^= argType.GetHashCode();
            }

            return h;
        }

        public override Affinity Relationship(IFunctionItemType other, TypeHierarchy th)
        {
            if (other == AnyFunctionType.GetInstance() || other is AnyFunctionTypeWithAssertions)
            {
                return Affinity.SUBSUMED_BY;
            }
            else if (Equals(other))
            {
                return Affinity.SAME_TYPE;
            }
            else if (other is ArrayItemType || other is MapType)
            {
                Affinity rrel = other.Relationship(this, th);
                switch (rrel)
                {
                    case Affinity.SUBSUMES:
                        return Affinity.SUBSUMED_BY;
                    case Affinity.SUBSUMED_BY:
                        return Affinity.SUBSUMES;
                    default:
                        return rrel;
                }
            }
            else
            {
                if (argTypes.Length != other.ArgumentTypes.Length)
                {
                    return Affinity.DISJOINT;
                }

                bool wider = false;
                bool narrower = false;
                for (int i = 0; i < argTypes.Length; i++)
                {
                    Affinity argRel = th.SequenceTypeRelationship(argTypes[i], other.ArgumentTypes[i]);
                    switch (argRel)
                    {
                        case Affinity.DISJOINT:
                            return Affinity.DISJOINT;
                        case Affinity.SUBSUMES:
                            narrower = true;
                            break;
                        case Affinity.SUBSUMED_BY:
                            wider = true;
                            break;
                        case Affinity.OVERLAPS:
                            wider = true;
                            narrower = true;
                            break;
                        case Affinity.SAME_TYPE:
                        default:
                            break;
                    }
                }

                Affinity resRel = th.SequenceTypeRelationship(resultType, other.ResultType);
                switch (resRel)
                {
                    case Affinity.DISJOINT:
                        return Affinity.DISJOINT;
                    case Affinity.SUBSUMES:
                        wider = true;
                        break;
                    case Affinity.SUBSUMED_BY:
                        narrower = true;
                        break;
                    case Affinity.OVERLAPS:
                        wider = true;
                        narrower = true;
                        break;
                    case Affinity.SAME_TYPE:
                    default:
                        break;
                }

                if (wider)
                {
                    if (narrower)
                    {
                        return Affinity.OVERLAPS;
                    }
                    else
                    {
                        return Affinity.SUBSUMES;
                    }
                }
                else
                {
                    if (narrower)
                    {
                        return Affinity.SUBSUMED_BY;
                    }
                    else
                    {
                        return Affinity.SAME_TYPE;
                    }
                }
            }
        }

        public override bool Matches(IItem item, TypeHierarchy th)
        {
            if (!(item is IFunctionItem))
            {
                return false;
            }

            if (item is MapItem)
            {

                // Bug 2938: Essentially a map is an instance of function(X) as Y
                // if (a) X is a subtype of xs:anyAtomicType, and (b) all the values in the map are instances of Y
                // Bug 4692: Adds the condition that the empty sequence must be an instance of Y.
                if (GetArity() == 1 && argTypes[0].GetCardinality() == StaticProperty.EXACTLY_ONE && argTypes[0].PrimaryType.IsPlainType() && Cardinality.AllowsZero(resultType.GetCardinality()))
                {
                    foreach (KeyValuePair pair in ((MapItem)item).KeyValuePairs())
                    {
                        if (!resultType.Matches(pair.value, th))
                        {
                            return false;
                        }
                    }

                    return true;
                }
                else
                {
                    return false;
                }
            }

            if (item is ArrayItem)
            {

                // Bug 2938: Essentially a array is an instance of function(X) as Y
                // if (a) X is a subtype of xs:integer, and (b) all the values in the array are instances of Y
                if (GetArity() == 1 && argTypes[0].GetCardinality() == StaticProperty.EXACTLY_ONE && argTypes[0].PrimaryType.IsPlainType())
                {
                    Affinity rel = th.Relationship(argTypes[0].PrimaryType, BuiltInAtomicType.INTEGER);
                    if (!(rel == Affinity.SAME_TYPE || rel == Affinity.SUBSUMED_BY))
                    {
                        return false;
                    }

                    foreach (IGroundedValue member in ((ArrayItem)item).Members())
                    {
                        if (!resultType.Matches(member, th))
                        {
                            return false;
                        }
                    }

                    return true;
                }
                else
                {
                    return false;
                }
            }

            Affinity affinity = th.Relationship(((IFunctionItem)item).FunctionItemType, this);
            return affinity == Affinity.SAME_TYPE || affinity == Affinity.SUBSUMED_BY;
        }

        public string ExplainMismatch(IItem item, TypeHierarchy th)
        {
            if (!(item is IFunctionItem))
            {
                return null;
            }

            if (item is MapItem)
            {
                if (GetArity() == 1)
                {
                    if (argTypes[0].GetCardinality() == StaticProperty.EXACTLY_ONE && argTypes[0].PrimaryType.IsPlainType())
                    {
                        foreach (KeyValuePair pair in ((MapItem)item).KeyValuePairs())
                        {
                            if (!resultType.Matches(pair.value, th))
                            {
                                string s = "The supplied map contains an entry with key (" + pair.key + ") whose corresponding value (" + Err.DepictSequence(pair.value) + ") is not an instance of the return type in the function signature (" + resultType + ")";
                                string more = resultType.ExplainMismatch(pair.value, th);
                                if (more != null)
                                {
                                    s = s + ". " + more;
                                }

                                return (s);
                            }
                        }
                    }
                    else
                    {
                        string s = "The function argument is of type " + argTypes[0] + "; a map can only be supplied for a function type whose argument type is atomic";
                        return (s);
                    }
                }
                else
                {
                    string s = "The function arity is " + GetArity() + "; a map can only be supplied for a function type with arity 1";
                    return (s);
                }
            }

            if (item is ArrayItem)
            {

                if (GetArity() == 1)
                {
                    if (argTypes[0].GetCardinality() == StaticProperty.EXACTLY_ONE && argTypes[0].PrimaryType.IsPlainType())
                    {
                        Affinity rel = th.Relationship(argTypes[0].PrimaryType, BuiltInAtomicType.INTEGER);
                        if (!(rel == Affinity.SAME_TYPE || rel == Affinity.SUBSUMED_BY))
                        {
                            string s = "The function expects an argument of type " + argTypes[0] + "; an array can only be supplied for a function that expects an integer";
                            return (s);
                        }
                        else
                        {
                            foreach (IGroundedValue member in ((ArrayItem)item).Members())
                            {
                                if (!resultType.Matches(member, th))
                                {
                                    string s = "The supplied array contains an entry (" + Err.DepictSequence(member) + ") is not an instance of the return type in the function signature (" + resultType + ")";
                                    string more = resultType.ExplainMismatch(member, th);
                                    if (more != null)
                                    {
                                        s = s + ". " + more;
                                    }

                                    return (s);
                                }
                            }
                        }
                    }
                    else
                    {
                        string s = "The function argument is of type " + argTypes[0] + "; an array can only be supplied for a function type whose argument type is xs:integer";
                        return (s);
                    }
                }
                else
                {
                    string s = "The function arity is " + GetArity() + "; an array can only be supplied for a function type with arity 1";
                    return (s);
                }
            }

            IFunctionItemType other = ((IFunctionItem)item).FunctionItemType;
            if (GetArity() != ((IFunctionItem)item).GetArity())
            {
                string s = "The required function arity is " + GetArity() + "; the supplied function has arity " + ((IFunctionItem)item).GetArity();
                return (s);
            }

            Affinity affinity = th.SequenceTypeRelationship(resultType, other.ResultType);
            if (affinity != Affinity.SAME_TYPE && affinity != Affinity.SUBSUMES)
            {
                string s = "The return type of the required function is " + resultType + " but the return type of the supplied function is " + other.ResultType;
                return (s);
            }

            for (int j = 0; j < GetArity(); j++)
            {
                affinity = th.SequenceTypeRelationship(argTypes[j], other.ArgumentTypes[j]);
                if (affinity != Affinity.SAME_TYPE && affinity != Affinity.SUBSUMED_BY)
                {
                    string s = "The type of the " + RoleDiagnostic.Ordinal(j + 1) + " argument of the required function is " + argTypes[j] + " but the declared type of the corresponding argument of the supplied function is " + other.ArgumentTypes[j];
                    return (s);
                }
            }

            return null;
        }

        public override Expression MakeFunctionSequenceCoercer(Expression exp, Func<RoleDiagnostic> role, bool allow40)
        {
            return new FunctionSequenceCoercer(exp, this, role, allow40);
        }
    }
}
