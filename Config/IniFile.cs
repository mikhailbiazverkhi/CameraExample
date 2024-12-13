using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace CameraExample.Config
{
    internal class IniFile
    {
        private string _path;

        public IniFile(string path)
        {
            _path = path;
        }

        public string Read(string section, string key, string defaultValue = "")
        {
            if (!File.Exists(_path))
            {
                throw new FileNotFoundException($"INI file not found at {_path}");
            }

            var lines = File.ReadAllLines(_path);
            var sectionFound = false;

            foreach (var line in lines)
            {
                if (line.StartsWith("[") && line.EndsWith("]"))
                    sectionFound = line.Equals($"[{section}]", StringComparison.OrdinalIgnoreCase);

                if (sectionFound && line.StartsWith($"{key}=", StringComparison.OrdinalIgnoreCase))
                    return line.Substring(key.Length + 1).Trim();
            }

            return defaultValue;
        }
    }
}
