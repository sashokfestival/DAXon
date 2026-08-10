////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System.Runtime.CompilerServices;

namespace OutSmart.DAXon.Internal
{
    // Bitmap arithmetic shared by the two HAMT-shaped tries (ImmutableHashTrieMap, MapTrie).
    internal static class Bits
    {
        // Number of set bits — with a mask of (bit - 1), an entry's index in the compact array.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int BitCount(int v)
        {
            uint x = (uint)v;
            x = x - ((x >> 1) & 0x55555555u);
            x = (x & 0x33333333u) + ((x >> 2) & 0x33333333u);
            x = (x + (x >> 4)) & 0x0f0f0f0fu;
            return (int)((x * 0x01010101u) >> 24);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int TrailingZeros(int v)
        {
            int n = 0;
            uint x = (uint)v;
            while ((x & 1u) == 0u)
            {
                x >>= 1;
                n++;
            }

            return n;
        }
    }
}
