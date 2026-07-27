////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace OutSmart.DAXon.Internal.Caching
{
    /// <summary>
    /// Bounded, thread-safe memoization cache with CLOCK (second-chance) eviction.
    /// Hits are lock-free dictionary reads; the only write a hit can make is the one-time
    /// 0-to-1 flip of the entry's reference bit, so steady-state lookups touch no shared
    /// mutable cache line. (Replaced a lock-per-hit LFU design whose lock serialized
    /// multi-threaded hosts — 29% of CPU in an 8-thread format-dateTime run, round AP.)
    /// Intended to be held in a STATIC field so the cache is per-process and survives
    /// re-instantiation of Processor/Configuration. Cache only pure functions whose key
    /// captures every result-affecting input (see REFACTORING-PLAN Appendix B,
    /// "Correctness rules").
    /// </summary>
    public sealed class ClockCache<TKey, TValue>
    {
        private readonly ConcurrentDictionary<TKey, Entry> _map;
        private readonly object _writeLock = new object();
        private readonly int _capacity;

        public int Count => _map.Count;

        public ClockCache(int capacity)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "capacity must be >= 1");
            }

            _capacity = capacity;
            _map = new ConcurrentDictionary<TKey, Entry>(Environment.ProcessorCount, capacity);
        }

        /// <summary>
        /// Returns the cached value for <paramref name="key"/>, computing it via
        /// <paramref name="factory"/> on a miss. The factory runs outside the lock, so a
        /// concurrent miss on the same key may compute twice — harmless for a pure factory
        /// (last writer wins). A throwing factory caches nothing and re-throws.
        /// </summary>
        public TValue GetOrAdd(TKey key, Func<TKey, TValue> factory)
        {
            if (_map.TryGetValue(key, out Entry hit))
            {
                if (hit.Referenced == 0)
                {
                    hit.Referenced = 1;   // benign race; written at most once per clock sweep
                }

                return hit.Value;
            }

            TValue value = factory(key);
            lock (_writeLock)
            {
                if (_map.Count >= _capacity && !_map.ContainsKey(key))
                {
                    EvictOne();
                }

                _map[key] = new Entry(value);
            }

            return value;
        }

        /// <summary>Looks up <paramref name="key"/>; a hit counts as a use (sets the reference bit).</summary>
        public bool TryGet(TKey key, out TValue value)
        {
            if (_map.TryGetValue(key, out Entry hit))
            {
                if (hit.Referenced == 0)
                {
                    hit.Referenced = 1;
                }

                value = hit.Value;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>Empties the cache. Test hook only — not part of the steady-state contract.</summary>
        public void Clear()
        {
            lock (_writeLock)
            {
                _map.Clear();
            }
        }

        // Second chance, caller holds _writeLock: entries referenced since the last sweep get
        // their bit cleared and stay; the first unreferenced entry is evicted. If every entry
        // was referenced, the last one scanned (bit now cleared) is evicted.
        private void EvictOne()
        {
            TKey lastSeen = default;
            bool any = false;
            foreach (KeyValuePair<TKey, Entry> kv in _map)
            {
                if (kv.Value.Referenced == 0)
                {
                    _map.TryRemove(kv.Key, out _);
                    return;
                }

                kv.Value.Referenced = 0;
                lastSeen = kv.Key;
                any = true;
            }

            if (any)
            {
                _map.TryRemove(lastSeen, out _);
            }
        }

        // Value is readonly: initialized before the entry is published through the dictionary,
        // so lock-free readers can never observe a half-written value.
        private sealed class Entry
        {
            internal readonly TValue Value;
            internal int Referenced;

            internal Entry(TValue value)
            {
                Value = value;
            }
        }
    }
}
