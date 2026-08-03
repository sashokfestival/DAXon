////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Model;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Xslt;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions.Parsing
{
    public class ParserExtension
    {
        public ParserExtension()
        {
        }

        public virtual void NeedExtension(XPathParser p, string what)
        {
            p.Grumble(what + " require support for Saxon extensions, available in Saxon-PE or higher");
        }

        private void NeedUpdate(XPathParser p, string what)
        {
            p.Grumble(what + " requires support for XQuery Update, available in Saxon-EE or higher");
        }

        public virtual void HandleExternalFunctionDeclaration(XQueryParser p, XQueryFunction func)
        {
            NeedExtension(p, "External function declarations");
        }

        public virtual ItemType ParseExtendedItemType(XPathParser p)
        {
            return null;
        }

        public virtual Expression ParseTypePattern(XPathParser p)
        {
            NeedExtension(p, "type-based patterns");
            return null;
        }

        public virtual void ParseItemTypeDeclaration(XQueryParser p)
        {
            NeedExtension(p, "Item type declarations");
        }

        public virtual void ParseRevalidationDeclaration(XQueryParser p)
        {
            NeedUpdate(p, "A revalidation declaration");
        }

        public virtual void ParseUpdatingFunctionDeclaration(XQueryParser p)
        {
            NeedUpdate(p, "An updating function");
        }

        public virtual Expression ParseExtendedExprSingle(XPathParser p)
        {
            return null;
        }

        internal class TemporaryXSLTVariableBinding : ILocalBinding
        {
            public SourceBinding declaration;

            public virtual int LocalSlotNumber => 0;

            public virtual IntegerValue[] IntegerBoundsForVariable => null;
            public TemporaryXSLTVariableBinding(SourceBinding decl)
            {
                this.declaration = decl;
            }

            public virtual SequenceType GetRequiredType()
            {
                return declaration.GetInferredType(true);
            }

            public virtual ISequence EvaluateVariable(IXPathContext context)
            {
                throw new NotSupportedException();
            }

            public virtual bool IsGlobal()
            {
                return false;
            }

            public virtual bool IsAssignable()
            {
                return false;
            }

            public virtual StructuredQName GetVariableQName()
            {
                return declaration.VariableQName;
            }

            public virtual void AddReference(VariableReference @ref, bool isLoopingReference)
            {
            }

            public virtual void SetIndexedVariable()
            {
            }

            public virtual bool IsIndexedVariable()
            {
                return false;
            }
        }
    }
}