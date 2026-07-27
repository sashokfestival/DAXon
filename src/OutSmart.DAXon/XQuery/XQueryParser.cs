////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Functions.Registry;
using OutSmart.DAXon.Values.Arrays;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Serialization;
using OutSmart.DAXon.Serialization.CharCodes;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Xslt;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Internal.Net;
using OutSmart.DAXon.Internal.Functional;
using OutSmart.DAXon.Internal.Regex;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions.Flwor;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
using OutSmart.DAXon.Internal.Jaxp.Transform;
using OutSmart.DAXon.Internal.Jaxp.Transform.Stream;
using OutSmart.DAXon.Internal.Streams;
using System.IO;
namespace OutSmart.DAXon.XQuery
{
    public class XQueryParser : XPathParser
    {

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        private static readonly OutSmart.DAXon.Internal.Regex.Pattern encNamePattern = OutSmart.DAXon.Internal.Regex.Pattern.Compile("^[A-Za-z]([A-Za-z0-9._\\x2D])*$");

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        public static readonly StructuredQName SAXON_MEMO_FUNCTION = new StructuredQName("saxon", NamespaceUri.SAXON, "memo-function");
        private bool memoFunction = false;
        private bool streaming = false;
        private int errorCount = 0;
        private XPathException firstError = null;
        protected Executable executable;
        private bool foundCopyNamespaces = false;
        private bool foundBoundarySpaceDeclaration = false;
        private bool foundOrderingDeclaration = false;
        private bool foundEmptyOrderingDeclaration = false;
        private bool foundDefaultCollation = false;
        private bool foundConstructionDeclaration = false;
        private bool foundDefaultFunctionNamespace = false;
        private bool foundDefaultElementNamespace = false;
        private bool foundBaseURIDeclaration = false;
        private bool foundContextItemDeclaration = false;
        private bool foundDefaultDecimalFormat = false;
        private bool preambleProcessed = false;
        public readonly HashSet<NamespaceUri> importedModules = new HashSet<NamespaceUri>(5);
        readonly IList<NamespaceUri> namespacesToBeSealed = new List<NamespaceUri>(10);
        readonly IList<Import> schemaImports = new List<Import>(5);
        readonly IList<Import> moduleImports = new List<Import>(5);
        private readonly HashSet<StructuredQName> outputPropertiesSeen = new HashSet<StructuredQName>(4);
        private Properties parameterDocProperties;
        public XQueryParser(IStaticContext env) : base(env)
        {
            this.languageVersion = 31; // Until proved otherwise
            SetLanguage(ParsedLanguage.XQUERY, languageVersion);
        }

        private XQueryParser NewParser()
        {
            XQueryParser qp = new XQueryParser(env);
            qp.SetLanguage(language, languageVersion);
            qp.SetParserExtension(parserExtension);
            return qp;
        }

        public virtual XQueryExpression MakeXQueryExpression(string query, QueryModule mainModule, Configuration config)
        {
            try
            {
                SetLanguage(ParsedLanguage.XQUERY, languageVersion);
                if (config.XMLVersion == Configuration.XML10)
                {
                    query = NormalizeLineEndings10(query);
                }
                else
                {
                    query = NormalizeLineEndings11(query);
                }

                Executable exec = mainModule.GetExecutable();
                if (exec == null)
                {
                    exec = new Executable(config);
                    exec.SetHostLanguage(HostLanguage.XQUERY);
                    exec.TopLevelPackage = mainModule.GetPackageData();
                    SetExecutable(exec); //mainModule.setExecutable(exec);
                }

                GlobalContextRequirement requirement = exec.GlobalContextRequirement;
                if (requirement != null)
                {
                    requirement.AddRequiredItemType(mainModule.GetRequiredContextItemType());
                }
                else if (mainModule.GetRequiredContextItemType() != null && mainModule.GetRequiredContextItemType() != AnyItemType.GetInstance())
                {
                    GlobalContextRequirement req = new GlobalContextRequirement();
                    req.SetExternal(true);
                    req.AddRequiredItemType(mainModule.GetRequiredContextItemType());
                    exec.GlobalContextRequirement = req;
                }


                Properties outputProps = new Properties(config.DefaultSerializationProperties);
                if (outputProps.GetProperty(OutputKeys.METHOD) == null)
                {
                    outputProps.SetProperty(OutputKeys.METHOD, "xml");
                }

                parameterDocProperties = new Properties(outputProps);
                exec.SetDefaultOutputProperties(new Properties(parameterDocProperties));

                FunctionLibraryList libList = new FunctionLibraryList();
                libList.AddFunctionLibrary(new ExecutableFunctionLibrary(config));
                exec.FunctionLibrary = libList;

                // this will be changed later
                SetExecutable(exec);
                CodeInjector = mainModule.CodeInjector;
                Expression exp = ParseQuery(query, mainModule);
                if (streaming)
                {
                    env.GetConfiguration().CheckLicensedFeature(Configuration.LicenseFeature.ENTERPRISE_XQUERY, "streaming", -1);
                }

                exec.FixupQueryModules(mainModule);

                // Make the XQueryExpression object
                XQueryExpression queryExp = config.MakeXQueryExpression(exp, mainModule, streaming);
                CreateRunTimeFunctionLibrary(mainModule, config, exec);
                return queryExp;
            }
            catch (XPathException e)
            {
                if (!e.HasBeenReported())
                {
                    ReportError(e);
                }

                throw e;
            }
        }

        public static void CreateRunTimeFunctionLibrary(QueryModule mainModule, Configuration config, Executable exec)
        {

            // Make the function library that's available at run-time (e.g. for saxon:evaluate() and function-lookup()).
            // This includes all user-defined functions regardless of which module they are in
            IFunctionLibrary userlib = exec.FunctionLibrary;
            FunctionLibraryList lib = new FunctionLibraryList();
            lib.AddFunctionLibrary(mainModule.GetBuiltInFunctionSet());
            lib.AddFunctionLibrary(config.GetBuiltInExtensionLibraryList(31));
            lib.AddFunctionLibrary(new ConstructorFunctionLibrary(config));
            lib.AddFunctionLibrary(config.GetIntegratedFunctionLibrary());
            lib.AddFunctionLibrary(mainModule.GlobalFunctionLibrary);
            config.AddExtensionBinders(lib);
            lib.AddFunctionLibrary(userlib);
            exec.FunctionLibrary = lib;
        }

        private static string NormalizeLineEndings11(string @in)
        {
            if (@in.IndexOf((char)0xd) < 0 && @in.IndexOf((char)0x85) < 0 && @in.IndexOf((char)0x2028) < 0)
            {
                return @in;
            }

            StringBuilder sb = new StringBuilder(@in.Length);
            for (int i = 0; i < @in.Length; i++)
            {
                char ch = @in[i];
                switch (ch)
                {
                    case (char)0x85:
                    case (char)0x2028:
                        sb.Append((char)0xa);
                        break;
                    case (char)0xd:
                        if (i < @in.Length - 1 && (@in[i + 1] == (char)0xa || @in[i + 1] == (char)0x85))
                        {
                            sb.Append((char)0xa);
                            i++;
                        }
                        else
                        {
                            sb.Append((char)0xa);
                        }

                        break;
                    default:
                        sb.Append(ch);
                        break;
                }
            }

            return sb.ToString();
        }

        private static string NormalizeLineEndings10(string @in)
        {
            if (@in.IndexOf((char)0xd) < 0)
            {
                return @in;
            }

            StringBuilder sb = new StringBuilder(@in.Length);
            for (int i = 0; i < @in.Length; i++)
            {
                char ch = @in[i];
                if (ch == 0xd)
                {
                    if (i < @in.Length - 1 && @in[i + 1] == (char)0xa)
                    {
                        sb.Append((char)0xa);
                        i++;
                    }
                    else
                    {
                        sb.Append((char)0xa);
                    }
                }
                else
                {
                    sb.Append(ch);
                }
            }

            return sb.ToString();
        }

        public virtual Executable GetExecutable()
        {
            return executable;
        }

        public virtual void SetExecutable(Executable exec)
        {
            executable = exec;
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        protected override void CustomizeTokenizer(Tokenizer t)
        {
            t.isXQuery = true;
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        public virtual void SetStreaming(bool option)
        {
            streaming = option;
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        public virtual bool IsStreaming()
        {
            return streaming;
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        private Expression ParseQuery(string queryString, QueryModule env)
        {
            this.env = env ?? throw new NullReferenceException();
            charChecker = env.GetConfiguration().ValidCharacterChecker;

            //        if (defaultContainer == null) {
            //        }
            language = ParsedLanguage.XQUERY;
            t = new Tokenizer();
            t.languageLevel = languageVersion = env.GetXPathVersion();
            t.isXQuery = true;
            try
            {
                t.Tokenize(queryString ?? throw new NullReferenceException(), 0, -1);
            }
            catch (XPathException err)
            {
                Grumble(err.GetMessage());
            }

            ParseVersionDeclaration();
            allowXPath40Syntax = t.allowSaxonExtensions = allowXPath40Syntax || env.GetConfiguration().GetBooleanProperty(Feature<bool>.ALLOW_SYNTAX_EXTENSIONS) || languageVersion >= 40;
            env.SetXPathVersion(languageVersion);
            env.InitializeFunctionLibraries();
            QNameParser qp = new QNameParser(env.LiveNamespaceResolver).WithAcceptEQName(true).WithUnescaper(new Unescaper(env.GetConfiguration().ValidCharacterChecker));
            SetQNameParser(qp);
            ParseProlog();
            ProcessPreamble();
            Expression exp = ParseExpression();
            exp = MakeTracer(exp, null);

            // Diagnostic code - show the expression before any optimizations
            //        exp.explain(ep);
            //        ep.close();
            // End of diagnostic code
            if (t.currentToken != Token.EOF)
            {
                Grumble("Unexpected token " + CurrentTokenDisplay() + ": no further input expected");
            }

            SetLocation(exp);
            ExpressionTool.SetDeepRetainedStaticContext(exp, env.MakeRetainedStaticContext());
            if (errorCount == 0)
            {
                return exp;
            }
            else
            {
                XPathException err = new XPathException("One or more static errors were reported during query analysis");
                err.SetHasBeenReported(true);
                err.ErrorCodeQName = firstError.ErrorCodeQName; // largely for the XQTS test driver
                throw err;
            }
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        public void ParseLibraryModule(string queryString, QueryModule env)
        {
            this.env = env;
            Configuration config = env.GetConfiguration();
            charChecker = config.ValidCharacterChecker;
            if (config.XMLVersion == Configuration.XML10)
            {
                queryString = NormalizeLineEndings10(queryString);
            }
            else
            {
                queryString = NormalizeLineEndings11(queryString);
            }

            Executable exec = env.GetExecutable();
            if (exec == null)
            {
                throw new InvalidOperationException("Query library module has no associated Executable");
            }

            executable = exec;

            t = new Tokenizer();
            t.languageLevel = languageVersion;
            t.isXQuery = true;
            QNameParser qp = new QNameParser(env.LiveNamespaceResolver).WithAcceptEQName(true).WithUnescaper(new Unescaper(config.ValidCharacterChecker));
            SetQNameParser(qp);
            try
            {
                t.Tokenize(queryString, 0, -1);
            }
            catch (XPathException err)
            {
                Grumble(err.GetMessage());
            }

            ParseVersionDeclaration();
            allowXPath40Syntax = t.allowSaxonExtensions = allowXPath40Syntax || env.GetConfiguration().GetBooleanProperty(Feature<bool>.ALLOW_SYNTAX_EXTENSIONS);
            env.SetXPathVersion(languageVersion);
            env.InitializeFunctionLibraries();
            if (t.currentToken != Token.MODULE_NAMESPACE)
            {
                if (t.currentToken == Token.EOF)
                {
                    Grumble("The file imported for module " + env.ModuleNamespace + (queryString.Trim().Length == 0 ? " is empty" : " has no significant content"));
                }
                else
                {
                    Grumble("The file imported for module " + env.ModuleNamespace + " is not a valid XQuery library module. " + "The content starts: " + Err.Truncate30(StringView.Of(queryString.Substring(t.currentTokenStartOffset))));
                }
            }

            ParseModuleDeclaration();
            ParseProlog();
            ProcessPreamble();
            if (t.currentToken != Token.EOF)
            {
                Grumble("Unrecognized content found after the variable and function declarations in a library module");
            }

            if (errorCount != 0)
            {
                XPathException err = new XPathException("Static errors were reported in the imported library module");
                err.ErrorCodeQName = firstError.ErrorCodeQName;
                throw err;
            }
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        private void ReportError(XPathException exception)
        {
            errorCount++;
            if (firstError == null)
            {
                firstError = exception;
            }

            ((QueryModule)env).ReportStaticError(exception);
            throw exception;
        }
        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        private void ParseVersionDeclaration()
        {
            if (t.currentToken == Token.XQUERY_VERSION)
            {
                NextToken();
                Expect(Token.STRING_LITERAL);
                string queryVersion = Unescape(t.currentTokenValue).ToString();
                string[] allowedVersions = new string[]
                {
                    "1.0",
                    "3.0",
                    "3.1",
                    "4.0"
                };
                if (Array.BinarySearch(allowedVersions, queryVersion) < 0)
                {
                    Grumble("Invalid XQuery version " + queryVersion, "XQST0031");
                }

                if (queryVersion.Equals("4.0"))
                {
                    languageVersion = 40;
                    allowXPath40Syntax = true;
                    t.languageLevel = 40;
                    env.GetPackageData().SetHostLanguage(HostLanguage.XQUERY, 40);
                }

                NextToken();
                if ("encoding".Equals(t.currentTokenValue))
                {
                    NextToken();
                    Expect(Token.STRING_LITERAL);
                    if (!encNamePattern.Matcher(Unescape(t.currentTokenValue)).Matches())
                    {
                        Grumble("Encoding name contains invalid characters", "XQST0087");
                    }


                    // we ignore the encoding now: it was handled earlier, while decoding the byte stream
                    NextToken();
                }

                Expect(Token.SEMICOLON);
                NextToken();
            }
            else
            {
                if (t.currentToken == Token.XQUERY_ENCODING)
                {
                    NextToken();
                    Expect(Token.STRING_LITERAL);
                    if (!encNamePattern.Matcher(t.currentTokenValue).Matches())
                    {
                        Grumble("Encoding name contains invalid characters", "XQST0087");
                    }


                    // we ignore the encoding now: it was handled earlier, while decoding the byte stream
                    NextToken();
                    Expect(Token.SEMICOLON);
                    NextToken();
                }
            }
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        private void ParseModuleDeclaration()
        {
            Expect(Token.MODULE_NAMESPACE);
            NextToken();
            Expect(Token.NAME);
            string prefix = t.currentTokenValue;
            NextToken();
            Expect(Token.EQUALS);
            NextToken();
            Expect(Token.STRING_LITERAL);
            NamespaceUri uri = NamespaceUri.Of(UriLiteral(t.currentTokenValue));
            CheckProhibitedPrefixes(prefix, uri);
            if (uri.IsEmpty())
            {
                Grumble("Module namespace cannot be \"\"", "XQST0088");
                uri = NamespaceUri.Of("http://saxon.fallback.namespace/"); // for error recovery
            }

            NextToken();
            Expect(Token.SEMICOLON);
            NextToken();
            try
            {
                ((QueryModule)env).ModuleNamespace = uri;
                ((QueryModule)env).DeclarePrologNamespace(prefix, uri);
                executable.AddQueryLibraryModule((QueryModule)env);
            }
            catch (XPathException err)
            {
                err.SetLocator(MakeLocation());
                ReportError(err);
            }
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        private void ParseProlog()
        {

            //boolean allowSetters = true;
            bool allowModuleDecl = true;
            bool allowDeclarations = true;
            while (true)
            {
                try
                {
                    if (t.currentToken == Token.MODULE_NAMESPACE)
                    {
                        NamespaceUri uri = ((QueryModule)env).ModuleNamespace;
                        if (uri == null)
                        {
                            Grumble("Module declaration must not be used in a main module");
                        }
                        else
                        {
                            Grumble("Module declaration appears more than once");
                        }

                        if (!allowModuleDecl)
                        {
                            Grumble("Module declaration must precede other declarations in the query prolog");
                        }
                    }

                    allowModuleDecl = false;
                    switch (t.currentToken)
                    {
                        case Token.DECLARE_NAMESPACE:
                            if (!allowDeclarations)
                            {
                                Grumble("Namespace declarations cannot follow variables, functions, or options");
                            }


                            //allowSetters = false;
                            ParseNamespaceDeclaration();
                            break;
                        case Token.DECLARE_ANNOTATED:

                            // we have read "declare %"
                            ProcessPreamble();
                            if (allowDeclarations)
                            {
                                SealNamespaces(namespacesToBeSealed, env.GetConfiguration());
                                allowDeclarations = false;
                            }

                            NextToken();
                            Expect(Token.PERCENT);
                            AnnotationList annotationList = ParseAnnotationsList();
                            if (IsKeyword("function"))
                            {
                                annotationList.Check(env.GetConfiguration(), "DF");
                                ParseFunctionDeclaration(annotationList);
                            }
                            else if (IsKeyword("variable"))
                            {
                                annotationList.Check(env.GetConfiguration(), "DV");
                                ParseVariableDeclaration(annotationList);
                            }
                            else if (IsKeyword("item-type"))
                            {
                                annotationList.Check(env.GetConfiguration(), "DI");
                                ParseItemTypeDeclaration(annotationList);
                            }
                            else
                            {
                                Grumble("Annotations can appear only in 'declare variable' and 'declare function'");
                            }

                            break;
                        case Token.DECLARE_FIXED:
                            CheckLanguageVersion40();
                            NextToken();
                            if (!IsKeyword("default"))
                            {
                                Grumble("expected 'default' after 'declare fixed");
                            }

                            NextToken();
                            Expect(Token.NAME);
                            switch (t.currentTokenValue)
                            {
                                case "element":
                                    if (!allowDeclarations)
                                    {
                                        Grumble("Namespace declarations cannot follow variables, functions, or options");
                                    }

                                    ParseDefaultElementNamespace(true);
                                    break;
                                case "function":
                                    if (!allowDeclarations)
                                    {
                                        Grumble("Namespace declarations cannot follow variables, functions, or options");
                                    }

                                    ParseDefaultFunctionNamespace();
                                    break;
                                default:
                                    Grumble("After 'declare fixed default', expected 'element' or 'function'");
                                    break;
                            }

                            break;
                        case Token.DECLARE_DEFAULT:
                            NextToken();
                            Expect(Token.NAME);
                            switch (t.currentTokenValue)
                            {
                                case "element":
                                    if (!allowDeclarations)
                                    {
                                        Grumble("Namespace declarations cannot follow variables, functions, or options");
                                    }


                                    //allowSetters = false;
                                    ParseDefaultElementNamespace(false);
                                    break;
                                case "function":
                                    if (!allowDeclarations)
                                    {
                                        Grumble("Namespace declarations cannot follow variables, functions, or options");
                                    }


                                    //allowSetters = false;
                                    ParseDefaultFunctionNamespace();
                                    break;
                                case "collation":
                                    if (!allowDeclarations)
                                    {
                                        Grumble("Collation declarations must appear earlier in the prolog");
                                    }

                                    ParseDefaultCollation();
                                    break;
                                case "order":
                                    if (!allowDeclarations)
                                    {
                                        Grumble("Order declarations must appear earlier in the prolog");
                                    }

                                    ParseDefaultOrder();
                                    break;
                                case "decimal-format":
                                    NextToken();
                                    ParseDefaultDecimalFormat();
                                    break;
                                default:
                                    Grumble("After 'declare default', expected 'element', 'function', or 'collation'");
                                    break;
                            }

                            break;
                        case Token.DECLARE_BOUNDARY_SPACE:
                            if (!allowDeclarations)
                            {
                                Grumble("'declare boundary-space' must appear earlier in the query prolog");
                            }

                            ParseBoundarySpaceDeclaration();
                            break;
                        case Token.DECLARE_ORDERING:
                            if (!allowDeclarations)
                            {
                                Grumble("'declare ordering' must appear earlier in the query prolog");
                            }

                            ParseOrderingDeclaration();
                            break;
                        case Token.DECLARE_COPY_NAMESPACES:
                            if (!allowDeclarations)
                            {
                                Grumble("'declare copy-namespaces' must appear earlier in the query prolog");
                            }

                            ParseCopyNamespacesDeclaration();
                            break;
                        case Token.DECLARE_BASEURI:
                            if (!allowDeclarations)
                            {
                                Grumble("'declare @base-uri' must appear earlier in the query prolog");
                            }

                            ParseBaseURIDeclaration();
                            break;
                        case Token.DECLARE_DECIMAL_FORMAT:
                            if (!allowDeclarations)
                            {
                                Grumble("'declare decimal-format' must appear earlier in the query prolog");
                            }

                            ParseDecimalFormatDeclaration();
                            break;
                        case Token.IMPORT_SCHEMA:

                            //allowSetters = false;
                            if (!allowDeclarations)
                            {
                                Grumble("Import schema must appear earlier in the prolog");
                            }

                            ParseSchemaImport();
                            break;
                        case Token.IMPORT_MODULE:

                            //allowSetters = false;
                            if (!allowDeclarations)
                            {
                                Grumble("Import module must appear earlier in the prolog");
                            }

                            ParseModuleImport();
                            break;
                        case Token.DECLARE_VARIABLE:

                            //allowSetters = false;
                            if (allowDeclarations)
                            {
                                SealNamespaces(namespacesToBeSealed, env.GetConfiguration());
                                allowDeclarations = false;
                            }

                            ProcessPreamble();
                            ParseVariableDeclaration(AnnotationList.EMPTY);
                            break;
                        case Token.DECLARE_CONTEXT:

                            //allowSetters = false;
                            if (allowDeclarations)
                            {
                                SealNamespaces(namespacesToBeSealed, env.GetConfiguration());
                                allowDeclarations = false;
                            }

                            ProcessPreamble();
                            ParseContextItemDeclaration();
                            break;
                        case Token.DECLARE_FUNCTION:
                            if (allowDeclarations)
                            {
                                SealNamespaces(namespacesToBeSealed, env.GetConfiguration());
                                allowDeclarations = false;
                            }

                            ProcessPreamble();
                            ParseFunctionDeclaration(AnnotationList.EMPTY);
                            break;
                        case Token.DECLARE_UPDATING:
                            NextToken();
                            if (!IsKeyword("function"))
                            {
                                Grumble("expected 'function' after 'declare updating");
                            }

                            if (allowDeclarations)
                            {
                                SealNamespaces(namespacesToBeSealed, env.GetConfiguration());
                                allowDeclarations = false;
                            }

                            ProcessPreamble();
                            parserExtension.ParseUpdatingFunctionDeclaration(this);
                            break;
                        case Token.DECLARE_OPTION:
                            if (allowDeclarations)
                            {
                                SealNamespaces(namespacesToBeSealed, env.GetConfiguration());
                                allowDeclarations = false;
                            }

                            ParseOptionDeclaration();
                            break;
                        case Token.DECLARE_ITEM_TYPE:
                            CheckSyntaxExtensions("declare item-type");
                            if (allowDeclarations)
                            {
                                SealNamespaces(namespacesToBeSealed, env.GetConfiguration());
                                allowDeclarations = false;
                            }

                            ParseItemTypeDeclaration(AnnotationList.EMPTY);
                            break;
                        case Token.DECLARE_CONSTRUCTION:
                            if (!allowDeclarations)
                            {
                                Grumble("'declare construction' must appear earlier in the query prolog");
                            }

                            ParseConstructionDeclaration();
                            break;
                        case Token.DECLARE_REVALIDATION:
                            if (!allowDeclarations)
                            {
                                Grumble("'declare revalidation' must appear earlier in the query prolog");
                            }

                            parserExtension.ParseRevalidationDeclaration(this);
                            break;
                        case Token.EOF:
                            NamespaceUri uri = ((QueryModule)env).ModuleNamespace;
                            if (uri == null)
                            {
                                Grumble("The main module must contain a query expression after any declarations in the prolog");
                            }
                            else
                            {
                                return;
                            }

                            break;
                        default:
                            return;
                    }

                    Expect(Token.SEMICOLON);
                    NextToken();
                }
                catch (XPathException err)
                {
                    if (err.GetLocator() == null)
                    {
                        err.SetLocator(MakeLocation());
                    }

                    if (!err.HasBeenReported())
                    {
                        errorCount++;
                        if (firstError == null)
                        {
                            firstError = err;
                        }

                        ReportError(err);
                    }


                    // we've reported an error, attempt to recover by skipping to the
                    // next semicolon
                    while (t.currentToken != Token.SEMICOLON)
                    {
                        NextToken();
                        if (t.currentToken == Token.EOF)
                        {
                            return;
                        }
                        else if (t.currentToken == Token.RCURLY)
                        {
                            t.LookAhead();
                        }
                        else if (t.currentToken == Token.TAG)
                        {
                            ParsePseudoXML(true);
                        }
                    }

                    NextToken();
                }
            }
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        protected override AnnotationList ParseAnnotationsList()
        {

            // we have read "declare" and have seen "%" as lookahead
            List<Annotation> annotations = new List<Annotation>();
            int options = 0;
            while (true)
            {
                t.State = Tokenizer.BARE_NAME_STATE;
                NextToken();
                Expect(Token.NAME);
                t.State = Tokenizer.DEFAULT_STATE;
                StructuredQName qName;
                NamespaceUri uri;

                //            if (t.currentTokenValue.indexOf(':') < 0) {
                //                uri = NamespaceUri.XQUERY;
                //                qName = new StructuredQName("", uri, t.currentTokenValue);
                //            } else {
                qName = MakeStructuredQName(t.currentTokenValue, NamespaceUri.XQUERY);
                uri = qName.GetNamespaceUri();

                //            }
                Annotation annotation = new Annotation(qName);
                if (uri.Equals(NamespaceUri.XQUERY))
                {
                    if (!qName.Equals(Annotation.PRIVATE) && !qName.Equals(Annotation.PUBLIC) && !qName.Equals(Annotation.UPDATING) && !qName.Equals(Annotation.SIMPLE))
                    {
                        Grumble("Unrecognized variable or function annotation " + qName.DisplayName, "XQST0045");
                    }

                    annotation.AddAnnotationParameter(new Int64Value(options));
                }
                else if (IsReservedInQuery(uri))
                {
                    Grumble("The annotation " + t.currentTokenValue + " is in a reserved namespace", "XQST0045"); //            } else if (uri.isEmpty()) {
                    //                grumble("The annotation " + t.currentTokenValue + " is in no namespace", "XQST0045");
                }
                else
                {
                }

                NextToken();
                if (t.currentToken == Token.LPAR)
                {
                    NextToken();
                    if (t.currentToken == Token.RPAR)
                    {
                        Grumble("Annotation parameter list cannot be empty");
                    }

                    while (true)
                    {
                        bool negative = t.currentToken == Token.MINUS;
                        if (negative)
                        {
                            if (!allowXPath40Syntax)
                            {
                                Grumble("Minus sign in annotation value requires 4.0 to be enabled");
                                return null;
                            }

                            NextToken();
                        }

                        Literal arg;
                        switch (t.currentToken)
                        {
                            case Token.STRING_LITERAL:
                                arg = (Literal)ParseStringLiteral(false);
                                break;
                            case Token.NUMBER:
                                arg = (Literal)ParseNumericLiteral(false);
                                break;
                            case Token.HEX_INTEGER:
                                arg = (Literal)ParseHexLiteral(false);
                                break;
                            case Token.BINARY_INTEGER:
                                arg = (Literal)ParseBinaryLiteral(false);
                                break;
                            case Token.FUNCTION:

                                // true() and folse() allowed in 4.0
                                if (t.currentTokenValue.Equals("true"))
                                {
                                    arg = Literal.MakeLiteral(BooleanValue.TRUE);
                                }
                                else if (t.currentTokenValue.Equals("false"))
                                {
                                    arg = Literal.MakeLiteral(BooleanValue.FALSE);
                                }
                                else
                                {
                                    Grumble("The only function calls allowed in an annotation are true() and false()");
                                    return null;
                                }

                                if (!allowXPath40Syntax)
                                {
                                    Grumble("Annotation values true() and false() require 4.0 to be enabled");
                                    return null;
                                }

                                NextToken();
                                Expect(Token.RPAR);
                                NextToken();
                                break;
                            default:
                                Grumble("Annotation parameter must be a literal");
                                return null;
                        }

                        IGroundedValue val = arg.GroundedValue;
                        if (negative)
                        {
                            if (val is NumericValue)
                            {
                                val = ((NumericValue)val).Negate();
                            }
                            else
                            {
                                Grumble("Minus sign in annotation parameter must be followed by a numeric literal");
                            }
                        }

                        if (val is StringValue || val is NumericValue || val is BooleanValue)
                        {
                            annotation.AddAnnotationParameter((AtomicValue)val);
                        }
                        else
                        {
                            Grumble("Annotation parameter must be a string or number");
                        }

                        if (t.currentToken == Token.RPAR)
                        {
                            NextToken();
                            break;
                        }

                        Expect(Token.COMMA);
                        NextToken();
                    }
                }

                annotations.Add(annotation);
                if (t.currentToken != Token.PERCENT)
                {
                    return new AnnotationList(annotations);
                }
            }
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        private void SealNamespaces(IList<NamespaceUri> namespacesToBeSealed, Configuration config)
        {
            foreach (NamespaceUri ns in namespacesToBeSealed)
            {
                config.SealNamespace(ns);
            }
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        private void ProcessPreamble()
        {
            if (preambleProcessed)
            {
                return;
            }

            preambleProcessed = true;
            if (foundDefaultCollation)
            {
                string collationName = env.GetDefaultCollationName();
                URI collationURI;
                try
                {
                    collationURI = new URI(collationName);
                    if (!collationURI.IsAbsolute())
                    {
                        URI @base = new URI(env.StaticBaseURI);
                        collationURI = @base.Resolve(collationURI);
                        collationName = collationURI.ToString();
                    }
                }
                catch (URISyntaxException err)
                {
                    Grumble("Default collation name '" + collationName + "' is not a valid URI", "XQST0046");
                    collationName = NamespaceConstant.CODEPOINT_COLLATION_URI;
                }

                if (env.GetConfiguration().GetCollation(collationName) == null)
                {
                    Grumble("Default collation name '" + collationName + "' is not a recognized collation", "XQST0038");
                    collationName = NamespaceConstant.CODEPOINT_COLLATION_URI;
                }

                ((QueryModule)env).SetDefaultCollationName(collationName);
            }

            foreach (Import imp in schemaImports)
            {
                try
                {
                    ApplySchemaImport(imp);
                }
                catch (XPathException err)
                {
                    if (!err.HasBeenReported())
                    {
                        throw err.MaybeWithLocation(MakeLocation(imp.offset));
                    }
                }
            }

            foreach (Import imp in moduleImports)
            {
                try
                {
                    ApplyModuleImport(imp);
                }
                catch (XPathException err)
                {
                    if (!err.HasBeenReported())
                    {
                        throw err.MaybeWithLocation(MakeLocation(imp.offset));
                    }
                }
            }
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        private void ParseDefaultCollation()
        {

            // <"default" "collation"> StringLiteral
            if (foundDefaultCollation)
            {
                Grumble("default collation appears more than once", "XQST0038");
            }

            foundDefaultCollation = true;
            NextToken();
            Expect(Token.STRING_LITERAL);
            string uri = UriLiteral(t.currentTokenValue);
            ((QueryModule)env).SetDefaultCollationName(uri);
            NextToken();
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        private void ParseDefaultOrder()
        {
            if (foundEmptyOrderingDeclaration)
            {
                Grumble("empty ordering declaration appears more than once", "XQST0069");
            }

            foundEmptyOrderingDeclaration = true;
            NextToken();
            if (!IsKeyword("empty"))
            {
                Grumble("After 'declare default order', expected keyword 'empty'");
            }

            NextToken();
            if (IsKeyword("least"))
            {
                ((QueryModule)env).SetEmptyLeast(true);
            }
            else if (IsKeyword("greatest"))
            {
                ((QueryModule)env).SetEmptyLeast(false);
            }
            else
            {
                Grumble("After 'declare default order empty', expected keyword 'least' or 'greatest'");
            }

            NextToken();
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        private void ParseBoundarySpaceDeclaration()
        {
            if (foundBoundarySpaceDeclaration)
            {
                Grumble("'declare boundary-space' appears more than once", "XQST0068");
            }

            foundBoundarySpaceDeclaration = true;
            NextToken();
            Expect(Token.NAME);
            if ("preserve".Equals(t.currentTokenValue))
            {
                ((QueryModule)env).SetPreserveBoundarySpace(true);
            }
            else if ("strip".Equals(t.currentTokenValue))
            {
                ((QueryModule)env).SetPreserveBoundarySpace(false);
            }
            else
            {
                Grumble("boundary-space must be 'preserve' or 'strip'");
            }

            NextToken();
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        private void ParseOrderingDeclaration()
        {
            if (foundOrderingDeclaration)
            {
                Grumble("ordering mode declaration appears more than once", "XQST0065");
            }

            foundOrderingDeclaration = true;
            NextToken();
            Expect(Token.NAME);
            if (!"ordered".Equals(t.currentTokenValue) && !"unordered".Equals(t.currentTokenValue))
            {
                Grumble("ordering mode must be 'ordered' or 'unordered'");
            }

            NextToken();
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        private void ParseCopyNamespacesDeclaration()
        {
            if (foundCopyNamespaces)
            {
                Grumble("declare copy-namespaces appears more than once", "XQST0055");
            }

            foundCopyNamespaces = true;
            NextToken();
            Expect(Token.NAME);
            if ("preserve".Equals(t.currentTokenValue))
            {
                ((QueryModule)env).SetPreserveNamespaces(true);
            }
            else if ("no-preserve".Equals(t.currentTokenValue))
            {
                ((QueryModule)env).SetPreserveNamespaces(false);
            }
            else
            {
                Grumble("copy-namespaces must be followed by 'preserve' or 'no-preserve'");
            }

            NextToken();
            Expect(Token.COMMA);
            NextToken();
            Expect(Token.NAME);
            if ("inherit".Equals(t.currentTokenValue))
            {
                ((QueryModule)env).SetInheritNamespaces(true);
            }
            else if ("no-inherit".Equals(t.currentTokenValue))
            {
                ((QueryModule)env).SetInheritNamespaces(false);
            }
            else
            {
                Grumble("After the comma in the copy-namespaces declaration, expected 'inherit' or 'no-inherit'");
            }

            NextToken();
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        private void ParseConstructionDeclaration()
        {
            if (foundConstructionDeclaration)
            {
                Grumble("declare construction appears more than once", "XQST0067");
            }

            foundConstructionDeclaration = true;
            NextToken();
            Expect(Token.NAME);
            int val;
            if ("preserve".Equals(t.currentTokenValue))
            {
                val = Validation.PRESERVE; //            if (!env.getExecutable().isSchemaAware()) {
                //                grumble("construction mode preserve is allowed only with a schema-aware query");
                //            }
            }
            else if ("strip".Equals(t.currentTokenValue))
            {
                val = Validation.STRIP;
            }
            else
            {
                Grumble("construction mode must be 'preserve' or 'strip'");
                val = Validation.STRIP;
            }

            ((QueryModule)env).ConstructionMode = val;
            NextToken();
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        protected virtual void ParseRevalidationDeclaration()
        {
            Grumble("declare revalidation is allowed only in XQuery Update");
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        private void ParseSchemaImport()
        {
            // NB: the "schema-aware not supported" check (XQST0009) is deferred to the END of this method so
            // that a malformed schema-import clause reports its own more-specific static error first, matching
            // Saxon/other processors: `namespace NCName := …` -> XPST0003, reserved prefix -> XQST0070, empty
            // target namespace with a prefix -> XQST0057 (K-CombinedErrorCodes-7, XQST0057, XQST0070_1). For a
            // well-formed import the result is unchanged (still XQST0009).
            Import sImport = new Import();
            string prefix = null;
            sImport.namespaceURI = null;
            sImport.locationURIs = new List<string>(5);
            sImport.offset = t.currentTokenStartOffset;
            NextToken();
            bool fixedDefault = false;
            if (IsKeyword("namespace"))
            {
                prefix = ReadNamespaceBinding();
            }
            else
            {
                if (IsKeyword("fixed"))
                {
                    CheckLanguageVersion40();
                    fixedDefault = true;
                    NextToken();
                }

                if (IsKeyword("default") || t.currentToken == Token.DEFAULT)
                {
                    NextToken();
                    if (!IsKeyword("element"))
                    {
                        Grumble("In 'import schema', expected 'element namespace'");
                    }

                    NextToken();
                    if (!IsKeyword("namespace"))
                    {
                        Grumble("In 'import schema', expected keyword 'namespace'");
                    }

                    NextToken();
                    prefix = "";
                }
            }

            if (t.currentToken == Token.STRING_LITERAL)
            {
                NamespaceUri uri = NamespaceUri.Of(UriLiteral(t.currentTokenValue));
                CheckProhibitedPrefixes(prefix, uri);
                sImport.namespaceURI = uri;
                NextToken();
                if (IsKeyword("at"))
                {
                    NextToken();
                    Expect(Token.STRING_LITERAL);
                    sImport.locationURIs.Add(UriLiteral(t.currentTokenValue));
                    NextToken();
                    while (t.currentToken == Token.COMMA)
                    {
                        NextToken();
                        Expect(Token.STRING_LITERAL);
                        sImport.locationURIs.Add(UriLiteral(t.currentTokenValue));
                        NextToken();
                    }
                }
                else if (t.currentToken != Token.SEMICOLON)
                {
                    Grumble("After the target namespace URI, expected 'at' or ';'");
                }
            }
            else
            {
                Grumble("After 'import schema', expected 'namespace', 'default', or a string-literal");
            }

            if (prefix != null)
            {
                try
                {
                    if ((prefix.Length == 0))
                    {
                        ((QueryModule)env).SetDefaultElementNamespace(sImport.namespaceURI, fixedDefault);
                    }
                    else
                    {
                        if (sImport.namespaceURI == null || sImport.namespaceURI.IsEmpty())
                        {
                            Grumble("A prefix cannot be bound to the null namespace", "XQST0057");
                        }

                        ((QueryModule)env).DeclarePrologNamespace(prefix, sImport.namespaceURI);
                    }
                }
                catch (XPathException err)
                {
                    err.SetLocator(MakeLocation());
                    ReportError(err);
                }
            }

            foreach (Import schemaImport in schemaImports)
            {
                if (schemaImport.namespaceURI.Equals(sImport.namespaceURI))
                {
                    Grumble("Schema namespace '" + sImport.namespaceURI + "' is imported more than once", "XQST0058");
                    break;
                }
            }

            // java.net.URI would reject these during schema loading (XQST0046); the port's lenient URI
            // accepts them, and on HE the load never happens anyway, so screen the locations here.
            foreach (string location in sImport.locationURIs)
            {
                if (!ResolveURI.IsValidUriSyntax(location))
                {
                    Grumble("Invalid schema location URI " + location, "XQST0046", sImport.offset);
                }
            }

            // Deferred from the top of the method (see note there): the clause is now fully validated, so a
            // schema-unaware processor reports XQST0009 only if no more-specific error was raised first.
            EnsureSchemaAware("import schema");
            schemaImports.Add(sImport);
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        private string ReadNamespaceBinding()
        {
            t.State = Tokenizer.DEFAULT_STATE;
            NextToken();
            Expect(Token.NAME);
            string prefix = t.currentTokenValue;
            NextToken();
            Expect(Token.EQUALS);
            NextToken();
            return prefix;
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        protected virtual void EnsureSchemaAware(string featureName, string errorCode = "XQST0009")
        {
            if (!env.GetConfiguration().IsLicensedFeature(Configuration.LicenseFeature.ENTERPRISE_XQUERY))
            {
                throw new XPathException("This Saxon version and license does not allow use of '" + featureName + "'", errorCode);
            }

            env.GetConfiguration().CheckLicensedFeature(Configuration.LicenseFeature.ENTERPRISE_XQUERY, featureName, -1);
            GetExecutable().SetSchemaAware(true);
            GetStaticContext().GetPackageData().SetSchemaAware(true);
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        private void ApplySchemaImport(Import sImport)
        {

            // Do the importing
            Configuration config = env.GetConfiguration();

            lock (config)
            {
                if (!config.IsSchemaAvailable(sImport.namespaceURI))
                {
                    if (!sImport.locationURIs.IsEmpty())
                    {
                        try
                        {
                            PipelineConfiguration pipe = config.MakePipelineConfiguration();
                            config.ReadMultipleSchemas(pipe, env.StaticBaseURI, sImport.locationURIs, sImport.namespaceURI);
                            namespacesToBeSealed.Add(sImport.namespaceURI);
                        }
                        catch (SchemaException err)
                        {
                            Grumble("Error in schema " + sImport.namespaceURI + ": " + err.GetMessage(), "XQST0059", sImport.offset);
                        }
                    }
                    else if (sImport.namespaceURI.Equals(NamespaceUri.XML) || sImport.namespaceURI.Equals(NamespaceUri.FN) || sImport.namespaceURI.Equals(NamespaceUri.SCHEMA_INSTANCE))
                    {
                        config.AddSchemaForBuiltInNamespace(sImport.namespaceURI);
                    }
                    else
                    {
                        Grumble("Unable to locate requested schema " + sImport.namespaceURI, "XQST0059", sImport.offset);
                    }
                }

                ((QueryModule)env).AddImportedSchema(sImport.namespaceURI, env.StaticBaseURI, sImport.locationURIs);
            }
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        private void ParseModuleImport()
        {
            QueryModule thisModule = (QueryModule)env;
            Import mImport = new Import();
            string prefix = null;
            mImport.namespaceURI = null;
            mImport.locationURIs = new List<string>(5);
            mImport.offset = t.currentTokenStartOffset;
            NextToken();
            if (t.currentToken == Token.NAME && t.currentTokenValue.Equals("namespace"))
            {
                prefix = ReadNamespaceBinding();
            }

            if (t.currentToken == Token.STRING_LITERAL)
            {
                NamespaceUri uri = NamespaceUri.Of(UriLiteral(t.currentTokenValue));
                CheckProhibitedPrefixes(prefix, uri);
                mImport.namespaceURI = uri;
                if (mImport.namespaceURI.IsEmpty())
                {
                    Grumble("Imported module namespace cannot be \"\"", "XQST0088");
                    mImport.namespaceURI = NamespaceUri.Of("http://saxon.fallback.namespace/line" + t.GetLineNumber()); // for error recovery
                }

                if (importedModules.Contains(mImport.namespaceURI))
                {
                    Grumble("Two 'import module' declarations specify the same module namespace", "XQST0047");
                }

                importedModules.Add(mImport.namespaceURI);
                ((QueryModule)env).AddImportedNamespace(mImport.namespaceURI);
                NextToken();
                if (IsKeyword("at"))
                {
                    do
                    {
                        NextToken();
                        Expect(Token.STRING_LITERAL);
                        mImport.locationURIs.Add(UriLiteral(t.currentTokenValue));
                        NextToken();
                    }
                    while (t.currentToken == Token.COMMA);
                }
            }
            else
            {
                Grumble("After 'import module', expected 'namespace' or a string-literal");
            }

            if (prefix != null)
            {
                try
                {
                    if (mImport.namespaceURI.Equals(thisModule.ModuleNamespace) && mImport.namespaceURI.Equals(thisModule.CheckURIForPrefix(prefix)))
                    {
                    }
                    else
                    {
                        thisModule.DeclarePrologNamespace(prefix, mImport.namespaceURI);
                    }
                }
                catch (XPathException err)
                {
                    err.SetLocator(MakeLocation());
                    ReportError(err);
                }
            }

            moduleImports.Add(mImport);
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        private void ApplyModuleImport(Import mImport)
        {
            IList<QueryModule> existingModules;

            // resolve the location URIs against the base URI
            for (int i = 0; i < mImport.locationURIs.Count; i++)
            {
                try
                {
                    string uri = mImport.locationURIs[i];
                    // java.net.URI raises URISyntaxException for these inside MakeAbsolute; the port's
                    // lenient URI does not, so screen explicitly to reach the same XQST0046.
                    if (!ResolveURI.IsValidUriSyntax(uri))
                    {
                        Grumble("Invalid URI " + uri, "XQST0046", mImport.offset);
                    }
                    URI abs = ResolveURI.MakeAbsolute(uri, env.StaticBaseURI);
                    mImport.locationURIs[i] = abs.ToString();
                }
                catch (URISyntaxException e)
                {
                    Grumble("Invalid URI " + mImport.locationURIs[i] + ": " + e.GetMessage(), "XQST0046", mImport.offset);
                }
            }


            // See if the URI is that of a separately-compiled query library
            QueryLibrary lib = ((QueryModule)env).UserQueryContext.GetCompiledLibrary(mImport.namespaceURI);
            if (lib != null)
            {
                executable.AddQueryLibraryModule(lib);
                existingModules = new List<QueryModule>();
                existingModules.Add(lib);
                lib.Link((QueryModule)env);
            }
            else if (!env.GetConfiguration().GetBooleanProperty(Feature<bool>.XQUERY_MULTIPLE_MODULE_IMPORTS))
            {

                // Unless this configuration option is set, if we already know a module with the right module URI, then we
                // use it irrespective of its location URI.
                IList<QueryModule> list = executable.GetQueryLibraryModules(mImport.namespaceURI);
                if (list != null && !list.IsEmpty())
                {
                    ((QueryModule)env).AddImportedModule(list[0]);
                    return;
                }
            }
            else
            {
                for (int h = mImport.locationURIs.Count - 1; h >= 0; h--)
                {
                    if (executable.IsQueryLocationHintProcessed(mImport.locationURIs[h]))
                    {
                        mImport.locationURIs.Remove(h);
                    }
                }
            }


            // If there are no location URIs left, and we already know a module with the right module URI.
            if (mImport.locationURIs.IsEmpty())
            {
                IList<QueryModule> list = executable.GetQueryLibraryModules(mImport.namespaceURI);
                if (list != null && !list.IsEmpty())
                {
                    foreach (QueryModule target in list)
                    {
                        ((QueryModule)env).AddImportedModule(target);
                    }

                    return;
                }
            }


            // Call the module URI resolver to find the remaining modules
            IModuleURIResolver resolver = ((QueryModule)env).UserQueryContext.ModuleURIResolver;
            string[] hints = new string[mImport.locationURIs.Count];
            for (int h = 0; h < hints.Length; h++)
            {
                hints[h] = mImport.locationURIs[h];
            }

            ResolvedResource[] sources = null;
            if (resolver != null)
            {
                try
                {
                    sources = resolver.Resolve(mImport.namespaceURI.ToString(), env.StaticBaseURI, hints);
                }
                catch (XPathException err)
                {
                    Grumble("Failed to resolve URI of imported module: " + err.GetMessage(), "XQST0059", mImport.offset);
                }
            }

            if (sources == null)
            {
                resolver = env.GetConfiguration().GetStandardModuleURIResolver();
                sources = resolver.Resolve(mImport.namespaceURI.ToString(), env.StaticBaseURI, hints);
            }

            foreach (string hint in mImport.locationURIs)
            {
                executable.AddQueryLocationHintProcessed(hint);
            }

            for (int m = 0; m < sources.Length; m++)
            {
                ResolvedResource ss = sources[m];
                string baseURI = ss.SystemId;
                if (baseURI == null)
                {
                    if (m < hints.Length)
                    {
                        baseURI = hints[m];
                    }
                    else
                    {
                        baseURI = env.StaticBaseURI; //grumble("No base URI available for imported module", "XQST0059", mImport.offset);
                    }

                    ss.SystemId = baseURI;
                }


                // Although the module hadn't been loaded when we started, it might have been loaded since, as
                // a result of a reference from another imported module.
                // TODO: use similar logic when loading schema modules
                existingModules = executable.GetQueryLibraryModules(mImport.namespaceURI);
                bool loaded = false;
                if (existingModules != null && m < hints.Length)
                {
                    foreach (QueryModule existingModule in existingModules)
                    {
                        URI uri = existingModule.LocationURI;
                        if (uri != null && uri.ToString().Equals(mImport.locationURIs[m]))
                        {
                            loaded = true;
                            break;
                        }
                    }
                }

                if (loaded)
                {
                    break;
                }

                try
                {
                    string queryText = QueryReader.ReadSourceQuery(env.GetConfiguration(), ss, charChecker);
                    try
                    {
                        if (ss.Stream != null)
                        {
                            ss.Stream.Dispose();
                        }
                        else if (ss.TextReader != null)
                        {
                            ss.TextReader.Dispose();
                        }
                    }
                    catch (IOException e)
                    {
                        throw new XPathException("Failure while closing file for imported query module");
                    }

                    QueryModule.MakeQueryModule(baseURI, executable, (QueryModule)env, queryText, mImport.namespaceURI);
                }
                catch (XPathException err)
                {
                    // Java surfaces an unreadable module location as XQST0059 (its resolver opens the
                    // resource eagerly); the port reads lazily, so the raw I/O failure from QueryReader
                    // lands here without a code. Errors from inside the module keep their own codes.
                    if (err.ErrorCodeQName == null)
                    {
                        err.SetErrorCode("XQST0059");
                    }
                    ReportError(err.MaybeWithLocation(MakeLocation()));
                }
            }
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        private void ParseBaseURIDeclaration()
        {
            if (foundBaseURIDeclaration)
            {
                Grumble("Base URI IDeclaration may only appear once", "XQST0032");
            }

            foundBaseURIDeclaration = true;
            NextToken();
            Expect(Token.STRING_LITERAL);
            string uri = UriLiteral(t.currentTokenValue);
            try
            {

                // if the supplied URI is relative, try to resolve it
                URI baseURI = new URI(uri);
                if (!baseURI.IsAbsolute())
                {
                    string oldBase = env.StaticBaseURI;
                    uri = ResolveURI.MakeAbsolute(uri, oldBase).ToString();
                }

                ((QueryModule)env).SetBaseURI(uri);
            }
            catch (URISyntaxException err)
            {

                // The spec says this "is not intrinsically an error", but can cause a failure later
                ((QueryModule)env).SetBaseURI(uri);
            }

            NextToken();
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        private void ParseDecimalFormatDeclaration()
        {
            NextToken();
            Expect(Token.NAME);
            StructuredQName formatName = MakeStructuredQName(t.currentTokenValue, NamespaceUri.NULL);
            if (env.GetDecimalFormatManager().GetNamedDecimalFormat(formatName) != null)
            {
                Grumble("Duplicate declaration of decimal-format " + formatName.DisplayName, "XQST0111");
            }

            NextToken();
            ParseDecimalFormatProperties(formatName);
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        private void ParseDefaultDecimalFormat()
        {
            if (foundDefaultDecimalFormat)
            {
                Grumble("Duplicate declaration of default decimal-format", "XQST0111");
            }

            foundDefaultDecimalFormat = true;
            ParseDecimalFormatProperties(null);
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        private void ParseDecimalFormatProperties(StructuredQName formatName)
        {
            int outerOffset = t.currentTokenStartOffset;
            DecimalFormatManager dfm = env.GetDecimalFormatManager();
            DecimalSymbols dfs = formatName == null ? dfm.DefaultDecimalFormat : dfm.ObtainNamedDecimalFormat(formatName);
            dfs.SetHostLanguage(HostLanguage.XQUERY, 31);
            HashSet<string> propertyNames = new HashSet<string>(10);
            while (t.currentToken != Token.SEMICOLON)
            {
                int offset = t.currentTokenStartOffset;
                string propertyName = t.currentTokenValue;
                if (propertyNames.Contains(propertyName))
                {
                    Grumble("Property name " + propertyName + " is defined more than once", "XQST0114", offset);
                }

                NextToken();
                Expect(Token.EQUALS);
                NextToken();
                Expect(Token.STRING_LITERAL);
                string propertyValue = Unescape(t.currentTokenValue).ToString();
                NextToken();
                propertyNames.Add(propertyName);
                switch (propertyName)
                {
                    case "decimal-separator":
                        dfs.SetDecimalSeparator(propertyValue);
                        break;
                    case "grouping-separator":
                        dfs.SetGroupingSeparator(propertyValue);
                        break;
                    case "infinity":
                        dfs.Infinity = propertyValue;
                        break;
                    case "minus-sign":
                        dfs.SetMinusSign(propertyValue);
                        break;
                    case "NaN":
                        dfs.NaN = propertyValue;
                        break;
                    case "percent":
                        dfs.SetPercent(propertyValue);
                        break;
                    case "per-mille":
                        dfs.SetPerMille(propertyValue);
                        break;
                    case "zero-digit":
                        try
                        {
                            dfs.SetZeroDigit(propertyValue);
                        }
                        catch (XPathException err)
                        {
                            throw err.WithErrorCode("XQST0097");
                        }

                        break;
                    case "digit":
                        dfs.SetDigit(propertyValue);
                        break;
                    case "pattern-separator":
                        dfs.SetPatternSeparator(propertyValue);
                        break;
                    case "exponent-separator":
                        dfs.SetExponentSeparator(propertyValue);
                        break;
                    default:
                        Grumble("Unknown decimal-format property: " + propertyName, "XPST0003", offset);
                        break;
                }
            }

            try
            {
                dfs.CheckConsistency(formatName);
            }
            catch (XPathException err)
            {
                Grumble(err.GetMessage(), "XQST0098", outerOffset);
            }
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        private void ParseDefaultFunctionNamespace()
        {
            if (foundDefaultFunctionNamespace)
            {
                Grumble("default function namespace appears more than once", "XQST0066");
            }

            foundDefaultFunctionNamespace = true;
            NextToken();
            Expect(Token.NAME);
            if (!"namespace".Equals(t.currentTokenValue))
            {
                Grumble("After 'declare default function', expected 'namespace'");
            }

            NextToken();
            Expect(Token.STRING_LITERAL);
            NamespaceUri uri = NamespaceUri.Of(UriLiteral(t.currentTokenValue));
            if (uri.Equals(NamespaceUri.XML) || uri.Equals(NamespaceUri.XMLNS))
            {
                Grumble("Reserved namespace used as default element/type namespace", "XQST0070");
            }

            ((QueryModule)env).SetDefaultFunctionNamespace(uri);
            NextToken();
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        private void ParseDefaultElementNamespace(bool isFixedDefault)
        {
            if (foundDefaultElementNamespace)
            {
                Grumble("default element namespace appears more than once", "XQST0066");
            }

            foundDefaultElementNamespace = true;
            NextToken();
            Expect(Token.NAME);
            if (!"namespace".Equals(t.currentTokenValue))
            {
                Grumble("After 'declare default element', expected 'namespace'");
            }

            NextToken();
            Expect(Token.STRING_LITERAL);
            NamespaceUri uri = NamespaceUri.Of(UriLiteral(t.currentTokenValue));
            if (uri.Equals(NamespaceUri.XML) || uri.Equals(NamespaceUri.XMLNS))
            {
                Grumble("Reserved namespace used as default element/type namespace", "XQST0070");
            }

            ((QueryModule)env).SetDefaultElementNamespace(uri, isFixedDefault);
            NextToken();
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        private void ParseNamespaceDeclaration()
        {
            NextToken();
            Expect(Token.NAME);
            string prefix = t.currentTokenValue;
            if (!NameChecker.IsValidNCName(prefix))
            {
                Grumble("Invalid namespace prefix " + Err.Wrap(prefix));
            }

            NextToken();
            Expect(Token.EQUALS);
            NextToken();
            Expect(Token.STRING_LITERAL);
            NamespaceUri uri = NamespaceUri.Of(UriLiteral(t.currentTokenValue));
            CheckProhibitedPrefixes(prefix, uri);
            if ("xml".Equals(prefix))
            {

                // disallowed here even if bound to the correct @namespace - erratum XQ.E19
                Grumble("Namespace prefix 'xml' cannot be declared", "XQST0070");
            }

            try
            {
                ((QueryModule)env).DeclarePrologNamespace(prefix, uri);
            }
            catch (XPathException err)
            {
                err.SetLocator(MakeLocation());
                ReportError(err);
            }

            NextToken();
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        private void CheckProhibitedPrefixes(string prefix, NamespaceUri uri)
        {
            if (prefix != null && !(prefix.Length == 0) && !NameChecker.IsValidNCName(prefix))
            {
                Grumble("The namespace prefix " + Err.Wrap(prefix) + " is not a valid NCName");
            }

            if (prefix == null)
            {
                prefix = "";
            }

            if (uri == null)
            {
                uri = NamespaceUri.NULL;
            }

            if ("xmlns".Equals(prefix))
            {
                Grumble("The namespace prefix 'xmlns' cannot be redeclared", "XQST0070");
            }

            if (uri.Equals(NamespaceUri.XMLNS))
            {
                Grumble("The xmlns namespace URI is reserved", "XQST0070");
            }

            if (uri.Equals(NamespaceUri.XML) && !prefix.Equals("xml"))
            {
                Grumble("The XML namespace cannot be bound to any prefix other than 'xml'", "XQST0070");
            }

            if (prefix.Equals("xml") && !uri.Equals(NamespaceUri.XML))
            {
                Grumble("The prefix 'xml' cannot be bound to any namespace other than " + NamespaceConstant.XML, "XQST0070");
            }
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        private void ParseVariableDeclaration(AnnotationList annotations)
        {
            int offset = t.currentTokenStartOffset;
            GlobalVariable var = new GlobalVariable();
            var.SetPackageData(env.GetPackageData());
            var.SetLineNumber(t.GetLineNumber() + 1);
            var.SetColumnNumber(t.GetColumnNumber() + 1);
            var.SetSystemId(env.GetSystemId());
            if (annotations != null)
            {
                CheckPublicPrivateAnnotations(annotations, "XQST0116");
                var.SetPrivate(annotations.Includes(Annotation.PRIVATE));
            }

            NextToken();
            Expect(Token.DOLLAR);
            t.State = Tokenizer.BARE_NAME_STATE;
            NextToken();
            Expect(Token.NAME);
            string varName = t.currentTokenValue;
            StructuredQName varQName = MakeStructuredQName(t.currentTokenValue, NamespaceUri.NULL);
            var.SetVariableQName(varQName);
            NamespaceUri uri = varQName.GetNamespaceUri();
            NamespaceUri moduleURI = ((QueryModule)env).ModuleNamespace;
            if (moduleURI != null && !moduleURI.Equals(uri))
            {
                Grumble("A variable declared in a library module must be in the module namespace", "XQST0048", offset);
            }

            NextToken();
            Values.SequenceType requiredType = Values.SequenceType.ANY_SEQUENCE;
            if (t.currentToken == Token.AS)
            {
                t.State = Tokenizer.SEQUENCE_TYPE_STATE;
                NextToken();
                requiredType = ParseSequenceType();
            }

            var.SetRequiredType(requiredType);
            if (t.currentToken == Token.ASSIGN)
            {
                t.State = Tokenizer.DEFAULT_STATE;
                NextToken();
                int refs = ((QueryModule)env).GetForwardReferenceCount(varQName);
                Expression exp = ParseExprSingle();
                if (((QueryModule)env).GetForwardReferenceCount(varQName) > refs)
                {
                    Grumble("Variable $" + var.GetVariableQName().DisplayName + " is referenced within its own declaration", "XPST0008");
                }

                exp = MakeTracer(exp, varQName);
                if (allowXPath40Syntax && requiredType != Values.SequenceType.ANY_SEQUENCE)
                {
                    TypeChecker checker = env.GetConfiguration().GetTypeChecker(false);
                    ExpressionVisitor visitor = ExpressionVisitor.Make(env);
                    Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.VARIABLE, varName, 0);
                    exp = checker.StaticTypeCheck(exp, requiredType, role, visitor);
                }

                var.SetBody(exp);
            }
            else if (t.currentToken == Token.NAME)
            {
                if ("external".Equals(t.currentTokenValue))
                {
                    GlobalParam par = new GlobalParam();
                    par.SetPackageData(env.GetPackageData());

                    par.SetLineNumber(var.GetLineNumber());
                    par.SetColumnNumber(var.GetColumnNumber());
                    par.SetSystemId(var.GetSystemId());
                    par.SetVariableQName(var.GetVariableQName());
                    par.SetRequiredType(var.GetRequiredType());
                    var = par;
                    NextToken();
                    if (t.currentToken == Token.ASSIGN)
                    {
                        t.State = Tokenizer.DEFAULT_STATE;
                        NextToken();
                        Expression exp = ParseExprSingle();
                        exp = MakeTracer(exp, varQName);
                        if (allowXPath40Syntax && requiredType != Values.SequenceType.ANY_SEQUENCE)
                        {
                            TypeChecker checker = env.GetConfiguration().GetTypeChecker(false);
                            ExpressionVisitor visitor = ExpressionVisitor.Make(env);
                            Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.VARIABLE, varName, 0);
                            exp = checker.StaticTypeCheck(exp, requiredType, role, visitor);
                        }

                        var.SetBody(exp);
                    }
                }
                else
                {
                    Grumble("Variable must either be initialized or be declared as external");
                }
            }
            else
            {
                Grumble("Expected ':=' or 'external' in variable declaration");
            }

            QueryModule qenv = (QueryModule)env;
            RetainedStaticContext rsc = env.MakeRetainedStaticContext();
            var.SetRetainedStaticContext(rsc);
            if (var.GetBody() != null)
            {
                ExpressionTool.SetDeepRetainedStaticContext(var.GetBody(), rsc);
            }

            if (qenv.ModuleNamespace != null && !uri.Equals(qenv.ModuleNamespace))
            {
                Grumble("Variable " + Err.Wrap(varName, Err.VARIABLE) + " is not defined in the module namespace");
            }

            try
            {
                qenv.DeclareVariable(var);
            }
            catch (XPathException e)
            {
                Grumble(e.GetMessage(), e.ErrorCodeQName, -1);
            }
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        private void ParseContextItemDeclaration()
        {
            int offset = t.currentTokenStartOffset;
            NextToken();
            if (!IsKeyword("item"))
            {
                Grumble("After 'declare context', expected 'item'");
            }

            if (foundContextItemDeclaration)
            {
                Grumble("More than one context item declaration found", "XQST0099", offset);
            }

            foundContextItemDeclaration = true;
            GlobalContextRequirement req = new GlobalContextRequirement();
            req.SetAbsentFocus(false);
            t.State = Tokenizer.BARE_NAME_STATE;
            NextToken();
            Types.ItemType requiredType = AnyItemType.GetInstance();
            if (t.currentToken == Token.AS)
            {
                t.State = Tokenizer.SEQUENCE_TYPE_STATE;
                NextToken();
                requiredType = ParseItemType();
            }

            req.AddRequiredItemType(requiredType);
            if (t.currentToken == Token.ASSIGN)
            {
                if (!((QueryModule)env).IsMainModule())
                {
                    Grumble("The context item must not be initialized in a library module", "XQST0113");
                }

                t.State = Tokenizer.DEFAULT_STATE;
                NextToken();
                Expression exp = ParseExprSingle();
                exp.SetRetainedStaticContext(env.MakeRetainedStaticContext());
                Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.CONTEXT_ITEM, "context item declaration", 0);
                exp = CardinalityChecker.MakeCardinalityChecker(exp, StaticProperty.EXACTLY_ONE, role);
                ExpressionVisitor visitor = ExpressionVisitor.Make(env);
                exp = exp.Simplify();
                ContextItemStaticInfo info = env.GetConfiguration().MakeContextItemStaticInfo(AnyItemType.GetInstance(), true);
                exp.SetRetainedStaticContext(env.MakeRetainedStaticContext());
                exp = exp.TypeCheck(visitor, info);
                req.DefaultValue = exp;
                req.SetExternal(false);
            }
            else if (t.currentToken == Token.NAME && "external".Equals(t.currentTokenValue))
            {
                req.SetAbsentFocus(false);
                req.SetExternal(true);
                NextToken();
                if (t.currentToken == Token.ASSIGN)
                {
                    if (!((QueryModule)env).IsMainModule())
                    {
                        Grumble("The context item must not be initialized in a library module", "XQST0113");
                    }

                    t.State = Tokenizer.DEFAULT_STATE;
                    NextToken();
                    Expression exp = ParseExprSingle();
                    Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.CONTEXT_ITEM, "context item declaration", 0);
                    exp = CardinalityChecker.MakeCardinalityChecker(exp, StaticProperty.EXACTLY_ONE, role);
                    exp.SetRetainedStaticContext(env.MakeRetainedStaticContext());
                    req.DefaultValue = exp;
                }
            }
            else
            {
                Grumble("Expected ':=' or 'external' in context item declaration");
            }

            Executable exec = GetExecutable();
            if (exec.GlobalContextRequirement != null)
            {

                // the context item is already declared in another module. Compare the required types
                GlobalContextRequirement gcr = exec.GlobalContextRequirement;
                if (gcr.DefaultValue == null && req.DefaultValue != null)
                {
                    gcr.DefaultValue = req.DefaultValue;
                }

                foreach (Types.ItemType otherType in gcr.RequiredItemTypes)
                {
                    if (otherType != AnyItemType.GetInstance())
                    {
                        TypeHierarchy th = env.GetConfiguration().GetTypeHierarchy();
                        Affinity rel = th.Relationship(requiredType, otherType);
                        if (rel == Affinity.DISJOINT)
                        {

                            // the two types are incompatible: fail now
                            Grumble("Different modules specify incompatible requirements for the type of the initial context item", "XPTY0004");
                        }
                    }
                }

                gcr.AddRequiredItemType(requiredType);
            }
            else
            {
                exec.GlobalContextRequirement = req;
            }
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        // XQST0106 (function) / XQST0116 (variable): a declaration must not carry more than one %public or
        // %private annotation (whether duplicated or conflicting).
        private void CheckPublicPrivateAnnotations(AnnotationList annotations, string errorCode)
        {
            int n = 0;
            foreach (Annotation a in annotations)
            {
                StructuredQName q = a.AnnotationQName;
                if (q.Equals(Annotation.PUBLIC) || q.Equals(Annotation.PRIVATE))
                {
                    n++;
                }
            }

            if (n > 1)
            {
                Grumble("A declaration must not have more than one %public or %private annotation", errorCode);
            }
        }

        public virtual void ParseFunctionDeclaration(AnnotationList annotations)
        {
            CheckPublicPrivateAnnotations(annotations, "XQST0106");
            if (annotations.Includes(SAXON_MEMO_FUNCTION))
            {
                if (env.GetConfiguration().EditionCode.Equals("HE"))
                {
                    Warning("saxon:memo-function option is ignored under Saxon-HE", DAXonErrorCode.SXJX0001);
                }
                else
                {
                    memoFunction = true;
                }
            }


            // the next token should be the < QNAME "("> pair
            int offset = t.currentTokenStartOffset;
            t.State = Tokenizer.DEFAULT_STATE;
            NextToken();
            Expect(Token.FUNCTION);
            NamespaceUri uri;
            StructuredQName qName;
            if (t.currentTokenValue.IndexOf(':') < 0)
            {
                uri = env.GetDefaultFunctionNamespace();
                qName = new StructuredQName("", uri, t.currentTokenValue);
            }
            else
            {
                qName = MakeStructuredQName(t.currentTokenValue, NamespaceUri.NULL);
                uri = qName.GetNamespaceUri();
            }

            if (uri.IsEmpty())
            {
                Grumble("The function must be in a namespace", "XQST0060");
            }

            NamespaceUri moduleURI = ((QueryModule)env).ModuleNamespace;
            if (moduleURI != null && !moduleURI.Equals(uri))
            {
                Grumble("A function in a library module must be in the module namespace", "XQST0048");
            }

            if (IsReservedInQuery(uri))
            {
                Grumble("The function name " + t.currentTokenValue + " is in a reserved namespace", "XQST0045");
            }

            XQueryFunction func = new XQueryFunction();
            func.SetFunctionName(qName);
            func.ResultType = Values.SequenceType.ANY_SEQUENCE;
            func.Body = null;
            ILocation loc = MakeNestedLocation(env.GetContainingLocation(), t.GetLineNumber(offset), t.GetColumnNumber(offset), null);
            func.SetLocation(loc);
            func.SetStaticContext((QueryModule)env);
            func.SetMemoFunction(memoFunction);
            func.SetUpdating(annotations.Includes(Annotation.UPDATING));
            func.Annotations = annotations;
            NextToken();
            HashSet<StructuredQName> paramNames = new HashSet<StructuredQName>(8);
            bool external = false;
            bool foundDefault = false;
            if (t.currentToken != Token.RPAR)
            {
                while (true)
                {

                    //     ParamList   ::=     Param ("," Param)*
                    //     Param       ::=     "$" VarName  TypeDeclaration?
                    Expect(Token.DOLLAR);
                    NextToken();
                    Expect(Token.NAME);
                    StructuredQName argQName = MakeStructuredQName(t.currentTokenValue, NamespaceUri.NULL);
                    if (paramNames.Contains(argQName))
                    {
                        Grumble("Duplicate parameter name " + Err.Wrap(t.currentTokenValue, Err.VARIABLE), "XQST0039");
                    }

                    paramNames.Add(argQName);
                    Values.SequenceType paramType = Values.SequenceType.ANY_SEQUENCE;
                    NextToken();
                    if (t.currentToken == Token.AS)
                    {
                        NextToken();
                        paramType = ParseSequenceType();
                    }

                    UserFunctionParameter arg = new UserFunctionParameter();
                    arg.SetRequiredType(paramType);
                    arg.SetVariableQName(argQName);
                    if (t.currentToken == Token.ASSIGN)
                    {
                        if (!allowXPath40Syntax)
                        {
                            Grumble("Default values for function parameters require XQuery 4.0 to be enabled");
                        }

                        foundDefault = true;
                        NextToken();
                        Expression defaultValue = ParseExprSingle();
                        if (!(defaultValue is Literal || defaultValue is ContextItemExpression))
                        {
                            Grumble("The default value for a function parameter must be either a constant, or '.' (temporary Saxon restriction)");
                        }

                        defaultValue.SetRetainedStaticContext(env.MakeRetainedStaticContext());
                        arg.DefaultValueExpression = defaultValue;
                        arg.SetRequired(false);
                    }
                    else if (foundDefault)
                    {
                        Grumble("If a parameter in a function declaration has a default value, " + "all subsequent parameters must also have default values");
                    }

                    func.AddParameter(arg);
                    if (t.currentToken == Token.RPAR)
                    {
                        break;
                    }
                    else if (t.currentToken == Token.COMMA)
                    {
                        NextToken();
                    }
                    else
                    {
                        Grumble("Expected ',' or ')' after function argument, found '" + Token.tokens[t.currentToken] + '\'');
                    }
                }


                // Don't declare the variables until the end, to prevent one parameter being referenced
                // as the default value of another
                foreach (UserFunctionParameter p in func.GetParameterDefinitions())
                {
                    DeclareRangeVariable(p);
                }
            }

            t.State = Tokenizer.BARE_NAME_STATE;
            NextToken();
            if (t.currentToken == Token.AS)
            {
                if (func.IsUpdating())
                {
                    Grumble("Cannot specify a return type for an updating function", "XUST0028");
                }

                t.State = Tokenizer.SEQUENCE_TYPE_STATE;
                NextToken();
                func.ResultType = ParseSequenceType();
            }

            if (IsKeyword("external"))
            {
                external = true;
            }
            else
            {
                Expect(Token.LCURLY);
                t.State = Tokenizer.DEFAULT_STATE;
                NextToken();
                if (t.currentToken == Token.RCURLY)
                {
                    Expression body = Literal.MakeEmptySequence();
                    body.SetRetainedStaticContext(env.MakeRetainedStaticContext());
                    SetLocation(body);
                    func.Body = body;
                }
                else
                {
                    Expression body = ParseExpression();
                    func.Body = body;
                    ExpressionTool.SetDeepRetainedStaticContext(body, env.MakeRetainedStaticContext());
                }

                Expect(Token.RCURLY);
                LookAhead(); // must be done manually after an RCURLY
            }

            UserFunctionParameter[] @params = func.GetParameterDefinitions();

            foreach (UserFunctionParameter param in @params)
            {
                UndeclareRangeVariable();
            }

            t.State = Tokenizer.DEFAULT_STATE;
            NextToken();
            QueryModule qenv = (QueryModule)env;
            if (external)
            {
                parserExtension.HandleExternalFunctionDeclaration(this, func);
            }
            else
            {
                try
                {
                    qenv.DeclareFunction(func);
                }
                catch (XPathException e)
                {
                    Grumble(e.GetMessage(), e.ErrorCodeQName, -1);
                }
            }

            memoFunction = false;
        }
        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        protected virtual void ParseItemTypeDeclaration(AnnotationList annotations)
        {
            parserExtension.ParseItemTypeDeclaration(this);
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        private void ParseOptionDeclaration()
        {
            NextToken();
            Expect(Token.NAME);
            NamespaceUri defaultUri = NamespaceUri.XQUERY;
            StructuredQName varName = MakeStructuredQName(t.currentTokenValue, defaultUri);
            NamespaceUri uri = varName.GetNamespaceUri();
            if (uri.IsEmpty())
            {
                Grumble("The QName identifying an option declaration must be prefixed", "XPST0081");
                return;
            }

            NextToken();
            Expect(Token.STRING_LITERAL);

            //String value = URILiteral(t.currentTokenValue).trim();
            string value = Unescape(t.currentTokenValue).ToString();
            if (uri.Equals(NamespaceUri.OUTPUT))
            {
                ParseOutputDeclaration(varName, value);
            }
            else if (uri.Equals(NamespaceUri.SAXON))
            {
                string localName = varName.GetLocalPart();
                switch (localName)
                {
                    case "output":
                        SetOutputProperty(value);
                        break;
                    case "memo-function":
                        value = value.Trim();
                        switch (value)
                        {
                            case "true":
                                memoFunction = true;
                                if (env.GetConfiguration().EditionCode.Equals("HE"))
                                {
                                    Warning("saxon:memo-function option is ignored under Saxon-HE", DAXonErrorCode.SXJX0001);
                                }

                                break;
                            case "false":
                                memoFunction = false;
                                break;
                            default:
                                Warning("Value of saxon:memo-function must be 'true' or 'false'", DAXonErrorCode.SXWN9042);
                                break;
                        }

                        break;
                    case "allow-cycles":
                        Warning("Value of saxon:allow-cycles is ignored", DAXonErrorCode.SXWN9042);
                        break;
                    default:
                        Warning("Unknown Saxon option declaration: " + varName.DisplayName, DAXonErrorCode.SXWN9042);
                        break;
                }
            }

            NextToken();
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        protected virtual void ParseOutputDeclaration(StructuredQName varName, string value)
        {
            if (!((QueryModule)env).IsMainModule())
            {
                Grumble("Output declarations must not appear in a library module", "XQST0108");
            }

            string localName = varName.GetLocalPart();
            if (outputPropertiesSeen.Contains(varName))
            {
                Grumble("Duplicate output declaration (" + varName + ")", "XQST0110");
            }

            outputPropertiesSeen.Add(varName);
            switch (localName)
            {
                case "parameter-document":
                    {
                        Configuration config = env.GetConfiguration();
                        ResourceRequest rr = new ResourceRequest();
                        rr.relativeUri = value;
                        rr.baseUri = env.StaticBaseURI;
                        try
                        {
                            rr.uri = ResolveURI.MakeAbsolute(value, env.StaticBaseURI).ToString();
                        }
                        catch (URISyntaxException err)
                        {
                            throw XPathException.MakeXPathException(err);
                        }

                        rr.nature = NamespaceConstant.OUTPUT;
                        rr.purpose = ResourceRequest.ANY_PURPOSE;
                        ResolvedResource source = rr.Resolve(config.GetResourceResolver(), new DirectResourceResolver(config));
                        ITreeInfo doc = config.BuildDocumentTree(source);
                        SerializationParamsHandler ph = new SerializationParamsHandler(parameterDocProperties);
                        ph.SetSerializationParams(doc.GetRootNode());
                        CharacterMap characterMap = ph.GetCharacterMap();
                        if (characterMap != null)
                        {
                            CharacterMapIndex index = new CharacterMapIndex();
                            index.PutCharacterMap(characterMap.Name, characterMap);
                            GetExecutable().SetCharacterMapIndex(index);
                            parameterDocProperties.SetProperty(DAXonOutputKeys.USE_CHARACTER_MAPS, characterMap.Name.ClarkName);
                        }

                        break;
                    }

                case "use-character-maps":
                    Grumble("Output declaration use-character-maps cannot appear except in a parameter file", "XQST0109");
                    break;
                default:
                    {
                        Properties props = GetExecutable().PrimarySerializationProperties.GetProperties();
                        ResultDocument.SetSerializationProperty(props, NamespaceUri.NULL, localName, value, env.GetNamespaceResolver(), false, env.GetConfiguration());
                        break;
                    }

                    break;
            }
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        private void SetOutputProperty(string property)
        {
            int equals = property.IndexOf('=');
            if (equals < 0)
            {
                BadOutputProperty("no equals sign");
            }
            else if (equals == 0)
            {
                BadOutputProperty("starts with '=");
            }

            string keyword = Whitespace.Trim(property.Substring(0, equals));
            string value = equals == property.Length - 1 ? "" : Whitespace.Trim(property.Substring(equals + 1));
            Properties props = GetExecutable().PrimarySerializationProperties.GetProperties();
            try
            {
                StructuredQName name = MakeStructuredQName(keyword, NamespaceUri.NULL);
                string lname = name.GetLocalPart();
                NamespaceUri uri = name.GetNamespaceUri();
                ResultDocument.SetSerializationProperty(props, uri, lname, value, env.GetNamespaceResolver(), false, env.GetConfiguration());
            }
            catch (XPathException e)
            {
                BadOutputProperty(e.GetMessage());
            }
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        private void BadOutputProperty(string s)
        {
            Warning("Invalid serialization property (" + s + ")", DAXonErrorCode.SXWN9043);
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        protected override Expression ParseFLWORExpression()
        {
            FLWORExpression flwor = new FLWORExpression();
            int exprOffset = t.currentTokenStartOffset;
            IList<Clause> clauseList = new List<Clause>(4);
            while (true)
            {
                int offset = t.currentTokenStartOffset;
                if (t.currentToken == Token.FOR || t.currentToken == Token.FOR_MEMBER)
                {
                    ParseForClause(flwor, clauseList);
                }
                else if (t.currentToken == Token.LET)
                {
                    ParseLetClause(flwor, clauseList);
                }
                else if (t.currentToken == Token.COUNT)
                {
                    ParseCountClause(clauseList);
                }
                else if (t.currentToken == Token.GROUP_BY)
                {
                    ParseGroupByClause(flwor, clauseList);
                }
                else if (t.currentToken == Token.FOR_TUMBLING || t.currentToken == Token.FOR_SLIDING)
                {
                    ParseWindowClause(flwor, clauseList);
                }
                else if (t.currentToken == Token.WHERE || IsKeyword("where"))
                {
                    NextToken();
                    Expression condition = ParseExprSingle();
                    WhereClause clause = new WhereClause(flwor, condition);
                    SetLocation(clause, t.currentTokenStartOffset);
                    clause.SetRepeated(ContainsLoopingClause(clauseList));
                    clauseList.Add(clause); //            } else if (t.currentToken == Token.WHILE || isKeyword("while")) {
                    //                if (!allowXPath40Syntax) {
                    //                    grumble("The 'while' clause requires XQuery 4.0 to be enabled");
                    //                }
                    //                nextToken();
                    //                Expression condition = parseExprSingle();
                    //                WhileClause clause = new WhileClause(flwor, condition);
                    //                setLocation(clause, t.currentTokenStartOffset);
                    //                clauseList.add(clause);
                }
                else if (IsKeyword("trace"))
                {
                    ParseTraceClause(flwor, clauseList);
                }
                else if (IsKeyword("stable") || IsKeyword("order"))
                {

                    // we read the "stable" keyword but ignore it; Saxon ordering is always stable
                    if (IsKeyword("stable"))
                    {
                        NextToken();
                        if (!IsKeyword("order"))
                        {
                            Grumble("'stable' must be followed by 'order by'");
                        }
                    }

                    TupleExpression tupleExpression = new TupleExpression();
                    IList<LocalVariableReference> vars = new List<LocalVariableReference>();
                    foreach (Clause c in clauseList)
                    {
                        foreach (LocalVariableBinding b in c.RangeVariables)
                        {
                            vars.Add(new LocalVariableReference(b));
                        }
                    }

                    tupleExpression.SetVariables(vars);
                    IList<SortSpec> sortSpecList;
                    t.State = Tokenizer.BARE_NAME_STATE;
                    NextToken();
                    if (!IsKeyword("by"))
                    {
                        Grumble("'order' must be followed by 'by'");
                    }

                    t.State = Tokenizer.DEFAULT_STATE;
                    NextToken();
                    sortSpecList = ParseSortDefinition();
                    SortKeyDefinition[] keys = new SortKeyDefinition[sortSpecList.Count];
                    for (int i = 0; i < keys.Length; i++)
                    {
                        SortSpec spec = sortSpecList[i];
                        SortKeyDefinition key = new SortKeyDefinition();
                        key.SetSortKey(sortSpecList[i].sortKey, false);
                        string str = spec.ascending ? "ascending" : "descending";
                        key.Order = new StringLiteral(BMPString.Of(str));
                        key.EmptyLeast = spec.emptyLeast;
                        if (spec.collation != null)
                        {
                            IStringCollator comparator = env.GetConfiguration().GetCollation(spec.collation);
                            if (comparator == null)
                            {
                                Grumble("Unknown collation '" + spec.collation + '\'', "XQST0076");
                            }

                            key.Collation = comparator;
                        }

                        keys[i] = key;
                    }

                    OrderByClause clause = new OrderByClause(flwor, keys, tupleExpression);
                    clause.SetRepeated(ContainsLoopingClause(clauseList));
                    clauseList.Add(clause);
                }
                else
                {
                    break;
                }

                SetLocation(clauseList[clauseList.Count - 1], offset);
            }

            int returnOffset = t.currentTokenStartOffset;
            Expect(Token.RETURN);
            t.State = Tokenizer.DEFAULT_STATE;
            NextToken();
            Expression returnExpression = ParseExprSingle();
            returnExpression = MakeTracer(returnExpression, null);

            // undeclare all the range variables
            for (int i = clauseList.Count - 1; i >= 0; i--)
            {
                Clause clause = clauseList[i];
                for (int n = 0; n < clause.RangeVariables.Length; n++)
                {
                    UndeclareRangeVariable();
                }
            }


            //        if (codeInjector != null) {
            //            List<Clause> expandedList = new List<>(clauseList.size() * 2);
            //            expandedList.add(clauseList.get(0));
            //            for (int i = 1; i < clauseList.size(); i++) {
            //                Clause extra = codeInjector.injectClause(
            //                        clauseList.get(i - 1),
            //                        env
            //                );
            //                if (extra != null) {
            //                    expandedList.add(extra);
            //                }
            //                expandedList.add(clauseList.get(i));
            //            }
            //            Clause extra = codeInjector.injectClause(
            //                    clauseList.get(clauseList.size() - 1), env);
            //            if (extra != null) {
            //                expandedList.add(extra);
            //            }
            //            clauseList = expandedList;
            //        }
            flwor.Init(clauseList, returnExpression);
            SetLocation(flwor, exprOffset);
            return flwor;
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        protected virtual LetExpression MakeLetExpression()
        {
            if (((QueryModule)env).UserQueryContext.IsCompileWithTracing())
            {
                return new EagerLetExpression();
            }
            else
            {
                return new LetExpression();
            }
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        protected static bool ContainsLoopingClause(IList<Clause> clauseList)
        {
            foreach (Clause c in clauseList)
            {
                if (FLWORExpression.IsLoopingClause(c))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        private void ParseForClause(FLWORExpression flwor, IList<Clause> clauseList)
        {
            bool first = true;
            bool forMember = t.currentToken == Token.FOR_MEMBER;

            // "for member $x as T in $array"
            // compiles to
            // "for $temp in array:members($array) let $x as T := $temp?value"
            do
            {
                NextToken();
                if (!first)
                {
                    if (IsKeyword("member"))
                    {
                        forMember = true;
                        NextToken();
                    }
                    else
                    {
                        forMember = false;
                    }
                }

                if (forMember && !allowXPath40Syntax)
                {
                    Grumble("The 'for member' syntax requires XQuery 4.0 to be enabled");
                }

                int offset = t.currentTokenStartOffset;
                ForClause clause = new ForClause();
                clause.SetRepeated(!first || ContainsLoopingClause(clauseList));
                if (first)
                {
                    first = false;
                }

                SetLocation(clause, offset);
                clauseList.Add(clause);
                Expect(Token.DOLLAR);
                NextToken();
                Expect(Token.NAME);
                StructuredQName explicitQName = MakeStructuredQName(t.currentTokenValue, NamespaceUri.NULL);
                StructuredQName iterationQName = explicitQName;
                if (forMember)
                {
                    iterationQName = new StructuredQName("vv", NamespaceUri.SAXON_GENERATED_VARIABLE, "fm" + clause.GetHashCode());
                }

                Values.SequenceType type = forMember ? Values.SequenceType.ANY_SEQUENCE : Values.SequenceType.SINGLE_ITEM;
                NextToken();
                bool explicitType = false;
                if (t.currentToken == Token.AS)
                {
                    explicitType = true;
                    NextToken();
                    type = ParseSequenceType();
                }

                bool allowingEmpty = false;
                if (IsKeyword("allowing"))
                {
                    if (forMember)
                    {
                        Grumble("'allowing empty' cannot appear in a 'for member' clause");
                    }

                    allowingEmpty = true;
                    clause.SetAllowingEmpty(true);
                    if (!explicitType)
                    {
                        type = forMember ? Values.SequenceType.ANY_SEQUENCE : Values.SequenceType.OPTIONAL_ITEM;
                    }

                    NextToken();
                    if (!IsKeyword("empty"))
                    {
                        Grumble("After 'allowing', expected 'empty'");
                    }

                    NextToken();
                }

                if (explicitType && !allowingEmpty && !forMember && type.GetCardinality() != StaticProperty.EXACTLY_ONE)
                {
                    Warning("Occurrence indicator on singleton range variable has no effect", DAXonErrorCode.SXWN9039);
                    type = Values.SequenceType.MakeSequenceType(type.PrimaryType, StaticProperty.EXACTLY_ONE);
                }

                LocalVariableBinding binding = new LocalVariableBinding(iterationQName, forMember ? Values.SequenceType.ANY_SEQUENCE : type);
                clause.RangeVariable = binding;
                if (IsKeyword("at"))
                {
                    NextToken();
                    Expect(Token.DOLLAR);
                    NextToken();
                    Expect(Token.NAME);
                    StructuredQName posQName = MakeStructuredQName(t.currentTokenValue, NamespaceUri.NULL);
                    if (!scanOnly && posQName.Equals(explicitQName))
                    {
                        Grumble("The two variables declared in a single 'for' clause must have different names", "XQST0089");
                    }

                    LocalVariableBinding pos = new LocalVariableBinding(posQName, Values.SequenceType.SINGLE_INTEGER);
                    clause.PositionVariable = pos;
                    NextToken();
                }

                Expect(Token.IN);
                NextToken();
                Expression collection = ParseExprSingle();
                if (forMember)
                {
                    collection = ArrayFunctionSet.GetInstance(40).MakeFunction("members", 1).MakeFunctionCall(collection);
                }

                clause.InitSequence(flwor, collection);
                DeclareRangeVariable(binding);
                if (clause.PositionVariable != null)
                {
                    DeclareRangeVariable(clause.PositionVariable);
                }

                if (allowingEmpty)
                {
                    CheckForClauseAllowingEmpty(flwor, clause);
                }

                if (forMember)
                {

                    // Generate "let $x as T := $temp?value"
                    LetClause letClause = new LetClause();
                    LocalVariableBinding letBinding = new LocalVariableBinding(explicitQName, type);
                    letClause.RangeVariable = letBinding;
                    LocalVariableReference tempRef = new LocalVariableReference(clause.RangeVariable);
                    LookupExpression lookup = new LookupExpression(tempRef, new StringLiteral("value"));
                    letClause.InitSequence(flwor, lookup);
                    DeclareRangeVariable(letBinding);
                    clauseList.Add(letClause);
                }
            }
            while (t.currentToken == Token.COMMA);
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        private void CheckForClauseAllowingEmpty(FLWORExpression flwor, ForClause clause)
        {
            if (!allowXPath30Syntax)
            {
                Grumble("The 'allowing empty' option requires XQuery 3.0");
            }

            Values.SequenceType type = clause.RangeVariable.GetRequiredType();
            if (!Cardinality.AllowsZero(type.GetCardinality()))
            {
                Warning("When 'allowing empty' is specified, the occurrence indicator on the range variable type should be '?'", DAXonErrorCode.SXWN9039);
            }
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        private void ParseLetClause(FLWORExpression flwor, IList<Clause> clauseList)
        {
            bool first = true;
            do
            {
                LetClause clause = new LetClause();
                SetLocation(clause, t.currentTokenStartOffset);
                clause.SetRepeated(ContainsLoopingClause(clauseList));
                if (first)
                {
                }

                clauseList.Add(clause);
                NextToken();
                if (first)
                {
                    first = false;
                }
                else
                {
                }

                Expect(Token.DOLLAR);
                NextToken();
                Expect(Token.NAME);
                string var = t.currentTokenValue;
                StructuredQName varQName = MakeStructuredQName(var, NamespaceUri.NULL);
                Values.SequenceType type = Values.SequenceType.ANY_SEQUENCE;
                NextToken();
                if (t.currentToken == Token.AS)
                {
                    NextToken();
                    type = ParseSequenceType();
                }

                LocalVariableBinding v = new LocalVariableBinding(varQName, type);
                Expect(Token.ASSIGN);
                NextToken();
                clause.InitSequence(flwor, ParseExprSingle());
                clause.RangeVariable = v;
                DeclareRangeVariable(v);
            }
            while (t.currentToken == Token.COMMA);
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        private void ParseCountClause(IList<Clause> clauseList)
        {
            CountClause clause = new CountClause();
            SetLocation(clause, t.currentTokenStartOffset);
            clause.SetRepeated(ContainsLoopingClause(clauseList));
            clauseList.Add(clause);
            NextToken();
            Expect(Token.DOLLAR);
            NextToken();
            Expect(Token.NAME);
            string var = t.currentTokenValue;
            StructuredQName varQName = MakeStructuredQName(var, NamespaceUri.NULL);
            Values.SequenceType type = Values.SequenceType.ANY_SEQUENCE;
            NextToken();
            LocalVariableBinding v = new LocalVariableBinding(varQName, type);
            clause.RangeVariable = v;
            DeclareRangeVariable(v);
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        private void ParseTraceClause(FLWORExpression flwor, IList<Clause> clauseList)
        {
            DiagnosticClause clause = new DiagnosticClause();
            SetLocation(clause, t.currentTokenStartOffset);
            clause.SetRepeated(ContainsLoopingClause(clauseList));
            clauseList.Add(clause);
            NextToken();
            clause.InitSequence(flwor, ParseExpression());
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        private void ParseGroupByClause(FLWORExpression flwor, IList<Clause> clauseList)
        {
            GroupByClause clause = new GroupByClause(env.GetConfiguration());
            SetLocation(clause, t.currentTokenStartOffset);
            clause.SetRepeated(ContainsLoopingClause(clauseList));
            IList<StructuredQName> variableNames = new List<StructuredQName>();
            IList<string> collations = new List<string>();
            NextToken();
            while (true)
            {
                Values.SequenceType type = Values.SequenceType.ANY_SEQUENCE;
                StructuredQName varQName = ReadVariableName();
                if (t.currentToken == Token.AS)
                {
                    NextToken();
                    type = ParseSequenceType();
                    if (t.currentToken != Token.ASSIGN)
                    {
                        Grumble("In group by, if the type is declared then it must be followed by ':= value'");
                    }
                }

                if (t.currentToken == Token.ASSIGN)
                {
                    LetClause letClause = new LetClause();
                    SetLocation(clause, t.currentTokenStartOffset);
                    clauseList.Add(letClause);
                    NextToken();
                    LocalVariableBinding v = new LocalVariableBinding(varQName, type);
                    Expression value = ParseExprSingle();
                    Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.MISC, "grouping key", 0);
                    Expression atomizedValue = Atomizer.MakeAtomizer(value, role);
                    letClause.InitSequence(flwor, atomizedValue);
                    letClause.RangeVariable = v;
                    DeclareRangeVariable(v);
                }

                variableNames.Add(varQName);
                if (IsKeyword("collation"))
                {
                    NextToken();
                    Expect(Token.STRING_LITERAL);
                    collations.Add(t.currentTokenValue);
                    NextToken();
                }
                else
                {
                    collations.Add(env.GetDefaultCollationName());
                }

                if (t.currentToken == Token.COMMA)
                {
                    NextToken();
                }
                else
                {
                    break;
                }
            }


            // Each of the variable names acts both as a variable reference (for a variable in the pre-grouping stream)
            // and a variable declaration (for a variable in the post-grouping stream).
            TupleExpression groupingTupleExpr = new TupleExpression();
            TupleExpression retainedTupleExpr = new TupleExpression();
            IList<LocalVariableReference> groupingRefs = new List<LocalVariableReference>();
            IList<LocalVariableReference> retainedRefs = new List<LocalVariableReference>();
            IList<LocalVariableBinding> groupedBindings = new List<LocalVariableBinding>();
            foreach (StructuredQName q in variableNames)
            {
                bool found = LocateDeclaration(clauseList, groupingRefs, groupedBindings, q);
                if (!found)
                {
                    Grumble("The grouping variable " + q.DisplayName + " must be the name of a variable bound earlier in the FLWOR expression", "XQST0094");
                }
            }

            groupingTupleExpr.SetVariables(groupingRefs);
            clause.InitGroupingTupleExpression(flwor, groupingTupleExpr);
            IList<LocalVariableBinding> ungroupedBindings = new List<LocalVariableBinding>();
            for (int i = clauseList.Count - 1; i >= 0; i--)
            {
                foreach (LocalVariableBinding b in clauseList[i].RangeVariables)
                {
                    if (!groupedBindings.Contains(b))
                    {
                        ungroupedBindings.Add(b);
                        retainedRefs.Add(new LocalVariableReference(b));
                    }
                }
            }

            retainedTupleExpr.SetVariables(retainedRefs);
            clause.InitRetainedTupleExpression(flwor, retainedTupleExpr);
            LocalVariableBinding[] bindings = new LocalVariableBinding[groupedBindings.Count + ungroupedBindings.Count];
            int k = 0;
            foreach (LocalVariableBinding b in groupedBindings)
            {
                bindings[k] = new LocalVariableBinding(b.GetVariableQName(), b.GetRequiredType());

                //declareRangeVariable(bindings[k]);
                k++;
            }

            foreach (LocalVariableBinding b in ungroupedBindings)
            {
                Types.ItemType itemType = b.GetRequiredType().PrimaryType;
                bindings[k] = new LocalVariableBinding(b.GetVariableQName(), Values.SequenceType.MakeSequenceType(itemType, StaticProperty.ALLOWS_ZERO_OR_MORE));

                //declareRangeVariable(bindings[k]);
                k++;
            }

            for (int z = groupedBindings.Count; z < bindings.Length; z++)
            {
                DeclareRangeVariable(bindings[z]);
            }

            for (int z = 0; z < groupedBindings.Count; z++)
            {
                DeclareRangeVariable(bindings[z]);
            }

            clause.SetVariableBindings(bindings);
            GenericAtomicComparer[] comparers = new GenericAtomicComparer[collations.Count];
            IXPathContext context = env.MakeEarlyEvaluationContext();
            for (int i = 0; i < comparers.Length; i++)
            {
                IStringCollator coll = env.GetConfiguration().GetCollation(collations[i]);
                comparers[i] = (GenericAtomicComparer)GenericAtomicComparer.MakeAtomicComparer(BuiltInAtomicType.ANY_ATOMIC, BuiltInAtomicType.ANY_ATOMIC, coll, context);
            }

            clause.SetComparers(comparers);
            clauseList.Add(clause);
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        private bool LocateDeclaration(IList<Clause> clauseList, IList<LocalVariableReference> groupingRefs, IList<LocalVariableBinding> groupedBindings, StructuredQName q)
        {
            for (int i = clauseList.Count - 1; i >= 0; i--)
            {
                foreach (LocalVariableBinding b in clauseList[i].RangeVariables)
                {
                    if (q.Equals(b.GetVariableQName()))
                    {
                        groupedBindings.Add(b);
                        groupingRefs.Add(new LocalVariableReference(b));
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        private StructuredQName ReadVariableName()
        {
            Expect(Token.DOLLAR);
            NextToken();
            Expect(Token.NAME);
            string name = t.currentTokenValue;
            NextToken();
            return MakeStructuredQName(name, NamespaceUri.NULL);
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        private void ParseWindowClause(FLWORExpression flwor, IList<Clause> clauseList)
        {
            WindowClause clause = new WindowClause();
            SetLocation(clause, t.currentTokenStartOffset);
            clause.SetRepeated(ContainsLoopingClause(clauseList));
            clause.SetIsSlidingWindow(t.currentToken == Token.FOR_SLIDING);
            NextToken();
            if (!IsKeyword("window"))
            {
                Grumble("after 'sliding' or 'tumbling', expected 'window', but found " + CurrentTokenDisplay());
            }

            NextToken();
            StructuredQName windowVarName = ReadVariableName();
            Values.SequenceType windowType = Values.SequenceType.ANY_SEQUENCE;
            if (t.currentToken == Token.AS)
            {
                NextToken();
                windowType = ParseSequenceType();
            }

            LocalVariableBinding windowVar = new LocalVariableBinding(windowVarName, windowType);
            clause.SetVariableBinding(WindowClause.WINDOW_VAR, windowVar);

            // We can't assume that all the items in the input sequence belong to the item type of the windows: test case SlidingWindowExpr507
            Values.SequenceType windowItemTypeMandatory = Values.SequenceType.SINGLE_ITEM;
            Values.SequenceType windowItemTypeOptional = Values.SequenceType.OPTIONAL_ITEM;
            Expect(Token.IN);
            NextToken();
            clause.InitSequence(flwor, ParseExprSingle());
            if (IsKeyword("start"))
            {
                t.State = Tokenizer.BARE_NAME_STATE;
                NextToken();
                if (t.currentToken == Token.DOLLAR)
                {
                    LocalVariableBinding startItemVar = new LocalVariableBinding(ReadVariableName(), windowItemTypeMandatory);
                    clause.SetVariableBinding(WindowClause.START_ITEM, startItemVar);
                    DeclareRangeVariable(startItemVar);
                }

                if (IsKeyword("at"))
                {
                    NextToken();
                    LocalVariableBinding startPositionVar = new LocalVariableBinding(ReadVariableName(), Values.SequenceType.SINGLE_INTEGER);
                    clause.SetVariableBinding(WindowClause.START_ITEM_POSITION, startPositionVar);
                    DeclareRangeVariable(startPositionVar);
                }

                if (IsKeyword("previous"))
                {
                    NextToken();
                    LocalVariableBinding startPreviousItemVar = new LocalVariableBinding(ReadVariableName(), windowItemTypeOptional);
                    clause.SetVariableBinding(WindowClause.START_PREVIOUS_ITEM, startPreviousItemVar);
                    DeclareRangeVariable(startPreviousItemVar);
                }

                if (IsKeyword("next"))
                {
                    NextToken();
                    LocalVariableBinding startNextItemVar = new LocalVariableBinding(ReadVariableName(), windowItemTypeOptional);
                    clause.SetVariableBinding(WindowClause.START_NEXT_ITEM, startNextItemVar);
                    DeclareRangeVariable(startNextItemVar);
                }

                if (IsKeyword("when"))
                {
                    t.State = Tokenizer.DEFAULT_STATE;
                    NextToken();
                    clause.InitStartCondition(flwor, ParseExprSingle());
                }
                else if (allowXPath40Syntax)
                {
                    clause.InitStartCondition(flwor, Literal.MakeLiteral(BooleanValue.TRUE, flwor));
                }
                else
                {
                    Grumble("Expected 'when' condition for window start, but found " + CurrentTokenDisplay());
                }
            }
            else if (allowXPath40Syntax)
            {
                clause.InitStartCondition(flwor, Literal.MakeLiteral(BooleanValue.TRUE, flwor));
            }
            else
            {
                Grumble("in window clause, expected 'start', but found " + CurrentTokenDisplay());
            }

            if (IsKeyword("only"))
            {
                clause.SetIncludeUnclosedWindows(false);
                NextToken();
            }

            if (IsKeyword("end"))
            {
                t.State = Tokenizer.BARE_NAME_STATE;
                NextToken();
                if (t.currentToken == Token.DOLLAR)
                {
                    LocalVariableBinding endItemVar = new LocalVariableBinding(ReadVariableName(), windowItemTypeMandatory);
                    clause.SetVariableBinding(WindowClause.END_ITEM, endItemVar);
                    DeclareRangeVariable(endItemVar);
                }

                if (IsKeyword("at"))
                {
                    NextToken();
                    LocalVariableBinding endPositionVar = new LocalVariableBinding(ReadVariableName(), Values.SequenceType.SINGLE_INTEGER);
                    clause.SetVariableBinding(WindowClause.END_ITEM_POSITION, endPositionVar);
                    DeclareRangeVariable(endPositionVar);
                }

                if (IsKeyword("previous"))
                {
                    NextToken();
                    LocalVariableBinding endPreviousItemVar = new LocalVariableBinding(ReadVariableName(), windowItemTypeOptional);
                    clause.SetVariableBinding(WindowClause.END_PREVIOUS_ITEM, endPreviousItemVar);
                    DeclareRangeVariable(endPreviousItemVar);
                }

                if (IsKeyword("next"))
                {
                    NextToken();
                    LocalVariableBinding endNextItemVar = new LocalVariableBinding(ReadVariableName(), windowItemTypeOptional);
                    clause.SetVariableBinding(WindowClause.END_NEXT_ITEM, endNextItemVar);
                    DeclareRangeVariable(endNextItemVar);
                }

                if (IsKeyword("when"))
                {
                    t.State = Tokenizer.DEFAULT_STATE;
                    NextToken();
                    clause.InitEndCondition(flwor, ParseExprSingle());
                }
                else if (allowXPath40Syntax)
                {
                    clause.InitEndCondition(flwor, Literal.MakeLiteral(BooleanValue.TRUE, flwor));
                }
                else
                {
                    Grumble("Expected 'when' condition for window end, but found " + CurrentTokenDisplay());
                }
            }
            else
            {

                // no "end" condition found
                if (clause.IsSlidingWindow())
                {
                    Grumble("A sliding window requires an end condition");
                }
            }

            DeclareRangeVariable(windowVar);
            clauseList.Add(clause);
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        public static Expression MakeStringJoin(Expression exp, IStaticContext env)
        {
            exp = Atomizer.MakeAtomizer(exp, null);
            Types.ItemType t = exp.GetItemType();
            if (!t.Equals(BuiltInAtomicType.STRING) && !t.Equals(BuiltInAtomicType.UNTYPED_ATOMIC))
            {
                exp = new AtomicSequenceConverter(exp, BuiltInAtomicType.STRING);
                ((AtomicSequenceConverter)exp).AllocateConverterStatically(env.GetConfiguration(), false);
            }

            if (exp.GetCardinality() == StaticProperty.EXACTLY_ONE)
            {
                return exp;
            }
            else
            {
                RetainedStaticContext rsc = new RetainedStaticContext(env);
                Expression fn = SystemFunction.MakeCall("string-join", rsc, exp, new StringLiteral(StringValue.SINGLE_SPACE));
                ExpressionTool.CopyLocationInfo(exp, fn);
                return fn;
            }
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        private IList<SortSpec> ParseSortDefinition()
        {
            IList<SortSpec> sortSpecList = new List<SortSpec>(5);
            while (true)
            {
                SortSpec sortSpec = new SortSpec();
                sortSpec.sortKey = ParseExprSingle();
                sortSpec.ascending = true;
                sortSpec.emptyLeast = ((QueryModule)env).IsEmptyLeast();
                sortSpec.collation = env.GetDefaultCollationName();

                if (IsKeyword("ascending"))
                {
                    NextToken();
                }
                else if (IsKeyword("descending"))
                {
                    sortSpec.ascending = false;
                    NextToken();
                }

                if (IsKeyword("empty"))
                {
                    NextToken();
                    if (IsKeyword("greatest"))
                    {
                        sortSpec.emptyLeast = false;
                        NextToken();
                    }
                    else if (IsKeyword("least"))
                    {
                        sortSpec.emptyLeast = true;
                        NextToken();
                    }
                    else
                    {
                        Grumble("'empty' must be followed by 'greatest' or 'least'");
                    }
                }

                if (IsKeyword("collation"))
                {
                    sortSpec.collation = ReadCollationName();
                }

                sortSpecList.Add(sortSpec);
                if (t.currentToken == Token.COMMA)
                {
                    NextToken();
                }
                else
                {
                    break;
                }
            }

            return sortSpecList;
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        protected virtual string ReadCollationName()
        {
            NextToken();
            Expect(Token.STRING_LITERAL);
            string collationName = UriLiteral(t.currentTokenValue);
            URI collationURI;
            try
            {
                collationURI = new URI(collationName);
                if (!collationURI.IsAbsolute())
                {
                    URI @base = new URI(env.StaticBaseURI);
                    collationURI = @base.Resolve(collationURI);
                    collationName = collationURI.ToString();
                }
            }
            catch (URISyntaxException err)
            {
                Grumble("Collation name '" + collationName + "' is not a valid URI", "XQST0046");
                collationName = NamespaceConstant.CODEPOINT_COLLATION_URI;
            }

            NextToken();
            return collationName;
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        protected override Expression ParseTypeswitchExpression()
        {

            // On entry, the "(" has already been read
            int offset = t.currentTokenStartOffset;
            NextToken();
            Expression operand = ParseExpression();
            IList<IList<Values.SequenceType>> types = new List<IList<Values.SequenceType>>(10);
            IList<Expression> actions = new List<Expression>(10);
            Expect(Token.RPAR);
            NextToken();

            // The code generated takes the form:
            //    let $zzz := operand return
            //    if ($zzz instance of t1) then action1
            //    else if ($zzz instance of t2) then action2
            //    else default-action
            //
            // If a variable is declared in a case clause or default clause,
            // then "action-n" takes the form
            //    let $v as type := $zzz return action-n
            // we were generating "let $v as type := $zzz return action-n" but this gives a compile time error if
            // there's a case clause that specifies an impossible type.
            LetExpression outerLet = MakeLetExpression();
            outerLet.SetRequiredType(Values.SequenceType.ANY_SEQUENCE);
            outerLet.SetVariableQName(new StructuredQName("zz", NamespaceUri.SAXON, "zz_typeswitchVar"));
            outerLet.Sequence = operand;
            bool braced = false;
            if (t.currentToken == Token.LCURLY)
            {
                CheckLanguageVersion40();
                braced = true;
                NextToken();
            }

            while (t.currentToken == Token.CASE || IsKeyword("case"))
            {
                IList<Values.SequenceType> typeList;
                Expression action;
                NextToken();
                if (t.currentToken == Token.DOLLAR)
                {
                    NextToken();
                    Expect(Token.NAME);
                    string var = t.currentTokenValue;
                    StructuredQName varQName = MakeStructuredQName(var, NamespaceUri.NULL);
                    NextToken();
                    Expect(Token.AS);
                    NextToken();
                    typeList = ParseSequenceTypeList();
                    action = MakeTracer(ParseTypeswitchReturnClause(varQName, outerLet), varQName);
                    if (action is TraceExpression)
                    {
                        ((TraceExpression)action).SetProperty("type", typeList[0].ToString());
                    }
                }
                else
                {
                    typeList = ParseSequenceTypeList();
                    action = MakeTracer(ParseExprSingle(), null);
                    if (action is TraceExpression)
                    {
                        ((TraceExpression)action).SetProperty("type", typeList[0].ToString());
                    }
                }

                types.Add(typeList);
                actions.Add(action);
            }

            if (types.IsEmpty())
            {
                Grumble("At least one case clause is required in a typeswitch");
            }

            Expect(Token.DEFAULT);
            int defaultOffset = t.currentTokenStartOffset;
            NextToken();
            Expression defaultAction;
            if (t.currentToken == Token.DOLLAR)
            {
                NextToken();
                Expect(Token.NAME);
                string var = t.currentTokenValue;
                StructuredQName varQName = MakeStructuredQName(var, NamespaceUri.NULL);
                NextToken();
                Expect(Token.RETURN);
                NextToken();
                defaultAction = MakeTracer(ParseTypeswitchReturnClause(varQName, outerLet), varQName);
            }
            else
            {
                t.TreatCurrentAsOperator();
                Expect(Token.RETURN);
                NextToken();
                defaultAction = MakeTracer(ParseExprSingle(), null);
            }

            Expression lastAction = defaultAction;

            // Note, the ragged "choose" later gets flattened into a single-level choose, saving stack space
            for (int i = types.Count - 1; i >= 0; i--)
            {
                LocalVariableReference var = new LocalVariableReference(outerLet);
                SetLocation(var);
                Expression ioe = new InstanceOfExpression(var, types[i][0]);
                for (int j = 1; j < types[i].Count; j++)
                {
                    ioe = new OrExpression(ioe, new InstanceOfExpression(var.Copy(new RebindingMap()), types[i][j]));
                }

                SetLocation(ioe);
                Expression ife = Choose.MakeConditional(ioe, actions[i], lastAction);
                SetLocation(ife);
                lastAction = ife;
            }

            outerLet.SetAction(lastAction);
            if (braced)
            {
                Expect(Token.RCURLY);
                t.LookAhead();
                NextToken();
            }

            return MakeTracer(outerLet, null);
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        //
        private IList<Values.SequenceType> ParseSequenceTypeList()
        {
            IList<Values.SequenceType> typeList = new List<Values.SequenceType>();
            while (true)
            {
                Values.SequenceType type = ParseSequenceType();
                typeList.Add(type);
                t.TreatCurrentAsOperator();
                if (t.currentToken == Token.UNION)
                {
                    NextToken();
                }
                else
                {
                    break;
                }
            }

            Expect(Token.RETURN);
            NextToken();
            return typeList;
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        private Expression ParseTypeswitchReturnClause(StructuredQName varQName, LetExpression outerLet)
        {
            Expression action;

            //        t.treatCurrentAsOperator();
            //        expect(Token.RETURN);
            //        nextToken();
            LetExpression innerLet = MakeLetExpression();
            innerLet.SetRequiredType(Values.SequenceType.ANY_SEQUENCE);
            innerLet.SetVariableQName(varQName);
            innerLet.Sequence = new LocalVariableReference(outerLet);
            DeclareRangeVariable(innerLet);
            action = ParseExprSingle();
            UndeclareRangeVariable();
            innerLet.SetAction(action);
            return innerLet; //        if (Literal.isEmptySequence(action)) {
            //            // The purpose of simplifying this now is that () is allowed in a branch even in XQuery Update when
            //            // other branches of the typeswitch are updating.
            //            return action;
            //        } else {
            //            return innerLet;
            //        }
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        //
        protected override Expression ParseSwitchExpression()
        {
            Expression operand;
            bool braced = false;
            if (t.currentToken == Token.SWITCH)
            {

                // On entry, the "(" has already been read
                NextToken();
                operand = ParseExpression();
                Expect(Token.RPAR);
                NextToken();
            }
            else if (t.currentToken == Token.KEYWORD_CURLY)
            {
                CheckLanguageVersion40();
                operand = Literal.MakeLiteral(BooleanValue.TRUE);
                braced = true;
                NextToken();
                if (IsKeyword("case"))
                {
                    t.currentToken = Token.CASE;
                }
            }
            else if (t.currentToken == Token.SWITCH_CASE)
            {
                CheckLanguageVersion40();
                operand = Literal.MakeLiteral(BooleanValue.TRUE);
                t.currentToken = Token.CASE;
            }
            else
            {
                throw new InvalidOperationException();
            }

            if (t.currentToken == Token.LCURLY)
            {
                CheckLanguageVersion40();
                braced = true;
                NextToken();
                if (IsKeyword("case"))
                {
                    t.currentToken = Token.CASE;
                }
            }

            IList<Expression> conditions = new List<Expression>(10);
            IList<Expression> actions = new List<Expression>(10);

            // The code generated takes the form:
            //    let $zzz := zero-or-one(atomize(operand)) return
            //    choose
            //      when ($zzz eq t1) then action1
            //      when ($zzz eq t2) then action2
            //      when (true) default-action
            //
            // We rely on the optimizer to convert this to a SwitchExpression in the case where all the case clauses
            // are literal constants.
            LetExpression outerLet = MakeLetExpression();
            outerLet.SetRequiredType(Values.SequenceType.OPTIONAL_ATOMIC);
            outerLet.SetVariableQName(new StructuredQName("zz", NamespaceUri.SAXON, "zz_switchVar"));
            outerLet.Sequence = Atomizer.MakeAtomizer(operand, null);
            do
            {
                IList<Expression> caseExpressions = new List<Expression>(4);
                Expect(Token.CASE);
                do
                {
                    NextToken();
                    Expression c = ParseExprSingle();
                    caseExpressions.Add(c);
                }
                while (t.currentToken == Token.CASE);
                Expect(Token.RETURN);
                NextToken();
                Expression action = ParseExprSingle();
                for (int i = 0; i < caseExpressions.Count; i++)
                {
                    SwitchCaseComparison vc = new SwitchCaseComparison(new LocalVariableReference(outerLet), Token.FEQ, caseExpressions[i], allowXPath40Syntax);
                    if (i == 0)
                    {
                        conditions.Add(vc);
                        actions.Add(action);
                    }
                    else
                    {
                        OrExpression orExpr = new OrExpression(conditions.Remove(conditions.Count - 1), vc);
                        conditions.Add(orExpr);
                    } //actions.add((i==0 ? action : action.copy()));
                }
            }
            while (t.currentToken == Token.CASE);
            Expect(Token.DEFAULT);
            NextToken();
            Expect(Token.RETURN);
            NextToken();
            Expression defaultExpr = ParseExprSingle();
            conditions.Add(Literal.MakeLiteral(BooleanValue.TRUE));
            actions.Add(defaultExpr);
            Choose choice = new Choose(conditions.ToArray(new Expression[0]), actions.ToArray(new Expression[conditions.Count]));
            outerLet.SetAction(choice);
            if (braced)
            {
                Expect(Token.RCURLY);
                t.LookAhead();
                NextToken();
            }

            return MakeTracer(outerLet, null);
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        //
        //
        protected override Expression ParseValidateExpression()
        {
            int offset = t.currentTokenStartOffset;
            int mode = Validation.STRICT;
            bool foundCurly = false;
            ISchemaType requiredType = null;
            // A validate expression in a non-schema-aware processor is XQST0075 (not XQST0009, which is
            // specific to schema import) — per XQuery 3.1 §2.2.5 / err:XQST0075.
            EnsureSchemaAware("validate expression", "XQST0075");
            switch (t.currentToken)
            {
                case Token.VALIDATE_STRICT:
                    mode = Validation.STRICT;
                    NextToken();
                    break;
                case Token.VALIDATE_LAX:
                    mode = Validation.LAX;
                    NextToken();
                    break;
                case Token.VALIDATE_TYPE:

                    //                if (XQUERY10.equals(queryVersion)) {
                    //                    grumble("validate-as-type requires XQuery 3.0");
                    //                }
                    mode = Validation.BY_TYPE;
                    NextToken();
                    Expect(Token.KEYWORD_CURLY);
                    if (!NameChecker.IsQName(StringTool.CodePoints(t.currentTokenValue)))
                    {
                        Grumble("Schema type name expected after 'validate type");
                    }

                    requiredType = env.GetConfiguration().GetSchemaType(MakeStructuredQName(t.currentTokenValue, env.GetDefaultElementNamespace()));
                    if (requiredType == null)
                    {
                        Grumble("Unknown schema type " + t.currentTokenValue, "XQST0104");
                    }

                    foundCurly = true;
                    break;
                case Token.KEYWORD_CURLY:
                    if (t.currentTokenValue.Equals("validate"))
                    {
                        mode = Validation.STRICT;
                    }
                    else
                    {
                        throw new InvalidOperationException("shouldn't be parsing a validate expression");
                    }

                    foundCurly = true;
                    break;
            }

            if (!foundCurly)
            {
                Expect(Token.LCURLY);
            }

            NextToken();
            Expression exp = ParseExpression();
            if (exp is ParentNodeConstructor)
            {
                ((ParentNodeConstructor)exp).SetValidationAction(mode, mode == Validation.BY_TYPE ? requiredType : null);
            }
            else
            {

                // the expression must return a single element or document node. The type-
                // checking machinery can't handle a union type, so we just check that it's
                // a node for now. Because we are reusing XSLT copy-of code, we need
                // an ad-hoc check that the node is of the right kind.
                // below code moved to XQuery-specific path in CopyOf
                //            try {
                //                RoleLocator role = new RoleLocator(RoleLocator.TYPE_OP, "validate", 0);
                //                setLocation(exp);
                //                exp = config.getTypeChecker().staticTypeCheck(exp,
                //                        SequenceType.SINGLE_NODE,
                //                        false,
                //                        role, ExpressionVisitor.make(env, getExecutable()));
                //            } catch (XPathException err) {
                //            }
                exp = new CopyOf(exp, true, mode, requiredType, true);
                SetLocation(exp);
                ((CopyOf)exp).SetRequireDocumentOrElement(true);
            }

            Expect(Token.RCURLY);
            t.LookAhead(); // always done manually after an RCURLY
            NextToken();
            return MakeTracer(exp, null);
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        //
        //
        protected override Expression ParseExtensionExpression()
        {
            ISchemaType requiredType = null;
            string trimmed = Whitespace.Trim(t.currentTokenValue);
            int c = 0;
            int len = trimmed.Length;
            while (c < len && " \t\r\n".IndexOf(trimmed[c]) < 0)
            {
                c++;
            }

            string qname = trimmed.Substring(0, c);
            string pragmaContents = "";
            while (c < len && " \t\r\n".IndexOf(trimmed[c]) >= 0)
            {
                c++;
            }

            if (c < len)
            {
                pragmaContents = trimmed.Substring(c) /*Java substring(begin,END==length()) -> C# to-end overload*/;
            }

            bool validateType = false;
            StructuredQName pragmaName = MakeStructuredQName(qname, NamespaceUri.NULL);
            NamespaceUri uri = pragmaName.GetNamespaceUri();
            string localName = pragmaName.GetLocalPart();
            if (uri.Equals(NamespaceUri.SAXON))
            {
                if ("validate-type".Equals(localName))
                {
                    if (!env.GetConfiguration().IsLicensedFeature(Configuration.LicenseFeature.ENTERPRISE_XQUERY))
                    {
                        Warning("Ignoring saxon:validate-type. To use this feature " + "you need the Saxon-EE processor from https://www.saxonica.com/", DAXonErrorCode.SXWN9042);
                    }
                    else
                    {
                        string typeName = Whitespace.Trim(pragmaContents);
                        if (!NameChecker.IsQName(StringTool.CodePoints(typeName)))
                        {
                            Grumble("Schema type name expected in saxon:validate-type pragma: found " + Err.Wrap(typeName));
                        }

                        requiredType = env.GetConfiguration().GetSchemaType(MakeStructuredQName(typeName, env.GetDefaultElementNamespace()));
                        if (requiredType == null)
                        {
                            Grumble("Unknown schema type " + typeName);
                        }

                        validateType = true;
                    }
                }
                else
                {
                    Warning("Ignored pragma " + qname + " (unrecognized Saxon pragma)", DAXonErrorCode.SXWN9042);
                }
            }

            NextToken();
            Expression expr;
            if (t.currentToken == Token.PRAGMA)
            {
                expr = ParseExtensionExpression();
            }
            else
            {
                Expect(Token.LCURLY);
                NextToken();
                if (t.currentToken == Token.RCURLY)
                {
                    t.LookAhead(); // always done manually after an RCURLY
                    NextToken();
                    Grumble("Unrecognized pragma, with no fallback expression", "XQST0079");
                }

                expr = ParseExpression();
                Expect(Token.RCURLY);
                t.LookAhead(); // always done manually after an RCURLY
                NextToken();
            }

            if (validateType)
            {
                if (expr is ParentNodeConstructor)
                {
                    ((ParentNodeConstructor)expr).SetValidationAction(Validation.BY_TYPE, requiredType);
                    return expr;
                }
                else if (expr is AttributeCreator)
                {
                    if (!(requiredType is ISimpleType))
                    {
                        Grumble("The type used for validating an attribute must be a simple type");
                    }


                    ((AttributeCreator)expr).SetSchemaType((ISimpleType)requiredType);
                    ((AttributeCreator)expr).SetValidationAction(Validation.BY_TYPE);
                    return expr;
                }
                else
                {
                    CopyOf copy = new CopyOf(expr, true, Validation.BY_TYPE, requiredType, true);
                    copy.SetLocation(MakeLocation());
                    return copy;
                }
            }
            else
            {
                return expr;
            }
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        //
        //
        protected override Expression ParseConstructor()
        {
            int offset = t.currentTokenStartOffset;
            switch (t.currentToken)
            {
                case Token.TAG:
                    Expression tag = ParsePseudoXML(false);
                    LookAhead();
                    t.State = Tokenizer.OPERATOR_STATE;
                    NextToken();
                    return tag;
                case Token.KEYWORD_CURLY:
                    string keyword = t.currentTokenValue;
                    switch (keyword)
                    {
                        case "validate":
                            Grumble("A validate expression is not allowed within a path expression");

                            //if (nodeKind.equals("validate")) {
                            // this allows a validate{} expression to appear as an operand of '/', which the grammar does not allow
                            // return parseValidateExpression();
                            break;
                        case "ordered":
                        case "unordered":

                            // these are currently no-ops in Saxon
                            NextToken();
                            Expression content;
                            if (t.currentToken == Token.RCURLY && allowXPath31Syntax)
                            {
                                content = Literal.MakeEmptySequence();
                            }
                            else
                            {
                                content = ParseExpression();
                            }

                            Expect(Token.RCURLY);
                            LookAhead(); // must be done manually after an RCURLY
                            NextToken();
                            return content;
                        case "document":
                            return ParseDocumentConstructor(offset);
                        case "element":
                            return ParseComputedElementConstructor(offset);
                        case "attribute":
                            return ParseComputedAttributeConstructor(offset);
                        case "text":
                            return ParseTextNodeConstructor(offset);
                        case "comment":
                            return ParseCommentConstructor(offset);
                        case "processing-instruction":
                            return ParseProcessingInstructionConstructor(offset);
                        case "namespace":
                            return ParseNamespaceConstructor(offset);
                        case "switch":
                            return ParseSwitchExpression();
                        default:
                            Grumble("Unrecognized keyword '" + t.currentTokenValue + "' before {...} ");
                            break;
                    }

                    break;
                case Token.ELEMENT_QNAME:
                    return ParseNamedElementConstructor(offset);
                case Token.ATTRIBUTE_QNAME:
                    return ParseNamedAttributeConstructor(offset);
                case Token.NAMESPACE_QNAME:
                    return ParseNamedNamespaceConstructor(offset);
                case Token.PI_QNAME:
                    return ParseNamedProcessingInstructionConstructor(offset);
            }

            return new ErrorExpression();
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        //
        //
        private Expression ParseDocumentConstructor(int offset)
        {
            NextToken();
            Expression content;
            if (t.currentToken == Token.RCURLY && allowXPath31Syntax)
            {
                content = Literal.MakeEmptySequence();
            }
            else
            {
                content = ParseExpression();
            }

            Expect(Token.RCURLY);
            LookAhead(); // must be done manually after an RCURLY
            NextToken();
            DocumentInstr doc = new DocumentInstr(false, null);
            if (!((QueryModule)env).IsPreserveNamespaces())
            {
                content = new CopyOf(content, false, Validation.PRESERVE, null, true);
            }

            doc.SetValidationAction(((QueryModule)env).ConstructionMode, null);
            doc.SetContentExpression(content);
            SetLocation(doc, offset);
            return doc;
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        //
        //
        private Expression ParseComputedElementConstructor(int offset)
        {
            NextToken();

            // get the expression that yields the element name
            Expression name = ParseExpression();
            Expect(Token.RCURLY);
            LookAhead(); // must be done manually after an RCURLY
            NextToken();
            Expect(Token.LCURLY);
            t.State = Tokenizer.DEFAULT_STATE;
            NextToken();
            Expression content = null;
            if (t.currentToken != Token.RCURLY)
            {

                // get the expression that yields the element content
                content = ParseExpression();

                // if the child expression creates another element,
                // suppress validation, as the parent already takes care of it
                if (content is ElementCreator && ((ElementCreator)content).GetSchemaType() == null)
                {
                    ((ElementCreator)content).SetValidationAction(Validation.PRESERVE, null);
                }

                Expect(Token.RCURLY);
            }

            LookAhead(); // done manually after an RCURLY
            NextToken();
            Instruction inst;
            if (name is Literal)
            {
                IGroundedValue vName = ((Literal)name).GroundedValue;

                // if element name is supplied as a literal, treat it like a direct element constructor
                INodeName elemName;
                if (vName is StringValue && !(vName is AnyURIValue))
                {
                    string lex = ((StringValue)vName).GetStringValue();
                    try
                    {
                        QNameParser oldQP = GetQNameParser();
                        SetQNameParser(oldQP.WithUnescaper(null));
                        elemName = MakeNodeName(lex, true);
                        SetQNameParser(oldQP);
                        elemName.ObtainFingerprint(env.GetConfiguration().GetNamePool());
                    }
                    catch (XPathException staticError)
                    {
                        if (staticError.HasErrorCode("XPST0008", "XPST0081"))
                        {
                            staticError.SetErrorCode("XQDY0074");
                        }
                        else if (staticError.HasErrorCode("XPST0003"))
                        {

                            Grumble("Invalid QName in element constructor: " + lex, "XQDY0074", offset);
                            return new ErrorExpression();
                        }

                        staticError.SetLocator(MakeLocation());
                        staticError.SetIsStaticError(false);
                        return new ErrorExpression(new XmlProcessingException(staticError));
                    }
                }
                else if (vName is QualifiedNameValue)
                {
                    NamespaceUri uri = ((QualifiedNameValue)vName).GetNamespaceURI();
                    elemName = new FingerprintedQName("", uri, ((QualifiedNameValue)vName).LocalName);
                    elemName.ObtainFingerprint(env.GetConfiguration().GetNamePool());
                }
                else
                {
                    Grumble("Element name must be either a string or a QName", "XPTY0004", offset);
                    return new ErrorExpression();
                }

                inst = new FixedElement(elemName, ((QueryModule)env).ActiveNamespaceBindings, ((QueryModule)env).IsInheritNamespaces(), true, null, ((QueryModule)env).ConstructionMode);
                if (content == null)
                {
                    content = Literal.MakeEmptySequence();
                }

                if (!((QueryModule)env).IsPreserveNamespaces())
                {
                    content = new CopyOf(content, false, Validation.PRESERVE, null, true);
                }

                ((FixedElement)inst).SetContentExpression(content);
                SetLocation(inst, offset);

                //makeContentConstructor(content, (InstructionWithChildren) inst, offset);
                return MakeTracer(inst, elemName.GetStructuredQName());
            }
            else
            {

                // it really is a computed element constructor: save the namespace context
                INamespaceResolver ns = new NamespaceResolverWithDefault(env.GetNamespaceResolver(), env.GetDefaultElementNamespace());
                inst = new ComputedElement(name, null, null, ((QueryModule)env).ConstructionMode, ((QueryModule)env).IsInheritNamespaces(), true);
                SetLocation(inst);
                if (content == null)
                {
                    content = Literal.MakeEmptySequence();
                }

                if (!((QueryModule)env).IsPreserveNamespaces())
                {
                    content = new CopyOf(content, false, Validation.PRESERVE, null, true);
                }

                ((ComputedElement)inst).SetContentExpression(content);
                SetLocation(inst, offset);

                //makeContentConstructor(content, (InstructionWithChildren) inst, offset);
                return MakeTracer(inst, null);
            }
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        //
        //
        private Expression ParseNamedElementConstructor(int offset)
        {
            INodeName nodeName = MakeNodeName(t.currentTokenValue, true);
            Expression content = null;
            NextToken();
            if (t.currentToken != Token.RCURLY)
            {
                content = ParseExpression();
                Expect(Token.RCURLY);
            }

            LookAhead(); // after an RCURLY
            NextToken();
            FixedElement el2 = new FixedElement(nodeName, ((QueryModule)env).ActiveNamespaceBindings, ((QueryModule)env).IsInheritNamespaces(), true, null, ((QueryModule)env).ConstructionMode);
            SetLocation(el2, offset);
            if (content == null)
            {
                content = Literal.MakeEmptySequence();
            }

            if (!((QueryModule)env).IsPreserveNamespaces())
            {
                content = new CopyOf(content, false, Validation.PRESERVE, null, true);
            }

            el2.SetContentExpression(content);
            return MakeTracer(el2, nodeName.GetStructuredQName());
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        //
        //
        private Expression ParseComputedAttributeConstructor(int offset)
        {
            NextToken();
            Expression name = ParseExpression();
            Expect(Token.RCURLY);
            LookAhead(); // must be done manually after an RCURLY
            NextToken();
            Expect(Token.LCURLY);
            t.State = Tokenizer.DEFAULT_STATE;
            NextToken();
            Expression content = null;
            if (t.currentToken != Token.RCURLY)
            {
                content = ParseExpression();
                Expect(Token.RCURLY);
            }

            LookAhead(); // after an RCURLY
            NextToken();
            if (name is Literal)
            {
                IGroundedValue vName = ((Literal)name).GroundedValue;
                if (vName is StringValue && !(vName is AnyURIValue))
                {
                    string lex = ((StringValue)vName).GetStringValue();
                    if (lex.Equals("xmlns") || lex.StartsWith("xmlns:", StringComparison.Ordinal))
                    {
                        Grumble("Cannot create a namespace using an attribute constructor", "XQDY0044", offset);
                    }

                    INodeName attributeName;
                    try
                    {
                        QNameParser oldQP = GetQNameParser();
                        SetQNameParser(oldQP.WithUnescaper(null));
                        attributeName = MakeNodeName(lex, false);
                        SetQNameParser(oldQP);
                    }
                    catch (XPathException staticError)
                    {
                        staticError.SetLocator(MakeLocation());
                        if (staticError.HasErrorCode("XPST0008", "XPST0081"))
                        {
                            staticError.SetErrorCode("XQDY0074");
                        }
                        else if (staticError.HasErrorCode("XPST0003"))
                        {
                            Grumble("Invalid QName in attribute constructor: " + lex, "XQDY0074", offset);
                            return new ErrorExpression();
                        }

                        throw staticError;
                    }

                    if ((attributeName.GetPrefix().Length == 0) && !attributeName.HasURI(NamespaceUri.NULL))
                    {
                        attributeName = new FingerprintedQName("_", attributeName.GetNamespaceUri(), attributeName.GetLocalPart(), attributeName.Fingerprint);
                    }

                    FixedAttribute fatt = new FixedAttribute(attributeName, Validation.STRIP, null);
                    fatt.SetRejectDuplicates();
                    MakeSimpleContent(content, fatt, offset);
                    return MakeTracer(fatt, null);
                }
                else if (vName is QNameValue)
                {
                    QNameValue qnv = (QNameValue)vName;
                    INodeName attributeName = new FingerprintedQName(qnv.GetPrefix(), qnv.GetNamespaceURI(), qnv.LocalName);
                    attributeName.ObtainFingerprint(env.GetConfiguration().GetNamePool());
                    FixedAttribute fatt = new FixedAttribute(attributeName, Validation.STRIP, null);
                    fatt.SetRejectDuplicates();
                    MakeSimpleContent(content, fatt, offset);
                    return MakeTracer(fatt, null);
                }
            }

            ComputedAttribute att = new ComputedAttribute(name, null, Validation.STRIP, null, true);
            att.SetRejectDuplicates();
            MakeSimpleContent(content, att, offset);
            return MakeTracer(att, null);
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        //
        //
        private Expression ParseNamedAttributeConstructor(int offset)
        {
            string warningMessage = null;
            if (t.currentTokenValue.Equals("xmlns") || t.currentTokenValue.StartsWith("xmlns:", StringComparison.Ordinal))
            {
                warningMessage = "Cannot create a namespace declaration using an attribute constructor";
            }

            INodeName attributeName = MakeNodeName(t.currentTokenValue, false);
            if (!attributeName.HasURI(NamespaceUri.NULL) && (attributeName.GetPrefix().Length == 0))
            {

                // This must be because the name was given as Q{uri}local. Invent a prefix.
                attributeName = new FingerprintedQName("_", attributeName.GetNamespaceUri(), attributeName.GetLocalPart());
            }

            Expression attContent = null;
            NextToken();
            if (t.currentToken != Token.RCURLY)
            {
                attContent = ParseExpression();
                Expect(Token.RCURLY);
            }

            LookAhead(); // after an RCURLY
            NextToken();
            if (warningMessage == null)
            {
                FixedAttribute att2 = new FixedAttribute(attributeName, Validation.STRIP, null);
                att2.SetRejectDuplicates();
                att2.SetRetainedStaticContext(env.MakeRetainedStaticContext());
                MakeSimpleContent(attContent, att2, offset);
                return MakeTracer(att2, attributeName.GetStructuredQName());
            }
            else
            {
                Warning(warningMessage, "XQDY0044");
                return new ErrorExpression(warningMessage, "XQDY0044", false);
            }
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        //
        //
        private Expression ParseTextNodeConstructor(int offset)
        {
            NextToken();
            Expression value;
            if (t.currentToken == Token.RCURLY && allowXPath31Syntax)
            {
                value = Literal.MakeEmptySequence();
            }
            else
            {
                value = ParseExpression();
            }

            Expect(Token.RCURLY);
            LookAhead(); // after an RCURLY
            NextToken();
            Expression select = Stringify(value, true, env);
            ValueOf vof = new ValueOf(select, false, true);
            SetLocation(vof, offset);
            return MakeTracer(vof, null);
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        //
        //
        private Expression ParseCommentConstructor(int offset)
        {
            NextToken();
            Expression value;
            if (t.currentToken == Token.RCURLY && allowXPath31Syntax)
            {
                value = Literal.MakeEmptySequence();
            }
            else
            {
                value = ParseExpression();
            }

            Expect(Token.RCURLY);
            LookAhead(); // after an RCURLY
            NextToken();
            Comment com = new Comment();
            MakeSimpleContent(value, com, offset);
            return MakeTracer(com, null);
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        //
        //
        private Expression ParseProcessingInstructionConstructor(int offset)
        {
            NextToken();
            Expression name = ParseExpression();
            Expect(Token.RCURLY);
            LookAhead(); // must be done manually after an RCURLY
            NextToken();
            Expect(Token.LCURLY);
            t.State = Tokenizer.DEFAULT_STATE;
            NextToken();
            Expression content = null;
            if (t.currentToken != Token.RCURLY)
            {
                content = ParseExpression();
                Expect(Token.RCURLY);
            }

            LookAhead(); // after an RCURLY
            NextToken();
            ProcessingInstruction pi = new ProcessingInstruction(name);
            MakeSimpleContent(content, pi, offset);
            return MakeTracer(pi, null);
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        //
        //
        private Expression ParseNamedProcessingInstructionConstructor(int offset)
        {
            string target = t.currentTokenValue;
            string warningMessage = null;
            if (target.EqualsIgnoreCase("xml"))
            {
                warningMessage = "A processing instruction must not be named 'xml' in any combination of upper and lower case";
            }

            if (!NameChecker.IsValidNCName(target))
            {
                Grumble("Invalid processing instruction name " + Err.Wrap(target));
            }

            Expression piName = new StringLiteral(target);
            Expression piContent = null;
            NextToken();
            if (t.currentToken != Token.RCURLY)
            {
                piContent = ParseExpression();
                Expect(Token.RCURLY);
            }

            LookAhead(); // after an RCURLY
            NextToken();
            if (warningMessage == null)
            {
                ProcessingInstruction pi2 = new ProcessingInstruction(piName);
                MakeSimpleContent(piContent, pi2, offset);
                return MakeTracer(pi2, null);
            }
            else
            {
                Warning(warningMessage, "XQDY0064");
                return new ErrorExpression(warningMessage, "XQDY0064", false);
            }
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        //
        //
        protected override Expression ParseTryCatchExpression()
        {
            if (!allowXPath30Syntax)
            {
                Grumble("try/catch requires XQuery 3.0");
            }

            int offset = t.currentTokenStartOffset;
            NextToken();
            Expression tryExpr;
            if (t.currentToken == Token.RCURLY && allowXPath31Syntax)
            {
                tryExpr = Literal.MakeEmptySequence();
            }
            else
            {
                tryExpr = ParseExpression();
            }

            TryCatch tryCatch = new TryCatch(tryExpr);
            SetLocation(tryCatch, offset);
            Expect(Token.RCURLY);
            LookAhead(); // must be done manually after an RCURLY
            NextToken();
            bool foundOneCatch = false;
            IList<IQNameTest> tests = new List<IQNameTest>();
            while (IsKeyword("catch"))
            {
                tests.Clear();
                foundOneCatch = true;
                bool seenCurly = false;
                do
                {
                    NextToken();
                    string tokv = t.currentTokenValue;
                    switch (t.currentToken)
                    {
                        case Token.NAME:
                            NextToken();
                            tests.Add(MakeQNameTest(Types.Type.ELEMENT, tokv));
                            break;
                        case Token.KEYWORD_CURLY:
                            NextToken();
                            tests.Add(MakeQNameTest(Types.Type.ELEMENT, tokv));
                            seenCurly = true;
                            break;
                        case Token.PREFIX:
                            NextToken();
                            tests.Add(MakeNamespaceTest(Types.Type.ELEMENT, tokv));
                            break;
                        case Token.SUFFIX:
                            NextToken();
                            tokv = t.currentTokenValue;
                            if (t.currentToken == Token.NAME)
                            {
                            }
                            else if (t.currentToken == Token.KEYWORD_CURLY)
                            {

                                // OK
                                seenCurly = true;
                            }
                            else
                            {
                                Grumble("Expected name after '*:'");
                            }

                            NextToken();
                            tests.Add(MakeLocalNameTest(Types.Type.ELEMENT, tokv));
                            break;
                        case Token.STAR:
                        case Token.MULT:
                            NextToken();
                            tests.Add(AnyNodeTest.GetInstance());
                            break;
                        default:
                            Grumble("Unrecognized name test in catch clause at " + Token.tokens[t.currentToken]);
                            return null;
                    }
                }
                while (t.currentToken == Token.UNION && !t.currentTokenValue.Equals("union")); // must be "|" not "union"!
                if (!seenCurly)
                {
                    Expect(Token.LCURLY);
                    NextToken();
                }

                IQNameTest test;
                if (tests.Count == 1)
                {
                    test = tests[0];
                }
                else
                {
                    test = (IQNameTest)new UnionQNameTest(tests);
                }

                catchDepth++;
                Expression catchExpr;
                if (t.currentToken == Token.RCURLY && allowXPath31Syntax)
                {
                    catchExpr = Literal.MakeEmptySequence();
                }
                else
                {
                    catchExpr = ParseExpression();
                }

                tryCatch.AddCatchExpression(test, catchExpr);
                Expect(Token.RCURLY);
                LookAhead(); // must be done manually after an RCURLY
                NextToken();
                catchDepth--;
            }

            if (!foundOneCatch)
            {
                Grumble("After try{}, expected 'catch'");
            }

            return tryCatch;
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        private Expression ParseNamespaceConstructor(int offset)
        {
            if (!allowXPath30Syntax)
            {
                Grumble("Namespace node constructors require XQuery 3.0");
            }

            NextToken();
            Expression nameExpr = ParseExpression();
            Expect(Token.RCURLY);
            LookAhead(); // must be done manually after an RCURLY
            NextToken();
            Expect(Token.LCURLY);
            t.State = Tokenizer.DEFAULT_STATE;
            NextToken();
            Expression content = null;
            if (t.currentToken != Token.RCURLY)
            {
                content = ParseExpression();
                Expect(Token.RCURLY);
            }

            LookAhead(); // after an RCURLY
            NextToken();
            NamespaceConstructor instr = new NamespaceConstructor(nameExpr);
            SetLocation(instr);
            MakeSimpleContent(content, instr, offset);
            return MakeTracer(instr, null);
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        private Expression ParseNamedNamespaceConstructor(int offset)
        {
            if (!allowXPath30Syntax)
            {
                Grumble("Namespace node constructors require XQuery 3.0");
            }

            string target = t.currentTokenValue;
            if (!NameChecker.IsValidNCName(target))
            {
                Grumble("Invalid namespace prefix " + Err.Wrap(target));
            }

            Expression nsName = new StringLiteral(target);
            Expression nsContent = null;
            NextToken();
            if (t.currentToken != Token.RCURLY)
            {
                nsContent = ParseExpression();
                Expect(Token.RCURLY);
            }

            LookAhead(); // after an RCURLY
            NextToken();
            NamespaceConstructor instr = new NamespaceConstructor(nsName);
            MakeSimpleContent(nsContent, instr, offset);
            return MakeTracer(instr, null);
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        protected virtual void MakeSimpleContent(Expression content, SimpleNodeConstructor inst, int offset)
        {
            if (content == null)
            {
                inst.Select = new StringLiteral(StringValue.EMPTY_STRING);
            }
            else
            {
                inst.Select = Stringify(content, false, env);
            }

            SetLocation(inst, offset);
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        private Expression ParsePseudoXML(bool allowEndTag)
        {
            Expression exp;
            int offset = t.inputOffset;

            // we're reading raw characters, so we don't want the currentTokenStartOffset
            char c = t.NextChar();
            switch (c)
            {
                case '!':
                    c = t.NextChar();
                    if (c == '-')
                    {
                        exp = ParseCommentConstructor();
                    }
                    else if (c == '[')
                    {
                        Grumble("A CDATA section is allowed only in element content");
                        return null; // if CDATA were allowed here, we would have already read it
                    }
                    else
                    {
                        Grumble("Expected '--' or '[CDATA[' after '<!'");
                        return null;
                    }

                    break;
                case '?':
                    exp = ParsePIConstructor();
                    break;
                case '/':
                    if (allowEndTag)
                    {
                        StringBuilder sb = new StringBuilder(16);
                        while (true)
                        {
                            c = t.NextChar();
                            if (c == '>')
                            {
                                break;
                            }
                            else if (c == Tokenizer.NUL)
                            {
                                Grumble("Expected '>' after '/'; found end of input");
                            }

                            sb.Append(c);
                        }

                        return new StringLiteral(sb.ToString());
                    }

                    Grumble("Unmatched XML end tag");
                    return new ErrorExpression();
                case Tokenizer.NUL:
                    Grumble("End of input encountered while parsing direct constructor");
                    return new ErrorExpression();
                default:
                    t.UnreadChar();
                    exp = ParseDirectElementConstructor(allowEndTag);
                    break;
            }

            SetLocation(exp, offset);
            return exp;
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        private Expression ParseDirectElementConstructor(bool isNested)
        {
            NamePool pool = env.GetConfiguration().GetNamePool();
            bool changesContext = false;
            int offset = t.inputOffset - 1;

            // we're reading raw characters, so we don't want the currentTokenStartOffset
            char c;
            StringBuilder buff = new StringBuilder(64);
            int namespaceCount = 0;
            while (true)
            {
                c = t.NextChar();
                if (c == ' ' || c == '\n' || c == '\r' || c == '\t' || c == '/' || c == '>')
                {
                    break;
                }
                else if (c == Tokenizer.NUL)
                {
                    Grumble("Found end of input while reading element name in XQuery element constructor");
                }

                buff.Append(c);
            }

            string elname = buff.ToString();
            if ((elname.Length == 0))
            {
                Grumble("Expected element name after '<'");
            }


            //Used LinkedHashMap because it is friendly to retain the order of attributes.
            Dictionary<string, AttributeDetails> attributes = new Dictionary<string, AttributeDetails>(10);
            while (true)
            {

                // loop through the attributes
                // We must process namespace declaration attributes first;
                // their scope applies to all preceding attribute names and values.
                // But finding the delimiting quote of an attribute value requires the
                // XPath expressions to be parsed, because they may contain nested quotes.
                // So we parse in "scanOnly" mode, which ignores any undeclared @namespace
                // prefixes, use the result of this parse to determine the length of the
                // attribute value, save the value, and reparse it when all the @namespace
                // declarations have been dealt with.
                c = SkipSpaces(c);
                if (c == '/' || c == '>')
                {
                    break;
                }
                else if (c == Tokenizer.NUL)
                {
                    Grumble("End of input encountered within element start tag");
                }

                int attOffset = t.inputOffset - 1;
                buff.SetLength(0);

                // read the attribute name
                do
                {
                    buff.Append(c);
                    c = t.NextChar();
                }
                while (c != ' ' && c != '\n' && c != '\r' && c != '\t' && c != '=' && c != Tokenizer.NUL);
                string attName = buff.ToString();
                if (!NameChecker.IsQName(StringTool.CodePoints(attName)))
                {
                    Grumble("Invalid attribute name " + Err.Wrap(attName, Err.ATTRIBUTE));
                }

                c = SkipSpaces(c);
                ExpectChar(c, '=');
                c = t.NextChar();
                c = SkipSpaces(c);
                if (c != '"' && c != '\'')
                {
                    if (c == Tokenizer.NUL)
                    {
                        Grumble("End of input encountered within element start tag");
                    }
                    else
                    {
                        Grumble("Expected ' or \" as attribute delimiter - found '" + c + "'");
                    }
                }

                char delim = c;
                if (c != '"' && c != '\'')
                {
                    Grumble("Expected ' or \" as attribute delimiter - found '" + c + "'");
                }

                bool isNamespace = "xmlns".Equals(attName) || attName.StartsWith("xmlns:", StringComparison.Ordinal);
                int end;
                if (isNamespace)
                {
                    end = MakeNamespaceContent(t.input, t.inputOffset, delim);
                    changesContext = true;
                }
                else
                {
                    Expression avt;
                    try
                    {
                        avt = MakeAttributeContent(t.input, t.inputOffset, delim, true);
                    }
                    catch (XPathException err)
                    {
                        if (!err.HasBeenReported())
                        {
                            Grumble(err.GetMessage());
                        }

                        throw err;
                    }


                    // by convention, this returns the end position when called with scanOnly set
                    end = (int)((Int64Value)((Literal)avt).GroundedValue).LongValue();
                }

                if (end >= t.input.Length)
                {
                    Grumble("Reached end of input while processing attributes in start tag");
                }


                // save the value with its surrounding quotes
                string val = t.input.Substring(t.inputOffset - 1, end - t.inputOffset + 2) /*Java substring(begin,END) -> C# (start,LENGTH)*/;

                // and without
                string rval = t.input.Substring(t.inputOffset, end - t.inputOffset) /*Java substring(begin,END) -> C# (start,LENGTH)*/;

                // account for any newlines found in the value
                // (note, subexpressions between curlies will have been parsed using a different tokenizer)
                string tail = val;
                int pos;
                while ((pos = tail.IndexOf('\n')) >= 0)
                {
                    t.IncrementLineNumber(t.inputOffset - 1 + pos);
                    tail = tail.Substring(pos + 1);
                }

                t.inputOffset = end + 1;
                if (isNamespace)
                {

                    // Processing follows the resolution of bug 5083: doubled curly braces represent single
                    // curly braces, single curly braces are not allowed.
                    StringBuilder sb = new StringBuilder(rval.Length);
                    bool prevDelim = false;
                    bool prevOpenCurly = false;
                    bool prevCloseCurly = false;
                    for (int i = 0; i < rval.Length; i++)
                    {
                        char n = rval[i];
                        if (n == delim)
                        {
                            prevDelim = !prevDelim;
                            if (prevDelim)
                            {
                                continue;
                            }
                        }

                        if (n == '{')
                        {
                            prevOpenCurly = !prevOpenCurly;
                            if (prevOpenCurly)
                            {
                                continue;
                            }
                        }
                        else if (prevOpenCurly)
                        {
                            Grumble("Namespace must not contain an unescaped opening brace", "XQST0022");
                        }

                        if (n == '}')
                        {
                            prevCloseCurly = !prevCloseCurly;
                            if (prevCloseCurly)
                            {
                                continue;
                            }
                        }
                        else if (prevCloseCurly)
                        {
                            Grumble("Namespace must not contain an unescaped closing brace", "XPST0003");
                        }

                        sb.Append(n);
                    }

                    if (prevOpenCurly)
                    {
                        Grumble("Namespace must not contain an unescaped opening brace", "XQST0022");
                    }

                    if (prevCloseCurly)
                    {
                        Grumble("Namespace must not contain an unescaped closing brace", "XPST0003");
                    }

                    rval = sb.ToString();
                    NamespaceUri uri = NamespaceUri.Of(UriLiteral(rval));
                    if (!StandardURIChecker.GetInstance().IsValidURI(uri.ToString()))
                    {
                        Grumble("Namespace must be a valid URI value", "XQST0046");
                    }

                    string prefix;
                    if ("xmlns".Equals(attName))
                    {
                        prefix = "";
                        if (uri.Equals(NamespaceUri.XML))
                        {
                            Grumble("Cannot have the XML namespace as the default namespace", "XQST0070");
                        }
                    }
                    else
                    {
                        prefix = attName.Substring(6);
                        if (prefix.Equals("xml") && !uri.Equals(NamespaceUri.XML))
                        {
                            Grumble("Cannot bind the prefix 'xml' to a namespace other than the XML namespace", "XQST0070");
                        }
                        else if (uri.Equals(NamespaceUri.XML) && !prefix.Equals("xml"))
                        {
                            Grumble("Cannot bind a prefix other than 'xml' to the XML namespace", "XQST0070");
                        }
                        else if (prefix.Equals("xmlns"))
                        {
                            Grumble("Cannot use xmlns as a namespace prefix", "XQST0070");
                        }

                        if (uri.IsEmpty())
                        {
                            if (env.GetConfiguration().XMLVersion == Configuration.XML10)
                            {
                                Grumble("Namespace URI must not be empty", "XQST0085");
                            }
                        }
                    }

                    namespaceCount++;
                    ((QueryModule)env).DeclareActiveNamespace(prefix, uri);
                }

                if (attributes.Get(attName) != null)
                {
                    if (isNamespace)
                    {
                        Grumble("Duplicate namespace declaration " + attName, "XQST0071", attOffset);
                    }
                    else
                    {
                        Grumble("Duplicate attribute name " + attName, "XQST0040", attOffset);
                    }
                }


                //                grumble("Value of xml:id must be a valid NCName", "XQST0082");
                //            }
                AttributeDetails a = new AttributeDetails();
                a.value = val;
                a.startOffset = attOffset;
                attributes.Put(attName, a);

                // on return, the current character is the closing quote
                c = t.NextChar();
                if (!(c == ' ' || c == '\n' || c == '\r' || c == '\t' || c == '/' || c == '>'))
                {
                    Grumble("There must be whitespace after every attribute except the last");
                }
            }

            StructuredQName qName = null;
            if (scanOnly)
            {
                qName = StandardNames.GetStructuredQName(StandardNames.XSL_ELEMENT); // any name will do
            }
            else
            {
                try
                {
                    string[] parts = NameChecker.GetQNameParts(elname);
                    NamespaceUri @namespace = ((QueryModule)env).CheckURIForPrefix(parts[0]);
                    if (@namespace == null)
                    {
                        Grumble("Undeclared prefix in element name " + Err.Wrap(elname, Err.ELEMENT), "XPST0081", offset);
                    }

                    qName = new StructuredQName(parts[0], @namespace, parts[1]);
                }
                catch (QNameException e)
                {
                    Grumble("Invalid element name " + Err.Wrap(elname, Err.ELEMENT), "XPST0003", offset);
                    qName = StandardNames.GetStructuredQName(StandardNames.XSL_ELEMENT); // any name will do
                }
            }

            int validationMode = ((QueryModule)env).ConstructionMode;
            FingerprintedQName fqn = new FingerprintedQName(qName.GetPrefix(), qName.GetNamespaceUri(), qName.GetLocalPart(), pool.AllocateFingerprint(qName.GetNamespaceUri(), qName.GetLocalPart()));
            FixedElement elInst = new FixedElement(fqn, ((QueryModule)env).ActiveNamespaceBindings, ((QueryModule)env).IsInheritNamespaces(), !isNested, null, validationMode);
            SetLocation(elInst, offset);
            IList<Expression> contents = new List<Expression>(10);
            IntHashSet attFingerprints = new IntHashSet(attributes.Count);

            // we've checked for duplicate lexical QNames, but not for duplicate expanded-QNames
            foreach (KeyValuePair<string, AttributeDetails> entry in attributes.EntrySet())
            {
                string attName = entry.Key;
                AttributeDetails a = entry.Value;
                string attValue = a.value;
                int attOffset = a.startOffset;
                if ("xmlns".Equals(attName) || attName.StartsWith("xmlns:", StringComparison.Ordinal))
                {
                }
                else if (scanOnly)
                {
                }
                else
                {
                    INodeName attributeName = null;
                    NamespaceUri attNamespace;
                    try
                    {
                        string[] parts = NameChecker.GetQNameParts(attName);
                        if ((parts[0].Length == 0))
                        {

                            // attributes don't use the default namespace
                            attNamespace = NamespaceUri.NULL;
                        }
                        else
                        {
                            attNamespace = ((QueryModule)env).CheckURIForPrefix(parts[0]);
                        }

                        if (attNamespace == null)
                        {
                            Grumble("Undeclared prefix in attribute name " + Err.Wrap(attName, Err.ATTRIBUTE), "XPST0081", attOffset);
                        }

                        attributeName = new FingerprintedQName(parts[0], attNamespace, parts[1]);
                        int key = attributeName.ObtainFingerprint(pool);
                        if (attFingerprints.Contains(key))
                        {
                            Grumble("Duplicate expanded attribute name " + attName, "XQST0040", attOffset);
                        }

                        attFingerprints.Add(key);
                    }
                    catch (QNameException e)
                    {
                        Grumble("Invalid attribute name " + Err.Wrap(attName, Err.ATTRIBUTE), "XPST0003", attOffset);
                    }

                    FixedAttribute attInst = new FixedAttribute(attributeName, Validation.STRIP, null);
                    SetLocation(attInst);
                    Expression select;
                    try
                    {
                        select = MakeAttributeContent(attValue, 1, attValue[0], false);
                    }
                    catch (XPathException err)
                    {
                        err.SetIsStaticError(true);
                        throw err;
                    }

                    attInst.SetRetainedStaticContext(env.MakeRetainedStaticContext());
                    attInst.Select = select;
                    attInst.SetRejectDuplicates();
                    SetLocation(attInst);
                    contents.Add(MakeTracer(attInst, attributeName.GetStructuredQName()));
                }
            }

            if (c == '/')
            {

                // empty element tag
                ExpectChar(t.NextChar(), '>');
            }
            else
            {
                ReadElementContent(elname, contents);
            }

            Expression[] elk = new Expression[contents.Count];
            for (int i = 0; i < contents.Count; i++)
            {

                if (validationMode != Validation.STRIP)
                {
                    contents[i].SuppressValidation(validationMode);
                }

                elk[i] = contents[i];
            }

            Block block = new Block(elk);
            if (changesContext)
            {
                block.SetRetainedStaticContext(env.MakeRetainedStaticContext());
            }

            elInst.SetContentExpression(block);

            // reset the @in-scope namespaces to what they were before
            for (int n = 0; n < namespaceCount; n++)
            {
                ((QueryModule)env).UndeclareNamespace();
            }

            return MakeTracer(elInst, qName);
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        private Expression MakeAttributeContent(string avt, int start, char terminator, bool scanOnly)
        {
            ILocation loc = MakeLocation();
            IList<Expression> components = new List<Expression>(10);
            int i0, i1, i2, i8, i9, len, last;
            last = start;
            len = avt.Length;
            while (last < len)
            {
                i2 = avt.IndexOf(terminator, last);
                if (i2 < 0)
                {
                    XPathException e = new XPathException("Attribute constructor is not properly terminated");
                    e.SetIsStaticError(true);
                    throw e;
                }

                i0 = avt.IndexOf("{", last);
                i1 = avt.IndexOf("{{", last);
                i8 = avt.IndexOf("}", last);
                i9 = avt.IndexOf("}}", last);
                if ((i0 < 0 || i2 < i0) && (i8 < 0 || i2 < i8))
                {

                    // found end of string
                    AddStringComponent(components, avt, last, i2);

                    // look for doubled quotes, and skip them (for now)
                    if (i2 + 1 < avt.Length && avt[i2 + 1] == terminator)
                    {
                        components.Add(new StringLiteral(terminator + ""));
                        last = i2 + 2; //continue;
                    }
                    else
                    {
                        last = i2;
                        break;
                    }
                } // found a "}"
                else if (i8 >= 0 && (i0 < 0 || i8 < i0))
                {

                    // found a "}"
                    if (i8 != i9)
                    {

                        // a "}" that isn't a "}}"
                        XPathException e = new XPathException("Closing curly brace in attribute value template \"" + avt + "\" must be doubled");
                        e.SetIsStaticError(true);
                        throw e;
                    }

                    AddStringComponent(components, avt, last, i8 + 1);
                    last = i8 + 2;
                } // found a doubled "{{"
                else if (i1 >= 0 && i1 == i0)
                {

                    // found a doubled "{{"
                    AddStringComponent(components, avt, last, i1 + 1);
                    last = i1 + 2;
                } // found a single "{"
                else if (i0 >= 0)
                {

                    // found a single "{"
                    if (i0 > last)
                    {
                        AddStringComponent(components, avt, last, i0);
                    }

                    Expression exp;
                    XPathParser parser = NewParser();
                    ((XQueryParser)parser).executable = executable;
                    parser.SetAllowAbsentExpression(allowXPath31Syntax);
                    parser.SetScanOnly(scanOnly);
                    parser.SetRangeVariableStack(rangeVariables);
                    parser.SetCatchDepth(catchDepth);
                    exp = parser.Parse(avt, i0 + 1, Token.RCURLY, env);
                    if (!scanOnly)
                    {
                        exp = exp.Simplify();
                    }

                    last = parser.GetTokenizer().currentTokenStartOffset + 1;
                    components.Add(MakeStringJoin(exp, env));
                }
                else
                {
                    throw new InvalidOperationException("Internal error parsing direct attribute constructor");
                }
            }


            // if this is simply a prescan, return the position of the end of the
            // AVT, so we can parse it properly later
            if (scanOnly)
            {
                return Literal.MakeLiteral(Int64Value.MakeIntegerValue(last));
            }


            // is it empty?
            if (components.IsEmpty())
            {
                return new StringLiteral(StringValue.EMPTY_STRING);
            }


            // is it a single component?
            if (components.Count == 1)
            {
                return components[0];
            }


            // otherwise, return an expression that concatenates the components
            Expression[] args = new Expression[components.Count];
            args = components.ToArray(args);
            RetainedStaticContext rsc = new RetainedStaticContext(env);
            Expression fn = SystemFunction.MakeCall("concat", rsc, args);
            fn.SetLocation(loc);
            return fn; //return visitor.simplify(fn);
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        private void AddStringComponent(IList<Expression> components, string avt, int start, int end)
        {

            // analyze fixed text within the value of a direct attribute constructor.
            if (start < end)
            {
                StringBuilder sb = new StringBuilder(end - start);
                for (int i = start; i < end; i++)
                {
                    char c = avt[i];
                    switch (c)
                    {
                        case '&':
                            {
                                int semic = avt.IndexOf(';', i);
                                if (semic < 0)
                                {
                                    Grumble("No closing ';' found for entity or character reference");
                                }
                                else
                                {
                                    string entity = avt.Substring(i + 1, semic - i - 1) /*Java substring(begin,END) -> C# (start,LENGTH)*/;
                                    sb.Append(new Unescaper(env.GetConfiguration().ValidCharacterChecker).AnalyzeEntityReference(entity));
                                    i = semic;
                                }

                                break;
                            }

                        case '<':
                            Grumble("The < character must not appear in attribute content");
                            break;
                        case '\n':
                        case '\t':
                            sb.Append(' ');
                            break;
                        case '\r':
                            sb.Append(' ');
                            if (i + 1 < end && avt[i + 1] == '\n')
                            {
                                i++;
                            }

                            break;
                        default:
                            sb.Append(c);
                            break;
                    }
                }

                components.Add(new StringLiteral(sb.ToString()));
            }
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        private int MakeNamespaceContent(string avt, int start, char terminator)
        {
            int i2, len, last;
            last = start;
            len = avt.Length;
            while (last < len)
            {
                i2 = avt.IndexOf(terminator, last);
                if (i2 < 0)
                {
                    XPathException e = new XPathException("Namespace declaration is not properly terminated");
                    e.SetIsStaticError(true);
                    throw e;
                }


                // look for doubled quotes, and skip them (for now)
                if (i2 + 1 < avt.Length && avt[i2 + 1] == terminator)
                {
                    last = i2 + 2; //continue;
                }
                else
                {
                    last = i2;
                    break;
                }
            }


            // return the position of the end of the literal
            return last;
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        private void ReadElementContent(string startTag, IList<Expression> components)
        {
            bool afterEnclosedExpr = false;
            while (true)
            {

                // read all the components of the element value
                StringBuilder text = new StringBuilder(64);
                char c;
                bool containsEntities = false;
                while (true)
                {
                    c = t.NextChar();
                    if (c == '<')
                    {

                        // See if we've got a CDATA section
                        if (t.NextChar() == '!')
                        {
                            if (t.NextChar() == '[')
                            {
                                ReadCDATASection(text);
                                containsEntities = true;
                                continue;
                            }
                            else
                            {
                                t.UnreadChar();
                                t.UnreadChar();
                            }
                        }
                        else
                        {
                            t.UnreadChar();
                        }

                        break;
                    }
                    else if (c == '&')
                    {
                        text.Append(ReadEntityReference());
                        containsEntities = true;
                    }
                    else if (c == '}')
                    {
                        c = t.NextChar();
                        if (c != '}')
                        {
                            Grumble("'}' must be written as '}}' within element content");
                        }

                        text.Append(c);
                    }
                    else if (c == '{')
                    {
                        c = t.NextChar();
                        if (c != '{')
                        {
                            c = '{';
                            break;
                        }

                        text.Append(c);
                    }
                    else if (c == Tokenizer.NUL)
                    {
                        Grumble("Reached end of input while reading XQuery element content");
                    }
                    else
                    {
                        if (!charChecker.Test(c) && !UTF16CharacterSet.IsSurrogate(c))
                        {
                            Grumble("Character code " + c + " is not a valid XML character");
                        }

                        text.Append(c);
                    }
                }

                string textStr = text.ToString();
                if (!(textStr.Length == 0) && (containsEntities | ((QueryModule)env).IsPreserveBoundarySpace() || !Whitespace.IsAllWhite(StringView.Of(textStr))))
                {
                    ValueOf inst = new ValueOf(new StringLiteral(new StringValue(textStr)), false, false);
                    SetLocation(inst);
                    components.Add(inst);
                    afterEnclosedExpr = false;
                }

                if (c == '<')
                {
                    Expression exp = ParsePseudoXML(true);

                    // An end tag can appear here, and is returned as a string value
                    if (exp is StringLiteral)
                    {
                        string endTag = ((StringLiteral)exp).GetString().ToString();
                        if (Whitespace.IsWhite(endTag[0]))
                        {
                            Grumble("End tag contains whitespace before the name");
                        }

                        endTag = Whitespace.Trim(endTag);
                        if (endTag.Equals(startTag))
                        {
                            return;
                        }
                        else
                        {
                            Grumble("End tag </" + endTag + "> does not match start tag <" + startTag + '>', "XQST0118"); // error code allocated by spec bug 11609
                        }
                    }
                    else
                    {
                        components.Add(exp);
                    }
                }
                else
                {

                    // we read an '{' indicating an enclosed expression
                    if (afterEnclosedExpr)
                    {
                        Expression previousComponent = components[components.Count - 1];
                        bool previousComponentIsNodeTest = true;
                        UType previousItemType = previousComponent.GetStaticUType(UType.ANY);
                        previousComponentIsNodeTest = UType.ANY_NODE.Subsumes(previousItemType);
                        if (!previousComponentIsNodeTest)
                        {

                            // Add a zero-length text node, to prevent {"a"}{"b"} generating an intervening space
                            // See tests (qxmp132, qxmp261)
                            ValueOf inst = new ValueOf(new StringLiteral(StringValue.EMPTY_STRING), false, false);
                            SetLocation(inst);
                            components.Add(inst);
                        }
                    }

                    t.UnreadChar();
                    t.State = Tokenizer.DEFAULT_STATE;
                    LookAhead();
                    NextToken();
                    if (t.currentToken == Token.RCURLY && allowXPath31Syntax)
                    {
                        components.Add(Literal.MakeEmptySequence());
                    }
                    else
                    {
                        Expression exp = ParseExpression();
                        if (!((QueryModule)env).IsPreserveNamespaces())
                        {
                            exp = new CopyOf(exp, false, Validation.PRESERVE, null, true);
                        }

                        components.Add(exp);
                        Expect(Token.RCURLY);
                    }

                    afterEnclosedExpr = true;
                }
            }
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        private Expression ParsePIConstructor()
        {
            StringBuilder pi = new StringBuilder(64);
            int firstSpace = -1;
            while (!pi.ToString().EndsWith("?>", StringComparison.Ordinal))
            {
                char c = t.NextChar();
                if (c == Tokenizer.NUL)
                {
                    Grumble("Found end of input while reading processing instruction constructor");
                }

                if (firstSpace < 0 && " \t\r\n".IndexOf(c) >= 0)
                {
                    firstSpace = pi.Length;
                }

                pi.Append(c);
            }

            pi.SetLength(pi.Length - 2);
            string target;
            string data = "";
            if (firstSpace < 0)
            {

                // there is no data part
                target = pi.ToString();
            }
            else
            {

                // trim leading space from the data part, but not trailing space
                target = pi.ToString().Substring(0, firstSpace);
                firstSpace++;
                while (firstSpace < pi.Length && " \t\r\n".IndexOf(pi[firstSpace]) >= 0)
                {
                    firstSpace++;
                }

                data = pi.ToString().Substring(firstSpace);
            }

            if (!NameChecker.IsValidNCName(target))
            {
                Grumble("Invalid processing instruction name " + Err.Wrap(target));
            }

            if (target.EqualsIgnoreCase("xml"))
            {
                Grumble("A processing instruction must not be named 'xml' in any combination of upper and lower case");
            }

            ProcessingInstruction instruction = new ProcessingInstruction(new StringLiteral(target));
            instruction.Select = new StringLiteral(data);
            SetLocation(instruction);
            return instruction;
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        private void ReadCDATASection(StringBuilder cdata)
        {
            char c;

            // CDATA section
            c = t.NextChar();
            ExpectChar(c, 'C');
            c = t.NextChar();
            ExpectChar(c, 'D');
            c = t.NextChar();
            ExpectChar(c, 'A');
            c = t.NextChar();
            ExpectChar(c, 'T');
            c = t.NextChar();
            ExpectChar(c, 'A');
            c = t.NextChar();
            ExpectChar(c, '[');
            while (!cdata.ToString().EndsWith("]]>", StringComparison.Ordinal))
            {
                char cc = t.NextChar();
                if (cc == Tokenizer.NUL)
                {
                    Grumble("No closing ']]>' found for CDATA section");
                }

                cdata.Append(cc);
            }

            cdata.SetLength(cdata.Length - 3);
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        private Expression ParseCommentConstructor()
        {
            char c = t.NextChar();

            // XML-like comment
            ExpectChar(c, '-');
            StringBuilder comment = new StringBuilder(256);
            while (!comment.ToString().EndsWith("--", StringComparison.Ordinal))
            {
                char cc = t.NextChar();
                if (cc == Tokenizer.NUL)
                {
                    Grumble("Reached end of input while reading XML comment constructor");
                }

                comment.Append(cc);
            }

            if (t.NextChar() != '>')
            {
                Grumble("'--' is not permitted in an XML comment");
            }

            string commentText = comment.Substring(0, comment.Length - 2);
            Comment instruction = new Comment();
            instruction.Select = new StringLiteral(new StringValue(commentText.ToString()));
            SetLocation(instruction);
            return instruction;
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        public static Expression Stringify(Expression exp, bool noNodeIfEmpty, IStaticContext env)
        {

            // Compare with XSLLeafNodeConstructor.makeSimpleContentConstructor
            // Fast path if given a string literal
            if (exp is StringLiteral)
            {
                return exp;
            }

            if (exp.LocalRetainedStaticContext == null)
            {
                exp.SetRetainedStaticContext(env.MakeRetainedStaticContext());
            }


            // Atomize the result
            exp = Atomizer.MakeAtomizer(exp, null);

            // Convert each atomic value to a string
            exp = new AtomicSequenceConverter(exp, BuiltInAtomicType.STRING);

            //((AtomicSequenceConverter) exp).allocateConverter(config, false);
            // Join the resulting strings with a separator
            exp = SystemFunction.MakeCall("string-join", exp.GetRetainedStaticContext(), exp, new StringLiteral(StringValue.SINGLE_SPACE));
            if (noNodeIfEmpty)
            {
                ((StringJoin)((SystemFunctionCall)exp).TargetFunction).SetReturnEmptyIfEmpty(true);
            }


            // All that's left for the instruction to do is to construct the right kind of node
            return exp;
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        protected override
        Literal MakeStringLiteral(string token, bool doUnescaping)
        {
            if (doUnescaping)
            {
                StringLiteral lit;
                if (token.IndexOf('&') == -1)
                {
                    lit = new StringLiteral(token);
                }
                else
                {
                    string sb = Unescape(token);
                    lit = new StringLiteral(StringValue.MakeStringValue(sb));
                }

                SetLocation(lit);
                return lit;
            }
            else
            {
                return base.MakeStringLiteral(token, doUnescaping);
            }
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        protected override string Unescape(string token)
        {
            return new Unescaper(env.GetConfiguration().ValidCharacterChecker).Unescape(token);
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        private string ReadEntityReference()
        {
            StringBuilder sb = new StringBuilder(64);
            while (true)
            {
                char c = t.NextChar();
                if (c == ';')
                {
                    break;
                }
                else if (c == Tokenizer.NUL)
                {
                    Grumble("No closing ';' found for entity or character reference");
                    return ""; // to keep the Java compiler happy
                }

                sb.Append(c);
            }

            string entity = sb.ToString();
            return new Unescaper(env.GetConfiguration().ValidCharacterChecker).AnalyzeEntityReference(entity);
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        /// <summary>
        /// Parse a string constructor: introduced in XQuery 3.1
        /// </summary>
        protected override Expression ParseStringConstructor()
        {

            // For legacy reasons (see bug 4208) parsing of string constructors is split
            // rather arbitrarily between the parser and tokenizer. This method is called
            // when the tokenizer has seen the sequence ``[xxxx`{ which it reports as
            // a STRING_CONSTRUCTOR_INITIAL token. At this point it hands over to the parser,
            // which continues by parsing the enclosed expression, and then reading
            // character-by-character to get the literal content outside the enclosed expressions.
            int offset = t.currentTokenStartOffset;
            if (!allowXPath31Syntax)
            {
                throw new XPathException("String constructor expressions require XQuery 3.1");
            }

            IList<Expression> components = new List<Expression>();
            components.Add(new StringLiteral(t.currentTokenValue));
            t.Next();
        outer:
            while (true)
            {
                bool emptyExpression = t.currentToken == Token.RCURLY;
                if (emptyExpression)
                {
                    components.Add(new StringLiteral(StringValue.EMPTY_STRING));
                }
                else
                {
                    Expression enclosed = ParseExpression();
                    Expression stringJoin = SystemFunction.MakeCall("string-join", env.MakeRetainedStaticContext(), enclosed, new StringLiteral(" "));
                    components.Add(stringJoin);
                }

                if (t.currentToken != Token.RCURLY)
                {
                    Grumble("Expected '}' after enclosed expression in string constructor");
                }

                StringBuilder sb = new StringBuilder(256);
                char c = t.NextChar();
                if (c != '`')
                {
                    Grumble("Expected '}`' after enclosed expression in string constructor");
                }

                char prior = (char)0;
                char penult = (char)0;
                bool continueOuter = false;
                while (true)
                {
                    c = t.NextChar();
                    if (c == Tokenizer.NUL)
                    {
                        Grumble("Reached end of input while reading string constructor");
                    }

                    if (prior == '`' && c == '{')
                    {
                        sb.SetLength(sb.Length - 1);
                        components.Add(new StringLiteral(sb.ToString()));
                        t.LookAhead();
                        t.Next();
                        if (t.currentToken == Token.RCURLY)
                        {
                            components.Add(Literal.MakeEmptySequence());
                            sb.SetLength(0);
                            continue;
                        }
                        else
                        {
                            continueOuter = true;
                            break;
                        }
                    }
                    else if (penult == ']' && prior == '`' && c == '`')
                    {
                        sb.SetLength(sb.Length - 2);
                        components.Add(new StringLiteral(sb.ToString()));
                        t.LookAhead();
                        t.Next();
                        continueOuter = false;
                        break;
                    }

                    sb.Append(c);
                    penult = prior;
                    prior = c;
                }

                if (!continueOuter)
                {
                    break;
                }
            }

            Expression[] args = components.ToArray(new Expression[0]);
            Expression result = SystemFunction.MakeCall("concat", env.MakeRetainedStaticContext(), args);
            SetLocation(result, offset);
            return result;
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        /// <summary>
        /// Parse a string constructor: introduced in XQuery 3.1
        /// </summary>
        public virtual string UriLiteral(string @in)
        {
            return Whitespace.Collapse(Unescape(@in)).ToString();
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        /// <summary>
        /// Parse a string constructor: introduced in XQuery 3.1
        /// </summary>
        protected virtual void LookAhead()
        {
            try
            {
                t.LookAhead();
            }
            catch (XPathException err)
            {
                Grumble(err.GetMessage());
            }
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        /// <summary>
        /// Parse a string constructor: introduced in XQuery 3.1
        /// </summary>
        protected override bool AtStartOfRelativePath()
        {
            return t.currentToken == Token.TAG || base.AtStartOfRelativePath(); // "<" after "/" is recognized in XQuery but not in XPath.
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        /// <summary>
        /// Parse a string constructor: introduced in XQuery 3.1
        /// </summary>
        protected override void TestPermittedAxis(int axis, string errorCode)
        {
            base.TestPermittedAxis(axis, errorCode);
            if (axis == AxisInfo.NAMESPACE && language == ParsedLanguage.XQUERY)
            {
                Grumble("The namespace axis is not available in XQuery", errorCode);
            }
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        /// <summary>
        /// Parse a string constructor: introduced in XQuery 3.1
        /// </summary>
        private char SkipSpaces(char c)
        {
            while (c == ' ' || c == '\n' || c == '\r' || c == '\t')
            {
                c = t.NextChar();
            }

            return c;
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        /// <summary>
        /// Parse a string constructor: introduced in XQuery 3.1
        /// </summary>
        private void ExpectChar(char actual, char expected)
        {
            if (actual != expected)
            {
                Grumble("Expected '" + expected + "', found " + (actual == Tokenizer.NUL ? "end of input" : "'" + actual + "'"));
            }
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        /// <summary>
        /// Parse a string constructor: introduced in XQuery 3.1
        /// </summary>
        /// <summary>
        /// Get the current language (XPath or XQuery)
        /// </summary>
        protected override string GetLanguage()
        {
            return "XQuery";
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        private class SortSpec
        {
            public Expression sortKey;
            public bool ascending;
            public bool emptyLeast;
            public string collation;
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        public class Unescaper
        {
            private readonly IIntPredicateProxy characterChecker;
            public Unescaper(IIntPredicateProxy characterChecker)
            {
                this.characterChecker = characterChecker;
            }

            public virtual string Unescape(string token)
            {
                StringBuilder sb = new StringBuilder(token.Length);
                for (int i = 0; i < token.Length; i++)
                {
                    char c = token[i];
                    if (c == '&')
                    {
                        int semic = token.IndexOf(';', i);
                        if (semic < 0)
                        {
                            throw new XPathException("No closing ';' found for entity or character reference", "XPST0003");
                        }
                        else
                        {
                            string entity = token.Substring(i + 1, semic - i - 1) /*Java substring(begin,END) -> C# (start,LENGTH)*/;
                            sb.Append(AnalyzeEntityReference(entity));
                            i = semic;
                        }
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }

                return sb.ToString();
            }

            public virtual string AnalyzeEntityReference(string entity)
            {
                if ("lt".Equals(entity))
                {
                    return "<";
                }
                else if ("gt".Equals(entity))
                {
                    return ">";
                }
                else if ("amp".Equals(entity))
                {
                    return "&";
                }
                else if ("quot".Equals(entity))
                {
                    return "\"";
                }
                else if ("apos".Equals(entity))
                {
                    return "'";
                }
                else if (entity.Length < 2 || entity[0] != '#')
                {
                    throw new XPathException("invalid character reference &" + entity + ';', "XPST0003");
                }
                else
                {

                    //entity = entity.toLowerCase();
                    return ParseCharacterReference(entity);
                }
            }

            private string ParseCharacterReference(string entity)
            {
                int value = 0;
                if (entity[1] == 'x')
                {
                    if (entity.Length < 3)
                    {
                        throw new XPathException("No hex digits in hexadecimal character reference", "XPST0003");
                    }

                    entity = entity.ToLowerCase();
                    for (int i = 2; i < entity.Length; i++)
                    {
                        int digit = "0123456789abcdef".IndexOf(entity[i]);
                        if (digit < 0)
                        {
                            throw new XPathException("Invalid hex digit '" + entity[i] + "' in character reference", "XPST0003");
                        }

                        value = (value * 16) + digit;
                        if (value > UTF16CharacterSet.NONBMP_MAX)
                        {
                            throw new XPathException("Character reference exceeds Unicode codepoint limit", "XQST0090");
                        }
                    }
                }
                else
                {
                    for (int i = 1; i < entity.Length; i++)
                    {
                        int digit = "0123456789".IndexOf(entity[i]);
                        if (digit < 0)
                        {
                            throw new XPathException("Invalid digit '" + entity[i] + "' in decimal character reference", "XPST0003");
                        }

                        value = (value * 10) + digit;
                        if (value > UTF16CharacterSet.NONBMP_MAX)
                        {
                            throw new XPathException("Character reference exceeds Unicode codepoint limit", "XQST0090");
                        }
                    }
                }

                if (!characterChecker.Test(value))
                {
                    throw new XPathException("Invalid XML character reference x" + (value).ToString("x"), "XQST0090");
                }


                // following code borrowed from AElfred
                // Check for surrogates: 00000000 0000xxxx yyyyyyyy zzzzzzzz
                //  (1101|10xx|xxyy|yyyy + 1101|11yy|zzzz|zzzz:
                if (value <= 0x0000ffff)
                {

                    // no surrogates needed
                    return "" + (char)value;
                }
                else
                {
                    value -= 0x10000;

                    // > 16 bits, surrogate needed
                    return "" + (char)(0xd800 | (value >> 10)) + (char)(0xdc00 | (value & 0x0003ff));
                }
            }
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        /// <summary>
        /// Parse a string constructor: introduced in XQuery 3.1
        /// </summary>
        /// <summary>
        /// Get the current language (XPath or XQuery)
        /// </summary>
        private class AttributeDetails
        {
            public string value;
            public int startOffset;
        }

        /// <summary>
        /// Callback to tailor the tokenizer
        /// </summary>
        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        /// <summary>
        /// Parse a string constructor: introduced in XQuery 3.1
        /// </summary>
        /// <summary>
        /// Get the current language (XPath or XQuery)
        /// </summary>
        private class Import
        {
            public NamespaceUri namespaceURI;
            public IList<string> locationURIs;
            public int offset;
        }
    }
}
