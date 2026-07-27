////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Regex;
using OutSmart.DAXon.Resources;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation.Rules;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Trees.Tiny;
using OutSmart.DAXon.Trees.Wrappers;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Internal.Net;
using OutSmart.DAXon.Internal.Functional;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
using OutSmart.DAXon.Internal.Jaxp.Transform;
using OutSmart.DAXon.Internal.Jaxp.Transform.Stream;
using OutSmart.DAXon.Internal.Streams;

namespace OutSmart.DAXon.Core
{
    //@CSharpInjectMembers(code = {
    //        "    public void setErrorReporter(global::System.Action<Saxon.Hej.s9api.IXmlProcessingError> reporter) {"
    //                + "        setErrorReporter(new Saxon.Impl.Helpers.ErrorReportingAction(reporter));"
    //                + "    }"
    //})
    public class Controller : IContextOriginator
    {
        public const string ANONYMOUS_PRINCIPAL_OUTPUT_URI = "dummy:/anonymous/principal/result";
        private readonly Configuration config;
        protected Executable executable;
        protected IItem globalContextItem;
        private bool globalContextItemPreset;
        private Dictionary<PackageData, Bindery> binderies;
        private GlobalParameterSet globalParameters;
        private bool convertParameters = true;
        private readonly Dictionary<GlobalVariable, HashSet<GlobalVariable>> globalVariableDependencies = new Dictionary<GlobalVariable, HashSet<GlobalVariable>>();
        protected ITraceListener traceListener;
        private bool tracingPaused;
        private Logger traceFunctionDestination;
        private IResourceResolver resourceResolver;
        protected IReceiver principalResult;
        protected string principalResultURI;
        private IUnparsedTextURIResolver unparsedTextResolver;
        private string defaultCollectionURI;
        private IErrorReporter errorReporter;
        private TreeModel treeModel = TreeModel.TINY_TREE;
        private DocumentPool sourceDocumentPool;
        private IntHashMap<Dictionary<long, KeyIndex>> localIndexes;
        private Dictionary<string, object> userDataTable;
        private NodeInfo lastRememberedNode = null;
        private int lastRememberedNumber = -1;
        private DateTimeValue currentDateTime;
        private bool dateTimePreset = false;
        private PathMap pathMap = null;
        protected int validationMode = Validation.DEFAULT;
        protected bool inUse = false;
        private bool stripSourceTrees = true;
        private ICollectionFinder collectionFinder = null;
        private StylesheetCache stylesheetCache = null;
        private Func<ISequenceIterator, FocusTrackingIterator> focusTrackerFactory = (iter => new FocusTrackingIterator(iter));
        private Func<ISequenceIterator, FocusTrackingIterator> multiThreadedFocusTrackerFactory;

        // --- Cooperative transformation deadline ------------------------------------------------
        // Hot evaluation loops (xsl:for-each, the tail-call trampoline, XPath 'for') call
        // CheckTimeout(). When a deadline is armed and passes, a clean SXTO0001 dynamic error
        // unwinds the stack, releasing every lock - unlike Thread.Abort, which would leave the
        // shared Processor/NamePool corrupt. The fast path (no deadline) is a single bool test, so
        // a run with no time limit pays nothing and its output stays byte-for-byte unchanged.
        private bool hasDeadline;
        private long deadlineTimestamp;      // Stopwatch timestamp at which to abort
        private int timeoutCountdown;        // throttle: sample the clock only every stride calls
        private TimeSpan timeoutSetting;     // remembered for the diagnostic message only
        private const int TimeoutCheckStride = 4096;

        // The controller whose deadline is active on the current thread, set when a deadline is
        // armed (once per run). Lets loops with no XPathContext at hand - notably iterating a
        // constant-folded integer range - honour the deadline via the static CheckActiveTimeout().
        // A transformation is single-threaded, so a plain per-thread field is safe; nested runs on
        // one thread (fn:load-xquery-module) inherit the same deadline, so the value stays correct.
        [ThreadStatic]
        private static Controller activeOnThread;

        /// <summary>
        /// Honour the current thread's active transformation deadline from a context-less loop.
        /// </summary>
        public static void CheckActiveTimeout()
        {
            activeOnThread?.CheckTimeout();
        }

        /// <summary>
        /// Arm - or, for a non-positive TimeSpan, clear - a wall-clock deadline for this
        /// transformation. Measured from the moment of this call, so it is armed once per run.
        /// When armed, hot evaluation loops abort with SXTO0001 once the limit is exceeded.
        /// </summary>
        public virtual void SetTimeout(TimeSpan timeout)
        {
            activeOnThread = this;   // this controller now owns the deadline on the running thread
            if (timeout <= TimeSpan.Zero)
            {
                hasDeadline = false;
                return;
            }

            timeoutSetting = timeout;
            long ticksFromNow = (long)(timeout.TotalSeconds * System.Diagnostics.Stopwatch.Frequency);
            deadlineTimestamp = System.Diagnostics.Stopwatch.GetTimestamp() + ticksFromNow;
            timeoutCountdown = TimeoutCheckStride;
            hasDeadline = true;
        }

        /// <summary>
        /// Called from hot evaluation loops. Returns immediately unless a deadline is armed and has
        /// passed, in which case it throws SXTO0001. The clock is sampled only every stride calls,
        /// so the per-iteration cost is a decrement and a branch.
        /// </summary>
        public void CheckTimeout()
        {
            if (!hasDeadline)
            {
                return;
            }

            if (--timeoutCountdown > 0)
            {
                return;
            }

            timeoutCountdown = TimeoutCheckStride;
            if (System.Diagnostics.Stopwatch.GetTimestamp() >= deadlineTimestamp)
            {
                ThrowTimeout();
            }
        }

        private void ThrowTimeout()
        {
            throw new XPathException(
                "Transformation exceeded its time limit of " + timeoutSetting.TotalSeconds + "s", DAXonErrorCode.SXTO0001);
        }

        /// <summary>
        /// Adopt an already-armed deadline from a parent controller. Used when a nested execution
        /// is spun up mid-run on its own controller (fn:load-xquery-module): the absolute deadline
        /// is shared, not restarted, so a nested module cannot buy itself a fresh time budget.
        /// A parent with no deadline clears this one.
        /// </summary>
        public void InheritDeadlineFrom(Controller parent)
        {
            activeOnThread = this;   // the nested run now owns the deadline on the running thread
            if (parent == null || !parent.hasDeadline)
            {
                hasDeadline = false;
                return;
            }

            deadlineTimestamp = parent.deadlineTimestamp;
            timeoutSetting = parent.timeoutSetting;
            timeoutCountdown = TimeoutCheckStride;
            hasDeadline = true;
        }

        /// <summary>
        /// Claim the current thread's active-deadline slot for a COMPILE scope: constant folding
        /// can evaluate attacker-sized work (sum(1 to 2000000000)) before any transformation - and
        /// so any run-time deadline - exists. Arms the Processor's limit when the configuration has
        /// one, else leaves the scope unlimited. Returns the previous owner, which the caller MUST
        /// restore (try/finally): a compile nested inside a running transformation (fn:transform)
        /// hands the slot back to that run's deadline on exit.
        /// </summary>
        internal static Controller ArmThreadDeadline(Configuration config)
        {
            Controller previous = activeOnThread;
            if (config.GetProcessor() is OutSmart.DAXon.Api.Processor p)
            {
                new Controller(config).SetTimeout(p.TransformTimeout);
            }
            else
            {
                activeOnThread = null;
            }

            return previous;
        }

        internal static void RestoreThreadDeadline(Controller previous)
        {
            activeOnThread = previous;
        }

        public virtual string BaseOutputURI
        {
            get => principalResultURI; set
            {
                principalResultURI = value;
            }
        }

        public virtual IReceiver PrincipalResult => principalResult;

        public virtual IErrorReporter ErrorReporter
        {
            get => errorReporter; set
            {
                errorReporter = value;
            }
        }

        public virtual IItem GlobalContextItem
        {
            get => globalContextItem; set
            {
                SetGlobalContextItem(value, false);
            }
        }

        public virtual IResourceResolver ResourceResolver
        {
            get => resourceResolver; set
            {
                resourceResolver = value;
            }
        }

        public virtual IUnparsedTextURIResolver UnparsedTextURIResolver
        {
            get => unparsedTextResolver; set
            {
                unparsedTextResolver = value;
            }
        }

        public virtual int SchemaValidationMode
        {
            get => validationMode; set
            {
                this.validationMode = value;
            }
        }

        public virtual TreeModel Model
        {
            get => treeModel; set
            {
                treeModel = value;
            }
        }

        public virtual ISpaceStrippingRule SpaceStrippingRule
        {
            get
            {
                if (config.GetParseOptions().SpaceStrippingRule == AllElementsSpaceStrippingRule.GetInstance())
                {
                    return AllElementsSpaceStrippingRule.GetInstance();
                }
                else if (executable is PreparedStylesheet)
                {
                    ISpaceStrippingRule rule = ((PreparedStylesheet)executable).GetTopLevelPackage().SpaceStrippingRule;
                    if (rule != null)
                    {
                        return rule;
                    }
                }

                return NoElementsSpaceStrippingRule.GetInstance();
            }
        }

        public virtual Logger TraceFunctionDestination
        {
            get => traceFunctionDestination; set
            {
                traceFunctionDestination = value;
            }
        }

        public virtual IntHashMap<Dictionary<long, KeyIndex>> LocalIndexes
        {
            get
            {
                lock (this)
                {
                    if (localIndexes == null)
                    {
                        localIndexes = new IntHashMap<Dictionary<long, KeyIndex>>();
                    }

                    return localIndexes;
                }
            }
        }

        public virtual PathMap PathMapForDocumentProjection => pathMap;
        public Controller(Configuration config)
        {
            this.config = config;

            // create a dummy executable
            executable = new Executable(config);
            sourceDocumentPool = new DocumentPool();
            Reset();
        }

        public Controller(Configuration config, Executable executable)
        {
            this.config = config;
            this.executable = executable;
            sourceDocumentPool = new DocumentPool();
            Reset();
        }

        public virtual void Reset()
        {
            globalParameters = new GlobalParameterSet();
            focusTrackerFactory = config.GetFocusTrackerFactory(executable, false);
            multiThreadedFocusTrackerFactory = config.GetFocusTrackerFactory(executable, true);

            resourceResolver = null;
            unparsedTextResolver = config.UnparsedTextURIResolver;
            validationMode = config.SchemaValidationMode;
            errorReporter = config.MakeErrorReporter();
            traceListener = null;
            traceFunctionDestination = config.Logger;
            ITraceListener tracer;
            try
            {
                tracer = config.MakeTraceListener();
            }
            catch (XPathException err)
            {
                throw new InvalidOperationException(err.GetMessage());
            }

            if (tracer != null)
            {
                AddTraceListener(tracer);
            }

            Model = config.GetParseOptions().Model;
            globalContextItem = null;
            currentDateTime = null;
            dateTimePreset = false;
            ClearPerTransformationData();
        }

        protected virtual void ClearPerTransformationData()
        {
            lock (this)
            {
                userDataTable = new Dictionary<string, object>(20);
                principalResult = null;
                tracingPaused = false;
                lastRememberedNode = null;
                lastRememberedNumber = -1;
                stylesheetCache = null;
                localIndexes = null;
                if (!globalContextItemPreset)
                {
                    globalContextItem = null;
                }
            }
        }

        public virtual Configuration GetConfiguration()
        {
            return config;
        }

        public virtual ISequence GetParameter(StructuredQName name)
        {
            return globalParameters[name];
        }

        public virtual IGroundedValue GetConvertedParameter(StructuredQName name, Values.SequenceType requiredType, IXPathContext context)
        {
            IGroundedValue val = globalParameters.ConvertParameterValue(name, requiredType, convertParameters, context);
            if (val != null)
            {

                // Check that any nodes belong to the right configuration
                Configuration config = GetConfiguration();
                ISequenceIterator iter = val.Iterate();
                IItem next;
                while ((next = iter.Next()) != null)
                {
                    if (next is NodeInfo && !config.IsCompatible(((NodeInfo)next).GetConfiguration()))
                    {
                        throw new XPathException("A node supplied in a global parameter must be built using the same Configuration " + "that was used to compile the stylesheet or query", DAXonErrorCode.SXXP0004);
                    }
                }


                // If the supplied value is a document node, and the document node has a systemID that is an absolute
                // URI, and the absolute URI does not already exist in the document pool, then register it in the document
                // pool, so that the document-uri() function will find it there, and so that a call on doc() will not
                // reload it.
                if (val is NodeInfo && ((NodeInfo)val).GetNodeKind() == Types.Type.DOCUMENT)
                {
                    string systemId = ((NodeInfo)val).Root.GetSystemId();
                    try
                    {
                        if (systemId != null && new URI(systemId).IsAbsolute())
                        {
                            DocumentKey key = new DocumentKey(systemId);
                            DocumentPool pool = GetDocumentPool();
                            if (pool.Find(key) == null)
                            {
                                pool.Add(((NodeInfo)val).GetTreeInfo(), key);
                            }
                        }
                    }
                    catch (URISyntaxException err)
                    {
                    }
                }

                val = val.Materialize();
            }

            return val;
        }

        public virtual SequenceCollector AllocateSequenceOutputter()
        {
            PipelineConfiguration pipe = MakePipelineConfiguration();
            return new SequenceCollector(pipe, 20);
        }

        public virtual SequenceCollector AllocateSequenceOutputter(int size)
        {
            PipelineConfiguration pipe = MakePipelineConfiguration();
            return new SequenceCollector(pipe, size);
        }

        public virtual PipelineConfiguration MakePipelineConfiguration()
        {
            ParseOptions parseOptions = GetConfiguration().GetParseOptions().WithSchemaValidationMode(validationMode).WithErrorReporter(errorReporter);
            PipelineConfiguration pipe = new PipelineConfiguration(GetConfiguration(), parseOptions);
            pipe.SetController(this);
            return pipe;
        }

        public virtual void ReportFatalError(XPathException err)
        {
            if (!err.HasBeenReported())
            {
                if (err.GetHostLanguage() == null)
                {
                    if (executable.GetHostLanguage() == HostLanguage.XSLT)
                    {
                        err.SetHostLanguage("XSLT");
                    }
                    else if (executable.GetHostLanguage() == HostLanguage.XQUERY)
                    {
                        err.SetHostLanguage("XQuery");
                    }
                }

                ErrorReporter.Report(new XmlProcessingException(err));
                err.SetHasBeenReported(true);
            }
        }

        public virtual void Warning(string message, string errorCode, ILocation locator)
        {
            if (locator == null)
            {
                locator = Loc.NONE;
            }

            if (errorCode == null)
            {
                errorCode = DAXonErrorCode.SXWN9000;
            }

            if (message == null)
            {
                message = "Unspecified warning";
            }

            XmlProcessingIncident warning = new XmlProcessingIncident(message, errorCode, locator).AsWarning();
            errorReporter.Report(warning);
        }

        protected virtual void HandleXPathException(XPathException err)
        {
            // The direct XmlReader parse path never wraps a SAX SAXParseException, so the historic
            // "unwrap a Crimson-wrapped runtime exception" branch is unreachable: always report and rethrow.
            ReportFatalError(err);
            throw err;
        }

        public virtual Executable GetExecutable()
        {
            return executable;
        }

        public virtual DocumentPool GetDocumentPool()
        {
            return sourceDocumentPool;
        }

        public virtual void ClearDocumentPool()
        {
            foreach (PackageData pack in GetExecutable().Packages)
            {
                sourceDocumentPool.DiscardIndexes(pack.GetKeyManager());
            }

            sourceDocumentPool = new DocumentPool();
        }

        public virtual Bindery GetBindery(PackageData packageData)
        {
            lock (this)
            {
                Bindery b = binderies.Get(packageData);
                if (b == null)
                {
                    b = new Bindery(packageData);
                    binderies.Put(packageData, b);
                }

                return b;
            }
        }

        public virtual void SetGlobalContextItem(IItem contextItem, bool alreadyStripped)
        {
            if (!alreadyStripped)
            {

                // Bug 2929 - don't do space-stripping twice
                if (globalContextItem is SpaceStrippedNode && ((SpaceStrippedNode)globalContextItem).UnderlyingNode == contextItem)
                {
                    return;
                }

                if (contextItem is NodeInfo)
                {

                    // In XSLT, apply strip-space and strip-type-annotations options
                    NodeInfo node = (NodeInfo)contextItem;
                    contextItem = PrepareInputTree(node);
                    if (node.GetNodeKind() == Types.Type.DOCUMENT && node.GetSystemId() != null)
                    {
                        DocumentKey key = new DocumentKey(node.GetSystemId());
                        if (GetDocumentPool().Find(key) == null)
                        {
                            GetDocumentPool().Add(node.GetTreeInfo(), key);
                        }
                    }
                }
            }

            if (contextItem is NodeInfo)
            {
                NodeInfo startNode = (NodeInfo)contextItem;
                if (startNode.GetConfiguration() == null)
                {

                    // must be a non-standard document implementation
                    throw new XPathException("The supplied source document must be associated with a Configuration");
                }

                if (!startNode.GetConfiguration().IsCompatible(executable.GetConfiguration()))
                {
                    throw new XPathException("Source document and stylesheet must use the same or compatible Configurations", DAXonErrorCode.SXXP0004);
                }

                if (startNode.GetTreeInfo().IsTyped() && !executable.IsSchemaAware())
                {
                    throw new XPathException("Cannot use a schema-validated source document unless the stylesheet is schema-aware");
                }
            }

            this.globalContextItem = contextItem;
            this.globalContextItemPreset = true;
        }

        public virtual void ClearGlobalContextItem()
        {
            this.globalContextItem = null;
            this.globalContextItemPreset = false;
        }

        public virtual ICollectionFinder GetCollectionFinder()
        {
            if (collectionFinder == null)
            {
                collectionFinder = config.CollectionFinder;
            }

            return collectionFinder;
        }

        public virtual void SetCollectionFinder(ICollectionFinder cf)
        {
            collectionFinder = cf;
        }

        public virtual void SetDefaultCollection(string uri)
        {
            defaultCollectionURI = uri;
        }

        public virtual string GetDefaultCollection()
        {
            return defaultCollectionURI == null ? GetConfiguration().DefaultCollection : defaultCollectionURI;
        }

        public virtual Builder MakeBuilder()
        {
            Builder b = treeModel.MakeBuilder(MakePipelineConfiguration());
            b.SetTiming(config.IsTiming());
            b.SetLineNumbering(config.IsLineNumbering());
            return b;
        }

        public virtual void SetStripSourceTrees(bool strip)
        {
            stripSourceTrees = strip;
        }

        public virtual bool IsStripSourceTree()
        {
            return stripSourceTrees;
        }

        protected virtual bool IsStylesheetContainingStripSpace()
        {
            ISpaceStrippingRule rule;
            return executable is PreparedStylesheet && (rule = ((PreparedStylesheet)executable).GetTopLevelPackage().SpaceStrippingRule) != null && rule != NoElementsSpaceStrippingRule.GetInstance();
        }

        public virtual bool IsStylesheetStrippingTypeAnnotations()
        {
            return executable is PreparedStylesheet && ((PreparedStylesheet)executable).GetTopLevelPackage().IsStripsTypeAnnotations();
        }

        public virtual Stripper MakeStripper(IReceiver next)
        {
            if (next == null)
            {
                next = new Sink(MakePipelineConfiguration());
            }

            return new Stripper(SpaceStrippingRule, next);
        }

        public virtual void RegisterDocument(ITreeInfo doc, DocumentKey uri)
        {
            if (!GetExecutable().IsSchemaAware() && !Untyped.INSTANCE.Equals(doc.GetRootNode().GetSchemaType()))
            {
                bool isXSLT = GetExecutable().GetHostLanguage() == HostLanguage.XSLT;
                string message;
                if (isXSLT)
                {
                    message = "The source document has been schema-validated, but" + " the stylesheet is not schema-aware. A stylesheet is schema-aware if" + " either (a) it contains an xsl:import-schema declaration, or (b) the stylesheet compiler" + " was configured to be schema-aware.";
                }
                else
                {
                    message = "The source document has been schema-validated, but" + " the query is not schema-aware. A query is schema-aware if" + " either (a) it contains an 'import schema' declaration, or (b) the query compiler" + " was configured to be schema-aware.";
                }

                throw new XPathException(message);
            }

            if (uri != null)
            {
                sourceDocumentPool.Add(doc, uri);
            }
        }

        public virtual RuleManager GetRuleManager()
        {
            Executable exec = GetExecutable();
            return exec is PreparedStylesheet ? ((PreparedStylesheet)GetExecutable()).GetRuleManager() : null;
        }

        public virtual void SetTraceListener(ITraceListener listener)
        {
            this.traceListener = listener;
        }

        public virtual ITraceListener GetTraceListener()
        {
            return traceListener;
        }

        public bool IsTracing()
        {
            return traceListener != null && !tracingPaused;
        }

        public void PauseTracing(bool pause)
        {
            tracingPaused = pause;
        }

        public virtual void AddTraceListener(ITraceListener trace)
        {
            if (trace != null)
            {
                traceListener = (ITraceListener)TraceEventMulticaster.Add(traceListener, trace);
            }
        }

        public virtual void OpenTraceEpisode()
        {
            if (traceListener != null)
            {
                traceListener.Open(this);
            }
        }

        public virtual void CloseTraceEpisode()
        {
            if (traceListener != null)
            {
                traceListener.Dispose();
            }
        }

        public virtual void RemoveTraceListener(ITraceListener trace)
        {
            traceListener = (ITraceListener)TraceEventMulticaster.Remove(traceListener, trace);
        }

        public virtual void InitializeController(GlobalParameterSet @params)
        {

            // get a new bindery, to clear out any variables from previous runs
            binderies = new Dictionary<PackageData, Bindery>();

            // if parameters were supplied, set them up
            try
            {
                executable.CheckSuppliedParameters(@params);
            }
            catch (XPathException e)
            {
                if (!e.HasBeenReported())
                {
                    ErrorReporter.Report(new XmlProcessingException(e));
                    throw e;
                }
            }

            globalParameters = @params;

            // Check the global context item
            globalContextItem = executable.CheckInitialContextItem(globalContextItem, NewXPathContext());
            if (traceListener != null)
            {

                //traceListener.open(this);
                PreEvaluateGlobals(NewXPathContext());
            }
        }

        public virtual void SetApplyFunctionConversionRulesToExternalVariables(bool applyConversionRules)
        {
            convertParameters = applyConversionRules; //topLevelBindery.setApplyFunctionConversionRulesToExternalVariables(applyConversionRules);
        }

        public virtual object GetUserData(object key, string name)
        {
            lock (this)
            {
                string keyValue = key.GetHashCode() + " " + name;

                return userDataTable.Get(keyValue);
            }
        }

        public virtual void SetUserData(object key, string name, object data)
        {
            lock (this)
            {

                string keyVal = key.GetHashCode() + " " + name;
                if (data == null)
                {
                    userDataTable.Remove(keyVal);
                }
                else
                {
                    userDataTable.Put(keyVal, data);
                }
            }
        }

        public virtual void SetRememberedNumber(NodeInfo node, int number)
        {
            lock (this)
            {
                lastRememberedNode = node;
                lastRememberedNumber = number;
            }
        }

        public virtual int GetRememberedNumber(NodeInfo node)
        {
            lock (this)
            {
                if (lastRememberedNode == node)
                {
                    return lastRememberedNumber;
                }

                return -1;
            }
        }

        protected virtual void CheckReadiness()
        {
            if (inUse)
            {
                throw new InvalidOperationException("The Controller is being used recursively or concurrently. This is not permitted.");
            }

            if (binderies == null)
            {
                throw new InvalidOperationException("The Controller has not been initialized");
            }

            inUse = true;
            ClearPerTransformationData();
            if (executable == null)
            {
                throw new XPathException("Stylesheet has not been prepared");
            }

            if (!dateTimePreset)
            {
                currentDateTime = null; // reset at start of each transformation
            }
        }

        public virtual NodeInfo MakeSourceTree(IActiveSource source, int validationMode)
        {
            if (source is NodeSource)
            {
                return ((NodeSource)source).Node;
            }
            else if (source is ITreeInfo)
            {
                return ((ITreeInfo)source).GetRootNode();
            }
            else if (source is NodeInfo)
            {
                return ((NodeInfo)source);
            }
            else if (source is ActiveStreamSource ass && source.GetSystemId() != null && ass.IsStreamless)
            {

                // Check to see if the document is already in the document pool. This can happen when a Transformer
                // is reused to perform multiple transformations on the same source document. Bug 4837.
                DocumentKey key = new DocumentKey(source.GetSystemId());
                ITreeInfo existing = sourceDocumentPool.Find(key);
                if (existing != null)
                {
                    return existing.GetRootNode();
                }
            }

            Builder sourceBuilder = MakeBuilder();
            sourceBuilder.SetUseEventLocation(true);
            if (sourceBuilder is TinyBuilder)
            {
                ((TinyBuilder)sourceBuilder).SetStatistics(config.GetTreeStatistics().SOURCE_DOCUMENT_STATISTICS);
            }

            IReceiver r = sourceBuilder;
            ISpaceStrippingRule spaceStrippingRule = NoElementsSpaceStrippingRule.GetInstance();
            if (config.IsStripsAllWhiteSpace() || IsStylesheetContainingStripSpace() || validationMode == Validation.STRICT || validationMode == Validation.LAX)
            {
                r = MakeStripper(sourceBuilder);
                spaceStrippingRule = SpaceStrippingRule;
            }

            if (IsStylesheetStrippingTypeAnnotations())
            {
                r = config.GetAnnotationStripper(r);
            }

            PipelineConfiguration pipe = sourceBuilder.GetPipelineConfiguration();
            pipe.SetParseOptions(pipe.GetParseOptions().WithSchemaValidationMode(validationMode));
            r.SetPipelineConfiguration(pipe);
            Sender.Send(source, r, null);
            NodeInfo doc = sourceBuilder.CurrentRoot;

            //globalContextItem = doc;
            sourceBuilder.Reset();
            if (source.GetSystemId() != null)
            {
                RegisterDocument(doc.GetTreeInfo(), new DocumentKey(source.GetSystemId()));
            }

            doc.GetTreeInfo().SpaceStrippingRule = spaceStrippingRule;
            return doc;
        }

        // Source-free source-tree build (P5): parse a System.Xml.XmlReader into the transform's source tree,
        // applying the stylesheet's strip-space / type-annotation stripping exactly as MakeSourceTree(Source).
        // The stripper is applied manually here (as in the Source path); the pipe parse options carry no
        // stripping rule, so Sender's own wrapping does not strip a second time.
        public virtual NodeInfo MakeSourceTree(global::System.Xml.XmlReader reader, string systemId, int validationMode)
        {
            Builder sourceBuilder = MakeBuilder();
            sourceBuilder.SetUseEventLocation(true);
            if (sourceBuilder is TinyBuilder)
            {
                ((TinyBuilder)sourceBuilder).SetStatistics(config.GetTreeStatistics().SOURCE_DOCUMENT_STATISTICS);
            }

            IReceiver r = sourceBuilder;
            ISpaceStrippingRule spaceStrippingRule = NoElementsSpaceStrippingRule.GetInstance();
            if (config.IsStripsAllWhiteSpace() || IsStylesheetContainingStripSpace() || validationMode == Validation.STRICT || validationMode == Validation.LAX)
            {
                r = MakeStripper(sourceBuilder);
                spaceStrippingRule = SpaceStrippingRule;
            }

            if (IsStylesheetStrippingTypeAnnotations())
            {
                r = config.GetAnnotationStripper(r);
            }

            PipelineConfiguration pipe = sourceBuilder.GetPipelineConfiguration();
            pipe.SetParseOptions(pipe.GetParseOptions().WithSchemaValidationMode(validationMode));
            r.SetPipelineConfiguration(pipe);
            Sender.Send(reader, systemId, r, null);
            NodeInfo doc = sourceBuilder.CurrentRoot;
            sourceBuilder.Reset();
            if (systemId != null)
            {
                RegisterDocument(doc.GetTreeInfo(), new DocumentKey(systemId));
            }

            doc.GetTreeInfo().SpaceStrippingRule = spaceStrippingRule;
            return doc;
        }

        public virtual NodeInfo PrepareInputTree(IActiveSource source)
        {
            // P5: PrepareInputTree only ever receives node sources (a NodeInfo, or a NodeSource wrapping one),
            // so extract the node directly rather than routing through Configuration.Unravel(Source).
            NodeInfo start = source is NodeSource ? ((NodeSource)source).Node : (NodeInfo)source;

            // Stripping type annotations happens before stripping of whitespace
            if (IsStylesheetStrippingTypeAnnotations())
            {
                ITreeInfo docInfo = start.GetTreeInfo();
                if (docInfo.IsTyped())
                {
                    TypeStrippedDocument strippedDoc = new TypeStrippedDocument(docInfo);
                    start = strippedDoc.Wrap(start);
                }
            }

            if (stripSourceTrees && IsStylesheetContainingStripSpace())
            {
                ITreeInfo docInfo = start.GetTreeInfo();
                ISpaceStrippingRule spaceStrippingRule = SpaceStrippingRule;
                if (docInfo.SpaceStrippingRule != spaceStrippingRule)
                {

                    // if not already space-stripped
                    SpaceStrippedDocument strippedDoc = new SpaceStrippedDocument(docInfo, spaceStrippingRule);

                    // Edge case: the global context item might itself be a whitespace text node that is stripped
                    if (!SpaceStrippedNode.IsPreservedNode(start, strippedDoc, start.GetParent()))
                    {
                        return null;
                    }

                    start = strippedDoc.Wrap(start);
                }
            }

            return start;
        }

        public virtual void PreEvaluateGlobals(IXPathContext context)
        {
            foreach (PackageData pack in GetExecutable().Packages)
            {
                foreach (GlobalVariable var in pack.GlobalVariableList)
                {
                    if (!var.IsUnused())
                    {
                        try
                        {
                            var.EvaluateVariable(context, var.DeclaringComponent);
                        }
                        catch (XPathException err)
                        {

                            // Don't report an exception unless the variable is actually evaluated
                            GetBindery(var.GetPackageData()).SetGlobalVariable(var, new FailureValue(err));
                        }
                    }
                }
            }
        }

        public virtual void RegisterGlobalVariableDependency(GlobalVariable one, GlobalVariable two)
        {
            lock (this)
            {
                if (one == two)
                {
                    throw new Circularity("Circular dependency among global variables: " + one.GetVariableQName().DisplayName + " depends on its own value");
                }

                HashSet<GlobalVariable> transitiveDependencies = globalVariableDependencies.Get(two);
                if (transitiveDependencies != null)
                {
                    if (transitiveDependencies.Contains(one))
                    {
                        throw new Circularity("Circular dependency among variables: " + one.GetVariableQName().DisplayName + " depends on the value of " + two.GetVariableQName().DisplayName + ", which depends directly or indirectly on the value of " + one.GetVariableQName().DisplayName);
                    }

                    foreach (GlobalVariable var in transitiveDependencies)
                    {

                        // register the transitive dependencies
                        RegisterGlobalVariableDependency(one, var);
                    }
                }

                HashSet<GlobalVariable> existingDependencies = globalVariableDependencies.Get(one);
                if (existingDependencies == null)
                {
                    existingDependencies = new HashSet<GlobalVariable>();
                    globalVariableDependencies.Put(one, existingDependencies);
                }

                existingDependencies.Add(two);
            }
        }

        public virtual void SetCurrentDateTime(DateTimeValue dateTime)
        {
            if (currentDateTime == null)
            {
                if (dateTime.GetComponent(AccessorFn.Component.TIMEZONE) == null)
                {
                    throw new XPathException("No timezone is present in supplied value of current date/time");
                }

                currentDateTime = dateTime;
                dateTimePreset = true;
            }
            else
            {
                throw new InvalidOperationException("Current date and time can only be set once, and cannot subsequently be changed");
            }
        }

        public virtual DateTimeValue GetCurrentDateTime()
        {
            if (currentDateTime == null)
            {
                currentDateTime = DateTimeValue.Now();
            }

            return currentDateTime;
        }

        public virtual int GetImplicitTimezone()
        {
            return GetCurrentDateTime().TimezoneInMinutes;
        }

        public virtual XPathContextMajor NewXPathContext()
        {
            XPathContextMajor c = new XPathContextMajor(this);
            c.CurrentOutputUri = principalResultURI;
            return c;
        }

        public virtual void SetUseDocumentProjection(PathMap pathMap)
        {
            this.pathMap = pathMap;
        }

        public virtual StylesheetCache GetStylesheetCache()
        {
            lock (this)
            {
                if (stylesheetCache == null)
                {
                    this.stylesheetCache = new StylesheetCache();
                }

                return stylesheetCache;
            }
        }

        public virtual Func<ISequenceIterator, FocusTrackingIterator> GetFocusTrackerFactory(bool multithreaded)
        {
            return multithreaded && multiThreadedFocusTrackerFactory != null ? multiThreadedFocusTrackerFactory : focusTrackerFactory;
        }

        public virtual IFocusIterator MakeFocusTracker(ISequenceIterator iter, bool multithreaded)
        {
            if (iter is IFocusIterator)
            {
                return (IFocusIterator)iter;
            }
            else
            {
                return GetFocusTrackerFactory(multithreaded).Apply(iter);
            }
        }

        public virtual void SetFocusTrackerFactory(Func<ISequenceIterator, FocusTrackingIterator> focusTrackerFactory)
        {
            this.focusTrackerFactory = focusTrackerFactory;
        }

        public virtual void SetMultithreadedFocusTrackerFactory(Func<ISequenceIterator, FocusTrackingIterator> focusTrackerFactory)
        {
            this.multiThreadedFocusTrackerFactory = focusTrackerFactory;
        }

        public virtual void SetMemoizingFocusTrackerFactory()
        {
            SetFocusTrackerFactory((@base) =>
            {
                FocusTrackingIterator fti;
                if (!(@base is IGroundedIterator && ((IGroundedIterator)@base).IsActuallyGrounded()) && !(@base is IGroupIterator) && !(@base is IRegexIterator))
                {
                    try
                    {
                        MemoSequence ms = new MemoSequence(@base);
                        fti = FocusTrackingIterator.Track(ms.Iterate());
                    }
                    catch (UncheckedXPathException e)
                    {
                        fti = FocusTrackingIterator.Track(@base);
                    }
                }
                else
                {
                    fti = FocusTrackingIterator.Track(@base);
                }

                return fti;
            });
        }
    }
}