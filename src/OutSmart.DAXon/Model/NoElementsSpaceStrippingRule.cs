////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Types;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Model
{
    /// <summary>
    /// A whitespace stripping rule that retains all whitespace text nodes
    /// </summary>
    internal class NoElementsSpaceStrippingRule : ISpaceStrippingRule
    {
        private static readonly NoElementsSpaceStrippingRule THE_INSTANCE = new NoElementsSpaceStrippingRule();
        public static NoElementsSpaceStrippingRule GetInstance()
        {
            return THE_INSTANCE;
        }

        public virtual int IsSpacePreserving(INodeName fingerprint, ISchemaType schemaType)
        {
            return Stripper.ALWAYS_PRESERVE;
        }

        public virtual ProxyReceiver MakeStripper(IReceiver next)
        {
            return null;
        }

        public virtual void Export(ExpressionPresenter presenter)
        {
            presenter.StartElement("strip.none");
            presenter.EndElement();
        }
    }
}