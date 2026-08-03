////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Api;

namespace OutSmart.DAXon.Events
{
    /// <summary>
    /// A location reported by a live source parser, which additionally knows the element-nesting depth
    /// within the current (possibly external) entity. A tree builder uses <c>LevelInEntity == 0</c> to mark
    /// the node sitting at the top level of its containing entity (for base-URI / xml:base handling).
    /// </summary>
    /// <remarks>
    /// Implemented by the native locator of <see cref="XmlReaderToReceiver"/> (the direct System.Xml.XmlReader
    /// path). Tree builders depend on this abstraction rather than on any concrete parser class.
    /// </remarks>
    internal interface ISourceLocator : ILocation
    {
        /// <summary>
        /// The element-nesting depth of this location within the current entity (0 at the top level of the
        /// entity, i.e. the document element or the root of an expanded external entity).
        /// </summary>
        int LevelInEntity { get; }
    }
}
