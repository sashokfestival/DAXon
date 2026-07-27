////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System.Collections.Generic;
using System.IO;

namespace OutSmart.DAXon.Internal.Collections
{
    /// <summary>
    /// Java Properties shim — string-keyed string-valued map with load/store and defaults.
    /// Minimal implementation sufficient for Saxon config loading.
    /// </summary>
    public class Properties : Dictionary<string, string>
    {
        private readonly Properties _defaults;

        public Properties() { }
        public Properties(Properties defaults) { _defaults = defaults; }

        public string GetProperty(string key)
        {
            if (TryGetValue(key, out var v))
                return v;
            return _defaults?.GetProperty(key);
        }
        public string GetProperty(string key, string defaultValue) => GetProperty(key) ?? defaultValue;
        public object SetProperty(string key, string value) { TryGetValue(key, out var prev); this[key] = value; return prev; }

        public ICollection<string> StringPropertyNames() => Keys;

        public void Load(TextReader reader)
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (line.Length == 0 || line.StartsWith("#", global::System.StringComparison.Ordinal) || line.StartsWith("!", global::System.StringComparison.Ordinal))
                    continue;
                var eq = line.IndexOfAny(new[] { '=', ':' });
                if (eq < 0)
                    this[line] = "";
                else
                    this[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }
        }
        public void Load(global::System.IO.Stream s) { using (var r = new StreamReader(s)) Load(r); }

        public void Store(TextWriter writer, string comments)
        {
            if (comments != null)
                writer.WriteLine("# " + comments);
            foreach (var kv in this)
                writer.WriteLine(kv.Key + "=" + kv.Value);
        }
    }
}
