////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace OutSmart.DAXon.Trees.Iterators
{
    /// <summary>
    /// Iterators that can report how many items remain without materializing them (fn:count over a
    /// TinyTree axis walk counts array entries instead of building a node object per entry). The call
    /// consumes the iterator: after a successful TryFastCount, Next() returns end-of-sequence.
    /// </summary>
    internal interface IFastCountable
    {
        bool TryFastCount(out int count);
    }
}
