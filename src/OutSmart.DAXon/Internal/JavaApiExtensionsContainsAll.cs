////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Generic;
using System.Linq;

namespace OutSmart.DAXon.Internal
{

    // Stub class for OutSmart.DAXon.Internal.System -- callers of System.NanoTime() / GetProperty() / etc.
    // are handled by Fix-Phase48-Java-System-Calls.ps1 patch already; this class exists
    // mainly so that `using OutSmart.DAXon.Internal;` doesn't cause failures where bare `System.X` is
    // used. We DO NOT define members here to avoid shadowing global::System.

    public static partial class JavaApiExtensionsContainsAll
    {
        // Phase 7.8: Java's StringBuilder.substring(start, end) -> ToString().Substring(start, end-start).
        public static string Substring(this global::System.Text.StringBuilder sb, int start) => sb.ToString().Substring(start);
        public static string Substring(this global::System.Text.StringBuilder sb, int start, int end) => sb.ToString().Substring(start, end - start);

        // Phase 7.8: Java's Collection.containsAll(c) -> all of c are contained.
        public static bool ContainsAll<T>(this global::System.Collections.Generic.HashSet<T> set, global::System.Collections.Generic.IEnumerable<T> other)
        {
            if (other == null)
                return true;
            foreach (var x in other) { if (!set.Contains(x)) return false; }
            return true;
        }
        public static bool ContainsAll<T>(this global::System.Collections.Generic.ICollection<T> set, global::System.Collections.Generic.IEnumerable<T> other)
        {
            if (other == null)
                return true;
            foreach (var x in other) { if (!set.Contains(x)) return false; }
            return true;
        }

        // Phase 7.8f: Java Predicate<T>.test(x) -> C# invocation. Only the Func<T,bool>
        // form lives here; the System.Predicate<T> form is in Util/Function/Functional.cs
        // (FunctionalExtensions.Test) -- having both caused CS0121 ambiguity.
        public static bool Test<T>(this global::System.Func<T, bool> f, T value) => f(value);
        // Phase 7.8f: Java Collection.addAll(c) on .NET Stack<T> (no native equivalent).
        public static void AddAll<T>(this global::System.Collections.Generic.Stack<T> s, global::System.Collections.Generic.IEnumerable<T> items) { foreach (var i in items) s.Push(i); }

        // Phase 7.8f: Java Stack.empty() -> Count == 0.
        public static bool Empty<T>(this global::System.Collections.Generic.Stack<T> s) => s.Count == 0;
    }
}
