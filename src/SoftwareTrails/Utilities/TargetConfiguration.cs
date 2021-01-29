using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Linq;
using System.Diagnostics;
using System.Globalization;

namespace UIController
{
    /// <summary>
    /// Holds a representation of the Target Processes .config file.
    /// This may be a subset of the actual configuration in the file.
    /// </summary>
    public class TargetConfiguration
    {
        private const string CONFIG_EXT = ".config";
        private const string BACKUP_EXT = ".wprofbackup";

        /// <summary>
        /// Constructor
        /// </summary>
        public TargetConfiguration(String targetPath)
        {
            if (String.IsNullOrWhiteSpace(targetPath))
                throw new ArgumentException("Param is null or whitespace", "targetPath");

            ConfigPath = String.Format(CultureInfo.CurrentCulture, "{0}{1}", targetPath, CONFIG_EXT);
            _backupConfigPath = String.Format(CultureInfo.CurrentCulture, "{0}{1}", _configPath, BACKUP_EXT);

            BackupOriginalConfig();
            Load();
        }

        public String ConfigPath
        {
            get
            {
                return _configPath;
            }
            set
            {
                _configPath = value;
            }
        }

        public String ConfigText
        {
            get
            {
                return _config.ToString();
            }
        }

        /// <summary>
        /// True if the .config has Concurrent GCs enabled.
        /// Different GC modes might have Concurrent GC enabled.
        /// I.e. This is a different thing to "gcConcurrent" mode.
        /// </summary>
        public bool ConcurrentGC
        {
            get
            {
                // TODO: Update for Single Proc Workstation mode (which should be false)
                String gcConcurrent = GetConfigValue("runtime", "gcConcurrent", "enabled");
                String gcServer = GetConfigValue("runtime", "gcConcurrent", "enabled");
                return ((String.IsNullOrWhiteSpace(gcConcurrent) && String.IsNullOrWhiteSpace(gcServer)) || // Workstation
                        gcConcurrent.ToLower(CultureInfo.CurrentCulture) == "true" ||
                        gcServer.ToLower(CultureInfo.CurrentCulture) == "true");
            }
            set
            {
                if (!value)
                {
                    SetConfigValue("runtime", "gcConcurrent", "enabled", "false");
                }
            }
        }

        /// <summary>
        /// Get the GC Mode
        /// </summary>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate")]
        public String GetConcurrentGCMode()
        {
            String gcConcurrent = GetConfigValue("runtime", "gcConcurrent", "enabled");
            if (!String.IsNullOrWhiteSpace(gcConcurrent) && gcConcurrent.ToLower(CultureInfo.CurrentCulture) == "true")
                return "Concurrent";

            String gcServer = GetConfigValue("runtime", "gcConcurrent", "enabled");
            if (!String.IsNullOrWhiteSpace(gcServer) && gcServer.ToLower(CultureInfo.CurrentCulture) == "true")
                return "Server";

            return "Workstation";
        }

        /// <summary>
        /// Get a config attribute value
        /// </summary>
        String GetConfigValue(String section, String element, String attribute)
        {
            Debug.Assert(_config != null);

            var sectionNode = _config.Root.Element(section);
            if (sectionNode == null)
                return null;

            var elementNode = sectionNode.Element(element);
            if (elementNode == null)
                return null;

            var elementNodeAttribute = elementNode.Attribute(attribute);
            if (elementNodeAttribute == null)
                return null;

            return elementNodeAttribute.Value;
        }

        /// <summary>
        /// Set a config attribute value
        /// </summary>
        void SetConfigValue(String section, String element, String attribute, String value)
        {
            Debug.Assert(_config != null);

            var sectionNode = _config.Root.Element(section);
            if (sectionNode == null)
                _config.Root.Add(sectionNode = new XElement(section));

            var elementNode = sectionNode.Element(element);
            if (elementNode == null)
                sectionNode.Add(elementNode = new XElement(element));

            elementNode.SetAttributeValue(attribute, value);
        }

        /// <summary>
        /// Loads the Target Process's .config file.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2201:DoNotRaiseReservedExceptionTypes")]
        public void Load()
        {
            if (File.Exists(_configPath))
            {
                _config = XDocument.Load(_configPath);
            }
            else
                _config = new XDocument(new XElement("configuration"));

            if (_config == null)
                throw new ApplicationException("Failed to Load .config");
        }

        /// <summary>
        /// Backs up the original applications .config file.
        /// If the file exists do not overwrite.
        /// </summary>
        private bool BackupOriginalConfig()
        {
            if (!File.Exists(_backupConfigPath))
            {
                try
                {
                    File.Copy(_configPath, _backupConfigPath);
                    return (File.Exists(_backupConfigPath));
                }
                catch(Exception ex)
                {
                    if (ex is ArgumentException ||
                        ex is IOException ||
                        ex is UnauthorizedAccessException)
                    {
                        Debug.Print(ex.Message);
                        return false;
                    }
                    throw;
                }
            }
            return false;
        }

        /// <summary>
        /// Save the current Config to disk.
        /// </summary>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        public bool Save()
        {
            try
            {
                _config.Save(_configPath);
                Load();
                return true;
            }
            catch (Exception)
            {
                Load();
                return false;
            }
        }

        private XDocument _config;
        private String _configPath;
        private String _backupConfigPath;
    }
}
