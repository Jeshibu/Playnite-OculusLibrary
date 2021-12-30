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

            var gameInfos = new List<GameMetadata>();

            var oculusLibraryLocations = pathSniffer.GetOculusLibraryLocations();
            var oculusInstallDir = pathSniffer.GetOculusSoftwareInstallationPath();

            // ID -> GameMetadata
            var finalGameInfos = new Dictionary<string, GameMetadata>();

            if (oculusLibraryLocations == null || !oculusLibraryLocations.Any())
            {
                logger.Error($"Cannot ascertain Oculus library locations");
                return gameInfos;
            }

            //go through each library directory to get installed games
            foreach (var currentLibraryBasePath in oculusLibraryLocations)
            {
                logger.Info($"Processing Oculus library location {currentLibraryBasePath}");

                foreach (var manifest in GetOculusAppManifests(currentLibraryBasePath, false))
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

                        gameInfos.Add(metadata);
                        finalGameInfos.Add(manifest.AppId, metadata);

                        logger.Info($"Completed manifest {manifest.CanonicalName} {manifest.AppId}");
                    }
                    catch (Exception ex)
                    {
                        logger.Error($"Exception while adding game for manifest {manifest.AppId} : {ex}");
                    }
                }
            }

            //get all games via the oculus software folder
            if (Directory.Exists($@"{oculusInstallDir}\CoreData\Manifests"))
            {
                foreach (var manifest in GetOculusAppManifests($@"{oculusInstallDir}\CoreData", true))
                {
                    if (manifest.AppId == null || finalGameInfos.ContainsKey(manifest.AppId))
                    {
                        // We only want actual Oculus Games that we haven't seen yet
                        continue;
                    }
                    var metadata = CreateMetadata(manifest, oculusInstallDir);
                    var existingGame = this.PlayniteApi.Database.Games.FirstOrDefault(g => g.PluginId == this.Id && g.GameId == metadata.GameId);
                    if (existingGame == null)
                    {
                        //only scrape oculus' api for new games, otherwise only installation data is interesting
                        //which is already covered in CreateMetadata
                        metadata = oculusScraper.GetMetaData(manifest.AppId, metadata);
                    }

                    gameInfos.Add(metadata);
                }
            }

            logger.Info($"Oculus GetGames Completing");

            return gameInfos;
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

        private List<OculusManifest> GetOculusAppManifests(string oculusBasePath, bool return_assets)
        {
            logger.Debug($"Listing Oculus manifests");

            string[] fileEntries = Directory.GetFiles($@"{oculusBasePath}\Manifests\");

            if (!fileEntries.Any())
            {
                logger.Info($"No Oculus game manifests found");
            }

            var manifests = new List<OculusManifest>();

            foreach (string fileName in fileEntries.Where(x => x.EndsWith(".json")))
            {
                try
                {
                    if (fileName.EndsWith("_assets.json") && !return_assets)
                    {
                        // not interested in the asset json files
                        continue;
                    }

                    var json = File.ReadAllText(fileName);

                    var manifest = OculusManifest.Parse(json);
                    if (manifest.CanonicalName.EndsWith("_assets"))
                    {
                        manifest.CanonicalName = manifest.CanonicalName.Substring(0, manifest.CanonicalName.Length - 7);
                    }

                    manifests.Add(manifest);
                }
                catch (Exception ex)
                {
                    logger.Error($"Exception while processing manifest ({fileName}) : {ex}");
                }
            }

            return manifests;
        }
    }
}