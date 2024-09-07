using OculusLibrary.DataExtraction;
using OculusLibrary.OS;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Controls;
using YamlDotNet.Serialization;

namespace OculusLibrary
{
    public partial class OculusLibraryPlugin : LibraryPlugin
    {
        public static Guid PluginId = new Guid("77346DD6-B0CC-4F7D-80F0-C1D138CCAE58");
        public override Guid Id { get; } = PluginId;

        public override string Name => GetPluginName(settings.Settings);
        public override string LibraryIcon => Path.Combine(ResourcePath, settings?.Settings.Branding == Branding.Meta ? "metaicon.png" : "oculusicon.png");
        public override LibraryClient Client => new OculusClient(PlayniteApi, LibraryIcon);

        private readonly IOculusPathSniffer pathSniffer;
        private readonly OculusManifestScraper manifestScraper;
        private readonly ILogger logger = LogManager.GetLogger();
        private OculusLibrarySettingsViewModel settings;
        private static readonly string ResourcePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Resources");

        private AggregateOculusMetadataCollector MetadataCollector
        {
            get
            {
                var graphQLClient = new GraphQLClient(PlayniteApi);
                var apiScraper = new OculusApiScraper(graphQLClient);
                return new AggregateOculusMetadataCollector(manifestScraper, apiScraper, PlayniteApi, settings.Settings);
            }
        }

        public OculusLibraryPlugin(IPlayniteAPI api) : base(api)
        {
            try
            {
                settings = new OculusLibrarySettingsViewModel(this, api);
                pathSniffer = new OculusPathSniffer(new RegistryValueProvider(), new PathNormaliser(new WMODriveQueryProvider()));
                manifestScraper = new OculusManifestScraper(pathSniffer);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in OculusLibraryPlugin constructor");
                throw;
            }
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            UpgradeSettings();
        }

        public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        {
            logger.Info("GetGames");
            try
            {
                return MetadataCollector.GetGames(settings.Settings, args.CancelToken);
            }
            catch (NotAuthenticatedException)
            {
                logger.Error("Not authenticated");
                PlayniteApi.Notifications.Add(new NotificationMessage("oculus-not-authenticated", $"{this.Name} user not authenticated", NotificationType.Error, () => OpenSettingsView()));
                return new GameMetadata[0];
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error getting Oculus games");
                PlayniteApi.Notifications.Add(new NotificationMessage("oculus-import-error", $"Error during {this.Name} library import: {ex.Message}", NotificationType.Error));
                return new GameMetadata[0];
            }
        }

        public static ExtendedGameMetadata GetBaseMetadata(OculusLibrarySettings settings)
        {
            return new ExtendedGameMetadata
            {
                Source = new MetadataNameProperty(GetPluginName(settings)),
                Features = new HashSet<MetadataProperty>(),
                Platforms = new HashSet<MetadataProperty>(),
                Developers = new HashSet<MetadataProperty>(),
                Publishers = new HashSet<MetadataProperty>(),
                Genres = new HashSet<MetadataProperty>(),
                AgeRatings = new HashSet<MetadataProperty>(),
                Links = new List<Link>(),
                Tags = new HashSet<MetadataProperty>(),
            };
        }

        public static string GetPluginName(OculusLibrarySettings settings) => settings.Branding == Branding.Meta ? "Meta" : "Oculus";

        public override LibraryMetadataProvider GetMetadataDownloader()
        {
            return MetadataCollector;
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settings;
        }

        public override UserControl GetSettingsView(bool firstRunView)
        {
            return new OculusLibrarySettingsView();
        }

        public override IEnumerable<PlayController> GetPlayActions(GetPlayActionsArgs args)
        {
            if (args.Game.PluginId != Id)
                yield break;

            var manifestData = manifestScraper.GetManifest(args.Game.GameId, installedOnly: true);
            if (manifestData == null || !File.Exists(manifestData.ExecutableFullPath))
            {
                string warning = $"No install manifest data found for {args.Game.Name}";
                logger.Warn(warning);
                PlayniteApi.Dialogs.ShowErrorMessage(warning);
            }


            if (settings.Settings.UseOculus)
            {
                yield return new AutomaticPlayController(args.Game)
                {
                    Type = AutomaticPlayActionType.File,
                    Path = manifestData.ExecutableFullPath,
                    Arguments = manifestData.LaunchParameters,
                    Name = $"Play {args.Game.Name}",
                    TrackingMode = TrackingMode.Directory,
                    TrackingPath = manifestData.InstallationPath,
                };
            }

            if (manifestData.LaunchFile2D != null)
            {
                yield return new AutomaticPlayController(args.Game)
                {
                    Type = AutomaticPlayActionType.File,
                    Path = manifestData.ExecutableFullPath2D,
                    Arguments = manifestData.LaunchParameters2D,
                    Name = $"Play {args.Game.Name} without VR",
                    TrackingMode = TrackingMode.Directory,
                    TrackingPath = manifestData.InstallationPath,
                };
            }

            if (settings.Settings.UseRevive)
            {
                if (!string.IsNullOrEmpty(manifestData.LaunchParameters) && !manifestData.LaunchParameters.StartsWith(" "))
                    manifestData.LaunchParameters = " " + manifestData.LaunchParameters;

                string relativeExePath = manifestData.ExecutableFullPath.Replace(manifestData.LibraryBasePath, string.Empty).TrimStart('\\');
                string arguments = $"/app {manifestData.CanonicalName} /library {manifestData.LibraryKey} \"{relativeExePath}\"{manifestData.LaunchParameters}";
                logger.Debug($"Revive arguments: {arguments}");
                yield return new AutomaticPlayController(args.Game)
                {
                    Type = AutomaticPlayActionType.File,
                    Path = settings.Settings.RevivePath,
                    Arguments = arguments,
                    Name = $"Play {args.Game.Name} with Revive (LAUNCH STEAMVR FIRST!)",
                    TrackingMode = TrackingMode.Directory,
                    TrackingPath = manifestData.InstallationPath,
                };
            }
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            UpdateYaml(settings.Settings, logger);
            base.OnApplicationStopped(args);
        }

        public static void UpdateYaml(OculusLibrarySettings settings, ILogger logger)
        {
            try
            {
                var filePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "extension.yaml");
                var yamlContent = new Deserializer().Deserialize(File.ReadAllText(filePath)) as IDictionary<object, object>;
                if (settings.Branding == Branding.Meta)
                {
                    yamlContent["Name"] = "Meta Quest Library Importer";
                    yamlContent["Icon"] = @"Resources\metaicon.png";
                }
                else
                {
                    yamlContent["Name"] = "Oculus Library Importer";
                    yamlContent["Icon"] = @"Resources\oculusicon.png";
                }
                var serialized = new Serializer().Serialize(yamlContent);
                File.WriteAllText(filePath, serialized);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error writing branding to yaml");
            }
        }

        private void UpgradeSettings()
        {
            bool upgraded = false;
            int latestVersion = 3;
            if (settings.Settings.Version == latestVersion)
                return;

            var games = PlayniteApi.Database.Games.Where(g => g.PluginId == Id).ToList();

            if (settings.Settings.Version < 2)
            {
                logger.Info($"Upgrading from version {settings.Settings.Version} to 2");
                foreach (var game in games)
                {
                    if (game.GameActions?.Count > 0)
                    {
                        game.GameActions = null;
                        game.IncludeLibraryPluginAction = true;
                        PlayniteApi.Database.Games.Update(game);
                    }
                }
                settings.SeedRevivePath();

                upgraded = true;
            }

            if (settings.Settings.Version < 3)
            {
                var platforms = PlayniteApi.Database.Platforms.Where(p => p.Name.StartsWith("Oculus Meta ")).ToList();
                if (platforms.Any())
                {
                    using (PlayniteApi.Database.Platforms.BufferedUpdate())
                    foreach (var platform in platforms)
                    {
                        platform.Name = platform.Name.Replace("Oculus Meta ", "Meta ");
                        PlayniteApi.Database.Platforms.Update(platform);
                    }
                }
                upgraded = true;
            }

            if (upgraded)
            {
                logger.Debug($"Saving version after upgrade to {latestVersion}");
                settings.Settings.Version = latestVersion;
                SavePluginSettings(settings.Settings);
            }
        }
    }
}