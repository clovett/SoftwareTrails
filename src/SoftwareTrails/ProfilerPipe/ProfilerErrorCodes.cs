using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SoftwareTrails
{
    /// <summary>
    /// Constants for error codes aside from those that come directly from the CLR profiling API.
    /// @see ClrProfilerConstants.
    /// </summary>
    public static class ProfilerErrorCodes
    {
        /// Subset of the Error Codes listed at http://msdn.microsoft.com/en-us/library/ms681381.aspx
        public static readonly uint ErrorSuccess = 0x0;
        public static readonly uint ErrorFileNotFound = 0x2;

        // Custom profiler error codes
        public static readonly uint InvalidConfiguration = 0x1000;
        public static readonly uint InvalidProcess = 0x1001;
        public static readonly uint NoRuntimeToAttachTo = 0x1002;
        public static readonly uint ProcessHasExited = 0x1003;
    }
}
