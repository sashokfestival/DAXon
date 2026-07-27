////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Functions;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
using System.IO;
namespace OutSmart.DAXon.Core.PlatformImpl
{
    public class CleanerProxy
    {
        private readonly Cleaner cleaner;
        private CleanerProxy(Cleaner cleaner)
        {
            this.cleaner = cleaner;
        }

        public static CleanerProxy MakeCleanerProxy(Configuration config)
        {
            return new CleanerProxy(Cleaner.Create());
        }

        public virtual CleanableProxy RegisterCleanupAction(object obj, Action runnable)
        {
            if (cleaner != null)
            {
                object cleanable = cleaner.Register(obj, runnable);
                return new CleanableProxy(cleanable);
            }
            else
            {
                return null;
            }
        }

        public class CleanableProxy
        {
            private readonly object cleanable;
            public CleanableProxy(object cleanable)
            {
                this.cleanable = cleanable;
            }

            public virtual void Clean()
            {
                ((Cleanable)cleanable).Clean();
            }
        }
    }
}