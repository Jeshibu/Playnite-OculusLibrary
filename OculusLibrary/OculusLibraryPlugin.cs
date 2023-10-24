using Microsoft.Win32;
using Newtonsoft.Json;
using OculusLibrary.DataExtraction;
using OculusLibrary.OS;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.Script.Serialization;
using System.Windows.Controls;

namespace OculusLibrary
{
    public partial class OculusLibraryPlugin : LibraryPlugin
    {
        public static Guid PluginId = new Guid("77346DD6-B0CC-4F7D-80F0-C1D138CCAE58");
        public static readonly string IconPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Resources", "oculusicon.png");
        public override Guid Id { get; } = PluginId;

        public override string Name { get; } = "Oculus";
        public override string LibraryIcon => IconPath;
        public override LibraryClient Client => new OculusClient();

        private readonly IOculusPathSniffer pathSniffer;
        private readonly AggregateOculusMetadataCollector metadataCollector;
        private readonly OculusApiScraper apiScraper;
        private readonly OculusManifestScraper manifestScraper;
        private readonly ILogger logger = LogManager.GetLogger();
        private OculusLibrarySettingsViewModel settings;

        public OculusLibraryPlugin(IPlayniteAPI api) : base(api)
        {
            try
            {
                settings = new OculusLibrarySettingsViewModel(this, api);
                pathSniffer = new OculusPathSniffer(new RegistryValueProvider(), new PathNormaliser(new WMODriveQueryProvider()));
                apiScraper = new OculusApiScraper();
                manifestScraper = new OculusManifestScraper(pathSniffer);
                metadataCollector = new AggregateOculusMetadataCollector(manifestScraper, apiScraper, api);
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
            try
            {
                return metadataCollector.GetGames(args.CancelToken);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error getting Oculus games");
                PlayniteApi.Notifications.Add(new NotificationMessage("oculus-import-error", $"Error during Oculus library import: {ex.Message}", NotificationType.Error));
                return new GameMetadata[0];
            }
        }

        public static GameMetadata GetBaseMetadata()
        {
            return new GameMetadata
            {
                Source = new MetadataNameProperty("Oculus"),
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

        public override LibraryMetadataProvider GetMetadataDownloader()
        {
            return metadataCollector;
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

        private void UpgradeSettings()
        {
            bool upgraded = false;
            int latestVersion = 2;
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

            //if(settings.Settings.Version < 3)
            //{
            //    upgraded = true;
            //}

            if (upgraded)
            {
                logger.Debug($"Saving version after upgrade to {latestVersion}");
                settings.Settings.Version = latestVersion;
                SavePluginSettings(settings.Settings);
            }
        }
    }
}