////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Values
{
    public abstract class AtomicValue : IItem, IAtomicSequence, IConversionResult, IIdentityComparable
    {
        protected readonly IAtomicType typeLabel;

        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        public virtual UnicodeString UnicodeStringValue
        {
            get
            {
                UnicodeString cs = PrimitiveStringValue;
                try
                {
                    return typeLabel.Postprocess(cs);
                }
                catch (XPathException err)
                {

                    // Ignore any XPath errors that occur during postprocessing
                    return cs;
                }
            }
        }

        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        public virtual UnicodeString CanonicalLexicalRepresentation => this.UnicodeStringValue;

        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        public abstract BuiltInAtomicType PrimitiveType { get; }

        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        public abstract UnicodeString PrimitiveStringValue { get; }
        public AtomicValue(IAtomicType typeLabel)
        {
            if (typeLabel == null)
                throw new NullReferenceException();
            this.typeLabel = typeLabel;
        }

        public virtual IAtomicSequence Atomize()
        {
            return this;
        }

        public AtomicValue Head()
        {
            return this;
        }

        public virtual int GetLength()
        {
            return 1;
        }

        public virtual bool IsUntypedAtomic()
        {
            return typeLabel == BuiltInAtomicType.UNTYPED_ATOMIC;
        }

        public abstract IAtomicMatchKey GetXPathMatchKey(IStringCollator collator, int implicitTimezone);
        public abstract IXPathComparable GetXPathComparable(IStringCollator collator, int implicitTimezone);
        public virtual IAtomicMatchKey AsMapKey()
        {
            try
            {
                return GetXPathMatchKey(CodepointCollator.GetInstance(), CalendarValue.NO_TIMEZONE);
            }
            catch (NoDynamicContextException e)
            {

                // Should not happen
                throw new InvalidOperationException("No implicit timezone available");
            }
        }

        public override bool Equals(object o)
        {
            throw new NotSupportedException("equals() not implemented");
        }

        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        public override int GetHashCode()
        {
            throw new NotSupportedException("hashCode() not implemented");
        }

        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        public virtual bool IsIdentical(AtomicValue v)
        {

            // default implementation
            return SimpleTypeComparison.GetInstance().Equal(this, v);
        }

        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        public virtual bool IsIdentical(IIdentityComparable other)
        {
            return other is AtomicValue && IsIdentical((AtomicValue)other);
        }

        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        public virtual int IdentityHashCode()
        {

            // default implementation, which presumes that if two objects are identical then they are equal.
            return GetHashCode();
        }

        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        public AtomicValue ItemAt(int n)
        {
            return n == 0 ? Head() : null;
        }

        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        public IAtomicType GetItemType()
        {
            return typeLabel;
        }
        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        public UType GetUType()
        {
            return GetItemType().GetUType();
        }

        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        public int GetCardinality()
        {
            return StaticProperty.EXACTLY_ONE;
        }

        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        public abstract AtomicValue CopyAsSubType(IAtomicType typeLabel);
        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        public virtual bool IsNaN()
        {
            return false;
        }
        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        public virtual bool EffectiveBooleanValue()
        {
            throw new XPathException("Effective boolean value is not defined for an atomic value of type " + Types.Type.DisplayTypeName(this)).AsTypeError().WithErrorCode("FORG0006"); // unless otherwise specified in a subclass
        }

        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        public virtual AtomicValue GetComponent(AccessorFn.Component component)
        {
            throw new NotSupportedException("Data type does not support component extraction");
        }

        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        public virtual void CheckPermittedContents(ISchemaType parentType, IStaticContext env, bool whole)
        {
            if (whole)
            {
                ISimpleType stype = null;
                if (parentType is ISimpleType)
                {
                    stype = (ISimpleType)parentType;
                }
                else if (parentType is IComplexType && ((IComplexType)parentType).IsSimpleContent())
                {
                    stype = ((IComplexType)parentType).SimpleContentType;
                }

                if (stype != null && !stype.IsNamespaceSensitive())
                {

                    // Can't validate namespace-sensitive content statically
                    ValidationFailure err = stype.ValidateContent(this.UnicodeStringValue, null, env.GetConfiguration().GetConversionRules());
                    if (err != null)
                    {
                        throw err.MakeException();
                    }

                    return;
                }
            }

            if (parentType is IComplexType && !((IComplexType)parentType).IsSimpleContent() && !((IComplexType)parentType).IsMixedContent() && !Whitespace.IsAllWhite(this.UnicodeStringValue))
            {
                XPathException err = new XPathException("Complex type " + parentType.Description + " does not allow text content " + Err.Wrap(this.UnicodeStringValue));
                err.SetIsTypeError(true);
                throw err;
            }
        }

        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        public virtual void CheckValidInJavascript()
        {
        }

        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        public virtual AtomicValue AsAtomic()
        {
            return this;
        }

        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        /// <summary>
        /// Get string value.
        /// </summary>
        public override string ToString()
        {
            return GetStringValue(); //throw new global::System.NotSupportedException();
            //return typeLabel + "(\"" + getStringValueCS() + "\")";
        }

        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        /// <summary>
        /// Get string value.
        /// </summary>
        public virtual string ToShortString()
        {
            return Show();
        }

        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        /// <summary>
        /// Get string value.
        /// </summary>
        public virtual string Show()
        {
            return typeLabel + "(\"" + this.UnicodeStringValue + "\")";
        }

        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        /// <summary>
        /// Get string value.
        /// </summary>
        public virtual SingleAtomicIterator Iterate()
        {
            return (SingleAtomicIterator)SingleAtomicIterator.MakeIterator(this);
        }

        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        /// <summary>
        /// Get string value.
        /// </summary>
        public virtual IEnumerator<AtomicValue> IIterator()
        {
            yield return this;
        }

        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        /// <summary>
        /// Get string value.
        /// </summary>
        public Genre GetGenre()
        {
            return Genre.ATOMIC;
        }
        IAtomicIterator IAtomicSequence.Iterate() => Iterate();
        ISequenceIterator IGroundedValue.Iterate() => Iterate();
        IItem IGroundedValue.ItemAt(int arg0) => ItemAt(arg0);
        IItem IGroundedValue.Head() => Head();
        IItem ISequence.Head() => Head();
        ISequenceIterator ISequence.Iterate() => Iterate();
        public virtual IGroundedValue Subsequence(int arg0, int arg1) => throw new NotImplementedException();
        public virtual string GetStringValue() => UnicodeStringValue.ToString();
        public IEnumerator<AtomicValue> GetEnumerator() { yield return this; }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        IItem IItem.Head() => Head();
        IItem IItem.ItemAt(int arg0) => ItemAt(arg0);
        SingletonIterator IItem.Iterate() => Iterate();

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
        public virtual IGroundedValue Reduce() => this;
        public virtual bool IsStreamed() => false;
        public virtual IGroundedValue Materialize() => this;
        public virtual IEnumerable<IItem> AsIterable() => new IItem[] { this };
        public virtual bool ContainsNode(NodeInfo sought) => OutSmart.DAXon.Expressions.SingletonIntersectExpression.ContainsNode(((OutSmart.DAXon.Model.ISequence)this).Iterate(), sought); // upstream GroundedValue default
        public virtual IGroundedValue Concatenate(IGroundedValue[] others)
        {
            // upstream GroundedValue default: chain this value's items with the others
            var __chain = new OutSmart.DAXon.Collections.Zeno.ZenoChain<OutSmart.DAXon.Model.IItem>().AddAll(((OutSmart.DAXon.Model.IGroundedValue)this).AsIterable());
            foreach (OutSmart.DAXon.Model.IGroundedValue __v in others)
                __chain = __chain.AddAll(__v.AsIterable());
            return new OutSmart.DAXon.Collections.Zeno.ZenoSequence(__chain);
        }
        public virtual ISequence MakeRepeatable() => this;
    }
}


