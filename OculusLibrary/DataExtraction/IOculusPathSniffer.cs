using System;
using System.Collections.Generic;

namespace OculusLibrary.DataExtraction
{
    public interface IOculusPathSniffer
    {
        Dictionary<Guid, string> GetOculusLibraryLocations();
        string GetOculusSoftwareInstallationPath();
    }
}