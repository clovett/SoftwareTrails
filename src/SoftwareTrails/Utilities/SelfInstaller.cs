using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace SoftwareTrails
{
    static class SelfInstaller
    {
        private const string SoftwareTrailsEmbeddedAssemblyPath32 = "SoftwareTrails.Profiler32.";
        private const string SoftwareTrailsEmbeddedAssemblyPath64 = "SoftwareTrails.Profiler64.";

        public static string GetTempSetupPath()
        {
            string temp = Path.Combine(Path.GetTempPath(), "SoftwareTrails");
            Directory.CreateDirectory(temp);
            return temp;
        }

        public static string InstallProfiler(string previousProfiler, bool is64Bit)
        {
            InstallCrt(is64Bit);

            string profiler = Path.Combine(GetTempSetupPath(), "DotNetProfiler.dll");
            string resourcePath = (is64Bit ? SoftwareTrailsEmbeddedAssemblyPath64 : SoftwareTrailsEmbeddedAssemblyPath32);

            ExtractResourceAndCopyIfChanged(resourcePath + "DotNetProfiler.dll", profiler);

            return profiler;
        }

        private static void InstallCrt(bool is64Bit)
        {
            string temp = GetTempSetupPath();
            string resourcePath = (is64Bit ? SoftwareTrailsEmbeddedAssemblyPath64 : SoftwareTrailsEmbeddedAssemblyPath32);

            string[] x64runtime = new string[] {
#if DEBUG
                "msvcp140d.dll", "vcruntime140d.dll", "vcruntime140_1d.dll" 
#else
                "msvcp140.dll", "vcruntime140.dll", "vcruntime140_1.dll"
#endif
            };

            string[] x86runtime = new string[] { 
#if DEBUG
                "msvcp140d.dll", "vcruntime140d.dll" 
#else
                "msvcp140.dll", "vcruntime140.dll"
#endif
            };

            string[] runtime = (is64Bit ? x64runtime : x86runtime);

            foreach (string dll in runtime)
            {
                string path = Path.Combine(temp, dll);
                ExtractResourceAndCopyIfChanged(resourcePath + dll, path);
            }
        }

        static bool ExtractResourceAndCopyIfChanged(string resourceName, string path)
        {
            string temptemp = Path.GetTempFileName();
            FileUtilities.ExtractEmbeddedResourceAsFile(resourceName, temptemp);
            bool changed = false;
            try
            {
                bool identical = FileUtilities.AreFilesIdentical(temptemp, path);
                if (!identical)
                {
                    File.Copy(temptemp, path, true);
                    changed = true;
                }
            }
            catch
            {
                // might be locked.
            }
            File.Delete(temptemp);

            return changed;
        }
    }
}
