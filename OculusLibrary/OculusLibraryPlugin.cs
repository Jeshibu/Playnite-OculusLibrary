using Microsoft.Win32;
using Newtonsoft.Json;
using OculusLibrary.DataExtraction;
using OculusLibrary.OS;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace OculusLibrary
{
    public partial class OculusLibraryPlugin : LibraryPlugin
    {
        public override Guid Id { get; } = Guid.Parse("77346DD6-B0CC-4F7D-80F0-C1D138CCAE58");

        public override string Name { get; } = "Oculus";

        private readonly IOculusPathSniffer pathSniffer;
        private readonly OculusApiScraper oculusScraper;
        private readonly ILogger logger;

        public OculusLibraryPlugin(IPlayniteAPI api) : base(api)
        {
            logger = LogManager.GetLogger();
            pathSniffer = new OculusPathSniffer(new RegistryValueProvider(), new PathNormaliser(new WMODriveQueryProvider()), logger);
            oculusScraper = new OculusApiScraper(logger);
        }

        public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        {
            logger.Info($"Executing Oculus GetGames");

            var oculusLibraryLocations = pathSniffer.GetOculusLibraryLocations();
            var oculusInstallDir = pathSniffer.GetOculusSoftwareInstallationPath();

            // ID -> GameMetadata
            var gamesById = new Dictionary<string, GameMetadata>();

            if (oculusLibraryLocations == null || !oculusLibraryLocations.Any())
            {
                logger.Error($"Cannot ascertain Oculus library locations");
                return gamesById.Values;
            }

            //go through each library directory to get installed games
            foreach (var currentLibraryBasePath in oculusLibraryLocations)
            {
                logger.Info($"Processing Oculus library location {currentLibraryBasePath}");

                foreach (var manifest in GetOculusAppManifests(currentLibraryBasePath))
                {
                    logger.Info($"Processing manifest {manifest.CanonicalName} {manifest.AppId}");

                    try
                    {
                        var metadata = CreateMetadata(manifest, oculusInstallDir, currentLibraryBasePath);
                        var existingGame = this.PlayniteApi.Database.Games.FirstOrDefault(g => g.PluginId == this.Id && g.GameId == metadata.GameId);
                        if (existingGame == null)
                        {
                            //only scrape oculus' api for new games, otherwise only installation data is interesting
                            //which is already covered in CreateMetadata
                            metadata = oculusScraper.GetMetaData(manifest.AppId, metadata);
                        }

                        gamesById.Add(manifest.AppId, metadata);

                        logger.Info($"Completed manifest {manifest.CanonicalName} {manifest.AppId}");
                    }
                    catch (Exception ex)
                    {
                        logger.Error($"Exception while adding game for manifest {manifest.AppId} : {ex}");
                    }
                }
            }

            logger.Debug("Installed manifests processed. Moving on to uninstalled.");

            //get all games via the oculus software folder
            if (Directory.Exists($@"{oculusInstallDir}\CoreData\Manifests"))
            {
                foreach (var manifest in GetOculusAppManifests($@"{oculusInstallDir}\CoreData"))
                {
                    logger.Info($"Processing manifest {manifest.CanonicalName} {manifest.AppId}");

                    if (manifest.AppId == null || gamesById.ContainsKey(manifest.AppId))
                    {
                        // We only want actual Oculus Games that we haven't seen yet
                        continue;
                    }
                    var metadata = CreateMetadata(manifest, oculusInstallDir);
                    var existingGame = PlayniteApi.Database.Games.FirstOrDefault(g => g.PluginId == this.Id && g.GameId == metadata.GameId);
                    if (existingGame == null)
                    {
                        //only scrape oculus' api for new games, otherwise only installation data is interesting
                        //which is already covered in CreateMetadata
                        metadata = oculusScraper.GetMetaData(manifest.AppId, metadata);
                    }

                    gamesById.Add(manifest.AppId, metadata);
                    logger.Info($"Completed manifest {manifest.CanonicalName} {manifest.AppId}");
                }
            }

            logger.Info($"Oculus GetGames Completing");

            return gamesById.Values;
        }

        private GameMetadata CreateMetadata(OculusManifest manifest, string oculusInstallDir, string currentLibraryBasePath = null)
        {
            var installationPath = $@"{currentLibraryBasePath}\Software\{manifest.CanonicalName}";
            var executableFullPath = $@"{installationPath}\{manifest.LaunchFile}";

            bool installed = !string.IsNullOrEmpty(currentLibraryBasePath) && !string.IsNullOrEmpty(manifest.LaunchFile) && File.Exists(executableFullPath);

            var output = new GameMetadata
            {
                Name = manifest.LaunchFile ?? manifest.CanonicalName,
                GameId = manifest.AppId,
                IsInstalled = installed,
                Features = new HashSet<MetadataProperty> { new MetadataNameProperty("VR") },
                Platforms = new HashSet<MetadataProperty>(),
                Developers = new HashSet<MetadataProperty>(),
                Publishers = new HashSet<MetadataProperty>(),
                Genres = new HashSet<MetadataProperty>(),
                AgeRatings = new HashSet<MetadataProperty>(),
                Links = new List<Link>(),
                Tags = new HashSet<MetadataProperty>(),
            };

            #region images
            var icon = $@"{oculusInstallDir}\CoreData\Software\StoreAssets\{manifest.CanonicalName}_assets\icon_image.jpg";

            if (!File.Exists(icon))
            {
                if (installed)
                {
                    logger.Debug($"Oculus store icon missing from file system- reverting to executable icon");
                    icon = executableFullPath;
                }
                else
                {
                    logger.Debug($"Oculus store icon missing from file system");
                    icon = null;
                }
            }

            var backgroundImage = $@"{oculusInstallDir}\CoreData\Software\StoreAssets\{manifest.CanonicalName}_assets\cover_landscape_image_large.png";

            if (!File.Exists(backgroundImage))
            {
                logger.Debug($"Oculus store background missing from file system- selecting no background");
                backgroundImage = null;
            }

            if (!string.IsNullOrEmpty(icon))
                output.Icon = new MetadataFile(icon);

            if (!string.IsNullOrEmpty(backgroundImage))
                output.BackgroundImage = new MetadataFile(backgroundImage);
            #endregion images

            if (installed)
            {
                output.InstallDirectory = installationPath;
                output.GameActions = new List<GameAction>
                {
                    new GameAction
                    {
                        IsPlayAction = true,
                        Type = GameActionType.File,
                        Path = executableFullPath,
                        Arguments = manifest.LaunchParameters
                    }
                };
            }

            return output;
        }

        private List<OculusManifest> GetOculusAppManifests(string oculusBasePath)
        {
            logger.Debug($"Listing Oculus manifests");

            string[] fileEntries = Directory.GetFiles($@"{oculusBasePath}\Manifests\", "*.json");

            if (!fileEntries.Any())
            {
                logger.Info($"No Oculus game manifests found");
            }

            var groupedFiles = fileEntries.GroupBy(f => Regex.Replace(f, @"(_assets)?\.json$", string.Empty));

            var manifests = new List<OculusManifest>();

            foreach (var fileGroup in groupedFiles)
            {
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

                    var manifest = OculusManifest.Parse(json);
                    if (manifest.ThirdParty)
                    {
                        continue; //The Oculus app also makes manifests for non-Oculus programs that it's seen running, ignore those
                    }

                    if (manifest.CanonicalName.EndsWith("_assets"))
                    {
                        manifest.CanonicalName = manifest.CanonicalName.Substring(0, manifest.CanonicalName.Length - 7);
                    }

                    manifests.Add(manifest);
                }
                catch (Exception ex)
                {
                    logger.Error($"Exception while processing manifest ({fileGroup}) : {ex}");
                }
            }

            return manifests;
        }
    }
}