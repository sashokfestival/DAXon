////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Serialization;
using OutSmart.DAXon.Internal.Collections;
using OutSmart.DAXon.Internal.Functional;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Jaxp.Transform;
using OutSmart.DAXon.Internal.Streams;
using OutSmart.DAXon.Values;
using System.IO;
namespace OutSmart.DAXon.Api
{
    public class Xslt30Transformer : AbstractXsltTransformer
    {
        private GlobalParameterSet globalParameterSet;
        private bool primed = false;
        private IItem globalContextItem = null;
        private bool alreadyStripped;

        /*staticParameters*/
        public virtual XdmItem GlobalContextItem
        {
            get => XdmItem.WrapItem(this.globalContextItem); set
            {
                SetGlobalContextItem(value, false);
            }
        }
        public Xslt30Transformer(Processor processor, XsltController controller, GlobalParameterSet staticParameters) : base(processor, controller)
        {
            globalParameterSet = new GlobalParameterSet();
        }

        /*staticParameters*/
        public virtual void SetGlobalContextItem(XdmItem globalContextItem, bool alreadyStripped)
        {
            lock (this)
            {
                if (primed)
                {
                    throw new InvalidOperationException("Stylesheet has already been evaluated");
                }

                this.globalContextItem = globalContextItem == null ? null : globalContextItem.UnderlyingValue;
                this.alreadyStripped = alreadyStripped;
            }
        }

        /*staticParameters*/
        public virtual void SetStylesheetParameters<T>(Dictionary<QName, T> parameters)
        {
            lock (this)
            {
                if (primed)
                {
                    throw new InvalidOperationException("Stylesheet has already been evaluated");
                }

                if (globalParameterSet == null)
                {
                    globalParameterSet = new GlobalParameterSet();
                }

                foreach (KeyValuePair<QName, T> param in parameters.EntrySet())
                {
                    StructuredQName name = param.Key.GetStructuredQName();
                    XdmValue value = (XdmValue)(object)param.Value;
                    try
                    {
                        globalParameterSet.Put(name, (IGroundedValue)(value.UnderlyingValue));
                    }
                    catch (UncheckedXPathException e)
                    {
                        throw new DAXonApiException(e);
                    }
                }
            }
        }

        /*staticParameters*/
        private void Prime()
        {
            if (!primed)
            {
                if (globalParameterSet == null)
                {
                    globalParameterSet = new GlobalParameterSet();
                }

                try
                {
                    controller.SetGlobalContextItem(globalContextItem, alreadyStripped);
                    controller.InitializeController(globalParameterSet);
                }
                catch (XPathException e)
                {
                    throw new DAXonApiException(e);
                }
            }

            // Arm the Processor-wide cooperative deadline relative to the start of this run.
            controller.SetTimeout(processor.TransformTimeout);
            primed = true;
        }

        /*staticParameters*/
        public virtual void SetInitialTemplateParameters<T>(Dictionary<QName, T> parameters, bool tunnel)
        {
            lock (this)
            {
                Dictionary<StructuredQName, ISequence> templateParams = new Dictionary<StructuredQName, ISequence>();
                foreach (KeyValuePair<QName, T> entry in parameters.EntrySet())
                {
                    QName key = entry.Key;
                    XdmValue value = (XdmValue)(object)entry.Value;
                    templateParams.Put<StructuredQName, ISequence>(key.GetStructuredQName(), (ISequence)(value.UnderlyingValue));
                }

                controller.SetInitialTemplateParameters(templateParams, tunnel);
            }
        }

        // .NET-native input overload (P5): apply templates to a document read directly from a Stream with an
        // explicit system identifier — the caller no longer constructs a JAXP Source.
        public virtual void ApplyTemplates(global::System.IO.Stream input, string systemId, IDestination destination)
        {
            lock (this)
            {
                if (destination == null)
                    throw new NullReferenceException();
                if (input == null)
                    throw new NullReferenceException("input");
                controller.OpenTraceEpisode();
                Prime();
                try
                {
                    IReceiver sOut = GetDestinationReceiver(controller, destination);
                    using (global::System.Xml.XmlReader reader = global::OutSmart.DAXon.Events.XmlReaderToReceiver.CreateXmlReader(null, input, systemId))
                    {
                        ApplyTemplatesToXmlReader(reader, systemId, sOut);
                    }

                    destination.CloseAndNotify();
                }
                catch (XPathException e)
                {
                    if (!e.HasBeenReported())
                    {
                        GetErrorReporter().Report(new XmlProcessingException(e));
                    }

                    throw new DAXonApiException(e);
                }
                finally
                {
                    controller.CloseTraceEpisode();
                }
            }
        }

        /*staticParameters*/
        internal virtual void ApplyTemplates(IActiveSource source, IDestination destination)
        {
            lock (this)
            {
                if (destination == null)
                    throw new NullReferenceException();
                if (source == null)
                {
                    XPathException err = new XPathException("No initial match selection supplied", "XTDE0044");
                    throw new DAXonApiException(err);
                }

                controller.OpenTraceEpisode();
                Prime();
                try
                {
                    IReceiver sOut = GetDestinationReceiver(controller, destination);
                    ApplyTemplatesToSource(source, sOut);
                    destination.CloseAndNotify();
                }
                catch (XPathException e)
                {
                    if (!e.HasBeenReported())
                    {
                        GetErrorReporter().Report(new XmlProcessingException(e));
                    }

                    throw new DAXonApiException(e);
                }
                finally
                {
                    controller.CloseTraceEpisode();
                }
            }
        }

        /*staticParameters*/
        internal virtual XdmValue ApplyTemplates(IActiveSource source)
        {
            lock (this)
            {
                if (source == null)
                    throw new NullReferenceException();
                RawDestination raw = new RawDestination();
                ApplyTemplates(source, raw);
                return raw.GetXdmValue();
            }
        }

        /*staticParameters*/
        internal virtual void Transform(IActiveSource source, IDestination destination)
        {
            lock (this)
            {
                if (destination == null)
                    throw new NullReferenceException();
                if (source == null)
                {
                    XPathException err = new XPathException("No initial match selection supplied", "XTDE0044");
                    throw new DAXonApiException(err);
                }

                if (controller.GetInitialMode().IsDeclaredStreamable())
                {
                    throw new DAXonApiException("Cannot use the transform() method when the initial mode is streamable");
                }

                Prime();
                try
                {
                    NodeInfo sourceNode;
                    if (source is NodeInfo)
                    {
                        controller.GlobalContextItem = (NodeInfo)source;
                        sourceNode = (NodeInfo)source;
                    }
                    else
                    {
                        sourceNode = controller.MakeSourceTree(source, GetSchemaValidationMode().GetNumber());
                        controller.GlobalContextItem = sourceNode;
                    }

                    IReceiver sOut = GetDestinationReceiver(controller, destination);
                    controller.ApplyTemplates(sourceNode, sOut);
                    destination.CloseAndNotify();
                }
                catch (XPathException e)
                {
                    if (!e.HasBeenReported())
                    {
                        GetErrorReporter().Report(new XmlProcessingException(e));
                    }

                    throw new DAXonApiException(e);
                }
            }
        }

        /*staticParameters*/
        public virtual void ApplyTemplates(XdmValue selection, IDestination destination)
        {
            lock (this)
            {
                if (selection == null)
                    throw new NullReferenceException();
                if (destination == null)
                    throw new NullReferenceException();
                controller.OpenTraceEpisode();
                Prime();
                try
                {
                    IReceiver sOut = GetDestinationReceiver(controller, destination);
                    if (baseOutputUriWasSet)
                    {
                        sOut.SetSystemId(GetBaseOutputURI());
                    }

                    controller.ApplyTemplates((ISequence)(selection.UnderlyingValue), sOut);
                    destination.CloseAndNotify();
                }
                catch (XPathException e)
                {
                    if (!e.HasBeenReported())
                    {
                        GetErrorReporter().Report(new XmlProcessingException(e));
                    }

                    throw new DAXonApiException(e);
                }
                finally
                {
                    controller.CloseTraceEpisode();
                }
            }
        }

        /*staticParameters*/
        public virtual XdmValue ApplyTemplates(XdmValue selection)
        {
            lock (this)
            {
                if (selection == null)
                    throw new NullReferenceException();
                RawDestination raw = new RawDestination();
                ApplyTemplates(selection, raw);
                return raw.GetXdmValue();
            }
        }

        /*staticParameters*/
        public virtual void CallTemplate(QName templateName, IDestination destination)
        {
            lock (this)
            {
                if (destination == null)
                    throw new NullReferenceException();
                controller.OpenTraceEpisode();
                Prime();
                if (templateName == null)
                {
                    templateName = new QName("xsl", NamespaceConstant.XSLT, "initial-template");
                }

                try
                {
                    IReceiver sOut = GetDestinationReceiver(controller, destination);
                    if (baseOutputUriWasSet)
                    {
                        sOut.SetSystemId(GetBaseOutputURI());
                    }


                    //sOut.open();
                    controller.CallTemplate(templateName.GetStructuredQName(), sOut);

                    //sOut.close();
                    destination.CloseAndNotify();
                }
                catch (XPathException e)
                {
                    destination.CloseAndNotify();
                    if (!e.HasBeenReported())
                    {
                        GetErrorReporter().Report(new XmlProcessingException(e));
                    }

                    throw new DAXonApiException(e);
                }
                finally
                {
                    controller.CloseTraceEpisode();
                }
            }
        }

        /*staticParameters*/
        public virtual XdmValue CallTemplate(QName templateName)
        {
            lock (this)
            {
                RawDestination dest = new RawDestination();
                CallTemplate(templateName, dest);
                return dest.GetXdmValue();
            }
        }

        /*staticParameters*/
        public virtual XdmValue CallFunction(QName function, XdmValue[] arguments)
        {
            lock (this)
            {
                if (function == null)
                    throw new NullReferenceException();
                if (arguments == null)
                    throw new NullReferenceException();
                controller.OpenTraceEpisode();
                Prime();
                try
                {
                    Component f = GetFunctionComponent(function, arguments);
                    UserFunction uf = (UserFunction)f.GetActor();
                    ISequence[] vr = TypeCheckFunctionArguments(uf, arguments);
                    XPathContextMajor context = controller.NewXPathContext();
                    context.SetCurrentComponent(f);
                    context.TemporaryOutputState = StandardNames.XSL_FUNCTION;
                    context.CurrentOutputUri = null;
                    ISequence result = uf.Call(context, vr);
                    result = result.Materialize();
                    return XdmValue.Wrap(result);
                }
                catch (XPathException e)
                {
                    if (!e.HasBeenReported())
                    {
                        GetErrorReporter().Report(new XmlProcessingException(e));
                    }

                    throw new DAXonApiException(e);
                }
                finally
                {
                    controller.CloseTraceEpisode();
                }
            }
        }

        /*staticParameters*/
        private Component GetFunctionComponent(QName function, XdmValue[] arguments)
        {
            lock (this)
            {
                SymbolicName fName = new SymbolicName.F(function.GetStructuredQName(), arguments.Length);
                PreparedStylesheet pss = (PreparedStylesheet)controller.GetExecutable();
                Component f = pss.GetComponent(fName);
                if (f == null)
                {
                    throw new XPathException("No public function with name " + function.ClarkName + " and arity " + arguments.Length + " has been declared in the stylesheet", "XTDE0041");
                }
                else if (f.GetVisibility() != Visibility.FINAL && f.GetVisibility() != Visibility.PUBLIC)
                {
                    throw new XPathException("Cannot invoke " + fName + " externally, because it is not public", "XTDE0041");
                }

                return f;
            }
        }

        /*staticParameters*/
        private ISequence[] TypeCheckFunctionArguments(UserFunction uf, XdmValue[] arguments)
        {
            Configuration config = processor.UnderlyingConfiguration;
            UserFunctionParameter[] @params = uf.GetParameterDefinitions();
            IGroundedValue[] vr = new IGroundedValue[arguments.Length];
            for (int i = 0; i < arguments.Length; i++)
            {
                Values.SequenceType type = @params[i].GetRequiredType();
                vr[i] = (IGroundedValue)arguments[i].UnderlyingValue;
                if (!type.Matches(vr[i], config.GetTypeHierarchy()))
                {
                    int pos = i;
                    Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.FUNCTION, uf.GetFunctionName().DisplayName, pos);
                    ISequence converted = config.GetTypeHierarchy().ApplyFunctionConversionRules(vr[i], type, role, Loc.NONE);
                    vr[i] = converted.Materialize();
                }
            }

            return vr;
        }

        /*staticParameters*/
        public virtual void CallFunction(QName function, XdmValue[] arguments, IDestination destination)
        {
            lock (this)
            {
                controller.OpenTraceEpisode();
                Prime();
                try
                {
                    Component f = GetFunctionComponent(function, arguments);
                    UserFunction uf = (UserFunction)f.GetActor();
                    ISequence[] vr = TypeCheckFunctionArguments(uf, arguments);
                    XPathContextMajor context = controller.NewXPathContext();
                    context.SetCurrentComponent(f);
                    context.TemporaryOutputState = StandardNames.XSL_FUNCTION;
                    context.CurrentOutputUri = null;
                    SerializationProperties @params = controller.GetExecutable().PrimarySerializationProperties;
                    IReceiver receiver = destination.GetReceiver(controller.MakePipelineConfiguration(), @params);
                    receiver.Open();
                    uf.Process(context, vr, new ComplexContentOutputter(receiver));
                    receiver.Dispose();
                }
                catch (XPathException e)
                {
                    GetErrorReporter().Report(new XmlProcessingException(e));
                    throw new DAXonApiException(e);
                }
                finally
                {
                    controller.CloseTraceEpisode();
                }

                destination.CloseAndNotify();
            }
        }

        /*staticParameters*/
        public virtual IDestination AsDocumentDestination(IDestination finalDestination)
        {
            return new AnonymousAbstractDestination(this, finalDestination);
        }

        /*staticParameters*/
        public virtual Serializer NewSerializer()
        {
            Serializer serializer = processor.NewSerializer();
            serializer.SetOutputProperties(controller.GetExecutable().PrimarySerializationProperties);
            return serializer;
        }

        /*staticParameters*/
        public virtual Serializer NewSerializer(string file)
        {
            Serializer serializer = processor.NewSerializer(file);
            serializer.SetOutputProperties(controller.GetExecutable().PrimarySerializationProperties);
            SetBaseOutputURI(new Uri(Path.GetFullPath(file)).AbsoluteUri);
            return serializer;
        }

        /*staticParameters*/
        public virtual Serializer NewSerializer(TextWriter writer)
        {
            Serializer serializer = NewSerializer();
            serializer.SetOutputWriter(writer);
            return serializer;
        }

        /*staticParameters*/
        public virtual Serializer NewSerializer(System.IO.Stream stream)
        {
            Serializer serializer = NewSerializer();
            serializer.SetOutputStream(stream);
            return serializer;
        }

        private sealed class AnonymousAbstractDestination : AbstractDestination
        {

            private readonly Xslt30Transformer parent;
            private readonly IDestination finalDestination;
            private IReceiver receiver;
            public AnonymousAbstractDestination(Xslt30Transformer parent, IDestination finalDestination)
            {
                this.parent = parent;
                this.finalDestination = finalDestination;
            }
            public override IReceiver GetReceiver(PipelineConfiguration pipe, SerializationProperties @params)
            {
                IReceiver rt = parent.GetReceivingTransformer(parent.controller, parent.globalParameterSet, finalDestination);
                rt = new SequenceNormalizerWithSpaceSeparator(rt);
                rt.SetPipelineConfiguration(pipe);
                return receiver = rt;
            }

            public override void Dispose()
            {
                try
                {
                    receiver.Dispose();
                }
                catch (XPathException e)
                {
                    throw new DAXonApiException(e);
                }
            }
        }
    }
}
