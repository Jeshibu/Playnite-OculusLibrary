using Newtonsoft.Json;
using System;

namespace OculusLibrary
{
    public class OculusManifest
    {
        public string AppId { get; set; }
        public string LaunchFile { get; set; }
        public string LaunchParameters { get; set; }
        public string CanonicalName { get; set; }
        public bool ThirdParty { get; set; }

        public static T Parse<T>(string json) where T : OculusManifest
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("JSON string cannot be null and empty");
            }

            var manifest = JsonConvert.DeserializeObject<T>(json);

            if (manifest == null)
            {
                throw new ManifestParseException("Could not deserialise json");
            }

            manifest.LaunchFile = manifest.LaunchFile?.Replace("/", @"\");

            return manifest;
        }
    }

    public class ExpandedOculusManifest : OculusManifest
    {
        public string LibraryBasePath { get; set; }

        public string InstallationPath { get => $@"{LibraryBasePath}\Software\{CanonicalName}"; }
        public string ExecutableFullPath { get => $@"{InstallationPath}\{LaunchFile}"; }
    }
}