////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2012 Michael Froh.
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Internal.Collections;using OutSmart.DAXon.Functions;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Collections.Trie
{
    // Original author: Michael Froh (published on Github). Released under MPL 2.0
    // by Saxonica Limited with permission from the author
    internal interface IImmutableMap<K, V> : IEnumerable<TrieKVP<K, V>>
    {
        IImmutableMap<K, V> Put(K key, V value);
        IImmutableMap<K, V> Remove(K key);
        V Get(K key);
        // PHASE7_IIMMUTABLEMAP_INDEXER
        V this[K key] { get; }
        IEnumerator<TrieKVP<K, V>> IIterator();
    }
}