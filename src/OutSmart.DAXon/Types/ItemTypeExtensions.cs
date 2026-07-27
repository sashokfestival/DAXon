////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Misc Java stdlib types Saxon references but we don't yet shim individually.

using System;
using System.Collections.Generic;
using System.IO;

// Saxon-internal stub namespaces — sub-packages permanently excluded for now.
// Stubs only what's needed for top-level references to resolve.

namespace OutSmart.DAXon.Types
{

    // Extension methods for ItemType -- provide stubs that callers can invoke
    // without each implementer having to declare them. Lives in OutSmart.DAXon.Internal (which
    // is implicitly imported by every Saxon file).
    public static class ItemTypeExtensions
    {
        // Phase 5: GetUType moved to Saxon side (excluded stubs.cs) where UType is accessible.
        // Removed `public static object GetUType(this ItemType t) => null;` -- object return broke 42 sites.
        // ItemType has no instance GetPrimitiveItemType, so BuiltInAtomicType's own recursive reduction
        // (walk to the base type until a Saxon-primitive) resolves THROUGH this extension whenever it
        // recurses on an ItemType-typed base (BuiltInAtomicType.cs:809). The old `=> t` returned the base
        // unchanged, so reduction stopped one level up: xs:int->xs:long instead of xs:int->xs:integer, and
        // arithmetic/comparison on any integer subtype below xs:long then failed to find a calculator
        // (XPTY0004). Delegate to the real covariant impl so the recursion actually reduces; non-atomic
        // item types (node/function/union tests) keep the identity behaviour they had before.
        // Union types (xs:numeric and other pure unions) have their own primitive item type (ANY_ATOMIC);
        // route them through the instance impl too. The old identity leaked the NumericType itself where callers
        // cast the result to BuiltInAtomicType (ValueComparison.OperandType) -> InvalidCastException on `+`/`eq`
        // whose static operand type is xs:numeric (e.g. remove((1,'two'),2)+1).
        public static ItemType GetPrimitiveItemType(this ItemType t) =>
            t is BuiltInAtomicType b ? (ItemType)b.GetPrimitiveItemType()
            : t is LocalUnionType u ? (ItemType)u.GetPrimitiveItemType()
            : t;
        // runtime 2026-06-05: hollow GetAtomizedItemType(this ItemType t) => t REMOVED. It returned the type
        // itself for every receiver, so for a bare-ItemType-typed NodeTest (e.g. DIMENSIONVALUE in FinDim's
        // `!=`) Atomizer.GetAtomizedItemType got back the NameTest instead of its content's atomic type ->
        // TypeChecker.StaticTypeCheck rule-3 promotion threw "NameTest cannot be converted to BuiltInAtomicType".
        // OutSmart.DAXon.Internal cannot reference Saxon's NodeTest at compile time, so the real dispatch lives Saxon-side in
        // excluded stubs.cs (alongside the GetUType/GetGenre dispatch shims).
        public static bool Matches(this ItemType t, object item, object th) => true;
        // Phase 7.24: 1-arg Matches overload (Java's `boolean matches(Item)`).
        public static bool Matches(this ItemType t, object item) => true;
        // Phase 7.8: Java's String.matches(regex) moved to Extensions/JavaApiExtensions.cs
        // because resolution from ItemTypeExtensions class was unreliable across callsites.
        // GetBasicAlphaCode promoted to a real ItemType interface member (above) so interface-typed calls in
        // TypeHierarchy.Relationship virtual-dispatch to each item type's real impl instead of this ""-stub
        // (the "" stub made "".StartsWith("")==true -> every atomic pair returned SUBSUMED_BY -> spurious XPTY0004).
        public static string GetFullAlphaCode(this ItemType t) => "";
        public static double GetNormalizedDefaultPriority(this ItemType t) => 0.0;
        // GetGenre(this ItemType) moved to a Genre-typed shim on the Saxon side (generated/OutSmart.DAXon/
        // excluded stubs.cs, DAXonItemTypeUTypeExt.GetGenre). OutSmart.DAXon.Internal cannot name OutSmart.DAXon.Model.Genre,
        // so the prior `=> null` here made `(Genre)itemType.GetGenre()` callers (e.g. AxisExpression.TypeCheck
        // :134, every xsl:for-each / path-step) unbox null -> NullReferenceException.
        // DELIBERATELY permissive, NOT ported from upstream isAtomizable (attempted 2026-07-10,
        // reverted): the faithful dispatch (MapType false; SpecificFunctionType only when arity-1
        // with integer-compatible arg) made TypeChecker raise STATIC FOTY0013 for expressions the
        // rest of this port reports at RUN time (spec-bank map-014/027/028 expect runtime
        // FOJS0003/XQDY0137/FOJS0005 from map:merge, raised only when the call executes). And it
        // buys no QT3 tests: hof-908/909 (typed `local:f#1 eq 3` -> FOTY0013) fail identically in
        // upstream Java — their arity-1 integer-arg type IS statically atomizable, the atomized
        // type is xs:error, and TypeChecker rule-3 raises XPTY0004 (same code path,
        // TypeChecker.java:192-204) — Java-parity WONTFIX.
        public static bool IsAtomizable(this ItemType t) => true;
        // Phase 5: 1-arg form taking TypeHierarchy (TypeChecker uses this).
        public static bool IsAtomizable(this ItemType t, object th) => true;
        public static string ExplainMismatch(this ItemType t, object item, object th) => "";
        public static string ToExportString(this ItemType t) => "";
    }
}
