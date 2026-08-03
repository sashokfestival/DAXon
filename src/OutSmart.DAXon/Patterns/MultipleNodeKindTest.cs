////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Ported from upstream net/sf/saxon/pattern/MultipleNodeKindTest.java (replaces the hollow stub whose
// Matches()/GetMatcher() always failed, so parent::node()/node() etc. matched nothing or NRE'd).

using OutSmart.DAXon.Model;
using OutSmart.DAXon.Trees.Tiny;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Collections;

namespace OutSmart.DAXon.Patterns
{
    /// <summary>A node test matching nodes of any of a fixed subset of node kinds (e.g. node(), parent::node()).</summary>
    internal sealed class MultipleNodeKindTest : NodeTest
    {
        public static readonly MultipleNodeKindTest PARENT_NODE =
            new MultipleNodeKindTest(UType.DOCUMENT.Union(UType.ELEMENT));

        public static readonly MultipleNodeKindTest DOC_ELEM_ATTR =
            new MultipleNodeKindTest(UType.DOCUMENT.Union(UType.ELEMENT).Union(UType.ATTRIBUTE));

        public static readonly MultipleNodeKindTest LEAF =
            new MultipleNodeKindTest(UType.TEXT.Union(UType.COMMENT).Union(UType.PI).Union(UType.NAMESPACE).Union(UType.ATTRIBUTE));

        public static readonly MultipleNodeKindTest CHILD_NODE =
            new MultipleNodeKindTest(UType.ELEMENT.Union(UType.TEXT).Union(UType.COMMENT).Union(UType.PI));

        public static readonly MultipleNodeKindTest DESCENDANT_NODE =
            new MultipleNodeKindTest(UType.ELEMENT.Union(UType.TEXT).Union(UType.COMMENT).Union(UType.PI));

        private readonly UType uType;
        private readonly int nodeKindMask;

        public override double DefaultPriority => -0.5;

        public MultipleNodeKindTest(UType u)
        {
            uType = u;
            if (UType.DOCUMENT.Overlaps(u))
            {
                nodeKindMask |= 1 << Types.Type.DOCUMENT;
            }
            if (UType.ELEMENT.Overlaps(u))
            {
                nodeKindMask |= 1 << Types.Type.ELEMENT;
            }
            if (UType.ATTRIBUTE.Overlaps(u))
            {
                nodeKindMask |= 1 << Types.Type.ATTRIBUTE;
            }
            if (UType.TEXT.Overlaps(u))
            {
                nodeKindMask |= 1 << Types.Type.TEXT;
            }
            if (UType.COMMENT.Overlaps(u))
            {
                nodeKindMask |= 1 << Types.Type.COMMENT;
            }
            if (UType.PI.Overlaps(u))
            {
                nodeKindMask |= 1 << Types.Type.PROCESSING_INSTRUCTION;
            }
            if (UType.NAMESPACE.Overlaps(u))
            {
                nodeKindMask |= 1 << Types.Type.NAMESPACE;
            }
        }

        public override UType GetUType() => uType;

        public override bool Matches(int nodeKind, INodeName name, ISchemaType annotation)
        {
            return (nodeKindMask & (1 << nodeKind)) != 0;
        }

        public override IIntPredicateProxy GetMatcher(INodeVectorTree tree)
        {
            return IntPredicateLambda.Of((nodeNr) =>
            {
                int nodeKind = tree.GetNodeKind(nodeNr);
                if (nodeKind == Types.Type.WHITESPACE_TEXT)
                {
                    nodeKind = Types.Type.TEXT;
                }
                // A TinyTree stores an element whose only child is text as TEXTUAL_ELEMENT (17); it must match
                // as an ELEMENT (upstream masks nodeKind & 0x0f here). Without this, abbreviated node() on the
                // child axis — rewritten to MultipleNodeKindTest.CHILD_NODE — silently dropped every
                // text-content element (e.g. `/root/node()` on a doc with <e>text</e> children).
                if (nodeKind == Types.Type.TEXTUAL_ELEMENT)
                {
                    nodeKind = Types.Type.ELEMENT;
                }

                return (nodeKindMask & (1 << nodeKind)) != 0;
            });
        }

        public override bool Test(NodeInfo node)
        {
            return (nodeKindMask & (1 << node.GetNodeKind())) != 0;
        }

        public override string ToString() => "node-kinds(" + uType + ")";
    }
}
