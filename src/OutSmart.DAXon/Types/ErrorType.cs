////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//import com.saxonica.ee.schema.UserSimpleType;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using static OutSmart.DAXon.Types.SchemaValidationStatus;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Collections.Trie;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Types
{
    /// <summary>
    /// This class has a singleton instance which represents the XML Schema 1.1 built-in type xs:error.
    /// </summary>
    public sealed class ErrorType : NodeTest, IAtomicType, IUnionType, IPlainType
    {
        private static readonly ErrorType theInstance = new ErrorType();

        /// <summary>
        /// Private constructor
        /// </summary>
        public string Name => "error";

        /// <summary>
        /// Private constructor
        /// </summary>
        public NamespaceUri TargetNamespace => NamespaceUri.SCHEMA;

        /// <summary>
        /// Private constructor
        /// </summary>
        public string EQName => "Q{" + NamespaceConstant.SCHEMA + "}error";

        /// <summary>
        /// Private constructor
        /// </summary>
        public IList<IPlainType> PlainMemberTypes => new List<IPlainType>();

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        public int RedefinitionLevel => 0;

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public SchemaValidationStatus ValidationStatus => VALIDATED;

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public ISchemaType BaseType => AnySimpleType.INSTANCE;

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public ISchemaType KnownBaseType => BaseType;

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public override int Fingerprint => StandardNames.XS_ERROR;

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public override StructuredQName MatchingNodeName => StandardNames.GetStructuredQName(StandardNames.XS_ERROR);

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public StructuredQName TypeName => new StructuredQName("xs", NamespaceUri.SCHEMA, "error");

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public string Description => "xs:error";

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public string DisplayName => "xs:error";

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public ISchemaType BuiltInBaseType => this;

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public int DerivationMethod => Derivation.DERIVATION_RESTRICTION;

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public int FinalProhibitions => Derivation.DERIVATION_EXTENSION | Derivation.DERIVATION_RESTRICTION | Derivation.DERIVATION_LIST | Derivation.DERIVATION_UNION;

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public int WhitespaceAction => Whitespace.COLLAPSE;

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public override int PrimitiveType => Types.Type.ITEM;

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public override double DefaultPriority => -1000;

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public SequenceType ResultTypeOfCast => SequenceType.OPTIONAL_ITEM;

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
        public BuiltInAtomicType PrimitiveAtomicType => throw new NotImplementedException();
        /// <summary>
        /// Private constructor
        /// </summary>
        private ErrorType()
        {
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        public override Genre GetGenre()
        {
            return Genre.ANY;
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        public override UType GetUType()
        {
            return UType.VOID;
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        public bool ContainsListType()
        {
            return false;
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        public bool IsBuiltInType()
        {
            return true;
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        public string GetSystemId()
        {
            return null;
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        public static ErrorType GetInstance()
        {
            return theInstance;
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public bool IsComplexType()
        {
            return false;
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public bool IsSimpleType()
        {
            return true;
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public bool IsSameType(ISchemaType other)
        {
            return other is ErrorType;
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public IAtomicSequence Atomize(NodeInfo node)
        {
            return StringValue.MakeUntypedAtomic(node.UnicodeStringValue);
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public void CheckTypeDerivationIsOK(ISchemaType type, int block)
        {
            if (type == this || type == AnySimpleType.INSTANCE)
            {
                return;
            }

            throw new SchemaException("Type xs:error is not validly derived from " + type.Description);
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public override bool IsAtomicType()
        {
            return false;
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public bool IsIdType()
        {
            return false;
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public bool IsIdRefType()
        {
            return false;
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public bool IsAnonymousType()
        {
            return false;
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public bool IsListType()
        {
            return false;
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public bool IsUnionType()
        {
            return true;
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public IAtomicSequence GetTypedValue(UnicodeString value, INamespaceResolver resolver, ConversionRules rules)
        {
            throw new ValidationFailure("Cast to xs:error always fails").MakeException();
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public StringConverter GetStringConverter(ConversionRules rules)
        {
            return null;
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public ValidationFailure ValidateContent(UnicodeString value, INamespaceResolver nsResolver, ConversionRules rules)
        {
            return new ValidationFailure("No content is ever valid against the type xs:error");
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public bool IsNamespaceSensitive()
        {
            return false;
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public int GetBlock()
        {
            return 0;
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public bool AllowsDerivation(int derivation)
        {
            return false;
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public void AnalyzeContentExpression(Expression expression, int kind)
        {
            throw new XPathException("No expression can ever return a value of type xs:error");
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public UnicodeString Preprocess(UnicodeString input)
        {
            return input;
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public UnicodeString Postprocess(UnicodeString input)
        {
            return input;
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public override bool IsPlainType()
        {
            return true;
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public override bool Matches(IItem item, TypeHierarchy th)
        {
            return false;
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public override bool Matches(int nodeKind, INodeName name, ISchemaType annotation)
        {
            return false;
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public override ItemType GetPrimitiveItemType()
        {
            return this;
        }
        IAtomicType IPlainType.GetPrimitiveItemType() => this;

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public override IAtomicType GetAtomizedItemType()
        {
            return BuiltInAtomicType.UNTYPED_ATOMIC;
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public override bool IsAtomizable(TypeHierarchy th)
        {
            return false;
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        // The return type is chosen so that use of the error() function will never give a static type error,
        // on the basis that item()? overlaps every other type, and it's almost impossible to make any
        // unwarranted inferences from it, except perhaps count(error()) lt 2.
        public string ToExportString()
        {
            return ToString();
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public override string ToString()
        {
            return "xs:error";
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public ValidationFailure Validate(AtomicValue primValue, UnicodeString lexicalValue, ConversionRules rules)
        {
            return new ValidationFailure("No value is valid against type xs:error");
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public bool IsOrdered(bool optimistic)
        {
            return false;
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public bool IsAbstract()
        {
            return true;
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public bool IsPrimitiveType()
        {
            return false;
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public StructuredQName GetStructuredQName()
        {
            return new StructuredQName("xs", NamespaceUri.SCHEMA, "error");
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public ValidationFailure CheckAgainstFacets(AtomicValue value, ConversionRules rules)
        {
            return null;
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <summary>
        /// Determine whether this is a built-in type or a user-defined type
        /// </summary>
        /// <summary>
        /// Get the validation status - always valid
        /// </summary>
        public override string ExplainMismatch(IItem item, TypeHierarchy th)
        {
            return ("Evaluation of the supplied expression will always fail");
        }
    }
}
