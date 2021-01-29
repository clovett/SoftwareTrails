using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows;
using System.Diagnostics;
using System.Security.Principal;
using UIController;
using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace SoftwareTrails
{
    public static class FileUtilities
    {
        /// <devdoc>
        /// Please use this "approved" method to compare file names.
        /// </devdoc>
        public static bool IsSamePath(string file1, string file2)
        {
            if (file1 == null || file1.Length == 0)
            {
                return (file2 == null || file2.Length == 0);
            }

            Uri uri1 = null;
            Uri uri2 = null;

            try
            {
                if (!Uri.TryCreate(file1, UriKind.Absolute, out uri1) || !Uri.TryCreate(file2, UriKind.Absolute, out uri2))
                {
                    return false;
                }

                if (uri1 != null && uri1.IsFile && uri2 != null && uri2.IsFile)
                {
                    return 0 == String.Compare(uri1.LocalPath, uri2.LocalPath, StringComparison.OrdinalIgnoreCase);
                }

                return file1 == file2;
            }
            catch (UriFormatException e)
            {
                System.Diagnostics.Trace.WriteLine("Exception " + e.Message);
            }

            return false;
        }

        public static bool ExtractEmbeddedResourceAsFile(string name, string path)
        {
            using (Stream s = typeof(FileUtilities).Assembly.GetManifestResourceStream(name))
            {
                if (s == null)
                {
                    MessageBox.Show("Embedded resource " + name + " is missing", "Build Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    byte[] buffer = new byte[64000];
                    int len = 0;
                    while ((len = s.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        fs.Write(buffer, 0, len);
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Resilient file delete, returns false if the delete fails.
        /// </summary>
        /// <param name="fileToDelete"></param>
        public static bool TryDeleteFile(string fileToDelete)
        {
            try
            {
                MakeReadWrite(fileToDelete);
                File.Delete(fileToDelete);
                return true;
            }
            catch (Exception)
            {
                return false;
            }

        }

        public static void MakeReadWrite(string filename)
        {
            if (File.Exists(filename))
            {
                var attrs = File.GetAttributes(filename);
                if ((attrs & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    File.SetAttributes(filename, attrs & ~FileAttributes.ReadOnly);
                }
            }
        }

        const int BufferSize = 64000;

        /// <summary>
        /// Return true if the two files contain identical bits.
        /// </summary>
        public static bool AreFilesIdentical(string file1, string file2)
        {
            try {
                byte[] buffer1 = new byte[BufferSize]; 
                byte[] buffer2 = new byte[BufferSize];

                using (var fs1 = new FileStream(file1, FileMode.Open, FileAccess.Read))
                {
                    using (var fs2 = new FileStream(file2, FileMode.Open, FileAccess.Read))
                    {
                        int len1 = fs1.Read(buffer1, 0, BufferSize);
                        int len2 = fs2.Read(buffer2, 0, BufferSize);
                        if (len1 != len2)
                        {
                            return false;
                        }
                        for (int i = 0; i < BufferSize; i++)
                        {
                            byte b1 = buffer1[i];
                            byte b2 = buffer2[i];
                            if (b1 != b2)
                            {
                                return false;
                            }
                        }
                    }
                }            
            } catch {
                return false;
            }
            return true;
        }

        public static bool IsAdmin()
        {
            WindowsIdentity id = WindowsIdentity.GetCurrent();

            WindowsPrincipal principal = new WindowsPrincipal(id);
            if (principal.IsInRole(WindowsBuiltInRole.Administrator))
            {
                return true;
            }
         
            return false;
        }

        public static bool IsCurrentProcessWow64
        {
            get
            {
                using (var currentProcess = Process.GetCurrentProcess())
                {
                    return NativeMethods.IsWow64Process(currentProcess) == NativeMethods.IsWow64.Yes;
                }
            }
        }


        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1060:MovePInvokesToNativeMethodsClass"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA2101:SpecifyMarshalingForPInvokeStringArguments", MessageId = "1"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA2101:SpecifyMarshalingForPInvokeStringArguments", MessageId = "3"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA2101:SpecifyMarshalingForPInvokeStringArguments", MessageId = "4"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA2101:SpecifyMarshalingForPInvokeStringArguments", MessageId = "2"), DllImport("Shell32.dll", EntryPoint = "ShellExecuteA",
            SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true,
            CallingConvention = CallingConvention.StdCall)]
        private static extern int ShellExecute(IntPtr handle, string verb, string file,
            string args, string dir, int show);


        public static void OpenUrl(IntPtr owner, Uri url)
        {
            const int SW_SHOWNORMAL = 1;
            int rc = ShellExecute(owner, "open", url.AbsoluteUri, null, Directory.GetCurrentDirectory(), SW_SHOWNORMAL);
        }

        public static string MainExecutable
        {
            get
            {
                Process p = Process.GetCurrentProcess();
                return p.MainModule.FileName;
            }
        }

        public static string StartupPath
        {
            get
            {
                Process p = Process.GetCurrentProcess();
                string exe = p.MainModule.FileName;
                return Path.GetDirectoryName(exe);
            }
        }

        internal static string GetFileVersion(string appName, string filename)
        {
            string version = null;
            string baseName = Path.GetFileNameWithoutExtension(appName);
            string manifest = Path.Combine(FileUtilities.StartupPath, baseName + ".exe.manifest");
            if (File.Exists(manifest))
            {
                try
                {
                    XNamespace ns = XNamespace.Get("urn:schemas-microsoft-com:asm.v1");
                    XDocument doc = XDocument.Load(manifest);
                    XElement id = doc.Root.Element(ns + "assemblyIdentity");
                    if (id != null)
                    {
                        version = (string)id.Attribute("version");
                    }
                }
                catch
                {
                }
            }
            if (version == null)
            {
                version = typeof(NativeMethods).Assembly.GetName().Version.ToString();
            }
            return version;
        }

    }

}
