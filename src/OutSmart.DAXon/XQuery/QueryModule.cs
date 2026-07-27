////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Functions.Registry;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal.Net;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.XQuery
{
    public class QueryModule : IStaticContext
    {
        private bool moduleIsMainModule;
        private readonly Configuration config;
        private StaticQueryContext userQueryContext;
        private readonly QueryModule topModule;
        private URI locationURI;
        private string baseURI;
        private NamespaceUri moduleNamespace; // null only if moduleIsMainModule is false
        private Dictionary<string, NamespaceUri> explicitPrologNamespaces;
        private IndexedStack<NamespaceBinding> activeNamespaces; // The namespace bindings declared in element constructors
        private Dictionary<StructuredQName, GlobalVariable> variables;
        private Dictionary<StructuredQName, GlobalVariable> libraryVariables;
        private Dictionary<StructuredQName, UndeclaredVariable> undeclaredVariables;
        private HashSet<NamespaceUri> importedSchemata; // The schema target namespaces imported into this module
        private Dictionary<NamespaceUri, HashSet<string>> loadedSchemata;
        private Executable executable;
        private IList<QueryModule> importers; // A list of QueryModule objects representing the modules that import this one,
        private FunctionLibraryList functionLibraryList;
        private XQueryFunctionLibrary globalFunctionLibrary; // used only on a top-level module
        private int localFunctionLibraryNr;
        private int importedFunctionLibraryNr;
        private int unboundFunctionLibraryNr;
        private HashSet<NamespaceUri> importedModuleNamespaces;
        private bool inheritNamespaces = true;
        private bool preserveNamespaces = true;
        private int constructionMode = Validation.PRESERVE;
        private NamespaceUri defaultFunctionNamespace;
        private NamespaceUri defaultElementNamespace;
        private bool fixedDefaultElementNamespace;
        private bool preserveSpace = false;
        private bool defaultEmptyLeast = true;
        private string defaultCollationName;
        private int revalidationMode = Validation.SKIP;
        private bool updating = false;
        private Types.ItemType requiredContextItemType = AnyItemType.GetInstance(); // must be the same for all modules
        private DecimalFormatManager decimalFormatManager = null; // used only in XQuery 3.0
        private ICodeInjector codeInjector;
        private PackageData packageData;
        private RetainedStaticContext moduleStaticContext = null;
        private ILocation moduleLocation;
        private OptimizerOptions optimizerOptions;
        private int languageLevel;
        private UnprefixedElementMatchingPolicy unprefixedElementMatchingPolicy = UnprefixedElementMatchingPolicy.DEFAULT_NAMESPACE;
        private HashSet<QueryModule> importedModules = new HashSet<QueryModule>();

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual int ConstructionMode
        {
            get => constructionMode; set
            {
                constructionMode = value;
            }
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual XQueryFunctionLibrary GlobalFunctionLibrary => globalFunctionLibrary;

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual HashSet<QueryModule> ImportedModules => importedModules;

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual QueryModule TopLevelModule => topModule;

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual StaticQueryContext UserQueryContext => userQueryContext;

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual NamespaceUri ModuleNamespace
        {
            get => moduleNamespace; set
            {
                moduleNamespace = value;
            }
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual URI LocationURI
        {
            get => locationURI; set
            {
                locationURI = value;
                moduleLocation = new Loc(locationURI.ToString(), 1, -1);
            }
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual string StaticBaseURI => baseURI;

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual SlotManager GlobalStackFrameMap => GetPackageData().GlobalSlotManager;

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual IEnumerable<GlobalVariable> ImportedGlobalVariables => libraryVariables.Values;

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual IEnumerable<GlobalVariable> AllGlobalVariables
        {
            get
            {
                if (IsMainModule())
                {
                    IList<GlobalVariable> allVars = new List<GlobalVariable>(libraryVariables.Values);
                    allVars.AddAll(variables.Values);
                    return allVars;
                }
                else
                {
                    return TopLevelModule.AllGlobalVariables;
                }
            }
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual IEnumerator<GlobalVariable> ModuleVariables => variables.Values.IIterator();

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual XQueryFunctionLibrary LocalFunctionLibrary => (XQueryFunctionLibrary)functionLibraryList[localFunctionLibraryNr];

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        //}
        public virtual INamespaceResolver LiveNamespaceResolver => new AnonymousINamespaceResolver(this);

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        //}
        public virtual int RevalidationMode
        {
            get => revalidationMode; set
            {
                if (value == Validation.STRICT || value == Validation.LAX || value == Validation.SKIP)
                {
                    revalidationMode = value;
                }
                else
                {
                    throw new ArgumentException("Invalid mode " + value);
                }
            }
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        //}
        public virtual NamespaceMap ActiveNamespaceBindings
        {
            get
            {
                if (activeNamespaces == null)
                {
                    return NamespaceMap.EmptyMap();
                }

                NamespaceMap result = NamespaceMap.EmptyMap();
                HashSet<string> prefixes = new HashSet<string>(10);
                for (int n = activeNamespaces.size() - 1; n >= 0; n--)
                {
                    NamespaceBinding an = activeNamespaces[n];
                    if (!prefixes.Contains(an.GetPrefix()))
                    {
                        prefixes.Add(an.GetPrefix());
                        if (!an.GetNamespaceUri().IsEmpty())
                        {
                            result = result.Put(an.GetPrefix(), an.GetNamespaceUri());
                        }
                    }
                }

                return result;
            }
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        //}
        public virtual ICodeInjector CodeInjector => codeInjector;
        public QueryModule(StaticQueryContext sqc)
        {
            config = sqc.GetConfiguration();
            moduleIsMainModule = true;
            topModule = this;
            languageLevel = sqc.LanguageVersion;
            activeNamespaces = new IndexedStack<NamespaceBinding>();
            baseURI = sqc.BaseURI;
            defaultCollationName = sqc.GetDefaultCollationName();
            try
            {
                locationURI = baseURI == null ? null : new URI(baseURI);
            }
            catch (URISyntaxException err)
            {
                throw new XPathException("Invalid location URI: " + baseURI);
            }

            executable = sqc.MakeExecutable();
            importers = null;
            Init(sqc);
            PackageData pd = new PackageData(config);
            pd.SetHostLanguage(HostLanguage.XQUERY, GetXPathVersion());
            pd.SetSchemaAware(IsSchemaAware());
            packageData = pd;
            foreach (GlobalVariable var in sqc.IterateDeclaredGlobalVariables())
            {
                DeclareVariable(var);
                pd.AddGlobalVariable(var);
                var.SetPackageData(pd);
            }

            executable.TopLevelPackage = pd;
            executable.AddPackage(pd);
            if (sqc.ModuleLocation == null)
            {
                moduleLocation = new Loc(sqc.GetSystemId(), 1, -1);
            }
            else
            {
                moduleLocation = sqc.ModuleLocation;
            }

            optimizerOptions = sqc.GetOptimizerOptions();
            unprefixedElementMatchingPolicy = sqc.GetUnprefixedElementMatchingPolicy();
        }

        public QueryModule(Configuration config, QueryModule importer)
        {
            this.config = config;
            importers = null;
            if (importer == null)
            {
                topModule = this;
            }
            else
            {
                topModule = importer.topModule;
                userQueryContext = importer.userQueryContext;
                importers = new List<QueryModule>(2);
                importers.Add(importer);
            }

            Init(userQueryContext);
            packageData = importer.GetPackageData();
            activeNamespaces = new IndexedStack<NamespaceBinding>();
            executable = null;
            optimizerOptions = importer.optimizerOptions;
        }

        private void Init(StaticQueryContext sqc)
        {

            //reset();
            userQueryContext = sqc;
            variables = new Dictionary<StructuredQName, GlobalVariable>(10);
            undeclaredVariables = new Dictionary<StructuredQName, UndeclaredVariable>(5);
            if (IsTopLevelModule())
            {
                libraryVariables = new Dictionary<StructuredQName, GlobalVariable>(10);
            }

            importedSchemata = new HashSet<NamespaceUri>(5);

            //importedSchemata.add(NamespaceConstant.JSON);
            importedModuleNamespaces = new HashSet<NamespaceUri>(5);
            moduleNamespace = null;
            activeNamespaces = new IndexedStack<NamespaceBinding>();
            explicitPrologNamespaces = new Dictionary<string, NamespaceUri>(10);
            if (sqc != null)
            {

                inheritNamespaces = sqc.IsInheritNamespaces();
                preserveNamespaces = sqc.IsPreserveNamespaces();
                preserveSpace = sqc.IsPreserveBoundarySpace();
                defaultEmptyLeast = sqc.IsEmptyLeast();
                defaultFunctionNamespace = sqc.DefaultFunctionNamespace;
                defaultElementNamespace = sqc.DefaultElementNamespace;
                defaultCollationName = sqc.GetDefaultCollationName();
                constructionMode = sqc.ConstructionMode;
                if (constructionMode == Validation.PRESERVE && !sqc.IsSchemaAware())
                {

                    // if not schema-aware, generate untyped output by default
                    constructionMode = Validation.STRIP;
                }

                requiredContextItemType = sqc.RequiredContextItemType;
                updating = sqc.IsUpdatingEnabled();
                codeInjector = sqc.CodeInjector;
                optimizerOptions = sqc.GetOptimizerOptions();
            } //initializeFunctionLibraries();
        }

        //reset();
        public static QueryModule MakeQueryModule(string baseURI, Executable executable, QueryModule importer, string query, NamespaceUri namespaceURI)
        {
            if (baseURI == null)
                throw new NullReferenceException("Base URI of XQuery module must not be null");
            Configuration config = executable.GetConfiguration();
            QueryModule module = new QueryModule(config, importer);
            importer.AddImportedModule(module);
            try
            {
                module.LocationURI = new URI(baseURI);
            }
            catch (URISyntaxException e)
            {
                throw new XPathException("Invalid location URI " + baseURI, e);
            }

            module.SetBaseURI(baseURI);
            module.SetExecutable(executable);
            module.ModuleNamespace = namespaceURI;
            executable.AddQueryLibraryModule(module);
            XQueryParser qp = (XQueryParser)config.NewExpressionParser("XQ", importer.IsUpdating(), module);
            if (importer.CodeInjector != null)
            {
                qp.CodeInjector = importer.CodeInjector;
            }
            else if (config.IsCompileWithTracing())
            {
                qp.CodeInjector = new XQueryTraceCodeInjector();
            }

            QNameParser qnp = new QNameParser(module.LiveNamespaceResolver).WithAcceptEQName(importer.GetXPathVersion() >= 30).WithUnescaper(new XQueryParser.Unescaper(config.ValidCharacterChecker));
            qp.SetQNameParser(qnp);
            qp.ParseLibraryModule(query, module);
            NamespaceUri @namespace = module.ModuleNamespace;
            if (@namespace == null)
            {
                IStaticError("Imported module must be a library module", "XQST0059");
            }

            if (!@namespace.Equals(namespaceURI))
            {
                IStaticError("Imported module's namespace does not match requested namespace", "XQST0059");
            }

            return module;
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual void InitializeFunctionLibraries()
        {
            StaticQueryContext sqc = userQueryContext;
            Configuration config = GetConfiguration();
            if (IsTopLevelModule())
            {
                globalFunctionLibrary = new XQueryFunctionLibrary(config);
            }

            functionLibraryList = new FunctionLibraryList();
            functionLibraryList.AddFunctionLibrary(GetBuiltInFunctionSet());
            functionLibraryList.AddFunctionLibrary(config.GetBuiltInExtensionLibraryList(sqc.LanguageVersion));
            functionLibraryList.AddFunctionLibrary(new ConstructorFunctionLibrary(config));
            localFunctionLibraryNr = functionLibraryList.AddFunctionLibrary(new XQueryFunctionLibrary(config));
            importedFunctionLibraryNr = functionLibraryList.AddFunctionLibrary(new ImportedFunctionLibrary(this, TopLevelModule.GlobalFunctionLibrary));
            if (sqc.ExtensionFunctionLibrary != null)
            {
                functionLibraryList.AddFunctionLibrary(sqc.ExtensionFunctionLibrary);
            }

            functionLibraryList.AddFunctionLibrary(config.GetIntegratedFunctionLibrary());
            config.AddExtensionBinders(functionLibraryList);
            unboundFunctionLibraryNr = functionLibraryList.AddFunctionLibrary(new UnboundFunctionLibrary());
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual BuiltInFunctionSet GetBuiltInFunctionSet()
        {
            if (IsUpdating())
            {
                return config.XQueryUpdateFunctionSet;
            }
            else
            {
                return config.GetXPathFunctionSet(languageLevel);
            }
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual Configuration GetConfiguration()
        {
            return config;
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual PackageData GetPackageData()
        {
            return packageData;
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual void SetPackageData(PackageData packageData)
        {
            this.packageData = packageData;
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual bool IsTopLevelModule()
        {
            return this == topModule;
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual void SetIsMainModule(bool main)
        {
            moduleIsMainModule = main;
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual bool IsMainModule()
        {
            return moduleIsMainModule;
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual bool MayImportModule(string @namespace)
        {
            if (@namespace.Equals(moduleNamespace))
            {
                return false;
            }

            if (importers == null)
            {
                return true;
            }

            foreach (QueryModule importer in importers)
            {
                if (!importer.MayImportModule(@namespace))
                {
                    return false;
                }
            }

            return true;
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual bool IsSchemaAware()
        {
            return executable.IsSchemaAware();
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual OptimizerOptions GetOptimizerOptions()
        {
            return optimizerOptions;
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual RetainedStaticContext MakeRetainedStaticContext()
        {

            // The only part of the RetainedStaticContext that can change as the query module is parsed is the
            // "activeNamespaces", that @is, namespaces declared on direct element constructors. If this is empty,
            // we can reuse the top-level static context on each request.
            if (activeNamespaces.IsEmpty())
            {
                if (moduleStaticContext == null)
                {
                    moduleStaticContext = new RetainedStaticContext(this);
                }

                return moduleStaticContext;
            }
            else
            {
                return new RetainedStaticContext(this);
            }
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual void SetInheritNamespaces(bool inherit)
        {
            inheritNamespaces = inherit;
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual bool IsInheritNamespaces()
        {
            return inheritNamespaces;
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual void SetPreserveNamespaces(bool inherit)
        {
            preserveNamespaces = inherit;
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual bool IsPreserveNamespaces()
        {
            return preserveNamespaces;
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual void SetPreserveBoundarySpace(bool preserve)
        {
            preserveSpace = preserve;
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual bool IsPreserveBoundarySpace()
        {
            return preserveSpace;
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual void SetEmptyLeast(bool least)
        {
            defaultEmptyLeast = least;
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual bool IsEmptyLeast()
        {
            return defaultEmptyLeast;
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual ImportedFunctionLibrary GetImportedFunctionLibrary()
        {
            return (ImportedFunctionLibrary)functionLibraryList[importedFunctionLibraryNr];
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual void AddImportedNamespace(NamespaceUri uri)
        {
            if (importedModuleNamespaces == null)
            {
                importedModuleNamespaces = new HashSet<NamespaceUri>(5);
            }

            importedModuleNamespaces.Add(uri);
            GetImportedFunctionLibrary().AddImportedNamespace(uri);
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual void AddImportedModule(QueryModule module)
        {
            importedModules.Add(module);
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual bool ImportsNamespace(NamespaceUri uri)
        {
            return importedModuleNamespaces != null && importedModuleNamespaces.Contains(uri);
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual Executable GetExecutable()
        {
            return executable;
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual void SetExecutable(Executable executable)
        {
            this.executable = executable; //        if (!executable.isSchemaAware()) {
            //            constructionMode = Validation.STRIP;
            //        }
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual ILocation GetContainingLocation()
        {
            return moduleLocation;
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual string GetSystemId()
        {
            return locationURI == null ? null : locationURI.ToString();
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual void SetBaseURI(string uri)
        {
            baseURI = uri;
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual void DeclareVariable(GlobalVariable var)
        {
            StructuredQName key = var.GetVariableQName();
            if (variables.Get(key) != null)
            {
                GlobalVariable oldVar = variables.Get(key);
                if (oldVar == var || oldVar.UltimateOriginalVariable == var.UltimateOriginalVariable)
                {
                }
                else
                {
                    string oldloc = " (see line " + oldVar.GetLineNumber();
                    string oldSysId = oldVar.GetSystemId();
                    if (oldSysId != null && !oldSysId.Equals(var.GetSystemId()))
                    {
                        oldloc += " in module " + oldVar.GetSystemId();
                    }

                    oldloc += ")";
                    throw new XPathException("Duplicate definition of global variable " + var.GetVariableQName().DisplayName + oldloc).WithErrorCode("XQST0049").AsStaticError().WithLocation(var);
                }
            }

            variables.Put(key, var);
            GetPackageData().AddGlobalVariable(var);
            Dictionary<StructuredQName, GlobalVariable> libVars = TopLevelModule.libraryVariables;
            GlobalVariable old = libVars.Get(key);
            if (old == null || old == var || old.UltimateOriginalVariable == var.UltimateOriginalVariable)
            {
            }
            else
            {
                throw new XPathException("Duplicate definition of global variable " + var.GetVariableQName().DisplayName + " (see line " + old.GetLineNumber() + " in module " + old.GetSystemId() + ')').WithErrorCode("XQST0049").AsStaticError().WithLocation(var);
            }

            if (!IsMainModule())
            {
                libVars.Put(key, var);
            }
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual IList<GlobalVariable> FixupGlobalVariables(SlotManager globalVariableMap)
        {
            IList<GlobalVariable> varDefinitions = new List<GlobalVariable>(20);
            IList<IEnumerable<GlobalVariable>> iters = new List<IEnumerable<GlobalVariable>>();
            iters.Add(variables.Values);
            iters.Add(libraryVariables.Values);
            foreach (IEnumerable<GlobalVariable> iter in iters)
            {
                foreach (GlobalVariable var in iter)
                {
                    if (!varDefinitions.Contains(var))
                    {
                        int slot = globalVariableMap.AllocateSlotNumber(var.GetVariableQName(), null);
                        var.Compile(GetExecutable(), slot);
                        varDefinitions.Add(var);
                    }
                }
            }

            return varDefinitions;
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual void CheckForCircularities(IList<GlobalVariable> compiledVars, XQueryFunctionLibrary globalFunctionLibrary)
        {
            IndexedStack<object> stack = null;
            foreach (GlobalVariable gv in compiledVars)
            {
                if (stack == null)
                {
                    stack = new IndexedStack<object>();
                }

                if (gv != null)
                {
                    gv.LookForCycles(stack, globalFunctionLibrary);
                }
            }
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual void TypeCheckGlobalVariables(IList<GlobalVariable> compiledVars)
        {
            ExpressionVisitor visitor = ExpressionVisitor.Make(this);
            foreach (GlobalVariable compiledVar in compiledVars)
            {
                compiledVar.TypeCheck(visitor);
            }

            if (IsMainModule())
            {
                GlobalContextRequirement gcr = executable.GlobalContextRequirement;
                if (gcr != null && gcr.DefaultValue != null)
                {
                    ContextItemStaticInfo info = GetConfiguration().MakeContextItemStaticInfo(AnyItemType.GetInstance(), true);
                    gcr.DefaultValue = gcr.DefaultValue.TypeCheck(visitor, info);
                }
            }
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual Expression BindVariable(StructuredQName qName)
        {
            GlobalVariable var = variables.Get(qName);
            if (var == null)
            {
                NamespaceUri uri = qName.GetNamespaceUri();
                if ((uri.Equals(NamespaceUri.NULL) && IsMainModule()) || uri.Equals(moduleNamespace) || ImportsNamespace(uri))
                {
                    QueryModule main = TopLevelModule;
                    var = main.libraryVariables.Get(qName);
                    if (var == null)
                    {

                        // If the namespace has been imported there's the possibility that
                        // the variable declaration hasn't yet been read, because of the limited provision
                        // for cyclic imports. In XQuery 3.0 forwards references are more generally allowed.
                        //if (getLanguageVersion() >= 30) {
                        UndeclaredVariable uvar = undeclaredVariables.Get(qName);
                        if (uvar != null)
                        {

                            // second or subsequent reference to the as-yet-undeclared variable
                            GlobalVariableReference @ref = new GlobalVariableReference(qName);
                            uvar.RegisterReference(@ref);
                            return @ref;
                        }
                        else
                        {

                            // first reference to the as-yet-undeclared variable
                            uvar = new UndeclaredVariable();
                            uvar.SetPackageData(main.GetPackageData());
                            uvar.SetVariableQName(qName);
                            GlobalVariableReference @ref = new GlobalVariableReference(qName);
                            uvar.RegisterReference(@ref);
                            undeclaredVariables.Put(qName, uvar);
                            return @ref;
                        } //                    } else {
                        //                        throw err;
                        //                    }
                    }
                    else
                    {
                        if (var.IsPrivate())
                        {
                            IStaticError("Variable $" + qName.DisplayName + " is private", "XPST0008");
                        }
                    }
                }
                else
                {

                    // If the namespace hasn't been imported then we might as well throw the error right away
                    IStaticError("Variable $" + qName.DisplayName + " has not been declared", "XPST0008");
                }
            }
            else
            {
                if (var.IsPrivate() && (var.GetSystemId() == null || !var.GetSystemId().Equals(GetSystemId())))
                {
                    string message = "Variable $" + qName.DisplayName + " is private";
                    if (var.GetSystemId() == null)
                    {
                        message += " (no base URI known)";
                    }

                    IStaticError(message, "XPST0008");
                }
            }

            GlobalVariableReference vref = new GlobalVariableReference(qName);
            var.RegisterReference(vref);
            return vref;
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual IFunctionLibrary GetFunctionLibrary()
        {
            return functionLibraryList;
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        public virtual void DeclareFunction(XQueryFunction function)
        {
            Configuration config = GetConfiguration();
            if (function.GetMinimumArity() <= 1 && function.NumberOfParameters >= 1)
            {
                StructuredQName name = function.GetFunctionName();
                ISchemaType t = config.GetSchemaType(name);
                if (t != null && t.IsAtomicType())
                {
                    string message = "Function name " + function.DisplayName + " clashes with the name of the constructor function for an atomic type";
                    string errorCode = "XQST0034";
                    IStaticError(message, errorCode);
                }
            }

            XQueryFunctionLibrary local = LocalFunctionLibrary;
            local.DeclareFunction(function);

            QueryModule main = TopLevelModule;
            main.globalFunctionLibrary.DeclareFunction(function); //}
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        //}
        private static void IStaticError(string message, string errorCode)
        {
            throw new XPathException(message, errorCode).AsStaticError();
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        //}
        public virtual void BindUnboundFunctionCalls()
        {
            UnboundFunctionLibrary lib = (UnboundFunctionLibrary)functionLibraryList[unboundFunctionLibraryNr];
            lib.BindUnboundFunctionReferences(functionLibraryList, GetConfiguration());
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        //}
        public virtual void FixupGlobalFunctions()
        {
            globalFunctionLibrary.FixupGlobalFunctions(this);
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        //}
        public virtual void OptimizeGlobalFunctions()
        {
            globalFunctionLibrary.OptimizeGlobalFunctions(this);
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        //}
        public virtual void ExplainGlobalFunctions(ExpressionPresenter @out)
        {
            globalFunctionLibrary.ExplainGlobalFunctions(@out);
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        //}
        public virtual UserFunction GetUserDefinedFunction(NamespaceUri uri, string localName, int arity)
        {
            return globalFunctionLibrary.GetUserDefinedFunction(uri, localName, arity);
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        //}
        public virtual void BindUnboundVariables()
        {
            foreach (UndeclaredVariable uv in undeclaredVariables.Values)
            {
                StructuredQName qName = uv.GetVariableQName();
                GlobalVariable var = variables.Get(qName);
                if (var == null)
                {
                    NamespaceUri uri = qName.GetNamespaceUri();
                    if (ImportsNamespace(uri))
                    {
                        QueryModule main = TopLevelModule;
                        var = main.libraryVariables.Get(qName);
                    }
                }

                if (var == null)
                {
                    IStaticError("Unresolved reference to variable $" + uv.GetVariableQName().DisplayName, "XPST0008");
                }
                else if (var.IsPrivate() && !var.GetSystemId().Equals(GetSystemId()))
                {
                    IStaticError("Cannot reference a private variable in a different module", "XPST0008");
                }
                else
                {
                    uv.TransferReferences(var);
                }
            }
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        //}
        public virtual void AddImportedSchema(NamespaceUri targetNamespace, string baseURI, IList<string> locationURIs)
        {
            if (importedSchemata == null)
            {
                importedSchemata = new HashSet<NamespaceUri>(5);
            }

            importedSchemata.Add(targetNamespace);
            Dictionary<NamespaceUri, HashSet<string>> loadedSchemata = TopLevelModule.loadedSchemata;
            if (loadedSchemata == null)
            {
                loadedSchemata = new Dictionary<NamespaceUri, HashSet<string>>(5);
                TopLevelModule.loadedSchemata = loadedSchemata;
            }

            HashSet<string> entries = loadedSchemata.Get(targetNamespace);
            if (entries == null)
            {
                entries = new HashSet<string>(locationURIs.Count);
                loadedSchemata.Put(targetNamespace, entries);
            }

            foreach (string relative in locationURIs)
            {
                try
                {
                    URI abs = ResolveURI.MakeAbsolute(relative, baseURI);
                    entries.Add(abs.ToString());
                }
                catch (URISyntaxException e)
                {
                }
            }
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        //}
        public virtual bool IsImportedSchema(NamespaceUri @namespace)
        {
            return importedSchemata != null && importedSchemata.Contains(@namespace);
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        //}
        public virtual HashSet<NamespaceUri> GetImportedSchemaNamespaces()
        {
            if (importedSchemata == null)
            {
                return new HashSet<NamespaceUri>();
            }
            else
            {
                return importedSchemata;
            }
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        //}
        public virtual void ReportStaticError(XPathException err)
        {
            if (!err.HasBeenReported())
            {
                ReportStaticError(new XmlProcessingException(err));
                err.SetHasBeenReported(true);
            }
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        //}
        public virtual void ReportStaticError(IXmlProcessingError err)
        {
            userQueryContext.ErrorReporter.Report(err);
            if (err.TerminationMessage != null)
            {
                throw new XmlProcessingAbort(err.TerminationMessage);
            }
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        //}
        public virtual IXPathContext MakeEarlyEvaluationContext()
        {
            return new EarlyEvaluationContext(GetConfiguration());
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        //}
        public virtual string GetDefaultCollationName()
        {
            if (defaultCollationName == null)
            {
                defaultCollationName = NamespaceConstant.CODEPOINT_COLLATION_URI;
            }

            return defaultCollationName;
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        //}
        public virtual void SetDefaultCollationName(string collation)
        {
            defaultCollationName = collation;
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        //}
        public virtual void DeclarePrologNamespace(string prefix, NamespaceUri uri)
        {
            if (prefix == null)
            {
                throw new NullReferenceException("Null prefix supplied to declarePrologNamespace()");
            }

            if (uri == null)
            {
                throw new NullReferenceException("Null namespace URI supplied to declarePrologNamespace()");
            }

            if (prefix.Equals("xml") != uri.Equals(NamespaceUri.XML))
            {
                IStaticError("Invalid declaration of the XML namespace", "XQST0070");
            }

            if (explicitPrologNamespaces.Get(prefix) != null)
            {
                IStaticError("Duplicate declaration of namespace prefix \"" + prefix + '"', "XQST0033");
            }
            else
            {
                explicitPrologNamespaces.Put(prefix, uri);
            }
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        //}
        public virtual void DeclareActiveNamespace(string prefix, NamespaceUri uri)
        {
            if (prefix == null)
            {
                throw new NullReferenceException("Null prefix supplied to declareActiveNamespace()");
            }

            if (uri == null)
            {
                throw new NullReferenceException("Null namespace URI supplied to declareActiveNamespace()");
            }

            NamespaceBinding entry = new NamespaceBinding(prefix, uri);
            activeNamespaces.IPush(entry);
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        //}
        public virtual void UndeclareNamespace()
        {
            activeNamespaces.Pop();
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        //}
        public virtual UnprefixedElementMatchingPolicy GetUnprefixedElementMatchingPolicy()
        {
            return unprefixedElementMatchingPolicy;
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        //}
        public virtual void SetUnprefixedElementMatchingPolicy(UnprefixedElementMatchingPolicy unprefixedElementMatchingPolicy)
        {
            this.unprefixedElementMatchingPolicy = unprefixedElementMatchingPolicy;
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        //}
        public virtual NamespaceUri CheckURIForPrefix(string prefix)
        {

            // Search the active namespaces first, then the passive ones.
            if (activeNamespaces != null)
            {
                for (int i = activeNamespaces.size() - 1; i >= 0; i--)
                {
                    if (activeNamespaces[i].GetPrefix().Equals(prefix))
                    {
                        NamespaceUri ns = activeNamespaces[i].GetNamespaceUri();
                        if (ns.IsEmpty() && !prefix.Equals(""))
                        {

                            // the namespace is undeclared
                            return null;
                        }

                        return ns;
                    }
                }
            }

            if ((prefix.Length == 0))
            {
                return defaultElementNamespace;
            }

            NamespaceUri uri = explicitPrologNamespaces.Get(prefix);
            if (uri != null)
            {

                // A zero-length URI means the prefix was undeclared in the prolog, and we mustn't look elsewhere
                return uri.IsEmpty() ? null : uri;
            }

            if (userQueryContext != null)
            {
                uri = userQueryContext.GetNamespaceForPrefix(prefix);
                if (uri != null)
                {
                    return uri;
                }
            }

            return null;
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        //}
        public virtual NamespaceUri GetDefaultElementNamespace()
        {
            return CheckURIForPrefix("");
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        //}
        public virtual void SetDefaultElementNamespace(NamespaceUri uri, bool isFixedDefault)
        {
            defaultElementNamespace = uri;
            fixedDefaultElementNamespace = isFixedDefault;
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        //}
        public virtual NamespaceUri GetDefaultFunctionNamespace()
        {
            return defaultFunctionNamespace;
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        //}
        public virtual void SetDefaultFunctionNamespace(NamespaceUri uri)
        {
            defaultFunctionNamespace = uri;
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        //}
        public virtual INamespaceResolver GetNamespaceResolver()
        {
            NamespaceMap result = NamespaceMap.EmptyMap();
            Dictionary<string, NamespaceUri> userDeclaredNamespaces = userQueryContext.UserDeclaredNamespaces;
            foreach (KeyValuePair<string, NamespaceUri> e in userDeclaredNamespaces.EntrySet())
            {
                result = result.Put(e.Key, e.Value);
            }

            foreach (KeyValuePair<string, NamespaceUri> e in explicitPrologNamespaces.EntrySet())
            {
                result = result.Put(e.Key, e.Value);
            }

            if (!defaultElementNamespace.IsEmpty())
            {
                result = result.Put("", defaultElementNamespace);
            }

            if (activeNamespaces == null)
            {
                return result;
            }

            HashSet<string> prefixes = new HashSet<string>(10);
            for (int n = activeNamespaces.size() - 1; n >= 0; n--)
            {
                NamespaceBinding an = activeNamespaces[n];
                if (!prefixes.Contains(an.GetPrefix()))
                {
                    prefixes.Add(an.GetPrefix());
                    if (an.GetNamespaceUri().IsEmpty())
                    {
                        result = result.Remove(an.GetPrefix());
                    }
                    else
                    {
                        result = result.Put(an.GetPrefix(), an.GetNamespaceUri());
                    }
                }
            }

            return result;
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        //}
        public virtual Types.ItemType GetRequiredContextItemType()
        {
            return requiredContextItemType;
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        //}
        public virtual DecimalFormatManager GetDecimalFormatManager()
        {
            if (decimalFormatManager == null)
            {
                decimalFormatManager = new DecimalFormatManager(HostLanguage.XQUERY, GetXPathVersion());
            }

            return decimalFormatManager;
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        //}
        public virtual void IssueWarning(string s, string errorCode, ILocation locator)
        {
            XmlProcessingIncident err = new XmlProcessingIncident(s, errorCode).AsWarning();
            err.SetLocation(locator);
            err.SetHostLanguage(HostLanguage.XQUERY);
            userQueryContext.ErrorReporter.Report(err);
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        //}
        public virtual bool IsInBackwardsCompatibleMode()
        {
            return false;
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        //}
        public virtual bool IsUpdating()
        {
            return updating;
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        //}
        public virtual void SetXPathVersion(int languageLevel)
        {
            this.languageLevel = languageLevel;
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        //}
        public virtual int GetXPathVersion()
        {
            return languageLevel;
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        //}
        public virtual KeyManager GetKeyManager()
        {
            return packageData.GetKeyManager();
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        //}
        public virtual Types.ItemType ResolveTypeAlias(StructuredQName typeName)
        {
            return GetPackageData().ObtainTypeAliasManager().GetItemType(typeName);
        }

        //reset();
        /// <summary>
        /// Reset function libraries
        /// </summary>
        //}
        /// <summary>
        /// Get the number of references to a not-yet-declared global variable
        /// </summary>
        public virtual int GetForwardReferenceCount(StructuredQName variableName)
        {
            UndeclaredVariable var = undeclaredVariables.Get(variableName);
            if (var == null)
            {
                return 0;
            }
            else
            {
                return var.CountReferences();
            }
        }

        private sealed class AnonymousINamespaceResolver : INamespaceResolver
        {

            private readonly QueryModule parent;
            public AnonymousINamespaceResolver(QueryModule parent)
            {
                this.parent = parent;
            }
            public NamespaceUri GetURIForPrefix(string prefix, bool useDefault)
            {
                return parent.CheckURIForPrefix(prefix);
            }

            public IEnumerator<string> IteratePrefixes()
            {
                return parent.GetNamespaceResolver().IteratePrefixes();
            }
        }
    }
}
