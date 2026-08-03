////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Generic;

namespace OutSmart.DAXon.Internal.Collections
{
    /// <summary>
    /// net472 array gap-fillers with no BCL one-liner on this TFM: CopyOf/CopyOfRange (return a
    /// resized copy), in-place Fill, value-array Equals/HashCode (java.util.Arrays.equals/hashCode
    /// semantics). The BCL-backed operations (Sort, BinarySearch, and the
    /// reassign-in-place copy via Array.Resize) call System.Array directly at their sites.
    /// </summary>
    internal static class ArrayTools
    {

        public static T[] CopyOf<T>(T[] src, int newLength)
        {
            var dst = new T[newLength];
            Array.Copy(src, dst, Math.Min(src.Length, newLength));
            return dst;
        }

        public static T[] CopyOfRange<T>(T[] src, int from, int to)
        {
            int len = to - from;
            var dst = new T[len];
            Array.Copy(src, from, dst, 0, Math.Min(len, src.Length - from));
            return dst;
        }

        public static bool Equals<T>(T[] a, T[] b)
        {
            if (a == null && b == null)
                return true;
            if (a == null || b == null)
                return false;
            if (a.Length != b.Length)
                return false;
            for (int i = 0; i < a.Length; i++)
                if (!EqualityComparer<T>.Default.Equals(a[i], b[i]))
                    return false;
            return true;
        }

        public static void Fill<T>(T[] a, T value)
        {
            for (int i = 0; i < a.Length; i++)
                a[i] = value;
        }

        public static void Fill<T>(T[] a, int from, int to, T value)
        {
            for (int i = from; i < to; i++)
                a[i] = value;
        }

        public static int HashCode<T>(T[] a)
        {
            if (a == null)
                return 0;
            unchecked
            {
                int hash = 1;
                foreach (var x in a)
                    hash = 31 * hash + (x == null ? 0 : x.GetHashCode());
                return hash;
            }
        }
        // Paulirwin sometimes translates Java's Arrays.hashCode(arr) as Arrays.GetHashCode(arr).
        public static int GetHashCode<T>(T[] a) => HashCode(a);
    }
}
