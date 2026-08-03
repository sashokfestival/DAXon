////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using System;

namespace OutSmart.DAXon.Expressions
{
    /// <summary>
    /// Bridges a delegate to the <see cref="IItemMappingFunction"/> functional interface
    /// (Item mapItem(Item)). Java passes lambdas/method-refs where an ItemMappingFunction is
    /// expected (e.g. new ItemMappingIterator(base, item -&gt; ...)); C# rejects the lambda
    /// (CS1660). Used by ForExpression's per-item evaluator.
    /// </summary>
    internal sealed class DelegateItemMappingFunction : IItemMappingFunction
    {
        private readonly Func<IItem, IItem> _f;

        public DelegateItemMappingFunction(Func<IItem, IItem> f)
        {
            _f = f;
        }

        public IItem MapItem(IItem item)
        {
            return _f(item);
        }
    }
}
