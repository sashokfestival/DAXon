////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OutSmart.DAXon.Xslt
{
    // Faithful port of net.sf.saxon.style.PackageVersion (Saxon 12.9). Was a hollow compat stub
    // (CompareTo => 0, no Equals/GetHashCode) — VersionedPackageName dictionary lookups always missed,
    // so every xsl:use-package raised XTSE3000 "Cannot find package".
    // An XSLT package version such as 1.12.5 or 3.0-alpha, per
    // http://www.w3.org/TR/xslt-30/#package-versions: dot-separated integers optionally followed by '-'NCName.
    public class PackageVersion : IComparable<PackageVersion>
    {

        public static readonly PackageVersion ZERO = new PackageVersion(new int[] { 0 });
        public static readonly PackageVersion ONE = new PackageVersion(new int[] { 1 });
        public static readonly PackageVersion MAX_VALUE = new PackageVersion(new int[] { int.MaxValue });
        public List<int> parts;
        public string suffix;

        /// <summary>
        /// Return a package version defined by a fixed sequence of int values, which implies no suffix
        /// </summary>
        public PackageVersion(int[] values)
        {
            parts = new List<int>(values);
            TrimTrailingZeroes();
        }

        /// <summary>
        /// Generate a package version from a string description per the XSLT 3.0 grammar.
        /// </summary>
        public PackageVersion(string s)
        {
            parts = new List<int>();
            string original = s;
            if (s.Contains("-"))
            {
                int i = s.IndexOf('-');
                suffix = s.Substring(i + 1);
                if (!NameChecker.IsValidNCName(suffix))
                {
                    throw new XPathException("Illegal NCName as package-version NamePart: " + original, "XTSE0020");
                }

                s = s.Substring(0, i);
            }

            if (s.Equals(""))
            {
                throw new XPathException("No numeric component of package-version: " + original, "XTSE0020");
            }

            if (s.StartsWith(".", StringComparison.Ordinal))
            {
                throw new XPathException("The package-version cannot start with '.'", "XTSE0020");
            }

            if (s.EndsWith(".", StringComparison.Ordinal))
            {
                throw new XPathException("The package-version cannot end with '.'", "XTSE0020");
            }

            foreach (string p in s.Trim().Split('.'))
            {
                parts.Add(ParseInteger(p));
            }

            TrimTrailingZeroes();
        }

        private void TrimTrailingZeroes()
        {
            for (int i = parts.Count - 1; i > 0; i--)
            {
                if (parts[i] != 0)
                {
                    return;
                }
                else
                {
                    parts.RemoveAt(i);
                }
            }
        }

        public static int ParseInteger(string s)
        {
            try
            {
                return int.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (Exception e)
            {
                throw new XPathException("Error in package-version: " + e.Message, "XTSE0020");
            }
        }

        public override bool Equals(object o)
        {
            if (o is PackageVersion)
            {
                PackageVersion p = (PackageVersion)o;
                if (parts.SequenceEqual(p.parts))
                {
                    if (suffix != null)
                    {
                        return suffix.Equals(p.suffix);
                    }
                    else
                    {
                        return p.suffix == null;
                    }
                }
            }

            return false;
        }

        public override int GetHashCode()
        {
            int h = 772211;
            foreach (int p in parts)
            {
                h = (h << 3) ^ p;
            }

            if (suffix != null)
            {
                h = (h << 3) ^ suffix.GetHashCode();
            }

            return h;
        }

        /// <summary>
        /// Compare two version numbers for equality, ignoring the suffix part: 2.1-alpha equals 2.1-beta.
        /// </summary>
        public virtual bool EqualsIgnoringSuffix(PackageVersion other)
        {
            return parts.SequenceEqual(other.parts);
        }

        public virtual int CompareTo(PackageVersion o)
        {
            PackageVersion pv = o;
            List<int> p = pv.parts;
            int extent = parts.Count - p.Count;
            int len = Math.Min(parts.Count, p.Count);
            for (int i = 0; i < len; i++)
            {
                int comp = parts[i].CompareTo(p[i]);
                if (comp != 0)
                {
                    return comp;
                }
            }

            if (extent == 0)
            {
                if (suffix != null)
                {
                    if (pv.suffix == null)
                    {
                        return -1;
                    }
                    else
                    {
                        return string.CompareOrdinal(suffix, pv.suffix);
                    }
                }
                else if (pv.suffix != null)
                {
                    return +1;
                }
            }

            return extent;
        }

        public override string ToString()
        {
            StringBuilder result = new StringBuilder();
            foreach (int i in parts)
            {
                result.Append(".").Append(i);
            }

            if (parts.Count != 0)
            {
                result = new StringBuilder(result.ToString(1, result.Length - 1));
            }

            if (suffix != null)
            {
                result.Append("-").Append(suffix);
            }

            return result.ToString();
        }

        /// <summary>
        /// Tests whether this package version is a prefix (shares all its components in order)
        /// of another package version, and thus this version.* should match it.
        /// </summary>
        public virtual bool IsPrefix(PackageVersion v)
        {
            if (v.parts.Count >= parts.Count)
            {
                for (int i = 0; i < parts.Count; i++)
                {
                    if (parts[i] != v.parts[i])
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }
    }
}
