////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Types;

namespace OutSmart.DAXon.Lib
{
    // Phase 7.8f: extends OutSmart.DAXon.Types.StringConverter so call sites
    // returning this through StringConverter-typed slots compile.
    public class StringToNonStringDerivedType : StringConverter
    {
        public StringToNonStringDerivedType() { }
        public StringToNonStringDerivedType(object a, object b) { }
        public override IConversionResult ConvertString(UnicodeString input) => throw new NotImplementedException("STUB: StringToNonStringDerivedType.ConvertString not ported (excluded stub)");
    }
}
