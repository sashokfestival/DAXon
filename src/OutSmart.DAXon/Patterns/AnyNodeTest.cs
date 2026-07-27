////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Trees.Tiny;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Patterns
{
    public sealed class AnyNodeTest : NodeTest, IQNameTest
    {
        private static readonly AnyNodeTest THE_INSTANCE = new AnyNodeTest();

        /// <summary>
        /// Determine the default priority of this node test when used on its own as a Pattern
        /// </summary>
        public override double DefaultPriority => -0.5;

        /// <summary>
        /// Private constructor
        /// </summary>
        private AnyNodeTest()
        {
        }
        public static AnyNodeTest GetInstance()
        {
            return THE_INSTANCE;
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        public override UType GetUType()
        {
            return UType.ANY_NODE;
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        public override bool Matches(int nodeKind, INodeName name, ISchemaType annotation)
        {
            return nodeKind != Types.Type.PARENT_POINTER;
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        public override IIntPredicateProxy GetMatcher(INodeVectorTree tree)
        {
            byte[] nodeKindArray = tree.NodeKindArray;
            return IntPredicateLambda.Of((nodeNr) => nodeKindArray[nodeNr] != Types.Type.PARENT_POINTER);
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        public override bool Test(NodeInfo node)
        {
            return true;
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        public bool Matches(StructuredQName qname)
        {
            return true;
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        public bool MatchesFingerprint(NamePool namePool, int fp)
        {
            return true;
        }

        /// <summary>
        /// Determine the default priority of this node test when used on its own as a Pattern
        /// </summary>
        public override string ToString()
        {
            return "node()";
        }

        /// <summary>
        /// Determine the default priority of this node test when used on its own as a Pattern
        /// </summary>
        public string ExportQNameTest()
        {
            return "*";
        }
    }
}
