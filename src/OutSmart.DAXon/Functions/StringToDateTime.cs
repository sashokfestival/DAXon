////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal.Functional;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Types;

// Phase 7.8: UnicodeChar.cs re-included into project. Stub removed to avoid CS0101.
// namespace OutSmart.DAXon.Text
// {
//     public class UnicodeChar { public UnicodeChar() {} public UnicodeChar(int cp) {} }
// }

namespace OutSmart.DAXon.Functions
{
    // New() must qualify StringToDateTime: this stub extends StringConverter, which has an inherited
    // nested type also named StringToDateTime (ctor (ConversionRules) only), so the bare name bound to
    // the inherited nested type and `new StringToDateTime()` failed with CS7036. Qualify to this stub.
    public class StringToDateTime : StringConverter
    {
        public StringToDateTime() { }
        public StringToDateTime(object rules) { }
        public static Func<Functions.StringToDateTime> New() => () => new Functions.StringToDateTime();
        public override IConversionResult ConvertString(UnicodeString input) => throw new NotImplementedException("STUB: StringToDateTime.ConvertString not ported (excluded stub)");
    }
}
