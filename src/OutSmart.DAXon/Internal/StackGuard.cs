////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace OutSmart.DAXon.Internal
{
    /// <summary>
    /// Proactive stack-overflow guard. On .NET Framework a real StackOverflowException is
    /// uncatchable and kills the process, so the Java behavior (catch StackOverflowError at
    /// the recursion sites and report SXLM0001) cannot be reproduced reactively. Instead the
    /// recursion entry points call Probe(), which raises the catchable RecursionDepthError
    /// while there is still guaranteed headroom to unwind — the ported catch sites then
    /// convert it to the same errors Java reports (SXLM0001 / XTDE3400).
    /// The remaining stack is measured directly (thread stack bounds are fixed for the
    /// thread's lifetime, so the low bound is cached per thread) because
    /// RuntimeHelpers.EnsureSufficientExecutionStack demands 512 KB on 64-bit Framework —
    /// half of a default 1 MB thread — which would reject recursion depths that in fact
    /// complete comfortably.
    /// </summary>
    internal static class StackGuard
    {
        // Headroom kept in reserve when RecursionDepthError is raised. Must cover the deepest
        // single engine descent between two probes PLUS the error path itself: unwinding a
        // 1000+-frame stack runs every finally funclet and each converting rethrow re-enters
        // EH dispatch from near the deep point — measured >127KB on the call-template cycle
        // (probe threw at 127KB remaining, process still died), comfortable at 256KB.
        private static readonly ulong Margin = IntPtr.Size == 8 ? 256UL * 1024 : 128UL * 1024;

        [ThreadStatic]
        private static ulong stackLow;   // low bound of this thread's reserved stack region

        private static volatile bool noApi;   // GetCurrentThreadStackLimits needs Win8/Server2012+

        // QTDBG_SG=1 traces remaining-stack headroom to stderr (cached: an env lookup per probe
        // would allocate on the hot path).
        private static readonly bool Dbg = Environment.GetEnvironmentVariable("QTDBG_SG") != null;
        private static int dbgCount;

        [DllImport("kernel32.dll")]
        private static extern void GetCurrentThreadStackLimits(out UIntPtr lowLimit, out UIntPtr highLimit);

        /// <summary>Throws RecursionDepthError if the remaining stack is too small to safely
        /// descend another recursion level. Adapts to the executing thread's stack size.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]   // sits on every guarded recursion entry
        public static void Probe()
        {
            Probe(0);
        }

        /// <summary>
        /// As <see cref="Probe()"/>, plus caller-supplied headroom. For recursions whose ERROR path
        /// costs far more stack than the descent: on .NET Framework every level that catches and
        /// re-codes an exception re-enters exception dispatch from inside its catch, so the stack
        /// grows while unwinding instead of shrinking. A caller that pays that per level must
        /// reserve for it here - a fixed margin cannot, since the shortfall scales with depth.
        /// </summary>
        public static void Probe(ulong extraMargin)
        {
            if (noApi)
            {
                FallbackProbe();
                return;
            }

            ulong low = stackLow;
            if (low == 0)
            {
                try
                {
                    GetCurrentThreadStackLimits(out UIntPtr lo, out _);
                    stackLow = low = (ulong)lo;
                }
                catch (EntryPointNotFoundException)
                {
                    noApi = true;
                    FallbackProbe();
                    return;
                }
            }

            unsafe
            {
                byte probe;
                ulong remaining = (ulong)&probe - low;
                if (Dbg && (++dbgCount & 255) == 0)
                {
                    Console.Error.WriteLine("[SG] remaining=" + remaining / 1024 + "KB");
                }

                if (remaining < Margin + extraMargin)
                {
                    if (Dbg)
                    {
                        Console.Error.WriteLine("[SG] THREW at remaining=" + remaining / 1024 + "KB");
                    }

                    throw new RecursionDepthError();
                }
            }
        }

        // Pre-Windows-8 fallback: the BCL probe (conservative — 512 KB on 64-bit Framework).
        private static void FallbackProbe()
        {
            try
            {
                RuntimeHelpers.EnsureSufficientExecutionStack();
            }
            catch (InsufficientExecutionStackException)
            {
                throw new RecursionDepthError();
            }
        }
    }
}
