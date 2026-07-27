////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
namespace OutSmart.DAXon.Internal.Functional
{
    // NOTE: this file lives UNDER the OutSmart.DAXon.Internal root, whose ex-Java.Lang `System`
    // shadowing class is in enclosing scope - so `using System;` is CS0138 and bare `Func`/`System.Func`
    // mis-bind. Per the I4 convention, qualify with global::System.* here. (Tree files are OutSmart.DAXon.Core.*,
    // where bare Func/Action + using System is fine; only the compat-rooted files need global::.)

    /// <summary>Extension methods mapping Java's functional-interface methods (apply/test/accept/get)
    /// onto BCL delegate invocation. The Java functional-interface delegate TYPES were retired in I5
    /// wave B2a (-> System.Func / System.Action); these extensions are the directive-#2 "extension
    /// methods for gaps" that keep the ~650 .Apply()/.Get()/.Accept() call sites unchanged. Predicate
    /// (compat) is still its own delegate until I5 wave B2b.</summary>
    public static class FunctionalExtensions
    {
        // ex-Function / ex-BiFunction -> System.Func
        public static TR Apply<T, TR>(this global::System.Func<T, TR> self, T t) => self == null ? default : self(t);
        public static TR Apply<T1, T2, TR>(this global::System.Func<T1, T2, TR> self, T1 t1, T2 t2) => self == null ? default : self(t1, t2);
        // ex-BiPredicate -> System.Func<T1,T2,bool>. (compat Predicate retired in B2b: its
        // Test<T>(this Func<T,bool>) form already lives in JavaApiExtensionsContainsAll - keeping a
        // copy here would duplicate that overload after the rename -> CS0111/CS0121.)
        public static bool Test<T1, T2>(this global::System.Func<T1, T2, bool> self, T1 t1, T2 t2) => self != null && self(t1, t2);
        // Phase 7.15: Test() on BCL System.Predicate<T> (Saxon sites use this where Java has Predicate.test).
        public static bool Test<T>(this global::System.Predicate<T> self, T t) => self != null && self(t);
        // ex-IntPredicate -> System.Func<int, bool>
        public static bool Test(this global::System.Func<int, bool> self, int t) => self != null && self(t);
        // ex-Consumer / ex-BiConsumer -> System.Action
        public static void Accept<T>(this global::System.Action<T> self, T t) { if (self != null) self(t); }
        public static void Accept<T1, T2>(this global::System.Action<T1, T2> self, T1 t1, T2 t2) { if (self != null) self(t1, t2); }
        // ex-Supplier -> System.Func<T>
        public static T Get<T>(this global::System.Func<T> self) => self == null ? default : self();
    }
}
