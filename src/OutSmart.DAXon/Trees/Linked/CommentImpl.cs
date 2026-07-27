////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;

namespace OutSmart.DAXon.Trees.Linked
{
    public class CommentImpl : NodeImpl
    {
        public CommentImpl() { }
        public CommentImpl(object data) { }
        public void SetLocation(object _loc) { }
        public void SetLocation(string systemId, int line, int column) { } /* Phase B: real CommentImpl.SetLocation(string,int,int); LinkedTreeBuilder calls it (CS1501), mirrors ProcInstImpl stub */
    }
}
