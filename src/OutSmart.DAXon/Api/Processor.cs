////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api.Push;
using OutSmart.DAXon.Serialization;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Streams;
using System.IO;
namespace OutSmart.DAXon.Api
{
    public class Processor : Configuration.IApiProvider
    {
        // Host-level resource limits, fixed at construction. Everything created from this
        // Processor inherits them: every transformation (XsltTransformer / Xslt30Transformer)
        // runs under TransformTimeout, and DocumentCache rejects inputs over MaxInputBytes.
        public static readonly TimeSpan DefaultTransformTimeout = TimeSpan.FromMinutes(1);

        // A parsed tree retains roughly 3x the source text, so one 150 MB input alone holds
        // ~450 MB; the cap keeps a single oversized document from exhausting the host.
        public const long DefaultMaxInputBytes = 150L * 1024 * 1024;

        /// <summary>
        /// Wall-clock limit per transformation; exceeded runs abort with SXTO0001.
        /// TimeSpan.Zero (or negative) means no limit.
        /// </summary>
        public TimeSpan TransformTimeout { get; }

        /// <summary>
        /// Largest input DocumentCache accepts: file length in bytes, or string length in
        /// chars for content entries. long.MaxValue effectively disables the check.
        /// </summary>
        public long MaxInputBytes { get; }

        private Configuration config;
        private SchemaManager schemaManager;

        // Saxon-base engine version (tracks the 12.9 base: SEF/fn:transform/xsl:product-version compat).
        public virtual string DAXonProductVersion => Core.Version.ProductVersion;

        // This distribution's own name/version (e.g. "OutSmart DAXon" / "1.0").
        public virtual string DistributionName => Core.Version.ProductName;
        public virtual string DistributionVersion => Core.Version.DistributionVersion;

        public virtual string DAXonEdition => config.EditionCode;

        public virtual string XmlVersion
        {
            get
            {
                if (config.XMLVersion == Configuration.XML10)
                {
                    return "1.0";
                }
                else
                {
                    return "1.1";
                }
            }
            set
            {
                switch (value)
                {
                    case "1.0":
                        config.XMLVersion = Configuration.XML10;
                        break;
                    case "1.1":
                        config.XMLVersion = Configuration.XML11;
                        break;
                    default:
                        throw new ArgumentException("XmlVersion");
                }
            }
        }

        public virtual Configuration UnderlyingConfiguration => config;

        /// <param name="transformTimeout">Wall-clock limit per transformation; null for the
        /// default (1 minute), TimeSpan.Zero (or negative) for no limit.</param>
        /// <param name="maxInputBytes">Largest input DocumentCache accepts; long.MaxValue
        /// effectively disables the check.</param>
        public Processor(TimeSpan? transformTimeout = null, long maxInputBytes = DefaultMaxInputBytes)
            : this(Configuration.NewLicensedConfiguration())
        {
            if (maxInputBytes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxInputBytes));
            }

            TransformTimeout = transformTimeout ?? DefaultTransformTimeout;
            MaxInputBytes = maxInputBytes;
        }

        public Processor(bool licensedEdition, TimeSpan? transformTimeout = null, long maxInputBytes = DefaultMaxInputBytes)
        {
            if (maxInputBytes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxInputBytes));
            }

            if (licensedEdition)
            {
                config = Configuration.NewConfiguration();
                if (config.EditionCode.Equals("EE"))
                {
                    schemaManager = MakeSchemaManager();
                }
            }
            else
            {
                config = new Configuration();
            }

            config.SetProcessor(this);
            TransformTimeout = transformTimeout ?? DefaultTransformTimeout;
            MaxInputBytes = maxInputBytes;
        }

        public Processor(Configuration config)
        {
            this.config = config;
            if (config.EditionCode.Equals("EE"))
            {
                schemaManager = MakeSchemaManager();
            }

            // Make the Processor discoverable from its Configuration (so config.GetProcessor()
            // yields it, e.g. to read TransformTimeout when a query builds its Controller). Don't
            // clobber a processor already registered on an externally-supplied config.
            if (config.GetProcessor() == null)
            {
                config.SetProcessor(this);
            }

            TransformTimeout = DefaultTransformTimeout;
            MaxInputBytes = DefaultMaxInputBytes;
        }

        public Processor(ResolvedResource source)
        {
            try
            {
                config = Configuration.ReadConfiguration(source);
                schemaManager = MakeSchemaManager();
            }
            catch (XPathException e)
            {
                throw new DAXonApiException(e);
            }
            catch (RecursionDepthError e)
            {
                throw new DAXonApiException(e.ToXPathException());
            }

            config.SetProcessor(this);
            TransformTimeout = DefaultTransformTimeout;
            MaxInputBytes = DefaultMaxInputBytes;
        }

        public virtual DocumentBuilder NewDocumentBuilder()
        {
            return new DocumentBuilder(config);
        }

        public virtual JsonBuilder NewJsonBuilder()
        {
            return new JsonBuilder(UnderlyingConfiguration);
        }

        public virtual XPathCompiler NewXPathCompiler()
        {
            return new XPathCompiler(this);
        }

        public virtual XsltCompiler NewXsltCompiler()
        {
            return new XsltCompiler(this);
        }

        public virtual XQueryCompiler NewXQueryCompiler()
        {
            return new XQueryCompiler(this);
        }

        public virtual Serializer NewSerializer()
        {
            return new Serializer(this);
        }

        public virtual Serializer NewSerializer(System.IO.Stream stream)
        {
            Serializer s = new Serializer(this);
            s.SetOutputStream(stream);
            return s;
        }

        public virtual Serializer NewSerializer(TextWriter writer)
        {
            Serializer s = new Serializer(this);
            s.SetOutputWriter(writer);
            return s;
        }

        public virtual Serializer NewSerializer(string file)
        {
            Serializer s = new Serializer(this);
            s.SetOutputFile(file);
            return s;
        }

        public virtual IPush NewPush(IDestination destination)
        {
            PipelineConfiguration pipe = UnderlyingConfiguration.MakePipelineConfiguration();
            SerializationProperties props = new SerializationProperties();
            return (IPush)(new PushToReceiver(destination.GetReceiver(pipe, props)));
        }

        public virtual void RegisterExtensionFunction(IExtensionFunction function)
        {
            ExtensionFunctionDefinitionWrapper wrapper = new ExtensionFunctionDefinitionWrapper(function);
            RegisterExtensionFunction(wrapper);
        }

        public virtual void RegisterExtensionFunction(ExtensionFunctionDefinition function)
        {
            try
            {
                config.RegisterExtensionFunction(function);
            }
            catch (Exception err)
            {
                throw new ArgumentException(err.Message, err);
            }
        }

        public virtual SchemaManager GetSchemaManager()
        {
            return schemaManager;
        }

        public virtual bool IsSchemaAware()
        {
            return config.IsLicensedFeature(Configuration.LicenseFeature.SCHEMA_VALIDATION);
        }

        public virtual void SetConfigurationProperty(string name, object value)
        {
            if (name.Equals(FeatureKeys.CONFIGURATION))
            {
                config = (Configuration)(object)value;
            }
            else
            {
                config.SetConfigurationProperty(name, value);
            }
        }

        public virtual object GetConfigurationProperty(string name)
        {
            return config.GetConfigurationProperty(name);
        }

        public virtual void SetConfigurationProperty<T>(Feature<T> feature, T value)
        {
            if ((object)feature == (object)Feature<Configuration>.CONFIGURATION)
            {
                config = (Configuration)(object)value;
            }
            else
            {
                config.SetConfigurationProperty(feature, value);
            }
        }

        public virtual T GetConfigurationProperty<T>(Feature<T> feature)
        {
            return config.GetConfigurationProperty(feature);
        }

        public virtual void DeclareCollation(string uri, IComparer<string> collation)
        {
            if (uri.Equals(NamespaceConstant.CODEPOINT_COLLATION_URI))
            {
                throw new ArgumentException("Cannot redeclare the Unicode codepoint collation URI");
            }

            if (uri.Equals(NamespaceConstant.HTML5_CASE_BLIND_COLLATION_URI))
            {
                throw new ArgumentException("Cannot redeclare the HTML5 caseblind collation URI");
            }

            IStringCollator saxonCollation = MakeStringCollator(uri, collation);
            config.RegisterCollation(uri, saxonCollation);
        }

        private static IStringCollator MakeStringCollator(string uri, IComparer<string> collation)
        {
            if (collation is RuleBasedCollator)
            {
                return new RuleBasedSubstringMatcher(uri, (RuleBasedCollator)collation);
            }
            else
            {
                return new SimpleCollation(uri, collation);
            }
        }

        public virtual void RegisterCollection(string collectionURI, IResourceCollection collection)
        {
            config.RegisterCollection(collectionURI, collection);
        }

        public virtual void SetCatalogFiles(params string[] fileNames)
        {
            if (config.GetResourceResolver() is IConfigurableResourceResolver)
            {
                CommandLineOptions.SetCatalogFiles(((IConfigurableResourceResolver)config.GetResourceResolver()), fileNames.ToList());
            }
        }

        public virtual void WriteXdmValue(XdmValue value, IDestination destination)
        {
            if (value == null)
                throw new NullReferenceException();
            if (destination == null)
                throw new NullReferenceException();
            bool closed = false;
            try
            {
                if (destination is Serializer)
                {
                    ((Serializer)destination).SerializeXdmValue(value);
                    closed = true;
                }
                else
                {
                    IReceiver @out = destination.GetReceiver(config.MakePipelineConfiguration(), config.ObtainDefaultSerializationProperties());
                    // using = abort-path release (a failed write frees the destination's file); Close inside = success path.
                    using (ComplexContentOutputter tree = new ComplexContentOutputter(@out))
                    {
                        tree.Open();
                        tree.StartDocument(ReceiverOption.NONE);
                        foreach (XdmItem item in value)
                        {
                            tree.Append(item.UnderlyingValue, Loc.NONE, ReceiverOption.ALL_NAMESPACES);
                        }

                        tree.EndDocument();
                        tree.Close();
                    }

                    destination.CloseAndNotify();
                    closed = true;
                }
            }
            catch (XPathException err)
            {
                throw new DAXonApiException(err);
            }
            catch (RecursionDepthError err)
            {
                throw new DAXonApiException(err.ToXPathException());
            }
            finally
            {
                if (!closed)
                {
                    DestinationHelper.ReleaseUnclosed(destination);
                }
            }
        }

        private SchemaManager MakeSchemaManager()
        {
            SchemaManager manager = null;
            return manager;
        }

        private class ExtensionFunctionDefinitionWrapper : ExtensionFunctionDefinition
        {
            private readonly IExtensionFunction function;

            public override StructuredQName FunctionQName => function.Name.GetStructuredQName();

            public override int MinimumNumberOfArguments => function.ArgumentTypes.Length;

            public override int MaximumNumberOfArguments => function.ArgumentTypes.Length;

            public override Values.SequenceType[] ArgumentTypes
            {
                get
                {
                    SequenceType[] declaredArgs = function.ArgumentTypes;
                    Values.SequenceType[] types = new Values.SequenceType[declaredArgs.Length];
                    for (int i = 0; i < declaredArgs.Length; i++)
                    {
                        types[i] = Values.SequenceType.MakeSequenceType(declaredArgs[i].GetItemType().UnderlyingItemType, declaredArgs[i].GetOccurrenceIndicator().GetCardinality());
                    }

                    return types;
                }
            }
            public ExtensionFunctionDefinitionWrapper(IExtensionFunction function)
            {
                this.function = function;
            }

            public override Values.SequenceType GetResultType(Values.SequenceType[] suppliedArgumentTypes)
            {
                SequenceType declaredResult = function.ResultType;
                return Values.SequenceType.MakeSequenceType(declaredResult.GetItemType().UnderlyingItemType, declaredResult.GetOccurrenceIndicator().GetCardinality());
            }

            public override bool TrustResultType()
            {
                return false;
            }

            public override bool DependsOnFocus()
            {
                return false;
            }

            public override bool HasSideEffects()
            {
                return false;
            }

            public override ExtensionFunctionCall MakeCallExpression()
            {
                return new AnonymousExtensionFunctionCall(this);
            }

            private sealed class AnonymousExtensionFunctionCall : ExtensionFunctionCall
            {

                private readonly ExtensionFunctionDefinitionWrapper parent;
                public AnonymousExtensionFunctionCall(ExtensionFunctionDefinitionWrapper parent)
                {
                    this.parent = parent;
                }
                public override ISequence Call(IXPathContext context, ISequence[] arguments)
                {
                    XdmValue[] args = new XdmValue[arguments.Length];
                    for (int i = 0; i < args.Length; i++)
                    {
                        IGroundedValue val = arguments[i].Materialize();
                        args[i] = XdmValue.Wrap(val);
                    }

                    try
                    {
                        XdmValue result = parent.function.Call(args);
                        return (ISequence)result.UnderlyingValue;
                    }
                    catch (DAXonApiException e)
                    {
                        throw new XPathException(e?.Message);
                    }
                }
            }
        }
    }
}