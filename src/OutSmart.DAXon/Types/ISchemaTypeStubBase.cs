////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Expressions;

namespace OutSmart.DAXon.Types
{
    // Stubs for additional excluded Type classes referenced by 40+ files each.
    // Runtime 2026-06-11 batch6: NumericType stub REMOVED (IsAtomicType=>true and
    // GetPlainMemberTypes=>null misrouted/blew up TypeHierarchy.ComputeRelationship for every
    // xs:numeric union check - "all options exhausted" IllegalState on parse-xml()/b[2] paths,
    // and silent wrong type relationships elsewhere). Real type/NumericType.cs re-included.
    // Phase 7.8: ISchemaTypeStubBase — minimal stub implementation of
    // ISchemaType so Untyped/AnyType/AnySimpleType (Java enum-singletons that
    // can't be C# enums implementing interfaces) can satisfy CS0029/CS1503
    // conversions to ISchemaType.
    public abstract class ISchemaTypeStubBase : ISchemaType
    {
        public virtual string Name => throw new NotImplementedException("STUB: ISchemaTypeStubBase.GetName not ported (excluded stub)");
        public virtual NamespaceUri TargetNamespace => throw new NotImplementedException("STUB: ISchemaTypeStubBase.GetTargetNamespace not ported (excluded stub)");
        public virtual int Fingerprint => -1;
        public virtual string DisplayName => throw new NotImplementedException("STUB: ISchemaTypeStubBase.GetDisplayName not ported (excluded stub)");
        public virtual string EQName => throw new NotImplementedException("STUB: ISchemaTypeStubBase.GetEQName not ported (excluded stub)");
        public virtual ISchemaType BaseType => throw new NotImplementedException("STUB: ISchemaTypeStubBase.GetBaseType not ported (excluded stub)");
        public virtual int DerivationMethod => 0;
        public virtual int FinalProhibitions => 0;
        public virtual string Description => GetType().Name;
        public virtual SchemaValidationStatus ValidationStatus => SchemaValidationStatus.VALIDATED;
        public virtual int RedefinitionLevel => 0;
        public virtual StructuredQName GetStructuredQName() => throw new NotImplementedException("STUB: ISchemaTypeStubBase.GetStructuredQName not ported (excluded stub)");
        public virtual bool IsComplexType() => false;
        public virtual bool IsSimpleType() => false;
        public virtual bool IsAtomicType() => false;
        public virtual bool IsAnonymousType() => false;
        public virtual int GetBlock() => 0;
        public virtual bool AllowsDerivation(int derivation) => true;
        public virtual void AnalyzeContentExpression(Expression expression, int kind) { }
        // Upstream AnySimpleType/AnyType/Untyped (the three subclasses) all atomize a node the same
        // way: to untypedAtomic over the string value.
        public virtual IAtomicSequence Atomize(NodeInfo node) => OutSmart.DAXon.Values.StringValue.MakeUntypedAtomic(node.UnicodeStringValue);
        public virtual bool IsSameType(ISchemaType other) => this == other;
        public virtual void CheckTypeDerivationIsOK(ISchemaType @base, int block) { }
        public virtual string GetSystemId() => throw new NotImplementedException("STUB: ISchemaTypeStubBase.GetSystemId not ported (excluded stub)");
        public virtual bool IsIdType() => false;
        public virtual bool IsIdRefType() => false;
    }
}
