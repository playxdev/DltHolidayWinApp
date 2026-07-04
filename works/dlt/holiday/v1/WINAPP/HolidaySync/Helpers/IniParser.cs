using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Dlt.Holiday.Sync.Helpers
{
    public class IniParser
    {
        private readonly Dictionary<string, Dictionary<string, string>> _sections;

        public IniParser(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException(string.Format("INI configuration file not found: {0}", filePath));

            _sections = new Dictionary<string, Dictionary<string, string>>(
                StringComparer.OrdinalIgnoreCase);

            Parse(filePath);
        }

        public string GetValue(string section, string key, string defaultValue = null)
        {
            if (_sections.TryGetValue(section, out var keys))
            {
                if (keys.TryGetValue(key, out var value))
                    return value;
            }

            return defaultValue;
        }

        private void Parse(string filePath)
        {
            string currentSection = string.Empty;
            var currentKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rawLine in File.ReadAllLines(filePath))
            {
                var line = rawLine.Trim();

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (line.StartsWith(";") || line.StartsWith("#"))
                    continue;

                var sectionMatch = Regex.Match(line, @"^\[(.+)\]$");
                if (sectionMatch.Success)
                {
                    if (!string.IsNullOrEmpty(currentSection) && currentKeys.Count > 0)
                    {
                        _sections[currentSection] = currentKeys;
                    }

                    currentSection = sectionMatch.Groups[1].Value.Trim();
                    currentKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    continue;
                }

                var kvpIndex = line.IndexOf('=');
                if (kvpIndex <= 0)
                    continue;

                var key = line.Substring(0, kvpIndex).Trim();
                var value = line.Substring(kvpIndex + 1).Trim();

                if ((value.StartsWith("\"") && value.EndsWith("\"")) ||
                    (value.StartsWith("'") && value.EndsWith("'")))
                {
                    value = value.Substring(1, value.Length - 2);
                }

                currentKeys[key] = value;
            }

            if (!string.IsNullOrEmpty(currentSection) && currentKeys.Count > 0)
            {
                _sections[currentSection] = currentKeys;
            }
        }
    }
}
