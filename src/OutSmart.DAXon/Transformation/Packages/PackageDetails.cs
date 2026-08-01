////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Xslt;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Lib;
using System.Threading;
namespace OutSmart.DAXon.Transformation.Packages
{
    /// <summary>
    /// Information about a package held in a package library; the package may or may not be loaded in memory
    /// </summary>
    public class PackageDetails
    {
        /// <summary>
        /// The name and version number of the package
        /// </summary>
        public VersionedPackageName nameAndVersion;
        public string baseName;
        public string shortName;
        /// <summary>
        /// The executable form of the compiled package
        /// </summary>
        public StylesheetPackage loadedPackage;
        public ResolvedResource sourceLocation;
        public ResolvedResource exportLocation;
        public int priority = int.MinValue;
        public Dictionary<StructuredQName, IGroundedValue> staticParams;
        public Thread beingProcessed;
    }
}