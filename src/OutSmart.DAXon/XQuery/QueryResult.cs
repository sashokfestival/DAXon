////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Faithful (partial) port of net/sf/saxon/query/QueryResult.java (Saxon 12.9).
//
// History: started as a hollow stub (every Serialize was `{ }`), which silently emptied
// every path that serializes via QueryResult: Serializer.SerializeNode/SerializeXdmValue,
// the xsl:message free-standing fallback, and Literal.Export node content.
//
// Ported: serialize(node, result, props) + serializeSequence(iter, config, result, props)
// using the proven inline sequence-copy (Open -> Append(item) -> Dispose), same pattern as
// functions/Serialize.cs (the real SequenceCopier needs a newer 0-arg Append this IReceiver
// lacks). NOT ported: wrap()/sendWrappedSequence (RESULT_NS diagnostic wrapper — only the
// DAXonDeepEqual debug-logging path uses it) and rewriteToDisk (XQuery Update).

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Serialization;
using OutSmart.DAXon.Trees.Iterators;
using Configuration = OutSmart.DAXon.Core.Configuration;
using Result = OutSmart.DAXon.Internal.Jaxp.Transform.Result;
using Properties = OutSmart.DAXon.Internal.Collections.Properties;

namespace OutSmart.DAXon.XQuery
{
    public class QueryResult
    {
        public const string RESULT_NS = "http://saxon.sf.net/2009/serialization/result";
        public QueryResult() { }

        public static void Serialize(NodeInfo node, Result destination, Properties outputProperties)
        {
            SerializeSequence(SingletonIterator.MakeIterator(node), node.GetConfiguration(), destination, outputProperties);
        }

        public static void Serialize(NodeInfo node, Result destination, SerializationProperties properties)
        {
            SerializeSequence(SingletonIterator.MakeIterator(node), node.GetConfiguration(), destination, properties);
        }

        public static void SerializeSequence(ISequenceIterator iterator, Configuration config, Result result, Properties outputProperties)
        {
            SerializeSequence(iterator, config, result, new SerializationProperties(outputProperties));
        }

        public static void SerializeSequence(ISequenceIterator iterator, Configuration config, Result result, SerializationProperties properties)
        {
            SerializerFactory sf = config.SerializerFactory;
            IReceiver tr = sf.GetReceiver(result, properties);
            tr.Open();
            IItem it;
            while ((it = iterator.Next()) != null) { tr.Append(it); }
            tr.Dispose();
        }

        // Legacy no-op shapes for the DAXonDeepEqual "undocumented diagnostic option" debug path,
        // whose input is the still-hollow Wrap(). Typed calls above always win overload resolution.
        public static void Serialize(object node, object destination, object props) { }
        public static void Serialize(object node, object destination) { }
        public static string Serialize(object node) => string.Empty;
        public static void SerializeSequence(object iter, object config, object destination) { }
        public static object Wrap(object iterator, object config) => null;
    }
}
