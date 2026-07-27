////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Functions.Registry;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal.Collections;
using OutSmart.DAXon.Internal.Functional;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.XPath
{
    public abstract class AbstractStaticContext : IStaticContext
    {
        private string baseURI = null;
        private Configuration config;
        private PackageData packageData;
        private ILocation containingLocation = Loc.NONE;
        private string defaultCollationName;
        private FunctionLibraryList libraryList = new FunctionLibraryList();
        private NamespaceUri defaultFunctionNamespace = NamespaceUri.FN;
        private NamespaceUri defaultElementNamespace = NamespaceUri.NULL;
        private bool backwardsCompatible = false;
        private int xpathLanguageLevel = 31;
        private readonly Dictionary<StructuredQName, Types.ItemType> typeAliases = new Dictionary<StructuredQName, Types.ItemType>();
        private UnprefixedElementMatchingPolicy unprefixedElementPolicy = UnprefixedElementMatchingPolicy.DEFAULT_NAMESPACE;
        private IWarningHandler warningHandler;

        /// <summary>
        /// Construct a dynamic context for early evaluation of constant subexpressions
        /// </summary>
        public virtual string StaticBaseURI => baseURI == null ? "" : baseURI;
        protected virtual void SetConfiguration(Configuration config)
        {
            this.config = config;
            this.defaultCollationName = config.GetDefaultCollationName();
            warningHandler = (message, code, locator) =>
            {
                XmlProcessingIncident incident = new XmlProcessingIncident(message, code, locator).AsWarning();
                config.MakeErrorReporter().Report(incident);
            };
        }

        /// <summary>
        /// Get the system configuration
        /// </summary>
        public virtual Configuration GetConfiguration()
        {
            return config;
        }

        /// <summary>
        /// Get the system configuration
        /// </summary>
        public virtual void SetPackageData(PackageData packageData)
        {
            this.packageData = packageData;
        }

        /// <summary>
        /// Get the system configuration
        /// </summary>
        public virtual PackageData GetPackageData()
        {
            return packageData;
        }

        /// <summary>
        /// Get the system configuration
        /// </summary>
        public virtual void SetSchemaAware(bool aware)
        {
            GetPackageData().SetSchemaAware(aware);
        }

        /// <summary>
        /// Get the system configuration
        /// </summary>
        public virtual RetainedStaticContext MakeRetainedStaticContext()
        {
            return new RetainedStaticContext(this);
        }

        /// <summary>
        /// Get the system configuration
        /// </summary>
        protected void SetDefaultFunctionLibrary()
        {
            FunctionLibraryList lib = new FunctionLibraryList();
            lib.AddFunctionLibrary(XPath31FunctionSet.GetInstance());
            lib.AddFunctionLibrary(GetConfiguration().GetBuiltInExtensionLibraryList(31));
            lib.AddFunctionLibrary(new ConstructorFunctionLibrary(GetConfiguration()));
            lib.AddFunctionLibrary(config.GetIntegratedFunctionLibrary());
            config.AddExtensionBinders(lib);
            SetFunctionLibrary(lib);
        }

        /// <summary>
        /// Get the system configuration
        /// </summary>
        public void SetDefaultFunctionLibrary(int version)
        {
            FunctionLibraryList lib = new FunctionLibraryList();
            lib.AddFunctionLibrary(config.GetXPathFunctionSet(version));
            lib.AddFunctionLibrary(config.GetBuiltInExtensionLibraryList(version));
            lib.AddFunctionLibrary(new ConstructorFunctionLibrary(config));
            lib.AddFunctionLibrary(config.GetIntegratedFunctionLibrary());
            config.AddExtensionBinders(lib);
            SetFunctionLibrary(lib);
        }

        /// <summary>
        /// Get the system configuration
        /// </summary>
        protected void AddFunctionLibrary(IFunctionLibrary library)
        {
            libraryList.AddFunctionLibrary(library);
        }

        /// <summary>
        /// Construct a dynamic context for early evaluation of constant subexpressions
        /// </summary>
        public virtual IXPathContext MakeEarlyEvaluationContext()
        {
            return new EarlyEvaluationContext(GetConfiguration());
        }

        /// <summary>
        /// Construct a dynamic context for early evaluation of constant subexpressions
        /// </summary>
        public virtual ILocation GetContainingLocation()
        {
            return containingLocation;
        }

        /// <summary>
        /// Construct a dynamic context for early evaluation of constant subexpressions
        /// </summary>
        public virtual void SetContainingLocation(ILocation location)
        {
            containingLocation = location;
        }

        /// <summary>
        /// Construct a dynamic context for early evaluation of constant subexpressions
        /// </summary>
        public virtual void SetBaseURI(string baseURI)
        {
            this.baseURI = baseURI;
        }

        /// <summary>
        /// Construct a dynamic context for early evaluation of constant subexpressions
        /// </summary>
        public virtual IFunctionLibrary GetFunctionLibrary()
        {
            return libraryList;
        }

        /// <summary>
        /// Construct a dynamic context for early evaluation of constant subexpressions
        /// </summary>
        public virtual void SetFunctionLibrary(FunctionLibraryList lib)
        {
            libraryList = lib;
        }

        /// <summary>
        /// Construct a dynamic context for early evaluation of constant subexpressions
        /// </summary>
        public virtual void SetDefaultCollationName(string collationName)
        {
            defaultCollationName = collationName;
        }

        /// <summary>
        /// Construct a dynamic context for early evaluation of constant subexpressions
        /// </summary>
        public virtual string GetDefaultCollationName()
        {
            return defaultCollationName;
        }

        /// <summary>
        /// Construct a dynamic context for early evaluation of constant subexpressions
        /// </summary>
        public virtual void SetWarningHandler(Action<string, ILocation> handler)
        {
            warningHandler = (message, code, loc) => handler.Accept(message, loc);
        }

        /// <summary>
        /// Construct a dynamic context for early evaluation of constant subexpressions
        /// </summary>
        public virtual void SetWarningHandler(IWarningHandler handler)
        {
            warningHandler = handler;
        }

        /// <summary>
        /// Construct a dynamic context for early evaluation of constant subexpressions
        /// </summary>
        public virtual IWarningHandler GetWarningHandler()
        {
            return warningHandler;
        }

        /// <summary>
        /// Construct a dynamic context for early evaluation of constant subexpressions
        /// </summary>
        public virtual void IssueWarning(string s, string errorCode, ILocation locator)
        {
            GetWarningHandler().Invoke(s, errorCode, locator);
        }

        /// <summary>
        /// Construct a dynamic context for early evaluation of constant subexpressions
        /// </summary>
        public virtual string GetSystemId()
        {
            return "";
        }

        /// <summary>
        /// Construct a dynamic context for early evaluation of constant subexpressions
        /// </summary>
        public virtual NamespaceUri GetDefaultElementNamespace()
        {
            return defaultElementNamespace;
        }

        /// <summary>
        /// Construct a dynamic context for early evaluation of constant subexpressions
        /// </summary>
        public virtual void SetDefaultElementNamespace(NamespaceUri uri)
        {
            defaultElementNamespace = uri;
        }

        /// <summary>
        /// Construct a dynamic context for early evaluation of constant subexpressions
        /// </summary>
        public virtual void SetDefaultFunctionNamespace(NamespaceUri uri)
        {
            defaultFunctionNamespace = uri;
        }

        /// <summary>
        /// Construct a dynamic context for early evaluation of constant subexpressions
        /// </summary>
        public virtual NamespaceUri GetDefaultFunctionNamespace()
        {
            return defaultFunctionNamespace;
        }

        /// <summary>
        /// Construct a dynamic context for early evaluation of constant subexpressions
        /// </summary>
        public virtual void SetXPathLanguageLevel(int level)
        {
            if (level == 40)
            {
                config.CheckLicensedFeature(Configuration.LicenseFeature.PROFESSIONAL_EDITION, "XPath 4.0", -1);
            }

            xpathLanguageLevel = level;
            if (packageData.HostLanguageVersion != level)
            {
                packageData.SetHostLanguage(packageData.GetHostLanguage(), level);
            }
        }

        /// <summary>
        /// Construct a dynamic context for early evaluation of constant subexpressions
        /// </summary>
        public virtual int GetXPathVersion()
        {
            return xpathLanguageLevel;
        }

        /// <summary>
        /// Construct a dynamic context for early evaluation of constant subexpressions
        /// </summary>
        public virtual void SetBackwardsCompatibilityMode(bool option)
        {
            backwardsCompatible = option;
        }

        /// <summary>
        /// Construct a dynamic context for early evaluation of constant subexpressions
        /// </summary>
        public virtual bool IsInBackwardsCompatibleMode()
        {
            return backwardsCompatible;
        }

        /// <summary>
        /// Construct a dynamic context for early evaluation of constant subexpressions
        /// </summary>
        public virtual void SetDecimalFormatManager(DecimalFormatManager manager)
        {
            GetPackageData().SetDecimalFormatManager(manager);
        }

        /// <summary>
        /// Construct a dynamic context for early evaluation of constant subexpressions
        /// </summary>
        public virtual Types.ItemType GetRequiredContextItemType()
        {
            return AnyItemType.GetInstance();
        }

        /// <summary>
        /// Construct a dynamic context for early evaluation of constant subexpressions
        /// </summary>
        public virtual DecimalFormatManager GetDecimalFormatManager()
        {
            DecimalFormatManager manager = GetPackageData().GetDecimalFormatManager();
            if (manager == null)
            {
                manager = new DecimalFormatManager(HostLanguage.XPATH, xpathLanguageLevel);
                GetPackageData().SetDecimalFormatManager(manager);
            }

            return manager;
        }

        /// <summary>
        /// Construct a dynamic context for early evaluation of constant subexpressions
        /// </summary>
        public virtual KeyManager GetKeyManager()
        {
            return GetPackageData().GetKeyManager();
        }

        /// <summary>
        /// Construct a dynamic context for early evaluation of constant subexpressions
        /// </summary>
        public virtual void SetTypeAlias(StructuredQName name, Types.ItemType type)
        {
            typeAliases.Put(name, type);
        }

        /// <summary>
        /// Construct a dynamic context for early evaluation of constant subexpressions
        /// </summary>
        public virtual Types.ItemType ResolveTypeAlias(StructuredQName typeName)
        {
            return typeAliases.Get(typeName);
        }

        /// <summary>
        /// Construct a dynamic context for early evaluation of constant subexpressions
        /// </summary>
        public virtual void SetUnprefixedElementMatchingPolicy(UnprefixedElementMatchingPolicy policy)
        {
            this.unprefixedElementPolicy = policy;
        }

        /// <summary>
        /// Construct a dynamic context for early evaluation of constant subexpressions
        /// </summary>
        public virtual UnprefixedElementMatchingPolicy GetUnprefixedElementMatchingPolicy()
        {
            return unprefixedElementPolicy;
        }
        public virtual Expression BindVariable(StructuredQName arg0) => throw new NotImplementedException();
        public virtual bool IsImportedSchema(NamespaceUri arg0) => throw new NotImplementedException();
        public virtual HashSet<NamespaceUri> GetImportedSchemaNamespaces() => throw new NotImplementedException();
        public virtual INamespaceResolver GetNamespaceResolver() => throw new NotImplementedException();

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
        // Upstream returns getConfiguration().getOptimizerOptions(); the NIE stub broke static-parameter
        // evaluation in fn:transform (xsl:param static='yes').
        public virtual OptimizerOptions GetOptimizerOptions() => config.GetOptimizerOptions();

        /// <summary>
        /// Interface defining a callback for handling warnings
        /// </summary>
        // Phase 5: IWarningHandler interface->delegate.
        public delegate void IWarningHandler(string message, string errorCode, ILocation location);
    }
}
