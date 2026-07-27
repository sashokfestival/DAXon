////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Resources;
using System.Collections.Generic;
using System.IO;
namespace OutSmart.DAXon.Lib
{
    /// <summary>
    /// The native currency of <see cref="IResourceResolver"/> and <see cref="IActiveSource"/>-based delivery.
    /// A resolver hands back a byte <see cref="System.IO.Stream"/>, a character <see cref="System.IO.TextReader"/>,
    /// or an already-built <see cref="NodeInfo"/> tree, plus systemId / content-type metadata and any parse-time
    /// filters. Phase 5 deleted the JAXP Source hierarchy entirely: this carrier and the native
    /// <see cref="IActiveSource"/> delivery interface replace it. <see cref="ToActiveSource()"/> turns a
    /// stream/reader/node resource into the <see cref="IActiveSource"/> the Sender pipeline delivers; filters and
    /// the please-close flag are applied by the delivery entry points that consume this resource.
    /// </summary>
    public sealed class ResolvedResource
    {
        /// <summary>Sentinel meaning "resolved, explicitly, to an empty resource".</summary>
        public static readonly ResolvedResource EMPTY = new ResolvedResource();

        /// <summary>A byte stream carrying the resource, or null.</summary>
        public Stream Stream;
        /// <summary>A character stream carrying the resource, or null.</summary>
        public TextReader TextReader;
        /// <summary>An already-parsed document/element tree, or null.</summary>
        public NodeInfo Node;
        public string SystemId;
        public string ContentType;
        /// <summary>The resource owns the stream and it must be closed after the parse.</summary>
        public bool PleaseCloseAfterUse;
        /// <summary>Filters (e.g. an IDFilter for a #fragment) to apply while parsing.</summary>
        public IList<IFilterFactory> Filters;

        public bool IsEmpty
        {
            get { return ReferenceEquals(this, EMPTY); }
        }

        /// <summary>
        /// Turn this resource into the <see cref="IActiveSource"/> that the Sender pipeline delivers: a
        /// stream/reader becomes an <see cref="ActiveStreamSource"/>, an already-built node becomes a
        /// <see cref="NodeInfo"/>'s active source. Filters and the please-close flag are NOT baked in here —
        /// they are applied via the ParseOptions at the delivery site (see <see cref="Sender"/>).
        /// </summary>
        public IActiveSource ToActiveSource()
        {
            if (Node != null)
            {
                return Node.AsActiveSource();
            }

            return new ActiveStreamSource(Stream, TextReader, SystemId);
        }
    }
}
