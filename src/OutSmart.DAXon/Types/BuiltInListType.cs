////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System.Collections.Generic;

namespace OutSmart.DAXon.Types
{
    // Faithful port of net.sf.saxon.type.BuiltInListType. Was a degenerate stub (one shared instance aliased
    // for IDREFS/NMTOKENS/ENTITIES/ANY_URIS, implementing nothing), so the xs:NMTOKENS/IDREFS/ENTITIES
    // constructor functions (which need IListType.GetItemType + ValidateContent + GetTypedValue) raised
    // XPST0017, and casting these to ISimpleType (DTD attribute typing) InvalidCast'd. The three built-in list
    // types register themselves in BuiltInType via MakeListType (called from the static field initialisers).
    internal class BuiltInListType : IListType
    {
        public static readonly BuiltInListType ENTITIES = MakeListType(NamespaceUri.SCHEMA, "ENTITIES");
        public static readonly BuiltInListType IDREFS = MakeListType(NamespaceUri.SCHEMA, "IDREFS");
        public static readonly BuiltInListType NMTOKENS = MakeListType(NamespaceUri.SCHEMA, "NMTOKENS");
        public static readonly BuiltInListType ANY_URIS = MakeListType(NamespaceUri.SCHEMA_INSTANCE, "anonymous_schemaLocationType");

        private readonly int fingerprint;
        private readonly BuiltInAtomicType itemType;
        public virtual int RedefinitionLevel => 0;
        public virtual SchemaValidationStatus ValidationStatus => SchemaValidationStatus.VALIDATED;
        public virtual ISchemaType BaseType => AnySimpleType.GetInstance();
        public virtual ISchemaType KnownBaseType => AnySimpleType.GetInstance();
        public virtual ISchemaType BuiltInBaseType => this;
        public virtual string Name => StandardNames.GetLocalName(fingerprint);
        public virtual string LocalName => StandardNames.GetLocalName(fingerprint);
        public virtual NamespaceUri TargetNamespace => NamespaceUri.SCHEMA;
        public virtual string EQName => "Q{" + NamespaceConstant.SCHEMA + "}" + Name;
        public virtual int Fingerprint => fingerprint;
        public virtual string DisplayName => StandardNames.GetDisplayName(fingerprint);
        public virtual int DerivationMethod => Derivation.DERIVATION_LIST;
        public virtual int FinalProhibitions => 0;
        public virtual int WhitespaceAction => Whitespace.COLLAPSE;
        public virtual string Description => DisplayName;

        public BuiltInListType(int fingerprint)
        {
            this.fingerprint = fingerprint;
            switch (fingerprint)
            {
                case StandardNames.XS_ENTITIES:
                    itemType = BuiltInAtomicType.ENTITY;
                    break;
                case StandardNames.XS_IDREFS:
                    itemType = BuiltInAtomicType.IDREF;
                    break;
                case StandardNames.XS_NMTOKENS:
                    itemType = BuiltInAtomicType.NMTOKEN;
                    break;
                case StandardNames.XSI_SCHEMA_LOCATION_TYPE:
                    itemType = BuiltInAtomicType.ANY_URI;
                    break;
            }
        }

        public static BuiltInListType GetInstance() => NMTOKENS;

        private static BuiltInListType MakeListType(NamespaceUri ns, string lname)
        {
            BuiltInListType t = new BuiltInListType(StandardNames.GetFingerprint(ns, lname));
            BuiltInType.Register(t.Fingerprint, t);
            return t;
        }

        public virtual ISimpleType GetItemType() => itemType;

        public virtual bool IsBuiltInType() => true;
        public virtual string GetSystemId() => null;
        public virtual bool IsAtomicType() => false;
        public virtual bool IsIdType() => false;
        public virtual bool IsIdRefType() => fingerprint == StandardNames.XS_IDREFS;
        public virtual bool IsListType() => true;
        public virtual bool IsUnionType() => false;
        public virtual bool IsAnonymousType() => false;
        public virtual bool IsNamespaceSensitive() => false;
        public virtual bool IsComplexType() => false;
        public virtual bool IsSimpleType() => true;
        public virtual StructuredQName GetStructuredQName() => new StructuredQName("xs", NamespaceUri.SCHEMA, LocalName);
        public virtual int GetBlock() => 0;
        public virtual bool AllowsDerivation(int derivation) => true;
        public virtual bool IsSameType(ISchemaType other) => other.Fingerprint == Fingerprint;
        public virtual void CheckTypeDerivationIsOK(ISchemaType type, int block) { }
        public virtual void AnalyzeContentExpression(Expression expression, int kind) => BuiltInAtomicType.AnalyzeContentExpression(this, expression, kind);
        public virtual UnicodeString Preprocess(UnicodeString input) => input;
        public virtual UnicodeString Postprocess(UnicodeString input) => input;

        public virtual IAtomicSequence Atomize(NodeInfo node)
        {
            try
            {
                return GetTypedValue(node.UnicodeStringValue, node.AllNamespaces, node.GetConfiguration().GetConversionRules());
            }
            catch (ValidationException err)
            {
                throw new XPathException("Internal error: value doesn't match its type annotation. " + err.Message);
            }
        }

        public virtual ValidationFailure ValidateContent(UnicodeString value, INamespaceResolver nsResolver, ConversionRules rules)
        {
            ISimpleType @base = GetItemType();
            Whitespace.Tokenizer iter = new Whitespace.Tokenizer(value);
            bool found = false;
            StringValue val;
            while ((val = iter.Next()) != null)
            {
                found = true;
                ValidationFailure v = @base.ValidateContent(val.UnicodeStringValue, nsResolver, rules);
                if (v != null)
                {
                    return v;
                }
            }

            if (!found)
            {
                return new ValidationFailure("The built-in list type " + StandardNames.GetDisplayName(fingerprint) + " does not allow a zero-length list");
            }

            return null;
        }

        public virtual IAtomicSequence GetTypedValue(UnicodeString value, INamespaceResolver resolver, ConversionRules rules)
        {
            Whitespace.Tokenizer iter = new Whitespace.Tokenizer(value);
            ISimpleType atomicType = GetItemType();
            List<AtomicValue> result = new List<AtomicValue>();
            StringValue val;
            while ((val = iter.Next()) != null)
            {
                foreach (AtomicValue av in atomicType.GetTypedValue(val.UnicodeStringValue, resolver, rules))
                {
                    result.Add(av);
                }
            }

            return new AtomicArray(result);
        }
    }
}
