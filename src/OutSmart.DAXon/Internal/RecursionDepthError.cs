////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;

namespace OutSmart.DAXon.Internal
{

    /// <summary>Recursion-depth-exceeded error (the functional-recursion guard's signal; upstream
    /// Saxon raised java.lang.StackOverflowError here). Carried as an XPathException cause.</summary>
    public class RecursionDepthError : global::System.Exception
    {
        public RecursionDepthError() : base("") { }
        public RecursionDepthError(string m) : base(m) { }
    }
}
