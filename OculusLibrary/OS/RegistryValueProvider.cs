using Microsoft.Win32;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OculusLibrary.OS
{
    public class RegistryValueProvider : IRegistryValueProvider
    {
        private ILogger logger = LogManager.GetLogger();
        public RegistryValueProvider() { }

        public List<string> GetSubKeysForPath(
            RegistryView platform,
            RegistryHive hive,
            string path)
        {
            RegistryKey rootKey = RegistryKey.OpenBaseKey(hive, platform);

            var output = rootKey.OpenSubKey(path)?.GetSubKeyNames()?.ToList();

            logger.Debug($"GetSubKeysForPath: platform: {platform}, hive: {hive}, path: {path}, output: {string.Join(Environment.NewLine, output ?? new List<string>())}");

            return output;
        }

        public string GetValueForPath(
            RegistryView platform,
            RegistryHive hive,
            string path,
            string keyName)
        {
            RegistryKey rootKey = RegistryKey.OpenBaseKey(hive, platform);

            var output = rootKey.OpenSubKey(path).GetValue(keyName).ToString();

            logger.Debug($"GetValueForPath: platform: {platform}, hive: {hive}, path: {path}, keyName: {keyName}, output: {output}");

            return output;
        }
    }
}
