////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////import com.saxonica.functions.registry.XPath40FunctionSet;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Types;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Functions.Registry
{
    internal class XSLT30FunctionSet : BuiltInFunctionSet
    {
        private static readonly XSLT30FunctionSet THE_INSTANCE = new XSLT30FunctionSet();

        protected XSLT30FunctionSet()
        {
            Init();
        }
        public static XSLT30FunctionSet GetInstance()
        {
            return THE_INSTANCE;
        }

        protected virtual BuiltInFunctionSet CorrespondingXPathFunctionSet()
        {
            return XPath31FunctionSet.GetInstance();
        }

        private void Init()
        {
            ImportFunctionSet(CorrespondingXPathFunctionSet());
            Register("accumulator-after", 1, (e) => e.Populate(() => new AccumulatorFn.AccumulatorAfter(), AnyItemType.GetInstance(), STAR, LATE | CITEM).Arg(0, BuiltInAtomicType.STRING, ONE, null));
            Register("accumulator-before", 1, (e) => e.Populate(() => new AccumulatorFn.AccumulatorBefore(), AnyItemType.GetInstance(), STAR, LATE | CITEM).Arg(0, BuiltInAtomicType.STRING, ONE, null));
            Register("available-system-properties", 0, (e) => e.Populate(() => new AvailableSystemProperties(), BuiltInAtomicType.QNAME, STAR, LATE));
            Register("current", 0, (e) => e.Populate(() => new Current(), Types.Type.ITEM_TYPE, ONE, LATE));
            Register("current-group", 0, (e) => e.Populate(() => new CurrentGroup(), Types.Type.ITEM_TYPE, STAR, LATE));
            Register("current-grouping-key", 0, (e) => e.Populate(() => new CurrentGroupingKey(), BuiltInAtomicType.ANY_ATOMIC, STAR, LATE));
            Register("current-merge-group", 0, (e) => e.Populate(() => new CurrentMergeGroup(), AnyItemType.GetInstance(), STAR, LATE));
            Register("current-merge-group", 1, (e) => e.Populate(() => new CurrentMergeGroup(), AnyItemType.GetInstance(), STAR, LATE).Arg(0, BuiltInAtomicType.STRING, ONE, null));
            Register("current-merge-key", 0, (e) => e.Populate(() => new CurrentMergeKey(), BuiltInAtomicType.ANY_ATOMIC, STAR, LATE));
            Register("current-output-uri", 0, (e) => e.Populate(() => new CurrentOutputUri(), BuiltInAtomicType.ANY_URI, OPT, LATE));
            Register("document", 1, (e) => e.Populate(() => new DocumentFn(), Types.Type.NODE_TYPE, STAR, BASE | LATE | UO).Arg(0, Types.Type.ITEM_TYPE, STAR, null));
            Register("document", 2, (e) => e.Populate(() => new DocumentFn(), Types.Type.NODE_TYPE, STAR, BASE | LATE | UO).Arg(0, Types.Type.ITEM_TYPE, STAR, null).Arg(1, Types.Type.NODE_TYPE, ONE, null));
            Register("element-available", 1, (e) => e.Populate(() => new ElementAvailable(), BuiltInAtomicType.BOOLEAN, ONE, NS).Arg(0, BuiltInAtomicType.STRING, ONE, null));
            Register("function-available", 1, (e) => e.Populate(() => new FunctionAvailable(), BuiltInAtomicType.BOOLEAN, ONE, NS | LATE).Arg(0, BuiltInAtomicType.STRING, ONE, null));
            Register("function-available", 2, (e) => e.Populate(() => new FunctionAvailable(), BuiltInAtomicType.BOOLEAN, ONE, NS | LATE).Arg(0, BuiltInAtomicType.STRING, ONE, null).Arg(1, BuiltInAtomicType.INTEGER, ONE, null));
            Register("key", 2, (e) => e.Populate(() => new KeyFn(), Types.Type.NODE_TYPE, STAR, CDOC | NS | LATE).Arg(0, BuiltInAtomicType.STRING, ONE, null).Arg(1, BuiltInAtomicType.ANY_ATOMIC, STAR, EMPTY));
            Register("key", 3, (e) => e.Populate(() => new KeyFn(), Types.Type.NODE_TYPE, STAR, NS | LATE).Arg(0, BuiltInAtomicType.STRING, ONE, null).Arg(1, BuiltInAtomicType.ANY_ATOMIC, STAR, EMPTY).Arg(2, Types.Type.NODE_TYPE, ONE, null));
            Register("regex-group", 1, (e) => e.Populate(() => new RegexGroup(), BuiltInAtomicType.STRING, ONE, LATE | SIDE).Arg(0, BuiltInAtomicType.INTEGER, ONE, null));

            // Mark it as having side-effects to prevent loop-lifting
            Register("stream-available", 1, (e) => e.Populate(StreamAvailable.New(), BuiltInAtomicType.BOOLEAN, ONE, LATE).Arg(0, BuiltInAtomicType.STRING, OPT, null));
            Register("system-property", 1, (e) => e.Populate(() => new SystemProperty(), BuiltInAtomicType.STRING, ONE, NS | LATE).Arg(0, BuiltInAtomicType.STRING, ONE, null));
            Register("type-available", 1, (e) => e.Populate(() => new TypeAvailable(), BuiltInAtomicType.BOOLEAN, ONE, NS).Arg(0, BuiltInAtomicType.STRING, ONE, null));
            Register("unparsed-entity-public-id", 1, (e) => e.Populate(() => new UnparsedEntity.UnparsedEntityPublicId(), BuiltInAtomicType.STRING, ONE, CDOC | LATE).Arg(0, BuiltInAtomicType.STRING, ONE, null));
            Register("unparsed-entity-public-id", 2, (e) => e.Populate(() => new UnparsedEntity.UnparsedEntityPublicId(), BuiltInAtomicType.STRING, ONE, 0).Arg(0, BuiltInAtomicType.STRING, ONE, null).Arg(1, Types.Type.NODE_TYPE, ONE, null));
            Register("unparsed-entity-uri", 1, (e) => e.Populate(() => new UnparsedEntity.UnparsedEntityUri(), BuiltInAtomicType.ANY_URI, ONE, CDOC | LATE).Arg(0, BuiltInAtomicType.STRING, ONE, null));
            Register("unparsed-entity-uri", 2, (e) => e.Populate(() => new UnparsedEntity.UnparsedEntityUri(), BuiltInAtomicType.ANY_URI, ONE, 0).Arg(0, BuiltInAtomicType.STRING, ONE, null).Arg(1, Types.Type.NODE_TYPE, ONE, null));
        }
    }
}