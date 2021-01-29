using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SoftwareTrailsAdmin
{
    public class AdminProgram
    {
        string path;
        bool register;

        static int Main(string[] args)
        {
            AdminProgram p = new AdminProgram();
            if (!p.ParseCommandLine(args))
            {
                return 1;
            }
            p.Run();
            return 0;
        }

        bool ParseCommandLine(string[] args)
        {
            for (int i = 0, n = args.Length; i < n; i++)
            {
                string arg = args[i];
                if (arg[0] == '/' || arg[0] == '-')
                {
                    switch (arg.Substring(1).ToLowerInvariant())
                    {
                        case "register":
                        case "r":
                            register = true;
                            path = args[++i];

                            if (!File.Exists(path))
                            {
                                Console.Error.WriteLine("Given path does not exist: " + path);
                                return false;
                            }
                            break;
                        case "help":
                        case "?":
                            PrintUsage();
                            return false;
                    }
                }
            }
            if (path == null)
            {
                PrintUsage();
                return false;
            }
            return true;
        }

        private void PrintUsage()
        {
            Console.Error.WriteLine("Usage: SoftwareTrailsAdmin [options]");
            Console.Error.WriteLine("Options:");
            Console.Error.WriteLine("\t/register dll");
        }

        public void Run()
        {
            // we should now be nice and elevated...
            if (register) {
                ProcessStartInfo pi = new ProcessStartInfo("regsvr32.exe");
                pi.Arguments = "\"" + path + "\"";
                pi.UseShellExecute = false;

                Process p = Process.Start(pi);
                p.WaitForExit();
            }
        }

    }
}
