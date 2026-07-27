////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Text;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// Implement the XPath normalize-unicode() function (both the 1-argument and 2-argument versions)
    /// </summary>
    public class NormalizeUnicode : SystemFunction
    {

        public static Func<NormalizeUnicode> New() => () => new NormalizeUnicode();
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            StringValue sv = (StringValue)arguments[0].Head();
            if (sv == null)
            {
                return StringValue.EMPTY_STRING;
            }

            string nf = arguments.Length == 1 ? "NFC" : Whitespace.Trim(arguments[1].Head().GetStringValue());
            return new StringValue(Normalize(sv.GetStringValue(), nf));
        }

        public static string Normalize(string sv, string form)
        {
            Normalizer.Form fb;
            if (form.EqualsIgnoreCase("NFC"))
            {
                fb = Normalizer.Form.NFC;
            }
            else if (form.EqualsIgnoreCase("NFD"))
            {
                fb = Normalizer.Form.NFD;
            }
            else if (form.EqualsIgnoreCase("NFKC"))
            {
                fb = Normalizer.Form.NFKC;
            }
            else if (form.EqualsIgnoreCase("NFKD"))
            {
                fb = Normalizer.Form.NFKD;
            }
            else if ((form.Length == 0))
            {
                return sv;
            }
            else
            {
                string msg = "Normalization form " + form + " is not supported";
                throw new XPathException(msg, "FOCH0003");
            }

            try
            {
                return Normalizer.Normalize(sv, fb);
            }
            catch (System.ArgumentException)
            {
                // .NET's String.Normalize rejects a whole string that contains any noncharacter code point
                // (U+FDD0–U+FDEF, U+xFFFE/U+xFFFF) with ArgumentException; Java's ICU normalizer passes each
                // noncharacter through unchanged (they have no decomposition/composition) while still
                // normalizing the surrounding text. Match Java by normalizing each maximal noncharacter-free
                // run and re-inserting the noncharacters verbatim.
                return NormalizeAroundNonChars(sv, fb);
            }
        }

        private static bool IsNonChar(int cp)
        {
            return (cp >= 0xFDD0 && cp <= 0xFDEF) || (cp & 0xFFFE) == 0xFFFE;
        }

        private static string NormalizeAroundNonChars(string sv, Normalizer.Form fb)
        {
            StringBuilder outb = new StringBuilder(sv.Length);
            StringBuilder run = new StringBuilder();
            int i = 0;
            while (i < sv.Length)
            {
                char c = sv[i];
                bool pair = char.IsHighSurrogate(c) && i + 1 < sv.Length && char.IsLowSurrogate(sv[i + 1]);
                int cp = pair ? char.ConvertToUtf32(c, sv[i + 1]) : c;
                int adv = pair ? 2 : 1;
                if (IsNonChar(cp))
                {
                    if (run.Length > 0) { outb.Append(Normalizer.Normalize(run.ToString(), fb)); run.Clear(); }
                    outb.Append(sv, i, adv);
                }
                else
                {
                    run.Append(sv, i, adv);
                }

                i += adv;
            }

            if (run.Length > 0) { outb.Append(Normalizer.Normalize(run.ToString(), fb)); }
            return outb.ToString();
        }
    }
}