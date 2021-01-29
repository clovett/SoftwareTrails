using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SoftwareTrails
{
    /// <summary>
    /// Constants that are returned by the CLR ICorProfiler calls as HRESULTs.
    /// From CorError.h.
    /// </summary>
    public static class ClrProfilerConstants
    {
        /// <summary>
        /// Returned from ICLRProfiling::AttachProfiler if Concurrent GC is on in the application since the
        /// CLR doesn't support memory callbacks in this case.
        /// </summary>
        public static readonly uint CorProfEConcurrentGCNotProfilable = 0x80131376;
        /// <summary>
        /// Returned from ICLRProfiling::AttachProfiler if there is already a profiler DLL injected into a process.
        /// This might mean it is running or that it is in the process of detaching.
        /// </summary>
        public static readonly uint CorProfEProfilerAlreadyActive = 0x8013136A;

        /// <summary>
        /// CORPROF_E_UNSUPPORTED_FOR_ATTACHING_PROFILER
        /// </summary>
        public static readonly uint CorProfEProfilerUnsupportedForAttachingProfiler = 0x8013136f;
    }
}
