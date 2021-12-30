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
        private readonly OculusWebsiteScraper oculusScraper;
        private readonly ILogger logger;

        public OculusLibraryPlugin(IPlayniteAPI api) : base(api)
        {
            logger = LogManager.GetLogger();
            pathSniffer = new OculusPathSniffer(new RegistryValueProvider(), new PathNormaliser(new WMODriveQueryProvider()), logger);
            oculusScraper = new OculusWebsiteScraper(logger);
        }

        public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        {
            logger.Info($"Executing Oculus GetGames");

            var gameInfos = new List<GameMetadata>();

            var oculusLibraryLocations = pathSniffer.GetOculusLibraryLocations();

            // May or may not be installed
            var sqliteIds = new List<string>();
            var coreDataIds = new List<string>();

            // Definitely Installed
            var softwareIds = new List<string>();

            // ID -> GameMetadata
            var finalGameInfos = new Dictionary<string, GameMetadata>();

            if (oculusLibraryLocations == null || !oculusLibraryLocations.Any())
            {
                logger.Error($"Cannot ascertain Oculus library locations");
                return gameInfos;
            }

            foreach (var currentLibraryBasePath in oculusLibraryLocations)
            {
                logger.Info($"Processing Oculus library location {currentLibraryBasePath}");

                foreach (var manifest in GetOculusAppManifests(currentLibraryBasePath, false))
                {
                    logger.Info($"Processing manifest {manifest.CanonicalName} {manifest.AppId}");

                    softwareIds.Add(manifest.AppId);

                    try
                    {
                        var installationPath = $@"{currentLibraryBasePath}\Software\{manifest.CanonicalName}";
                        var executableFullPath = $@"{installationPath}\{manifest.LaunchFile}";

                        // set a default name
                        var executableName = Path.GetFileNameWithoutExtension(executableFullPath);

                        var icon = $@"{currentLibraryBasePath}\..\CoreData\Software\StoreAssets\{manifest.CanonicalName}_assets\icon_image.jpg";

                        if (!File.Exists(icon))
                        {
                            logger.Debug($"Oculus store icon missing from file system- reverting to executable icon");
                            icon = executableFullPath;
                        }

                        var backgroundImage = $@"{currentLibraryBasePath}\..\CoreData\Software\StoreAssets\{manifest.CanonicalName}_assets\cover_landscape_image_large.png";

                        if (!File.Exists(backgroundImage))
                        {
                            logger.Debug($"Oculus store background missing from file system- selecting no background");
                            backgroundImage = string.Empty;
                        }

                        using (var view = PlayniteApi.WebViews.CreateOffscreenView())
                        {
                            var scrapedData = oculusScraper.GetGameData(view, manifest.AppId);

                            if (scrapedData == null)
                            {
                                logger.Debug($"Failed to retrieve scraped data for game");
                            }

                            logger.Info($"Executable {executableFullPath}");

                            var gameMetadata = ConvertToGameMetadata(scrapedData, executableName, installationPath, icon, backgroundImage);
                            gameMetadata.GameActions = new List<GameAction>
                            {
                                new GameAction
                                {
                                    IsPlayAction = true,
                                    Type = GameActionType.File,
                                    Path = executableFullPath,
                                    Arguments = manifest.LaunchParameters
                                }
                            };

                            gameInfos.Add(gameMetadata);
                            finalGameInfos.Add(manifest.AppId, gameMetadata);

                            logger.Info($"Completed manifest {manifest.CanonicalName} {manifest.AppId}");
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error($"Exception while adding game for manifest {manifest.AppId} : {ex}");
                    }
                }
            }


            foreach (var currentLibraryBasePath in oculusLibraryLocations)
            {
                logger.Info($"Processing Oculus library location {currentLibraryBasePath}");

                if (Directory.Exists($@"{currentLibraryBasePath}\..\CoreData\Manifests"))
                {
                    foreach (var manifest in GetOculusAppManifests($@"{currentLibraryBasePath}\..\CoreData", true))
                    {
                        if (manifest.AppId == null || finalGameInfos.ContainsKey(manifest.AppId))
                        {
                            // We only want actual Oculus Games that we haven't seen yet
                            continue;
                        }

                        var icon = $@"{currentLibraryBasePath}\..\CoreData\Software\StoreAssets\{manifest.CanonicalName}_assets\icon_image.jpg";
                        if (!File.Exists(icon))
                        {
                            icon = string.Empty;
                        }
                        var backgroundImage = $@"{currentLibraryBasePath}\..\CoreData\Software\StoreAssets\{manifest.CanonicalName}_assets\cover_landscape_image_large.png";
                        if (!File.Exists(backgroundImage))
                        {
                            logger.Debug($"Oculus store background missing from file system- selecting no background");
                            backgroundImage = string.Empty;
                        }

                        using (var view = PlayniteApi.WebViews.CreateOffscreenView())
                        {
                            var scrapedData = oculusScraper.GetGameData(view, manifest.AppId);

                            if (scrapedData == null)
                            {
                                logger.Debug($"Failed to retrieve scraped data for game");
                            }

                            var gameMetadata = ConvertToGameMetadata(scrapedData, manifest.CanonicalName, null, icon, backgroundImage);

                            gameInfos.Add(gameMetadata);
                        }
                    }
                }
            }

            logger.Info($"Oculus GetGames Completing");

            return gameInfos;
        }

        private static GameMetadata ConvertToGameMetadata(OculusGameData oculusGameData, string manifestName, string installDirectory = null, string iconPath = null, string backgroundImagePath = null)
        {
            var output = new GameMetadata
            {
                Name = oculusGameData?.Name ?? manifestName,
                Description = oculusGameData?.Description ?? string.Empty,
                GameId = oculusGameData.AppId,
                IsInstalled = installDirectory != null,
                InstallDirectory = installDirectory,
                Icon = new MetadataFile(iconPath),
                BackgroundImage = new MetadataFile(backgroundImagePath ?? oculusGameData?.BackgroundImageUrl),
                Version = oculusGameData?.Version,
                Features = new HashSet<MetadataProperty> { new MetadataNameProperty("VR") },
                Platforms = new HashSet<MetadataProperty>(),
                Developers = new HashSet<MetadataProperty>(),
                Publishers = new HashSet<MetadataProperty>(),
                Genres = new HashSet<MetadataProperty>(),
                AgeRatings = new HashSet<MetadataProperty>(),
                Links = new List<Link>(),
            };
            output.Features.Add(new MetadataNameProperty("VR"));

            if (oculusGameData != null)
            {
                CopyMetadataNameProperties(oculusGameData.Developers, output.Developers);
                CopyMetadataNameProperties(oculusGameData.Publishers, output.Publishers);
                CopyMetadataNameProperties(oculusGameData.Genres, output.Genres);
                CopyMetadataNameProperties(oculusGameData.SupportedPlatforms.Select(x => "Oculus " + x), output.Platforms);
                CopyMetadataNameProperties(oculusGameData.AgeRatings, output.AgeRatings);
                #pragma warning disable IDE0055 //disable formatting
                AddIfPresent(oculusGameData.SupportedPlatforms,     "Rift",                 output.Platforms, "PC (Windows)");
                AddIfPresent(oculusGameData.GameModes,              "Single User",          output.Features, "Single Player");
                AddIfPresent(oculusGameData.GameModes,              "Multiplayer",          output.Features, "Multiplayer");
                AddIfPresent(oculusGameData.GameModes,              "Co-op",                output.Features, "Co-Op");
                AddIfPresent(oculusGameData.PlayerModes,            "Sitting",              output.Features, "VR Seated");
                AddIfPresent(oculusGameData.PlayerModes,            "Standing",             output.Features, "VR Standing");
                AddIfPresent(oculusGameData.PlayerModes,            "Roomscale",            output.Features, "VR Room-Scale");
                AddIfPresent(oculusGameData.SupportedControllers,   "Gamepad",              output.Features, "VR Gamepad", "Full Controller Support");
                AddIfPresent(oculusGameData.SupportedControllers,   "Oculus Touch",         output.Features, "VR Motion Controllers");
                AddIfPresent(oculusGameData.SupportedControllers,   "Touch (as Gamepad)",   output.Features, "VR Motion Controllers");
                AddIfPresent(oculusGameData.SupportedControllers,   "Racing Wheel",         output.Features, "Racing Wheel Support"); //found on Dirt Rally
                AddIfPresent(oculusGameData.SupportedControllers,   "Flight Stick",         output.Features, "Flight Stick Support"); //found on End Space
                #pragma warning restore IDE0055
                //not sure if these controller values should be passed along or if they'd just be clutter: Touchpad, Gear VR Controller, Keyboard & Mouse, Oculus Remote

                if (oculusGameData.ReleaseDate.HasValue)
                    output.ReleaseDate = new ReleaseDate(oculusGameData.ReleaseDate.Value);
                if (!string.IsNullOrEmpty(oculusGameData.StoreUrl))
                    output.Links.Add(new Link("Oculus Store", oculusGameData.StoreUrl));
                if (!string.IsNullOrEmpty(oculusGameData.Website))
                    output.Links.Add(new Link("Website", oculusGameData.Website));
            }
            return output;
        }

        private static void AddIfPresent(string[] oculusData, string oculusName, HashSet<MetadataProperty> metadataProperties, params string[] playniteNames)
        {
            if (oculusData.Contains(oculusName))
            {
                foreach (var playniteName in playniteNames)
                {
                    metadataProperties.Add(new MetadataNameProperty(playniteName));
                }
            }
        }

        private static void CopyMetadataNameProperties(IEnumerable<string> from, HashSet<MetadataProperty> to)
        {
            foreach (var f in from)
            {
                to.Add(new MetadataNameProperty(f));
            }
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