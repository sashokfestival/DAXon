////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions
{
    public class DefaultedArgumentExpression : PseudoExpression
    {

        /// <summary>
        /// Constructor
        /// </summary>
        public override string ExpressionName => "defaultValue";
        /// <summary>
        /// Constructor
        /// </summary>
        public DefaultedArgumentExpression()
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public override Expression Copy(RebindingMap rebindings)
        {
            return this;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public override void Export(ExpressionPresenter destination)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public override Elaborator GetElaborator()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public class DefaultCollationArgument : DefaultedArgumentExpression
        {
            public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
            {
                return new StringLiteral(visitor.StaticContext.GetDefaultCollationName());
            }
        }
    }
}