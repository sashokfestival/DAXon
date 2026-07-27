////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.XPath
{
    public class IndependentContext : AbstractStaticContext, IXPathStaticContext, INamespaceResolver
    {
        protected Dictionary<string, NamespaceUri> namespaces = new Dictionary<string, NamespaceUri>(10);
        protected Dictionary<StructuredQName, XPathVariable> variables = new Dictionary<StructuredQName, XPathVariable>(20);
        protected INamespaceResolver externalResolver = null;
        protected Types.ItemType requiredContextItemType = AnyItemType.GetInstance();
        protected HashSet<NamespaceUri> importedSchemaNamespaces = new HashSet<NamespaceUri>();
        protected bool autoDeclare = false;
        protected Executable executable;
        protected RetainedStaticContext retainedStaticContext;
        protected OptimizerOptions optimizerOptions;
        protected bool parentlessContextItem;

        /// <summary>
        /// Create an IndependentContext along with a new (non-schema-aware) Saxon Configuration
        /// </summary>
        public virtual IEnumerable<XPathVariable> ExternalVariables => variables.Values;

        /// <summary>
        /// Create an IndependentContext along with a new (non-schema-aware) Saxon Configuration
        /// </summary>
        public virtual ICollection<XPathVariable> DeclaredVariables => variables.Values;
        /// <summary>
        /// Create an IndependentContext along with a new (non-schema-aware) Saxon Configuration
        /// </summary>
        public IndependentContext() : this(new Configuration())
        {
        }

        /// <summary>
        /// Create an IndependentContext along with a new (non-schema-aware) Saxon Configuration
        /// </summary>
        public IndependentContext(Configuration config)
        {
            SetConfiguration(config);
            ClearNamespaces();
            SetDefaultFunctionLibrary(31);
            SetDefaultCollationName(config.GetDefaultCollationName());
            SetOptimizerOptions(config.GetOptimizerOptions());
            PackageData pd = new PackageData(config);
            pd.SetHostLanguage(HostLanguage.XPATH, 31);
            pd.SetSchemaAware(false);
            SetPackageData(pd);
        }

        /// <summary>
        /// Create an IndependentContext along with a new (non-schema-aware) Saxon Configuration
        /// </summary>
        public IndependentContext(IndependentContext ic) : this(ic.GetConfiguration())
        {
            SetPackageData(ic.GetPackageData());
            SetBaseURI(ic.StaticBaseURI);
            SetContainingLocation(ic.GetContainingLocation());
            SetDefaultElementNamespace(ic.GetDefaultElementNamespace());
            SetDefaultFunctionNamespace(ic.GetDefaultFunctionNamespace());
            SetBackwardsCompatibilityMode(ic.IsInBackwardsCompatibleMode());
            namespaces = new Dictionary<string, NamespaceUri>(ic.namespaces);
            variables = new Dictionary<StructuredQName, XPathVariable>(10);
            FunctionLibraryList libList = (FunctionLibraryList)ic.GetFunctionLibrary();
            if (libList != null)
            {
                SetFunctionLibrary((FunctionLibraryList)libList.Copy());
            }

            SetDefaultCollationName(ic.GetDefaultCollationName());
            SetImportedSchemaNamespaces(ic.importedSchemaNamespaces);
            externalResolver = ic.externalResolver;
            autoDeclare = ic.autoDeclare;
            SetUnprefixedElementMatchingPolicy(ic.GetUnprefixedElementMatchingPolicy());
            SetXPathLanguageLevel(ic.GetXPathVersion());
            requiredContextItemType = ic.requiredContextItemType;
            SetExecutable(ic.GetExecutable());
            SetOptimizerOptions(ic.GetOptimizerOptions());
        }

        /// <summary>
        /// Create an IndependentContext along with a new (non-schema-aware) Saxon Configuration
        /// </summary>
        public override RetainedStaticContext MakeRetainedStaticContext()
        {
            if (retainedStaticContext == null)
            {
                retainedStaticContext = new RetainedStaticContext(this);
            }

            return retainedStaticContext;
        }

        /// <summary>
        /// Create an IndependentContext along with a new (non-schema-aware) Saxon Configuration
        /// </summary>
        public virtual void DeclareNamespace(string prefix, NamespaceUri uri)
        {
            if (prefix == null)
            {
                throw new NullReferenceException("Null prefix supplied to declareNamespace()");
            }

            if (uri == null)
            {
                throw new NullReferenceException("Null namespace URI supplied to declareNamespace()");
            }

            if ("".Equals(prefix))
            {
                SetDefaultElementNamespace(uri);
            }
            else
            {
                namespaces.Put(prefix, uri);
            }
        }

        /// <summary>
        /// Create an IndependentContext along with a new (non-schema-aware) Saxon Configuration
        /// </summary>
        public override void SetDefaultElementNamespace(NamespaceUri uri)
        {
            if (uri == null)
            {
                uri = NamespaceUri.NULL;
            }

            base.SetDefaultElementNamespace(uri);
            namespaces.Put("", uri);
        }

        /// <summary>
        /// Create an IndependentContext along with a new (non-schema-aware) Saxon Configuration
        /// </summary>
        public virtual void ClearNamespaces()
        {
            namespaces.Clear();
            DeclareNamespace("xml", NamespaceUri.XML);
            DeclareNamespace("xsl", NamespaceUri.XSLT);
            DeclareNamespace("saxon", NamespaceUri.SAXON);
            DeclareNamespace("xs", NamespaceUri.SCHEMA);
            DeclareNamespace("", NamespaceUri.NULL);
        }

        /// <summary>
        /// Create an IndependentContext along with a new (non-schema-aware) Saxon Configuration
        /// </summary>
        public virtual void ClearAllNamespaces()
        {
            namespaces.Clear();
            DeclareNamespace("xml", NamespaceUri.XML);
            DeclareNamespace("", NamespaceUri.NULL);
        }

        /// <summary>
        /// Create an IndependentContext along with a new (non-schema-aware) Saxon Configuration
        /// </summary>
        public virtual void SetNamespaces(NodeInfo node)
        {
            namespaces.Clear();
            int kind = node.GetNodeKind();
            if (kind == Types.Type.ATTRIBUTE || kind == Types.Type.TEXT || kind == Types.Type.COMMENT || kind == Types.Type.PROCESSING_INSTRUCTION || kind == Types.Type.NAMESPACE)
            {
                node = node.GetParent();
            }

            if (node == null)
            {
                return;
            }

            IAxisIterator iter = node.IterateAxis(AxisInfo.NAMESPACE);
            while (true)
            {
                NodeInfo ns = iter.Next();
                if (ns == null)
                {
                    return;
                }

                string prefix = ns.GetLocalPart();
                if ("".Equals(prefix))
                {
                    SetDefaultElementNamespace(NamespaceUri.Of(ns.GetStringValue()));
                }
                else
                {
                    DeclareNamespace(ns.GetLocalPart(), NamespaceUri.Of(ns.GetStringValue()));
                }
            }
        }

        /// <summary>
        /// Create an IndependentContext along with a new (non-schema-aware) Saxon Configuration
        /// </summary>
        public void SetNamespaceResolver(INamespaceResolver resolver)
        {
            externalResolver = resolver;
        }

        /// <summary>
        /// Create an IndependentContext along with a new (non-schema-aware) Saxon Configuration
        /// </summary>
        public virtual void SetAllowUndeclaredVariables(bool allow)
        {
            autoDeclare = allow;
        }

        /// <summary>
        /// Create an IndependentContext along with a new (non-schema-aware) Saxon Configuration
        /// </summary>
        public virtual bool IsAllowUndeclaredVariables()
        {
            return autoDeclare;
        }

        /// <summary>
        /// Create an IndependentContext along with a new (non-schema-aware) Saxon Configuration
        /// </summary>
        public XPathVariable DeclareVariable(QNameValue qname)
        {
            return DeclareVariable(qname.GetStructuredQName());
        }

        /// <summary>
        /// Create an IndependentContext along with a new (non-schema-aware) Saxon Configuration
        /// </summary>
        public XPathVariable DeclareVariable(NamespaceUri namespaceURI, string localName)
        {
            StructuredQName qName = new StructuredQName("", namespaceURI == null ? NamespaceUri.NULL : namespaceURI, localName);
            return DeclareVariable(qName);
        }

        /// <summary>
        /// Create an IndependentContext along with a new (non-schema-aware) Saxon Configuration
        /// </summary>
        public virtual XPathVariable DeclareVariable(StructuredQName qName)
        {
            XPathVariable var = variables.Get(qName);
            if (var != null)
            {
                return var;
            }
            else
            {
                var = XPathVariable.Make(qName);
                int slot = variables.Count;
                var.SetSlotNumber(slot);
                variables.Put(qName, var);
                return var;
            }
        }

        /// <summary>
        /// Create an IndependentContext along with a new (non-schema-aware) Saxon Configuration
        /// </summary>
        public virtual XPathVariable GetExternalVariable(StructuredQName qName)
        {
            return variables.Get(qName);
        }

        /// <summary>
        /// Create an IndependentContext along with a new (non-schema-aware) Saxon Configuration
        /// </summary>
        public virtual int GetSlotNumber(QNameValue qname)
        {
            StructuredQName sq = qname.GetStructuredQName();
            XPathVariable var = variables.Get(sq);
            if (var == null)
            {
                return -1;
            }

            return var.LocalSlotNumber;
        }

        /// <summary>
        /// Create an IndependentContext along with a new (non-schema-aware) Saxon Configuration
        /// </summary>
        public override INamespaceResolver GetNamespaceResolver()
        {
            if (externalResolver != null)
            {
                return externalResolver;
            }
            else
            {
                return this;
            }
        }

        /// <summary>
        /// Create an IndependentContext along with a new (non-schema-aware) Saxon Configuration
        /// </summary>
        public NamespaceUri GetURIForPrefix(string prefix, bool useDefault)
        {
            if (externalResolver != null)
            {
                return externalResolver.GetURIForPrefix(prefix, useDefault);
            }

            if ((prefix.Length == 0))
            {
                return useDefault ? GetDefaultElementNamespace() : NamespaceUri.NULL;
            }
            else
            {
                return namespaces.Get(prefix);
            }
        }

        /// <summary>
        /// Create an IndependentContext along with a new (non-schema-aware) Saxon Configuration
        /// </summary>
        public IEnumerator<string> IteratePrefixes()
        {
            if (externalResolver != null)
            {
                return externalResolver.IteratePrefixes();
            }
            else
            {
                return namespaces.KeySet().IIterator();
            }
        }

        /// <summary>
        /// Create an IndependentContext along with a new (non-schema-aware) Saxon Configuration
        /// </summary>
        public override Expression BindVariable(StructuredQName qName)
        {
            XPathVariable var = variables.Get(qName);
            if (var == null)
            {
                if (autoDeclare)
                {
                    return new LocalVariableReference(DeclareVariable(qName));
                }
                else
                {
                    throw new XPathException("Undeclared variable in XPath expression: $" + qName.ClarkName, "XPST0008");
                }
            }
            else
            {
                return new LocalVariableReference(var);
            }
        }

        /// <summary>
        /// Create an IndependentContext along with a new (non-schema-aware) Saxon Configuration
        /// </summary>
        public SlotManager GetStackFrameMap()
        {
            SlotManager map = GetConfiguration().MakeSlotManager();
            XPathVariable[] va = new XPathVariable[variables.Count];
            foreach (XPathVariable var in variables.Values)
            {
                va[var.LocalSlotNumber] = var;
            }

            foreach (XPathVariable v in va)
            {
                map.AllocateSlotNumber(v.GetVariableQName(), v);
            }

            return map;
        }

        /// <summary>
        /// Create an IndependentContext along with a new (non-schema-aware) Saxon Configuration
        /// </summary>
        public override bool IsImportedSchema(NamespaceUri @namespace)
        {
            return importedSchemaNamespaces.Contains(@namespace);
        }

        /// <summary>
        /// Create an IndependentContext along with a new (non-schema-aware) Saxon Configuration
        /// </summary>
        public override HashSet<NamespaceUri> GetImportedSchemaNamespaces()
        {
            return importedSchemaNamespaces;
        }

        /// <summary>
        /// Create an IndependentContext along with a new (non-schema-aware) Saxon Configuration
        /// </summary>
        public virtual void SetImportedSchemaNamespaces(HashSet<NamespaceUri> namespaces)
        {
            importedSchemaNamespaces = namespaces;
            if (!namespaces.IsEmpty())
            {
                SetSchemaAware(true);
            }
        }

        /// <summary>
        /// Create an IndependentContext along with a new (non-schema-aware) Saxon Configuration
        /// </summary>
        public virtual void SetRequiredContextItemType(Types.ItemType type)
        {
            requiredContextItemType = type;
        }

        /// <summary>
        /// Create an IndependentContext along with a new (non-schema-aware) Saxon Configuration
        /// </summary>
        public override Types.ItemType GetRequiredContextItemType()
        {
            return requiredContextItemType;
        }

        /// <summary>
        /// Create an IndependentContext along with a new (non-schema-aware) Saxon Configuration
        /// </summary>
        public virtual void SetOptimizerOptions(OptimizerOptions options)
        {
            this.optimizerOptions = options;
        }

        /// <summary>
        /// Create an IndependentContext along with a new (non-schema-aware) Saxon Configuration
        /// </summary>
        public override OptimizerOptions GetOptimizerOptions()
        {
            return this.optimizerOptions;
        }

        /// <summary>
        /// Create an IndependentContext along with a new (non-schema-aware) Saxon Configuration
        /// </summary>
        public virtual void SetExecutable(Executable exec)
        {
            executable = exec;
        }

        /// <summary>
        /// Create an IndependentContext along with a new (non-schema-aware) Saxon Configuration
        /// </summary>
        public virtual Executable GetExecutable()
        {
            return executable;
        }

        /// <summary>
        /// Create an IndependentContext along with a new (non-schema-aware) Saxon Configuration
        /// </summary>
        public virtual int GetColumnNumber()
        {
            return -1;
        }

        /// <summary>
        /// Create an IndependentContext along with a new (non-schema-aware) Saxon Configuration
        /// </summary>
        public virtual string GetPublicId()
        {
            return null;
        }

        /// <summary>
        /// Create an IndependentContext along with a new (non-schema-aware) Saxon Configuration
        /// </summary>
        public virtual int GetLineNumber()
        {
            return -1;
        }

        /// <summary>
        /// Create an IndependentContext along with a new (non-schema-aware) Saxon Configuration
        /// </summary>
        public bool IsContextItemParentless()
        {
            return parentlessContextItem;
        }

        /// <summary>
        /// Create an IndependentContext along with a new (non-schema-aware) Saxon Configuration
        /// </summary>
        public virtual void SetContextItemParentless(bool parentless)
        {
            parentlessContextItem = parentless;
        }
    }
}
