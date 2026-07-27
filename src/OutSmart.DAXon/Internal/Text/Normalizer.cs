////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// java.time stubs.
using System;

namespace OutSmart.DAXon.Internal.Text
{
    public sealed class Normalizer
    {
        public static string Normalize(string s, Form form) => form switch
        {
            Form.NFC => s.Normalize(global::System.Text.NormalizationForm.FormC),
            Form.NFD => s.Normalize(global::System.Text.NormalizationForm.FormD),
            Form.NFKC => s.Normalize(global::System.Text.NormalizationForm.FormKC),
            Form.NFKD => s.Normalize(global::System.Text.NormalizationForm.FormKD),
            _ => s
        };
        public static bool IsNormalized(string s, Form form) => s.IsNormalized();
        public enum Form { NFC, NFD, NFKC, NFKD }
    }
}
