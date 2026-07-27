////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Serialization;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Net;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Jaxp.Transform;
using OutSmart.DAXon.Internal.Jaxp.Transform.Stream;
using OutSmart.DAXon.Internal.Streams;
namespace OutSmart.DAXon.Api
{
    public class DocumentBuilder
    {
        private readonly Configuration config;
        private SchemaValidator schemaValidator;
        private bool dtdValidation;
        private bool lineNumbering;
        private TreeModel treeModel = TreeModel.TINY_TREE;
        private WhitespaceStrippingPolicy whitespacePolicy = WhitespaceStrippingPolicy.UNSPECIFIED;
        private URI baseURI;
        private XQueryExecutable projectionQuery;

        public virtual SchemaValidator SchemaValidator
        {
            get => schemaValidator; set
            {
                schemaValidator = value;
            }
        }

        public virtual URI BaseURI
        {
            get => baseURI; set
            {
                if (!value.IsAbsolute())
                {
                    throw new ArgumentException("Supplied base URI must be absolute");
                }

                baseURI = value;
            }
        }

        public virtual XQueryExecutable DocumentProjectionQuery
        {
            get => this.projectionQuery; set
            {
                this.projectionQuery = value;
            }
        }
        public DocumentBuilder(Configuration config)
        {
            this.config = config;
        }

        public virtual void SetTreeModel(TreeModel model)
        {
            this.treeModel = model;
        }

        public virtual TreeModel GetTreeModel()
        {
            return treeModel;
        }

        public virtual void SetLineNumbering(bool option)
        {
            lineNumbering = option;
        }

        public virtual bool IsLineNumbering()
        {
            return lineNumbering;
        }

        public virtual void SetDTDValidation(bool option)
        {
            dtdValidation = option;
        }

        public virtual bool IsDTDValidation()
        {
            return dtdValidation;
        }

        public virtual void SetWhitespaceStrippingPolicy(WhitespaceStrippingPolicy policy)
        {
            whitespacePolicy = policy;
        }

        public virtual WhitespaceStrippingPolicy GetWhitespaceStrippingPolicy()
        {
            return whitespacePolicy;
        }

        internal virtual XdmNode Build(ResolvedResource source)
        {
            if (source == null)
                throw new NullReferenceException("source");
            ParseOptions options = GetParseOptions(source);
            try
            {
                ITreeInfo doc = config.BuildDocumentTree(source, options);
                return new XdmNode(doc.GetRootNode());
            }
            catch (XPathException e)
            {
                throw new DAXonApiException(e);
            }
        }

        // Source-independent parse options (whitespace policy, DTD/schema validation, tree model, line
        // numbering, document projection). Shared by GetParseOptions(Source) and the Source-free build path.
        private ParseOptions GetParseOptions()
        {
            if (!(whitespacePolicy == WhitespaceStrippingPolicy.UNSPECIFIED || whitespacePolicy == WhitespaceStrippingPolicy.IGNORABLE || whitespacePolicy.Ordinal() == Whitespace.XSLT))
            {
                if (dtdValidation)
                {
                    throw new DAXonApiException("When DTD validation is used, the whitespace stripping policy must be IGNORABLE");
                }

                if (schemaValidator != null)
                {
                    throw new DAXonApiException("When schema validation is used, the whitespace stripping policy must be IGNORABLE");
                }
            }

            ParseOptions options = config.GetParseOptions().WithDTDValidationMode(dtdValidation ? Validation.STRICT : Validation.STRIP);
            if (schemaValidator != null)
            {
                options = options.WithSchemaValidationMode(schemaValidator.IsLax() ? Validation.LAX : Validation.STRICT);
                if (schemaValidator.DocumentElementName != null)
                {
                    QName qn = schemaValidator.DocumentElementName;
                    options = options.WithTopLevelElement(qn.GetStructuredQName());
                }

                if (schemaValidator.DocumentElementType != null)
                {
                    options = options.WithTopLevelType(schemaValidator.DocumentElementType);
                }

                options = options.WithExpandAttributeDefaults(schemaValidator.IsExpandAttributeDefaults());
                options = options.WithUseXsiSchemaLocation(schemaValidator.IsUseXsiSchemaLocation());
                options = options.WithValidationParams(schemaValidator.ValidationParameters);
                options = options.WithInvalidityHandler(schemaValidator.InvalidityHandler);
            }

            if (treeModel != null)
            {
                options = options.WithModel(treeModel);
            }

            if (whitespacePolicy != null && whitespacePolicy != WhitespaceStrippingPolicy.UNSPECIFIED)
            {
                int option = whitespacePolicy.Ordinal();
                if (option == Whitespace.XSLT)
                {
                    options = options.WithSpaceStrippingRule(NoElementsSpaceStrippingRule.GetInstance());
                    options = options.WithFilter(whitespacePolicy.MakeStripper());
                }
                else
                {
                    options = options.WithSpaceStrippingRule(whitespacePolicy.SpaceStrippingRule);
                }
            }

            options = options.WithLineNumbering(lineNumbering);
            if (projectionQuery != null)
            {
                XQueryExpression exp = projectionQuery.UnderlyingCompiledQuery;
                IFilterFactory ff = config.MakeDocumentProjector(exp);
                if (ff != null)
                {
                    options = options.WithFilter(ff);
                }
            }

            return options;
        }

        // Source-specific parse options: default the system id from the base URI and fold in an
        // AugmentedSource's own options. The core (above) is shared with the Source-free build path.
        private ParseOptions GetParseOptions(ResolvedResource source)
        {
            if (source.SystemId == null && baseURI != null)
            {
                source.SystemId = baseURI.ToString();
            }

            ParseOptions options = GetParseOptions();
            return options;
        }

        // Drop element-content (ignorable) whitespace only when the effective policy strips it (the default).
        // The reader is asked to DTD-validate purely so .NET classifies that whitespace (number-4501); errors
        // are swallowed. A NONE/XSLT policy keeps all whitespace, so it must not enable the DTD classification.
        private bool StripsIgnorableWhitespace()
            => whitespacePolicy == WhitespaceStrippingPolicy.UNSPECIFIED || whitespacePolicy == WhitespaceStrippingPolicy.IGNORABLE;

        // .NET-native input overloads (P5): build a document directly from a Stream/TextReader with an
        // explicit system identifier — the caller no longer constructs a JAXP Source.
        public virtual XdmNode Build(global::System.IO.Stream input, string systemId)
        {
            if (input == null)
                throw new NullReferenceException("input");
            bool ws = StripsIgnorableWhitespace();
            return BuildFromXmlReader(global::OutSmart.DAXon.Events.XmlReaderToReceiver.CreateXmlReader(null, input, systemId, null, ws, ws), systemId);
        }

        public virtual XdmNode Build(global::System.IO.TextReader input, string systemId)
        {
            if (input == null)
                throw new NullReferenceException("input");
            bool ws = StripsIgnorableWhitespace();
            return BuildFromXmlReader(global::OutSmart.DAXon.Events.XmlReaderToReceiver.CreateXmlReader(input, null, systemId, null, ws, ws), systemId);
        }

        // Saxonica .NET-API compat: base URI set as a property, then Build with just the reader.
        public virtual Uri BaseUri { get; set; }

        public virtual XdmNode Build(global::System.IO.TextReader input)
            => Build(input, BaseUri != null ? BaseUri.AbsoluteUri : "urn:input");

        // Source-free document build: parse the reader straight into a tree (Configuration.BuildDocumentTree
        // -> Sender.Send(XmlReader) -> XmlReaderToReceiver), without constructing a JAXP StreamSource/Source.
        private XdmNode BuildFromXmlReader(global::System.Xml.XmlReader reader, string systemId)
        {
            ParseOptions options = GetParseOptions();
            try
            {
                using (reader)
                {
                    ITreeInfo doc = config.BuildDocumentTree(reader, systemId, options);
                    return new XdmNode(doc.GetRootNode());
                }
            }
            catch (XPathException e)
            {
                throw new DAXonApiException(e);
            }
        }

        public virtual XdmNode Build(string file)
        {
            // P5: build via the native XmlReader path (a bare systemId opens through XmlReader.Create), no JAXP Source.
            bool ws = StripsIgnorableWhitespace();
            return BuildFromXmlReader(global::OutSmart.DAXon.Events.XmlReaderToReceiver.CreateXmlReader(null, null, file, null, ws, ws), file);
        }

        private IReceiver InjectValidator(IReceiver r, Builder builder)
        {
            if (schemaValidator != null)
            {
                PipelineConfiguration pipe = builder.GetPipelineConfiguration();
                IReceiver val = schemaValidator.GetReceiver(pipe, config.ObtainDefaultSerializationProperties());
                val.SetPipelineConfiguration(pipe);
                if (val is ProxyReceiver)
                {
                    ((ProxyReceiver)val).SetUnderlyingReceiver(r);
                }

                return val;
            }

            return r;
        }


        public virtual BuildingStreamWriterImpl NewBuildingStreamWriter()
        {
            PipelineConfiguration pipe = config.MakePipelineConfiguration();
            Builder builder = treeModel.MakeBuilder(pipe);
            builder.SetLineNumbering(lineNumbering);
            IReceiver r = builder;
            r = new NamespaceReducer(r);
            r = InjectValidator(r, builder);
            return new BuildingStreamWriterImpl(r, builder);
        }

        public virtual XdmNode Wrap(object node)
        {
            if (node is NodeInfo)
            {
                NodeInfo nodeInfo = (NodeInfo)node;
                if (nodeInfo.GetConfiguration().IsCompatible(config))
                {
                    return new XdmNode(nodeInfo);
                }
                else
                {
                    throw new ArgumentException("Supplied NodeInfo was created using a different Configuration");
                }
            }
            else
            {
                try
                {
                    JPConverter converter = JPConverter.Allocate(node.GetType(), null, config);
                    NodeInfo nodeInfo = (NodeInfo)converter.Convert(node, new EarlyEvaluationContext(config));
                    return XdmItem.WrapItem(nodeInfo);
                }
                catch (XPathException e)
                {
                    throw new ArgumentException(e.GetMessage());
                }
                catch (InvalidCastException e)
                {
                    throw new ArgumentException("Class " + node.GetType() + " is not a recognized external node type");
                }
            }
        }

        public virtual void Parse(ResolvedResource source, IDestination destination)
        {
            try
            {
                ParseOptions options = GetParseOptions(source);
                PipelineConfiguration pipe = config.MakePipelineConfiguration();
                Sender.Send(source, destination.GetReceiver(pipe, new SerializationProperties()), options);
            }
            catch (XPathException e)
            {
                throw new DAXonApiException(e);
            }
        }

        public virtual void Parse(string file, IDestination destination)
        {
            // P5: parse via the native XmlReader path (no JAXP StreamSource); mirrors Parse(Source).
            try
            {
                ParseOptions options = GetParseOptions();
                PipelineConfiguration pipe = config.MakePipelineConfiguration();
                using (global::System.Xml.XmlReader reader = global::OutSmart.DAXon.Events.XmlReaderToReceiver.CreateXmlReader(null, null, file))
                {
                    Sender.Send(reader, file, destination.GetReceiver(pipe, new SerializationProperties()), options);
                }
            }
            catch (XPathException e)
            {
                throw new DAXonApiException(e);
            }
        }
    }
}
