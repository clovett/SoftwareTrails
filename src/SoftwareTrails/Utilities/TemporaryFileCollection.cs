using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SoftwareTrails
{
    class TemporaryFileCollection
    {
        HashSet<string> tempFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public void Add(string file)
        {
            tempFiles.Add(file);
        }

        public void AddRange(string[] files)
        {
            if (files != null)
            {
                foreach (string file in files)
                {
                    tempFiles.Add(file);
                }
            }
        }

        public string[] CleanupTempFiles()
        {
            if (tempFiles.Count == 0)
            {
                return new string[0];
            }
            List<string> remaining = new List<string>();
            foreach (string file in tempFiles)
            {
                if (!FileUtilities.TryDeleteFile(file))
                {
                    remaining.Add(file);
                }
            }
            tempFiles.Clear();
            string[] result = remaining.ToArray();
            AddRange(result);
            return result;
        }

    }
}
