////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Events;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Transformation;
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
namespace OutSmart.DAXon.Xslt
{
    public class XSLImportSchema : StyleElement
    {
        public override bool IsDeclaration()
        {
            return true;
        }

        public override void PrepareAttributes()
        {
            string @namespace = null;
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string value = att.Value;
                string f = attName.DisplayName;
                if (f.Equals("schema-location"))
                {
                }
                else if (f.Equals("namespace"))
                {
                    @namespace = Whitespace.Trim(value);
                }
                else
                {
                    CheckUnknownAttribute(attName);
                }
            }

            if ("".Equals(@namespace))
            {
                CompileError("The zero-length string is not a valid namespace URI. " + "For a schema with no @namespace, omit the namespace attribute");
            }
        }

        //
        public override void Validate(ComponentDeclaration decl)
        {
            CheckTopLevel("XTSE0010", false);
        }

        //
        public override void Index(ComponentDeclaration decl, PrincipalStylesheetModule top)
        {
        }

        //
        //
        public virtual void ReadSchema()
        {
            try
            {
                string schemaLoc = Whitespace.Trim(GetAttributeValue(NamespaceUri.NULL, "schema-location"));
                string @namespace = Whitespace.Trim(GetAttributeValue(NamespaceUri.NULL, "namespace"));
                if (@namespace == null)
                {
                    @namespace = "";
                }
                else
                {
                    @namespace = @namespace.Trim();
                }

                Configuration config = GetConfiguration();
                try
                {
                    config.CheckLicensedFeature(Configuration.LicenseFeature.ENTERPRISE_XSLT, "xsl:import-schema", GetPackageData().LocalLicenseId);
                }
                catch (LicenseException err)
                {
                    throw new XPathException(err?.Message).WithErrorCode("XTSE1650").WithLocation(this);
                }

                NodeInfo inlineSchema = null;
                NamespaceUri targetNamespace = null;
                foreach (NodeInfo child in Children())
                {
                    if (inlineSchema != null)
                    {
                        CompileError(DisplayName + " must not have more than one child element");
                    }

                    inlineSchema = child;
                    if (inlineSchema.Fingerprint != StandardNames.XS_SCHEMA)
                    {
                        CompileError("The only child element permitted for " + DisplayName + " is xs:schema");
                    }

                    if (schemaLoc != null)
                    {
                        CompileError("The schema-location attribute must be absent if an inline schema is present", "XTSE0215");
                    }

                    if ((@namespace.Length == 0))
                    {
                        @namespace = inlineSchema.GetAttributeValue(NamespaceUri.NULL, "targetNamespace");
                        if (@namespace == null)
                        {
                            @namespace = "";
                        }
                    }

                    targetNamespace = NamespaceUri.Of(@namespace);
                    targetNamespace = config.ReadInlineSchema(inlineSchema, targetNamespace, GetCompilation().GetCompilerInfo().ErrorReporter);
                    GetPrincipalStylesheetModule().AddImportedSchema(targetNamespace);
                }

                if (inlineSchema != null)
                {
                    return;
                }

                if (@namespace.Equals(NamespaceConstant.XML) || @namespace.Equals(NamespaceConstant.FN) || @namespace.Equals(NamespaceConstant.SCHEMA_INSTANCE))
                {
                    targetNamespace = NamespaceUri.Of(@namespace);
                    config.AddSchemaForBuiltInNamespace(targetNamespace);
                    GetPrincipalStylesheetModule().AddImportedSchema(targetNamespace);
                    return;
                }

                targetNamespace = NamespaceUri.Of(@namespace);
                bool namespaceKnown = config.IsSchemaAvailable(targetNamespace);
                if (schemaLoc == null && !namespaceKnown)
                {
                    IssueWarning("No schema for this namespace is known, " + "and no schema-location was supplied, so no schema has been imported", DAXonErrorCode.SXWN9006);
                    return;
                }

                if (namespaceKnown && !config.GetBooleanProperty(Feature<bool>.MULTIPLE_SCHEMA_IMPORTS))
                {
                    if (schemaLoc != null)
                    {
                        IssueWarning("The schema document at " + schemaLoc + " is ignored because a schema for this namespace is already loaded", DAXonErrorCode.SXWN9006);
                    }
                }

                if (!namespaceKnown)
                {
                    PipelineConfiguration pipe = config.MakePipelineConfiguration();

                    //                ISchemaURIResolver schemaResolver = config.makeSchemaURIResolver(
                    pipe.SetErrorReporter(GetCompilation().GetCompilerInfo().ErrorReporter);
                    targetNamespace = config.ReadSchema(pipe, GetBaseURI(), schemaLoc, targetNamespace);
                }

                GetPrincipalStylesheetModule().AddImportedSchema(targetNamespace);
            }
            catch (SchemaException err)
            {
                if (err.ErrorCodeQName == null)
                {
                    CompileError(err.Message, "XTSE0220");
                }
                else
                {
                    CompileError(err.Message, err.ErrorCodeQName);
                }
            }
        }

        //
        //
        public override void CompileDeclaration(Compilation compilation, ComponentDeclaration decl)
        {
        }
    }
}