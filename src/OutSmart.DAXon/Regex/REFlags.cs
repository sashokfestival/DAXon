////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Regex
{
    public class REFlags
    {
        private bool caseIndependent;
        private bool multiLine;
        private bool singleLine;
        private bool allowWhitespace;
        private bool literal;
        private bool xpath20;
        private bool xpath30;
        private bool xsd11;
        private bool debug; // flags = ";g"
        private bool allowUnknownBlockNames = false; //flags = ";k"
        public REFlags(string flags, string language)
        {
            if (language.Equals("XSD10"))
            {
            }
            else if (language.Contains("XSD11"))
            {
                allowUnknownBlockNames = !language.Contains("XP");
                xsd11 = true;
            }

            if (language.Contains("XP20"))
            {
                xpath20 = true;
            }
            else if (language.Contains("XP30") || language.Contains("XP31"))
            {
                xpath20 = true;
                xpath30 = true;
            }

            int semi = flags.IndexOf(';');
            int endStd = semi >= 0 ? semi : flags.Length;
            for (int i = 0; i < endStd; i++)
            {
                char c = flags[i];
                switch (c)
                {
                    case 'i':
                        caseIndependent = true;
                        break;
                    case 'm':
                        multiLine = true;
                        break;
                    case 's':
                        singleLine = true;
                        break;
                    case 'q':
                        literal = true;
                        if (!xpath30)
                        {
                            throw new RESyntaxException("'q' flag requires XPath 3.0 to be enabled");
                        }

                        break;
                    case 'x':
                        allowWhitespace = true;
                        break;
                    default:
                        throw new RESyntaxException("Unrecognized flag '" + c + "'");
                }
            }

            for (int i = semi + 1; i < flags.Length; i++)
            {
                char c = flags[i];
                switch (c)
                {
                    case 'g':
                        debug = true;
                        break;
                    case 'k':
                        allowUnknownBlockNames = true;
                        break;
                    case 'K':
                        allowUnknownBlockNames = false;
                        break;
                }
            }
        }

        public virtual bool IsCaseIndependent()
        {
            return caseIndependent;
        }

        public virtual bool IsMultiLine()
        {
            return multiLine;
        }

        public virtual bool IsSingleLine()
        {
            return singleLine;
        }

        public virtual bool IsAllowWhitespace()
        {
            return allowWhitespace;
        }

        public virtual bool IsLiteral()
        {
            return literal;
        }

        public virtual bool IsAllowsXPath20Extensions()
        {
            return xpath20;
        }

        public virtual bool IsAllowsXPath30Extensions()
        {
            return xpath30;
        }

        public virtual bool IsAllowsXSD11Syntax()
        {
            return xsd11;
        }

        public virtual void SetDebug(bool debug)
        {
            this.debug = debug;
        }

        public virtual bool IsDebug()
        {
            return debug;
        }

        public virtual void SetAllowUnknownBlockNames(bool allow)
        {
            this.allowUnknownBlockNames = allow;
        }

        public virtual bool IsAllowUnknownBlockNames()
        {
            return allowUnknownBlockNames;
        }
    }
}