////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Internal.Collections;
using OutSmart.DAXon.Internal.Functional;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;

using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Api
{
    /// <summary>
    /// A helper class for implementing the {@link IDestination} interface
    /// </summary>
    public class DestinationHelper
    {
        private readonly IDestination helpee;
        private readonly IList<IAction> listeners = new List<IAction>();

        public virtual IList<IAction> Listeners => listeners;
        public DestinationHelper(IDestination helpee)
        {
            this.helpee = helpee;
        }

        public void OnClose(IAction listener)
        {
            listeners.Add(listener);
        }

        public virtual void CloseAndNotify()
        {
            helpee.Dispose();
            foreach (IAction action in listeners)
            {
                action.Act();
            }
        }
    }
}