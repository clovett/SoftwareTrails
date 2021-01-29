using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Threading;
using System.Net;
using System.IO;

namespace SoftwareTrails
{
    public class SetupRequestEventArgs : EventArgs
    {
        public XDocument Changes { get; set; }
        public bool NewVersionAvailable { get; set; }
    }

    /// <summary>
    /// This class downloads the latest changes.xml from http://www.lovettsoftware.com/downloads/SoftwareTrails/Application Files/SoftwareTrails_1_0_0_145/Setup/changes.xml.deploy
    /// </summary>
    public class ChangeListRequest
    {
        Settings settings;
        XDocument changeList;
        string appName;

        /// <summary>
        /// Create new ChangeListRequest for the given app.
        /// </summary>
        /// <param name="appName">The click once app name, for example, 'SoftwareTrails.application'</param>
        /// <param name="settings">The settings object to store last request times</param>
        public ChangeListRequest(string appName, Settings settings)
        {
            this.settings = settings;
            this.appName = appName;
        }

        public XDocument Changes { get { return this.changeList; } }

        public void BeginGetChangeList(Uri setupHost)
        {
            ThreadPool.QueueUserWorkItem(GetChangeList, setupHost);
        }

        public event EventHandler<SetupRequestEventArgs> Completed;

        private void OnCompleted(XDocument doc, bool newVersion = false)
        {
            changeList = doc;

            if (Completed != null)
            {
                Completed(this, new SetupRequestEventArgs() { Changes = doc, NewVersionAvailable = newVersion });
            }
        }

        static XNamespace asmNamespace = XNamespace.Get("urn:schemas-microsoft-com:asm.v1");

        private void GetChangeList(object state)
        {
            try
            {
                Uri host = (Uri)state;
                XDocument manifest = GetDocument(new Uri(host, "SoftwareTrails.application"));
                if (manifest == null || manifest.Root == null)
                {
                    OnCompleted(null);
                    return;
                }

                XElement root = manifest.Root;
                XElement assemblyIdentity = root.Element(asmNamespace + "assemblyIdentity");
                if (assemblyIdentity == null || assemblyIdentity.Attribute("version") == null)
                {
                    OnCompleted(null);
                    return;
                }

                string version = (string)assemblyIdentity.Attribute("version");

                string prefix = Path.GetFileNameWithoutExtension(this.appName) + "_";

                string folder = prefix + version.Replace(".", "_");

                XDocument changelist = GetDocument(new Uri(host, "Application Files/" + folder + "/Setup/changes.xml.deploy"));

                string exe = FileUtilities.MainExecutable;
                string currentVersion = FileUtilities.GetFileVersion(appName, exe);

                Version latest = Version.Parse(version);
                Version current = Version.Parse(currentVersion);
                
                OnCompleted(changelist, current < latest);
            }
            catch
            {
                OnCompleted(null);
                return;
            }
        }

        internal XDocument GetBuiltInList()
        {
            try
            {
                // get the built in changelist.
                string filename = new Uri(new Uri(FileUtilities.MainExecutable), "Setup/changes.xml").LocalPath;
                XDocument doc = XDocument.Load(filename);
                return doc;
            }
            catch
            {
                //MessageBoxEx.Show("Internal error parsing Walkabout.Setup.changes.xml");
            }

            return null;
        }


        private XDocument GetDocument(Uri url)
        {
            try
            {
                WebRequest request = WebRequest.Create(url);
                WebResponse response = request.GetResponse();
                using (Stream s = response.GetResponseStream())
                {
                    XDocument doc = XDocument.Load(s);
                    return doc;
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
