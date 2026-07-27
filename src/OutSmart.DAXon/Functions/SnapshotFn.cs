////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Trees.Tiny;
using OutSmart.DAXon.Trees.Wrappers;
using OutSmart.DAXon.Types;
using System.Collections.Generic;

namespace OutSmart.DAXon.Functions
{
    // Faithful port of net.sf.saxon.functions.SnapshotFn (Saxon 12.9). The class was missing from the port,
    // so fn:snapshot() was unregistered (XPST0017).
    // XSLT 3.0 function snapshot(): a deep-copy that also includes a shallow copy of all ancestors.
    public class SnapshotFn : SystemFunction
    {

        public override string StreamerName => "SnapshotFn";
        public override int GetCardinality(Expression[] arguments)
        {
            return arguments[0].GetCardinality();
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            ISequence @in = arguments.Length == 0 ? (ISequence)context.GetContextItem() : arguments[0];
            ISequenceIterator iter = SnapshotSequence(@in.Iterate());
            return new LazySequence(iter);
        }

        public static ISequenceIterator SnapshotSequence(ISequenceIterator nodes)
        {
            return ItemMappingIterator.IMap(nodes, SnapshotSingle);
        }

        /// <summary>
        /// Take a snapshot of a single item
        /// </summary>
        public static IItem SnapshotSingle(IItem origin)
        {
            if (origin is NodeInfo)
            {
                if (((NodeInfo)origin).GetParent() == null)
                {
                    VirtualCopy vc = VirtualCopy.MakeVirtualCopy((NodeInfo)origin);
                    vc.GetTreeInfo().SetCopyAccumulators(true);
                    return vc;
                }
                else
                {
                    return SnapshotNode.MakeSnapshot((NodeInfo)origin);
                }
            }
            else
            {
                return origin;
            }
        }

        public static List<NodeInfo> MakeAncestorList(NodeInfo origin)
        {
            List<NodeInfo> ancestors = new List<NodeInfo>(20);
            IAxisIterator iter = origin.IterateAxis(AxisInfo.ANCESTOR);
            NodeInfo item;
            while ((item = iter.Next()) != null)
            {
                ancestors.Add(item);
            }

            return ancestors;
        }

        public static BuilderMonitor OpenAncestors(NodeInfo origin, List<NodeInfo> ancestors, IXPathContext context)
        {
            NodeInfo root = origin.Root;
            TinyBuilder builder = new TinyBuilder(context.GetController().MakePipelineConfiguration());
            builder.SetStatistics(context.GetConfiguration().GetTreeStatistics().TEMPORARY_TREE_STATISTICS);
            builder.SetSystemId(root.GetSystemId());
            builder.SetTiming(false);

            BuilderMonitor bm = builder.GetBuilderMonitor();
            bm.Open();

            ITreeInfo source = root.GetTreeInfo();
            IEnumerator<string> unparsedEntities = source.UnparsedEntityNames;
            while (unparsedEntities.MoveNext())
            {
                string name = unparsedEntities.Current;
                string[] properties = source.GetUnparsedEntity(name);
                builder.SetUnparsedEntity(name, properties[0], properties[1]);
            }

            ISchemaType ancestorType;
            if (context.GetController().GetExecutable().IsSchemaAware())
            {
                ancestorType = AnyType.GetInstance();
            }
            else
            {
                ancestorType = Untyped.GetInstance();
            }

            for (int i = ancestors.Count - 1; i >= 0; i--)
            {
                NodeInfo anc = ancestors[i];
                int kind = anc.GetNodeKind();
                switch (kind)
                {
                    case OutSmart.DAXon.Types.Type.ELEMENT:
                        bm.StartElement(NameOfNode.MakeName(anc), ancestorType, anc.Attributes(), anc.AllNamespaces, Loc.NONE, ReceiverOption.NONE);
                        break;
                    case OutSmart.DAXon.Types.Type.DOCUMENT:
                        bm.StartDocument(ReceiverOption.NONE);
                        break;
                    default:
                        throw new System.InvalidOperationException("Unknown ancestor node kind " + anc.GetNodeKind());
                }
            }

            return bm;
        }

        public static void CloseAncestors(List<NodeInfo> ancestors, IReceiver bm)
        {
            foreach (NodeInfo anc in ancestors)
            {
                switch (anc.GetNodeKind())
                {
                    case OutSmart.DAXon.Types.Type.ELEMENT:
                        bm.EndElement();
                        break;
                    case OutSmart.DAXon.Types.Type.DOCUMENT:
                        bm.EndDocument();
                        break;
                    default:
                        throw new System.InvalidOperationException("Unknown ancestor node kind " + anc.GetNodeKind());
                }
            }

            bm.Dispose(); // Java Receiver.close()
        }
    }
}
