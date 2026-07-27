////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Xslt;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
using OutSmart.DAXon.Internal.Jaxp.Transform;
using System.IO;
using System.Threading;
namespace OutSmart.DAXon.Transformation.Packages
{
    public class PackageLibrary
    {
        private readonly Configuration config;
        private readonly CompilerInfo compilerInfo;
        private Dictionary<string, IList<PackageVersion>> packageVersions = new Dictionary<string, IList<PackageVersion>>();
        private Dictionary<VersionedPackageName, PackageDetails> packages = new Dictionary<VersionedPackageName, PackageDetails>();

        public virtual IList<StylesheetPackage> Packages
        {
            get
            {
                lock (this)
                {
                    IList<StylesheetPackage> result = new List<StylesheetPackage>();
                    foreach (PackageDetails details in packages.Values())
                    {
                        if (details.loadedPackage != null)
                        {
                            result.Add(details.loadedPackage);
                        }
                    }

                    return result;
                }
            }
        }
        public PackageLibrary(CompilerInfo compilerInfo)
        {
            this.compilerInfo = compilerInfo;
            this.config = compilerInfo.GetConfiguration();
        }

        public PackageLibrary(PackageLibrary library)
        {
            packageVersions = new Dictionary<string, IList<PackageVersion>>(library.packageVersions);
            packages = new Dictionary<VersionedPackageName, PackageDetails>(library.packages);
            compilerInfo = library.compilerInfo;
            config = library.config;
        }

        public PackageLibrary(CompilerInfo info, HashSet<string> files)
        {
            compilerInfo = info;
            config = info.GetConfiguration();
            foreach (string file in files)
            {
                PackageInspector inspector = new PackageInspector(config.MakePipelineConfiguration());
                PackageDetails details = inspector.GetPackageDetails(file, config);
                if (details == null)
                {
                    string message = "Unable to get package name and version for file " + Path.GetFileName(file);
                    string diagnostics = inspector.Diagnostics;
                    if (diagnostics != null)
                    {
                        message += " (" + diagnostics + ")";
                    }

                    throw new XPathException(message);
                }

                AddPackage(details);
            }
        }

        public virtual CompilerInfo GetCompilerInfo()
        {
            return compilerInfo;
        }

        public virtual void AddPackage(StylesheetPackage packageIn)
        {
            lock (this)
            {
                string name = packageIn.PackageName;
                PackageVersion version = packageIn.GetPackageVersion();
                VersionedPackageName vp = new VersionedPackageName(name, version);
                PackageDetails details = new PackageDetails();
                details.nameAndVersion = vp;
                details.loadedPackage = packageIn;
                if (vp.packageName != null)
                {
                    packages.Put(vp, details);
                    AddPackage(details);
                }
            }
        }

        public virtual void AddPackage(PackageDetails details)
        {
            lock (this)
            {
                VersionedPackageName vp = details.nameAndVersion;
                string name = vp.packageName;
                PackageVersion version = vp.packageVersion;
                IList<PackageVersion> versions = packageVersions.Get(name);

                if (versions == null)
                {
                    versions = new List<PackageVersion>();
                    packageVersions.Put(name, versions);
                }

                versions.Add(version);
                packages.Put(vp, details);
            }
        }

        public virtual void AddPackage(string file)
        {
            PackageInspector inspector = new PackageInspector(config.MakePipelineConfiguration());
            PackageDetails details = inspector.GetPackageDetails(file, config);
            if (details == null)
            {
                string message = "Unable to get package name and version for file " + Path.GetFileName(file);
                string diagnostics = inspector.Diagnostics;
                if (diagnostics != null)
                {
                    message += " (" + diagnostics + ")";
                }

                throw new XPathException(message);
            }

            AddPackage(details);
        }

        public virtual PackageDetails FindPackage(string name, PackageVersionRanges ranges)
        {
            lock (this)
            {
                HashSet<PackageDetails> candidates = new HashSet<PackageDetails>();
                IList<PackageVersion> available = packageVersions.Get(name);
                if (available == null)
                {
                    return null;
                }

                int maxPriority = int.MinValue;
                foreach (PackageVersion pv in available)
                {
                    PackageDetails details = packages.Get(new VersionedPackageName(name, pv));
                    if (ranges.Contains(pv))
                    {
                        candidates.Add(details);
                        int priority = details.priority;
                        if (priority > maxPriority)
                        {
                            maxPriority = priority;
                        }
                    }
                }

                if (candidates.IsEmpty())
                {
                    return null;
                }
                else if (candidates.Count == 1)
                {
                    return candidates.First();
                }
                else
                {

                    // more than one candidate
                    HashSet<PackageVersion> shortList = new HashSet<PackageVersion>();
                    PackageDetails highest = null;
                    if (maxPriority == int.MinValue)
                    {
                        foreach (PackageDetails details in candidates)
                        {
                            if (highest == null || details.nameAndVersion.packageVersion.CompareTo(highest.nameAndVersion.packageVersion) > 0)
                            {
                                highest = details;
                            }
                        }
                    }
                    else
                    {
                        foreach (PackageDetails details in candidates)
                        {
                            int priority = details.priority;
                            PackageVersion pv = details.nameAndVersion.packageVersion;
                            if (priority != int.MinValue && priority == maxPriority && (highest == null || pv.CompareTo(highest.nameAndVersion.packageVersion) > 0))
                            {
                                highest = details;
                            }
                        }
                    }

                    return highest;
                }
            }
        }

        public virtual PackageDetails FindDetailsForAlias(string shortName)
        {
            lock (this)
            {
                PackageDetails selected = null;
                foreach (PackageDetails details in packages.Values())
                {
                    if (shortName.Equals(details.shortName))
                    {
                        if (selected == null)
                        {
                            selected = details;
                        }
                        else
                        {
                            throw new InvalidOperationException("Non-unique shortName in package library: " + shortName);
                        }
                    }
                }

                return selected;
            }
        }

        public virtual StylesheetPackage ObtainLoadedPackage(PackageDetails details, IList<VersionedPackageName> disallowed)
        {
            if (details.loadedPackage != null)
            {
                return details.loadedPackage;
            }
            else if (details.exportLocation != null)
            {
                TestForCycles(details, disallowed);
                details.beingProcessed = Thread.CurrentThread;
                ResolvedResource input = details.exportLocation;
                IIPackageLoader loader = config.MakePackageLoader();
                StylesheetPackage pack = loader.LoadPackage(input);
                CheckNameAndVersion(pack, details);
                details.loadedPackage = pack;
                details.beingProcessed = null;
                return pack;
            }
            else if (details.sourceLocation != null)
            {
                TestForCycles(details, disallowed);
                details.beingProcessed = Thread.CurrentThread;
                Compilation compilation = new Compilation(config, compilerInfo);
                compilation.SetUsingPackages(disallowed);
                compilation.SetExpectedNameAndVersion(details.nameAndVersion);
                compilation.ClearParameters();
                compilation.SetLibraryPackage(true);
                if (details.staticParams != null)
                {
                    foreach (KeyValuePair<StructuredQName, IGroundedValue> entry in details.staticParams.EntrySet())
                    {
                        compilation.SetParameter(entry.Key, entry.Value);
                    }
                }

                PrincipalStylesheetModule psm = compilation.CompilePackage(details.sourceLocation);
                details.beingProcessed = null;
                if (compilation.ErrorCount > 0)
                {
                    throw new XPathException("Errors found in package " + details.nameAndVersion.packageName);
                }

                StylesheetPackage styPack = psm.GetStylesheetPackage();
                CheckNameAndVersion(styPack, details);
                details.loadedPackage = styPack;
                return styPack;
            }
            else
            {
                return null;
            }
        }

        private void TestForCycles(PackageDetails details, IList<VersionedPackageName> disallowed)
        {
            if (details.beingProcessed == Thread.CurrentThread)
            {

                // Report a cycle of package dependencies
                StringBuilder buffer = new StringBuilder(1024);
                foreach (VersionedPackageName n in disallowed)
                {
                    buffer.Append(n.packageName);
                    buffer.Append(", ");
                }

                buffer.Append("and ");
                buffer.Append(details.nameAndVersion.packageName);
                throw new XPathException("There is a cycle of package dependencies involving " + buffer, "XTSE3005");
            }
        }

        private void CheckNameAndVersion(StylesheetPackage pack, PackageDetails details)
        {
            string storedName = pack.PackageName;
            if (details.baseName != null)
            {
                if (!details.baseName.Equals(storedName))
                {
                    throw new XPathException("Base name of package (" + details.baseName + ") does not match the value in the XSLT source (" + storedName + ")");
                }
            }
            else
            {
                if (!details.nameAndVersion.packageName.Equals(storedName))
                {
                    throw new XPathException("Registered name of package (" + details.nameAndVersion.packageName + ") does not match the value in the XSLT source (" + storedName + ")");
                }
            }

            PackageVersion actualVersion = pack.GetPackageVersion();

            // Bug 6762. Suggestion is to change this to equalsIgnoringSuffix(), but needs further testing
            if (!actualVersion.Equals(details.nameAndVersion.packageVersion))
            {
                throw new XPathException("Registered version number of package (" + details.nameAndVersion.packageVersion + ") does not match the value in the XSLT source (" + actualVersion + ")");
            }
        }
    }
}
