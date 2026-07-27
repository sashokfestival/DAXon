////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;

namespace OutSmart.DAXon.Api
{
    public static class Steps
    {
        public static object Child() => throw new NotImplementedException("STUB: Steps.Child not ported (excluded stub)");
        public static object Child(string name) => throw new NotImplementedException("STUB: Steps.Child not ported (excluded stub)");
        public static object Child(string uri, string local) => throw new NotImplementedException("STUB: Steps.Child not ported (excluded stub)");
        public static object Child(Func<object, bool> filter) => throw new NotImplementedException("STUB: Steps.Child not ported (excluded stub)");
        public static object Ancestor() => throw new NotImplementedException("STUB: Steps.Ancestor not ported (excluded stub)");
        public static object AncestorOrSelf() => throw new NotImplementedException("STUB: Steps.AncestorOrSelf not ported (excluded stub)");
        public static object AncestorOrSelf(object test) => throw new NotImplementedException("STUB: Steps.AncestorOrSelf not ported (excluded stub)");
        public static object Descendant() => throw new NotImplementedException("STUB: Steps.Descendant not ported (excluded stub)");
        public static object Parent() => throw new NotImplementedException("STUB: Steps.Parent not ported (excluded stub)");
        public static object Self() => throw new NotImplementedException("STUB: Steps.Self not ported (excluded stub)");
        public static object Attribute() => throw new NotImplementedException("STUB: Steps.Attribute not ported (excluded stub)");
    }
}
