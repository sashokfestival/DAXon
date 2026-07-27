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
    /// <summary>
    /// Extension methods that bridge common Java API differences without
    /// requiring source-level rewrites. Sites like `someList.IsEmpty()` or
    /// `str.IsEmpty()` resolve to these extension methods instead of CS1061.
    ///
    /// Added Phase 4.7c -- targets the largest remaining cascade contributor
    /// (382 IsEmpty errors) without rewriting hundreds of call sites.
    ///
    /// NOTE: located in `OutSmart.DAXon.Internal` namespace (not `Java`) because nearly every
    /// generated Saxon file has `using OutSmart.DAXon.Internal;` -- the extensions become
    /// visible without additional using directives.
    /// </summary>
    public static class JavaApiExtensions
    {
        // String.isEmpty() -- Java's instance method, no C# equivalent.
        // Java semantics: throws if null. We preserve that by using .Length.
        public static bool IsEmpty(this string s) => s.Length == 0;

        // Collection<T>.isEmpty() -- generic.
        public static bool IsEmpty<T>(this ICollection<T> c) => c.Count == 0;

        // IDictionary<K,V>.isEmpty() -- generic dictionaries.
        public static bool IsEmpty<TKey, TValue>(this IDictionary<TKey, TValue> d) => d.Count == 0;

        // IEnumerable<T>.isEmpty() -- catch-all for LINQ-able sequences.
        // Note: avoid this on Sequences that materialize -- it iterates one element.
        public static bool IsEmpty<T>(this IEnumerable<T> seq) => !seq.Any();

        // String.charAt(int) -- already handled by indexer fix mostly, but for
        // safety expose as extension for any leftover sites.
        public static char CharAt(this string s, int index) => s[index];

        // String.length() (Java-style method call) -- paulirwin emits `.Length()`
        // (capital L). The C# string property `s.Length` exists but property cannot
        // be called with parens. This extension lets `s.Length()` compile by
        // returning the property value.
        // CAUTION: this WILL shadow `.Length` property on other instance methods named
        // `Length()`. We accept this risk since the patches/build are caught at compile time.
        // NOTE: extension method names that overlap an existing property OFTEN don't
        // resolve correctly because C# prefers instance members. Test before committing.
        // Below is currently commented out -- the property `string.Length` always wins.
        // Use Fix-Java-Length-Property.ps1 to handle these instead.
        // public static int Length(this string s) => s.Length;

        // String.equalsIgnoreCase(string) -- already handled by bulk fix mostly.
        public static bool EqualsIgnoreCase(this string s, string other)
            => string.Equals(s, other, StringComparison.OrdinalIgnoreCase);

        // String.contains(string) -- Java's instance method differs in name from C# String.Contains (works!).
        // Skip -- C# already has this.

        // String.toLowerCase() / toUpperCase() -- Java naming.
        public static string ToLowerCase(this string s) => s.ToLowerInvariant();
        public static string ToUpperCase(this string s) => s.ToUpperInvariant();

        // Exception.getMessage() -- already bulk-rewritten, but extension as safety net.
        public static string GetMessage(this Exception ex) => ex.Message;

        // Collection.size() -- already bulk-rewritten.
        // String.length() -- already bulk-rewritten.

        // Stack<T>.IPush / Push -- Java Stack uses push(); C# Stack uses Push(); already
        // bulk-rewritten the .IPush typo. No extension needed.

        // List.add(item) -- bridges to C# ICollection<T>.Add (which returns void).
        // Java's List.add returns bool; we discard. Skip -- ICollection<T> has Add already.

        // Collection.contains(item) -- Java naming.
        // C# already has Contains via ICollection<T>, so no extension needed.

        // Map.put(k, v) -- bridges to indexer assignment. Java returns previous value;
        // we discard return for simplicity.
        public static TValue Put<TKey, TValue>(this IDictionary<TKey, TValue> d, TKey key, TValue value)
        {
            d.TryGetValue(key, out var prev);
            d[key] = value;
            return prev;
        }

        // Map.get(k) -- Java returns null on miss; C# indexer throws. Use TryGetValue.
        public static TValue Get<TKey, TValue>(this IDictionary<TKey, TValue> d, TKey key)
            => d.TryGetValue(key, out var v) ? v : default;

        // Map.getOrDefault(k, default) -- Java 8+.
        public static TValue GetOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> d, TKey key, TValue defaultValue)
            => d.TryGetValue(key, out var v) ? v : defaultValue;

        // Map.entrySet() -- returns a view of the entries. C# iterates dict directly.
        public static ICollection<KeyValuePair<TKey, TValue>> EntrySet<TKey, TValue>(this IDictionary<TKey, TValue> d)
            => d;

        // Java's Collection.stream() -- returns an IEnumerable<T>-like view.
        // Java callers often chain .filter().map().collect(toList()). For now
        // we expose just the IEnumerable so simple .Stream().Where() etc. work.
        public static global::System.Collections.Generic.IEnumerable<T> Stream<T>(this IEnumerable<T> coll) => coll ?? global::System.Linq.Enumerable.Empty<T>();

        // Java's Stream.filter(predicate). Mirrors LINQ Where.
        public static global::System.Collections.Generic.IEnumerable<T> Filter<T>(this IEnumerable<T> coll, global::System.Func<T, bool> predicate)
            => coll == null ? global::System.Linq.Enumerable.Empty<T>() : global::System.Linq.Enumerable.Where(coll, predicate);

        // Java's String.replaceFirst(regex, replacement) -- regex-based replace,
        // first occurrence only. The C# equivalent is Regex.Replace with count=1.
        public static string ReplaceFirst(this string s, string regex, string replacement) =>
            s == null ? null : new global::System.Text.RegularExpressions.Regex(regex).Replace(s, replacement, 1);

        // Java's String.matches(regex) -- whole-string regex match.
        // (There's another Matches extension in Stubs/JavaInternals.cs but it lives in
        // class ItemTypeExtensions which may not resolve at all callsites due to
        // mixed extension-method discovery. Re-declare here to ensure coverage.)
        public static bool Matches(this string s, string regex) =>
            s != null && global::System.Text.RegularExpressions.Regex.IsMatch(s, "^(?:" + regex + ")$");

        // Java's WeakReference<T>.get() -- C# uses TryGetTarget. KeyManager.cs uses
        // System.WeakReference<X> directly (not our OutSmart.DAXon.Internal.Collections.WeakReference shim).
        public static T Get<T>(this global::System.WeakReference<T> wr) where T : class =>
            wr != null && wr.TryGetTarget(out var t) ? t : null;

        // Java's Class.getClassLoader() -- C# System.Type doesn't have it. Stub returns null.
        public static global::OutSmart.DAXon.Internal.ClassLoader GetClassLoader(this global::System.Type t) => null;

        // Map.keySet() / Map.values() -- already on IDictionary as Keys / Values.
        public static ICollection<TKey> KeySet<TKey, TValue>(this IDictionary<TKey, TValue> d) => d.Keys;
        public static ICollection<TValue> Values<TKey, TValue>(this IDictionary<TKey, TValue> d) => d.Values;

        // Map.containsKey(k) -- bridges to ContainsKey.
        public static bool ContainsKey<TKey, TValue>(this IDictionary<TKey, TValue> d, TKey key) => d.ContainsKey(key);

        // Map.computeIfAbsent(k, fn) -- Java 8+. If key absent, compute via fn(k),
        // insert, return value. If present, return existing.
        // Use global:: prefix because we're inside OutSmart.DAXon.Internal namespace and System
        // gets shadowed.
        public static TValue ComputeIfAbsent<TKey, TValue>(
            this IDictionary<TKey, TValue> d, TKey key, global::System.Func<TKey, TValue> fn)
        {
            if (d.TryGetValue(key, out var existing))
                return existing;
            var computed = fn(key);
            d[key] = computed;
            return computed;
        }

        // Map.computeIfPresent(k, fn) -- if present, compute via fn(k, oldVal).
        // If fn returns null, remove the entry. Otherwise replace.
        public static TValue ComputeIfPresent<TKey, TValue>(
            this IDictionary<TKey, TValue> d, TKey key, global::System.Func<TKey, TValue, TValue> fn)
            where TValue : class
        {
            if (!d.TryGetValue(key, out var existing) || existing == null)
                return null;
            var computed = fn(key, existing);
            if (computed == null) { d.Remove(key); return null; }
            d[key] = computed;
            return computed;
        }

        // Map.merge(k, v, fn) -- Java 8+. If absent or null, insert v. Else fn(old, v).
        public static TValue Merge<TKey, TValue>(
            this IDictionary<TKey, TValue> d, TKey key, TValue value, global::System.Func<TValue, TValue, TValue> fn)
            where TValue : class
        {
            if (!d.TryGetValue(key, out var existing) || existing == null)
            {
                d[key] = value;
                return value;
            }
            var merged = fn(existing, value);
            if (merged == null) { d.Remove(key); return null; }
            d[key] = merged;
            return merged;
        }

        // Map.putIfAbsent(k, v) -- inserts only if absent. Returns old value or null.
        public static TValue PutIfAbsent<TKey, TValue>(
            this IDictionary<TKey, TValue> d, TKey key, TValue value)
        {
            if (d.TryGetValue(key, out var existing))
                return existing;
            d[key] = value;
            return default;
        }
        // SubList exists at line ~155; do not redeclare.

        // Throwable.getCause() -- bridges to InnerException.
        // Both extension method (for typed receivers) and a top-level GetCause(this)
        // helper for callers inside classes deriving from Exception (they get
        // resolved via extension method lookup).
        public static Exception GetCause(this Exception ex) => ex?.InnerException;

        // List/Set.size() -- Java method, already partially handled by bulk rewriter.
        public static int Size<T>(this ICollection<T> c) => c.Count;
        public static int Size<TKey, TValue>(this IDictionary<TKey, TValue> d) => d.Count;

        // String.startsWith / endsWith / contains -- already on C# string. Skip.

        // String.indexOf(int codePoint) -- C# string.IndexOf(char) works for BMP.
        public static int IndexOf(this string s, int codePoint) => s.IndexOf((char)codePoint);

        // Java StringBuffer/StringBuilder.indexOf(String) -- C# StringBuilder has no IndexOf.
        // (FormatNumber.cs probes the decimal point position in a freshly built buffer.)
        public static int IndexOf(this global::System.Text.StringBuilder sb, string s)
            => sb.ToString().IndexOf(s, StringComparison.Ordinal);

        // Java's `iterable.iterator()` method -> C# IEnumerator. paulirwin emitted
        // as `.IIterator()` (capitalized, no parens). Provide extension method.
        public static global::System.Collections.Generic.IEnumerator<T> IIterator<T>(this global::System.Collections.Generic.IEnumerable<T> source)
            => source == null ? null : source.GetEnumerator();

        // IntSet.Count is added in generated/OutSmart.DAXon/excluded stubs.cs
        // (lives there because OutSmart.DAXon.Internal is the lower layer and can't reference Saxon types).

        // CharSequence-like adapter: paulirwin sometimes emits CharSequence params; we
        // expose string-side helpers on CharSequence-typed receivers. Since `string`
        // doesn't implement OutSmart.DAXon.Internal.CharSequence, the extension goes on string only.
        // String -> CharSequence conversion can't be implicit in C# (interfaces), so
        // separate methods on string already cover it.

        // PrimitiveUType.ToUType() stub removed -- extension method on `object`
        // doesn't reliably bind for enum receivers; needs targeted type.

        // List.toArray(T[]) -- Java's IList.toArray(T[]) signature. Copies into the
        // given array or returns a new one of the right size. Naive implementation.
        public static T[] ToArray<T>(this global::System.Collections.Generic.IList<T> list, T[] arr)
        {
            if (arr == null || arr.Length < list.Count)
                arr = new T[list.Count];
            for (int i = 0; i < list.Count; i++)
                arr[i] = list[i];
            return arr;
        }
        public static T[] ToArray<T>(this global::System.Collections.Generic.ICollection<T> coll, T[] arr)
        {
            if (arr == null || arr.Length < coll.Count)
                arr = new T[coll.Count];
            int i = 0;
            foreach (var x in coll)
                arr[i++] = x;
            return arr;
        }

        // Provide Java-style `.Message` property access on Saxon's RoleDiagnostic
        // and ValidationFailure types via reflection. Saves us from adding it to
        // each Saxon file individually (which would be invasive).
        public static string Message(this object obj)
        {
            if (obj == null)
                return null;
            var getMsg = obj.GetType().GetMethod("GetMessage", global::System.Type.EmptyTypes);
            if (getMsg != null)
                return getMsg.Invoke(obj, null) as string;
            // Fallback: try Message property (for System.Exception subclasses)
            var prop = obj.GetType().GetProperty("Message");
            if (prop != null)
                return prop.GetValue(obj) as string;
            return obj.ToString();
        }

        // System.Type extensions to mimic Java's Class methods. paulirwin often emits
        // `typeof(Foo).getName()` -> `typeof(Foo).GetName()` which we provide.
        public static string GetName(this global::System.Type t) => t?.FullName ?? "";
        public static string GetSimpleName(this global::System.Type t) => t?.Name ?? "";
        public static bool IsInstance(this global::System.Type t, object obj) => t?.IsInstanceOfType(obj) ?? false;
        // Phase 5: 0-arg IsInstance() removed -- callers (typeof(X).IsInstance() passed to Children())
        // need INodePredicate (Saxon-specific). 16 CS7036 errors remain; less harmful than 44 CS1503.
        public static object NewInstance(this global::System.Type t) => t == null ? null : global::System.Activator.CreateInstance(t);

        // List<T> extensions -- Java's List has methods C# lacks at the type level.
        public static void Add<T>(this global::System.Collections.Generic.IList<T> list, int index, T item) { list.Insert(index, item); }
        public static T Remove<T>(this global::System.Collections.Generic.IList<T> list, int index)
        {
            var x = list[index];
            list.RemoveAt(index);
            return x;
        }
        public static global::System.Collections.Generic.IList<T> SubList<T>(this global::System.Collections.Generic.IList<T> list, int from, int to)
        {
            var result = new global::System.Collections.Generic.List<T>();
            for (int i = from; i < to; i++)
                result.Add(list[i]);
            return result;
        }
        public static void AddAll<T>(this global::System.Collections.Generic.ICollection<T> dest, global::System.Collections.Generic.IEnumerable<T> src)
        {
            if (src != null)
                foreach (var item in src)
                    dest.Add(item);
        }
        public static int IndexOf<T>(this global::System.Collections.Generic.IList<T> list, T item, int from)
        {
            for (int i = from; i < list.Count; i++)
                if (global::System.Collections.Generic.EqualityComparer<T>.Default.Equals(list[i], item))
                    return i;
            return -1;
        }
        // Java Stream.collect(Collectors.toList()) on a plain sequence: the collector arg is a
        // stub (Collectors.ToList() returns object), so honor the dominant toList() case by
        // materializing the sequence. Instance Collect (e.g. XdmStream) takes precedence, so
        // this only binds where the receiver is a bare IEnumerable<T>.
        public static global::System.Collections.Generic.List<T> Collect<T>(this global::System.Collections.Generic.IEnumerable<T> src, object collector)
            => src == null ? new global::System.Collections.Generic.List<T>() : global::System.Linq.Enumerable.ToList(src);

        // Java StringBuilder.setLength(int) on C# System.Text.StringBuilder.
        // paulirwin emits the Java call name on whichever StringBuilder type was
        // inferred — System.Text.StringBuilder needs an extension to make it work.
        public static void SetLength(this global::System.Text.StringBuilder sb, int n) { if (sb != null) sb.Length = n; }
        public static char CharAt(this global::System.Text.StringBuilder sb, int index) => sb[index];
        public static void SetCharAt(this global::System.Text.StringBuilder sb, int index, char c) { sb[index] = c; }
        public static global::System.Text.StringBuilder AppendCodePoint(this global::System.Text.StringBuilder sb, int codePoint)
        {
            if (codePoint < 0x10000) { sb.Append((char)codePoint); }
            else
            {
                int adjusted = codePoint - 0x10000;
                sb.Append((char)(0xD800 + (adjusted >> 10)));
                sb.Append((char)(0xDC00 + (adjusted & 0x3FF)));
            }
            return sb;
        }

        // StringBuilder indexer-like access (java sb[i] -> via extension): use CharAtIdx since
        // System.Text.StringBuilder has int indexer already, no need for extension.
        public static global::System.Text.StringBuilder DeleteCharAt(this global::System.Text.StringBuilder sb, int index) { sb.Remove(index, 1); return sb; }
        public static global::System.Text.StringBuilder Delete(this global::System.Text.StringBuilder sb, int start, int end) { sb.Remove(start, end - start); return sb; }
        // I5 B2/B3 (StringBuilder retired -> System.Text.StringBuilder): the Java-only gap methods the
        // compat StringBuilder wrapper carried, ported verbatim (status-quo behavior) as extensions.
        public static global::System.Text.StringBuilder Reverse(this global::System.Text.StringBuilder sb)
        {
            // NOTE: matches the compat wrapper's naive char reversal (does NOT re-pair surrogates -
            // a pre-existing divergence from java.lang.StringBuilder.reverse(); left as status quo).
            var arr = sb.ToString().ToCharArray(); global::System.Array.Reverse(arr); sb.Clear(); sb.Append(arr); return sb;
        }
        public static int CodePointAt(this global::System.Text.StringBuilder sb, int index) => global::System.Char.ConvertToUtf32(sb.ToString(), index);
        public static int CodePointCount(this global::System.Text.StringBuilder sb, int beginIndex, int endIndex)
        {
            int n = 0;
            for (int i = beginIndex; i < endIndex;)
            {
                char c = sb[i];
                if (global::System.Char.IsHighSurrogate(c) && i + 1 < endIndex && global::System.Char.IsLowSurrogate(sb[i + 1])) { i += 2; } else { i++; }
                n++;
            }
            return n;
        }
        public static int IndexOf(this global::System.Text.StringBuilder sb, string s, int fromIndex) => sb.ToString().IndexOf(s, fromIndex, global::System.StringComparison.Ordinal);
        public static global::System.Text.StringBuilder Insert(this global::System.Text.StringBuilder sb, int offset, char[] chars, int charsOffset, int len) { sb.Insert(offset, new string(chars, charsOffset, len)); return sb; }
        // Java's Number.intValue() / longValue() / doubleValue() etc. on boxed numeric types.
        // These get called on `object` (boxed) in paulirwin output; extension on object
        // would shadow too widely, so target System.IConvertible (covers all numeric primitives).
        public static int IntValue(this global::System.IConvertible v) => v == null ? 0 : v.ToInt32(null);
        public static long LongValue(this global::System.IConvertible v) => v == null ? 0L : v.ToInt64(null);
        public static double DoubleValue(this global::System.IConvertible v) => v == null ? 0d : v.ToDouble(null);
        public static float FloatValue(this global::System.IConvertible v) => v == null ? 0f : v.ToSingle(null);
        public static short ShortValue(this global::System.IConvertible v) => v == null ? (short)0 : v.ToInt16(null);
        public static byte ByteValue(this global::System.IConvertible v) => v == null ? (byte)0 : v.ToByte(null);

        // Java String.getBytes() -- default charset varies in Java; pick UTF-8 (most common
        // for Saxon serialization paths).
        public static byte[] GetBytes(this string s) => s == null ? new byte[0] : global::System.Text.Encoding.UTF8.GetBytes(s);
        public static byte[] GetBytes(this string s, string charset)
            => s == null ? new byte[0] : global::System.Text.Encoding.GetEncoding(charset).GetBytes(s);
        public static byte[] GetBytes(this string s, global::System.Text.Encoding enc)
            => s == null ? new byte[0] : (enc ?? global::System.Text.Encoding.UTF8).GetBytes(s);
        // Phase 5: GetBytes(Charset) overload — Java's String.getBytes(Charset).
        public static byte[] GetBytes(this string s, global::OutSmart.DAXon.Internal.Charsets.Charset cs)
            => s == null ? new byte[0] : (cs?.Inner ?? global::System.Text.Encoding.UTF8).GetBytes(s);

        // Java Thread.getId() -- C# Thread has ManagedThreadId.
        public static long GetId(this global::System.Threading.Thread t)
            => t == null ? 0 : (long)t.ManagedThreadId;
        public static string GetName(this global::System.Threading.Thread t) => t?.Name ?? "";

        // Java's String.split(String regex) — C# net472 lacks the (string) overload (added in .NET Standard 2.1).
        // Provide as extension. Java semantics: argument is a REGEX pattern, not literal.
        public static string[] Split(this string s, string regex)
            => s == null ? new string[0] : global::System.Text.RegularExpressions.Regex.Split(s, regex);
        public static string[] Split(this string s, string regex, int limit)
        {
            if (s == null)
                return new string[0];
            var parts = global::System.Text.RegularExpressions.Regex.Split(s, regex);
            if (limit > 0 && parts.Length > limit)
            {
                var head = new string[limit];
                global::System.Array.Copy(parts, head, limit - 1);
                head[limit - 1] = string.Join("", parts, limit - 1, parts.Length - limit + 1);
                return head;
            }
            return parts;
        }

        // java.util.Queue.poll()/offer() on the BCL Queue<T> -- after the compat ConcurrentLinkedQueue<T> wrapper was
        // retired (de-Java Stage 2). poll() returns null/default on an empty queue (never throws, unlike Dequeue());
        // offer() is enqueue. Consistent with the Get/Put/Filter extension idiom used across this codebase.
        public static T Poll<T>(this global::System.Collections.Generic.Queue<T> q) => q.Count > 0 ? q.Dequeue() : default;
        public static void Offer<T>(this global::System.Collections.Generic.Queue<T> q, T item) => q.Enqueue(item);
    }
}
