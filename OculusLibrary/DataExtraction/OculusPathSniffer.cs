using Microsoft.Win32;
using OculusLibrary.DataExtraction;
using OculusLibrary.OS;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OculusLibrary.DataExtraction
{
    public class OculusPathSniffer : IOculusPathSniffer
    {
        private readonly IRegistryValueProvider registryValueProvider;
        private readonly IPathNormaliser pathNormaliser;
        private readonly ILogger logger = LogManager.GetLogger();

        public OculusPathSniffer(
            IRegistryValueProvider registryValueProvider,
            IPathNormaliser pathNormaliser)
        {
            this.registryValueProvider = registryValueProvider;
            this.pathNormaliser = pathNormaliser;
        }

        private Dictionary<Guid, string> GetOculusLibraryLocations(RegistryView platformView)
        {
            var libraries = new Dictionary<Guid, string>();

            logger.Debug($"Getting Oculus library locations from registry ({platformView})");

            try
            {
                var libraryKeyTitles = registryValueProvider.GetSubKeysForPath(platformView,
                                                                                RegistryHive.CurrentUser,
                                                                                @"Software\Oculus VR, LLC\Oculus\Libraries\");

                if (libraryKeyTitles == null || !libraryKeyTitles.Any())
                {
                    logger.Error("No libraries found");
                    return null;
                }

                foreach (var libraryKeyTitle in libraryKeyTitles)
                {
                    if (!Guid.TryParse(libraryKeyTitle, out Guid libraryKey))
                    {
                        logger.Warn($"Could not parse library key {libraryKeyTitle} as a GUID");
                        continue;
                    }

                    var libraryPath = registryValueProvider.GetValueForPath(platformView,
                                                                            RegistryHive.CurrentUser,
                                                                            $@"Software\Oculus VR, LLC\Oculus\Libraries\{libraryKeyTitle}",
                                                                            "Path");

                    if (!string.IsNullOrWhiteSpace(libraryPath))
                    {
                        libraryPath = pathNormaliser.Normalise(libraryPath);
                        libraries.Add(libraryKey, libraryPath);
                        logger.Debug($"Found library: {libraryPath}");
                    }
                }

                logger.Debug($"Libraries located: {libraries.Count}");

                return libraries;
            }
            catch (Exception ex)
            {
                logger.Error($"Exception opening registry keys: {ex}");
                return null;
            }
        }

        public Dictionary<Guid, string> GetOculusLibraryLocations()
        {
            logger.Debug("Trying to get Oculus base path (REG64)");

            var libraryLocations = GetOculusLibraryLocations(RegistryView.Registry64);

            if (libraryLocations == null)
            {
                logger.Debug("Trying to get Oculus base path (REG32)");
                libraryLocations = GetOculusLibraryLocations(RegistryView.Registry32);
            }

            return libraryLocations;
        }

        private string GetOculusSoftwareInstallationPath(RegistryView platformView)
        {
            try
            {
                return registryValueProvider.GetValueForPath(platformView, RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Oculus", "InstallLocation");
            }
            catch (Exception ex)
            {
                logger.Error($"Exception opening registry key: {ex}");
                return null;
            }

        }

        public string GetOculusSoftwareInstallationPath()
        {
            logger.Debug("Trying to get Oculus install path (REG64)");

            var installDir = GetOculusSoftwareInstallationPath(RegistryView.Registry64);

            if (installDir == null)
            {
                logger.Debug("Trying to get Oculus install path (REG32)");
                installDir = GetOculusSoftwareInstallationPath(RegistryView.Registry32);
            }

            return installDir;
        }
    }
}
