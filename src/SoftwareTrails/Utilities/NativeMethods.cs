using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.ComponentModel;

namespace SoftwareTrails
{
    public class NativeMethods
    {
        public enum IsWow64
        {
            Yes,
            No,
            Failed,
        }

        /// <summary>
        /// Determine if the specified process is a Wow64 process or not.
        /// </summary>
        public static IsWow64 IsWow64Process(Process process)
        {
            if (process == null)
                return IsWow64.No;

            bool isWow64Process = false;
            try
            {
                if (!IsWow64Process(process.Handle, out isWow64Process))
                {
                    return IsWow64.Failed;
                }
            }
            catch (Win32Exception)
            {
                return IsWow64.Failed;
            }
            return isWow64Process ? IsWow64.Yes : IsWow64.No;
        }

        [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsWow64Process(
             [In] IntPtr hProcess,
             [Out] out bool wow64Process
             );
    }
}
