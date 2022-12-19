using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace OculusLibrary.DataExtraction
{
    public class OculusManifestScraper
    {
        private static Regex normalizeFilenameToCanonicalName = new Regex(@"(_assets)?\.json$", RegexOptions.Compiled);
        private readonly ILogger logger = LogManager.GetLogger();
        private readonly IOculusPathSniffer pathSniffer;
        private List<string> _libraryLocations;
        private string _oculusInstallDir;
        private List<string> libraryLocations
        {
            get { return _libraryLocations ?? (_libraryLocations = pathSniffer.GetOculusLibraryLocations()); }
        }
        private string oculusInstallDir
        {
            get { return _oculusInstallDir ?? (_oculusInstallDir = pathSniffer.GetOculusSoftwareInstallationPath()); }
        }

        public OculusManifestScraper(IOculusPathSniffer pathSniffer)
        {
            this.pathSniffer = pathSniffer;
        }

        public IEnumerable<GameMetadata> GetGames(bool minimal)
        {
            logger.Info($"Executing OculusManifestScraper.GetGames");

            var manifests = GetManifests();
            return manifests.Select(m => CreateMetadataFromExpandedManifest(m, minimal));
        }

        public IEnumerable<ExpandedOculusManifest> GetManifests(bool installedOnly = false)
        {
            logger.Info($"Executing OculusManifestScraper.GetGames");

            if (libraryLocations == null || !libraryLocations.Any() || string.IsNullOrEmpty(oculusInstallDir))
            {
                logger.Error($"Cannot ascertain Oculus library/installation locations");
                yield break;
            }

            var parsedIds = new HashSet<string>();

            //go through each library directory to get installed games
            foreach (var currentLibraryBasePath in libraryLocations)
            {
                logger.Info($"Processing Oculus library location {currentLibraryBasePath}");

                foreach (var manifest in GetOculusAppManifests(currentLibraryBasePath))
                {
                    logger.Info($"Processing manifest {manifest.CanonicalName} {manifest.AppId}");

                    if (manifest.AppId == null || !parsedIds.Add(manifest.AppId)) //skip duplicates and games without AppId
                    {
                        continue;
                    }

                    manifest.LibraryBasePath = currentLibraryBasePath;

                    yield return manifest;
                }
            }

            logger.Debug("Installed manifests processed. Moving on to uninstalled.");

            //get all games via the oculus software folder
            if (Directory.Exists($@"{oculusInstallDir}\CoreData\Manifests") && !installedOnly)
            {
                foreach (var manifest in GetOculusAppManifests($@"{oculusInstallDir}\CoreData"))
                {
                    logger.Info($"Processing manifest {manifest.CanonicalName} {manifest.AppId}");

                    if (manifest.AppId == null || !parsedIds.Add(manifest.AppId)) //skip duplicates and games without AppId
                    {
                        continue;
                    }

                    yield return manifest;
                }
            }

            logger.Info($"OculusManifestScraper.GetGames Completing");
        }

        private IEnumerable<ExpandedOculusManifest> GetOculusAppManifests(string oculusBasePath)
        {
            logger.Debug($"Listing Oculus manifests");

            string[] fileEntries = Directory.GetFiles($@"{oculusBasePath}\Manifests\", "*.json");

            if (!fileEntries.Any())
            {
                logger.Info($"No Oculus game manifests found");
            }

            var groupedFiles = fileEntries.GroupBy(f => normalizeFilenameToCanonicalName.Replace(f, string.Empty));

            foreach (var fileGroup in groupedFiles)
            {
                ExpandedOculusManifest manifest = null;

                try
                {
                    //try for non *_assets.json manifests first
                    //if the game is installed the normal manifest will have install data
                    //the assets manifest will never have install data
                    string fileName = fileGroup.FirstOrDefault(f => !f.EndsWith("_assets.json"));

                    if (fileName == default)
                    {
                        fileName = fileGroup.First();
                    }

                    var json = File.ReadAllText(fileName);

                    manifest = OculusManifest.Parse<ExpandedOculusManifest>(json);
                    if (manifest.ThirdParty || manifest.AppId == null)
                    {
                        continue; //The Oculus app also makes manifests for non-Oculus programs that it's seen running, ignore those
                    }

                    manifest.LibraryBasePath = oculusBasePath;

                    if (manifest.CanonicalName.EndsWith("_assets"))
                    {
                        manifest.CanonicalName = manifest.CanonicalName.Substring(0, manifest.CanonicalName.Length - 7);
                    }
                }
                catch (Exception ex)
                {
                    logger.Error($"Exception while processing manifest ({fileGroup}) : {ex}");
                }

                if (manifest != null)
                    yield return manifest;
            }
        }

        private string GetAssetPathIfItExists(string canonicalName, string fileName)
        {
            string path = $@"{oculusInstallDir}\CoreData\Software\StoreAssets\{canonicalName}_assets\{fileName}";
            if (File.Exists(path))
            {
                return path;
            }
            else
            {
                logger.Debug($"Missing asset {path}");
                return null;
            }
        }

        private GameMetadata CreateMetadataFromExpandedManifest(ExpandedOculusManifest manifest, bool minimal)
        {
            bool installed = !string.IsNullOrEmpty(manifest.LibraryBasePath) && !string.IsNullOrEmpty(manifest.LaunchFile) && File.Exists(manifest.ExecutableFullPath);

            var output = OculusLibraryPlugin.GetBaseMetadata();
            output.Name = manifest.LaunchFile ?? manifest.CanonicalName;
            output.GameId = manifest.AppId;

            if (!minimal)
            {
                var icon = GetAssetPathIfItExists(manifest.CanonicalName, "icon_image.jpg");

                if (icon == null && installed)
                {
                    logger.Debug($"Oculus store icon missing from file system- reverting to executable icon");
                    icon = manifest.ExecutableFullPath;
                }

                if (!string.IsNullOrEmpty(icon))
                    output.Icon = new MetadataFile(icon);

                var coverImage = GetAssetPathIfItExists(manifest.CanonicalName, "cover_square_image.jpg");
                if (!string.IsNullOrEmpty(coverImage))
                    output.CoverImage = new MetadataFile(coverImage);
            }

            output.IsInstalled = installed;
            if (installed)
            {
                output.InstallDirectory = manifest.InstallationPath;
            }

            return output;
        }

        public ExpandedOculusManifest GetManifest(string appId, bool installedOnly = false)
        {
            return GetManifests(installedOnly).FirstOrDefault(g => g.AppId == appId);
        }
    }
}