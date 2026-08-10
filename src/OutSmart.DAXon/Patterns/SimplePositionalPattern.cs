////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Types;

namespace OutSmart.DAXon.Patterns
{
    // Faithful port of net.sf.saxon.pattern.SimplePositionalPattern (Saxon 12.9). Was a hollow stub whose
    // implicit conversion to Pattern threw, so match="x[3]" (literal integer predicate) crashed at compile.
    // A pattern of the form A[N] where A is an axis step with a node test and N is a numeric literal.
    internal sealed class SimplePositionalPattern : Pattern
    {
        private readonly NodeTest nodeTest;
        private readonly int position;

        public override int Fingerprint => nodeTest.Fingerprint;

        public SimplePositionalPattern(NodeTest nodeTest, int position)
        {
            this.nodeTest = nodeTest;
            this.position = position;
        }

        public override bool Matches(IItem item, IXPathContext context)
        {
            return item is NodeInfo && MatchesBeneathAnchor((NodeInfo)item, null, context);
        }

        public override UType GetUType()
        {
            return nodeTest.GetUType();
        }

        public override ItemType GetItemType()
        {
            return nodeTest.GetPrimitiveItemType();
        }

        public override bool Equals(object other)
        {
            if (other is SimplePositionalPattern)
            {
                SimplePositionalPattern fp = (SimplePositionalPattern)other;
                return nodeTest.Equals(fp.nodeTest) && position == fp.position;
            }
            else
            {
                return false;
            }
        }

        protected override int ComputeHashCode()
        {
            return nodeTest.GetHashCode() ^ (position << 3);
        }

        public override bool IsMotionless()
        {
            return false;
        }

        public override bool MatchesBeneathAnchor(NodeInfo node, NodeInfo anchor, IXPathContext context)
        {
            if (!nodeTest.Test(node))
            {
                return false;
            }

            if (anchor != null && node.GetParent() != anchor)
            {
                return false;
            }

            return position == Navigator.GetSiblingPosition(node, nodeTest, position);
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            SimplePositionalPattern n = new SimplePositionalPattern(nodeTest.Copy(), position);
            ExpressionTool.CopyLocationInfo(this, n);
            n.OriginalText = OriginalText;
            return n;
        }

        public override string Reconstruct()
        {
            return nodeTest + "[" + position + "]";
        }

        public override void Export(ExpressionPresenter presenter)
        {
            presenter.StartElement("p.simPos");
            presenter.EmitAttribute("test", AlphaCode.FromItemType(nodeTest));
            presenter.EmitAttribute("pos", position + "");
            presenter.EndElement();
        }
    }
}
