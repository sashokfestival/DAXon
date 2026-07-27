////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// java.time stubs.
using System;

namespace OutSmart.DAXon.Internal.Collections
{
    public sealed class Locale
    {
        public static readonly Locale ENGLISH = new Locale("en");
        public static readonly Locale US = new Locale("en", "US");
        public static Locale Default => ENGLISH;
        public string Language { get; }
        public string Country { get; }
        public Locale(string lang) { Language = lang; Country = ""; }
        public Locale(string lang, string country) { Language = lang; Country = country; }
        public string GetLanguage() => Language;
        public string GetCountry() => Country;
        public override string ToString() => string.IsNullOrEmpty(Country) ? Language : Language + "_" + Country;
    }
}
