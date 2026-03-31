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

namespace OculusLibrary;

public class OculusLibraryPlugin : LibraryPlugin
{
    public static Guid PluginId = new("77346DD6-B0CC-4F7D-80F0-C1D138CCAE58");
    public override Guid Id { get; } = PluginId;

    public override string Name => GetPluginName(_settings.Settings);
    public override string LibraryIcon => Path.Combine(ResourcePath, _settings?.Settings.Branding == Branding.Meta ? "metaicon.png" : "oculusicon.png");
    public override LibraryClient Client => new OculusClient(PlayniteApi, LibraryIcon);

    private readonly OculusManifestScraper _manifestScraper;
    private readonly ILogger _logger = LogManager.GetLogger();
    private readonly OculusLibrarySettingsViewModel _settings;
    private static readonly string ResourcePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "Resources");

    private AggregateOculusMetadataCollector MetadataCollector
    {
        get
        {
            var graphQLClient = new GraphQLClient(PlayniteApi.WebViews);
            var apiScraper = new OculusApiScraper(graphQLClient);
            return new AggregateOculusMetadataCollector(_manifestScraper, apiScraper, PlayniteApi, _settings.Settings);
        }
    }

    public OculusLibraryPlugin(IPlayniteAPI api) : base(api)
    {
        try
        {
            _settings = new OculusLibrarySettingsViewModel(this, api);
            var pathSniffer = new OculusPathSniffer(new RegistryValueProvider(), new PathNormaliser(new WMODriveQueryProvider()));
            _manifestScraper = new OculusManifestScraper(pathSniffer);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in OculusLibraryPlugin constructor");
            throw;
        }
    }

    public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
    {
        UpgradeSettings();
    }

    public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
    {
        _logger.Info("GetGames");
        try
        {
            return MetadataCollector.GetGames(args.CancelToken);
        }
        catch (NotAuthenticatedException)
        {
            _logger.Error("Not authenticated");
            PlayniteApi.Notifications.Add(new("oculus-not-authenticated", $"{this.Name} user not authenticated", NotificationType.Error, () => OpenSettingsView()));
            return [];
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting Oculus games");
            PlayniteApi.Notifications.Add(new("oculus-import-error", $"Error during {this.Name} library import: {ex.Message}", NotificationType.Error));
            return [];
        }
    }

    public static ExtendedGameMetadata GetBaseMetadata(OculusLibrarySettings settings) => new()
    {
        Source = new MetadataNameProperty(GetPluginName(settings)),
        Features = [],
        Platforms = [],
        Developers = [],
        Publishers = [],
        Genres = [],
        AgeRatings = [],
        Links = [],
        Tags = [],
    };

    private static string GetPluginName(OculusLibrarySettings settings) => settings.Branding.ToString();

    public override LibraryMetadataProvider GetMetadataDownloader() => MetadataCollector;

    public override ISettings GetSettings(bool firstRunSettings) => _settings;

    public override UserControl GetSettingsView(bool firstRunView) => new OculusLibrarySettingsView();

    public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
    {
        void DebugGetMetadata(string gameId)
        {
            var game = new Game { GameId = gameId };
            var metadata = MetadataCollector.GetMetadata(game);
        }

        yield return new() { MenuSection = "@Oculus/Meta", Description = "Debug: get Chronostrike metadata", Action = _ => DebugGetMetadata("24697269806585974")};
        yield return new() { MenuSection = "@Oculus/Meta", Description = "Debug: get Ghost Town metadata", Action = _ => DebugGetMetadata("9531494193591052")};
    }

    public override IEnumerable<PlayController> GetPlayActions(GetPlayActionsArgs args)
    {
        if (args.Game.PluginId != Id)
            yield break;

        var manifestData = _manifestScraper.GetManifest(args.Game.GameId, installedOnly: true);
        if (manifestData == null || !File.Exists(manifestData.ExecutableFullPath))
        {
            string warning = $"No install manifest data found for {args.Game.Name}";
            _logger.Warn(warning);
            PlayniteApi.Dialogs.ShowErrorMessage(warning);
            yield break;
        }

        if (_settings.Settings.UseOculus)
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

        if (_settings.Settings.UseRevive)
        {
            if (!string.IsNullOrEmpty(manifestData.LaunchParameters) && !manifestData.LaunchParameters.StartsWith(" "))
                manifestData.LaunchParameters = " " + manifestData.LaunchParameters;

            string relativeExePath = manifestData.ExecutableFullPath.Replace(manifestData.LibraryBasePath, string.Empty).TrimStart('\\');
            string arguments = $"/app {manifestData.CanonicalName} /library {manifestData.LibraryKey} \"{relativeExePath}\"{manifestData.LaunchParameters}";
            _logger.Debug($"Revive arguments: {arguments}");
            yield return new AutomaticPlayController(args.Game)
            {
                Type = AutomaticPlayActionType.File,
                Path = _settings.Settings.RevivePath,
                Arguments = arguments,
                Name = $"Play {args.Game.Name} with Revive (LAUNCH STEAMVR FIRST!)",
                TrackingMode = TrackingMode.Directory,
                TrackingPath = manifestData.InstallationPath,
            };
        }
    }

    public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
    {
        UpdateYaml(_settings.Settings, _logger);
        base.OnApplicationStopped(args);
    }

    public static void UpdateYaml(OculusLibrarySettings settings, ILogger logger)
    {
        try
        {
            //Originally this was a direct extension.yaml edit, but YamlDotNet is also used by the Metadata Local plugin
            //which uses version 5.4.0, which can't just take a file path and output a Dictionary<object,object>
            //So that's too much of a pain, now we have this hackier method of just keeping two copies of extension.yaml
            //and copying and overwriting to switch branding
            string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            string source = Path.Combine(dir, $"extension_{settings.Branding}.yaml");
            string target = Path.Combine(dir, "extension.yaml");
            File.Copy(source, target, overwrite: true);
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
        if (_settings.Settings.Version == latestVersion)
            return;

        var games = PlayniteApi.Database.Games.Where(g => g.PluginId == Id).ToList();

        if (_settings.Settings.Version < 2)
        {
            _logger.Info($"Upgrading from version {_settings.Settings.Version} to 2");
            foreach (var game in games)
            {
                if (game.GameActions?.Count > 0)
                {
                    game.GameActions = null;
                    game.IncludeLibraryPluginAction = true;
                    PlayniteApi.Database.Games.Update(game);
                }
            }

            _settings.SeedRevivePath();

            upgraded = true;
        }

        if (_settings.Settings.Version < 3)
        {
            var platforms = PlayniteApi.Database.Platforms.Where(p => p.Name.StartsWith("Oculus Meta ")).ToList();
            if (platforms.Any())
            {
                using var bufferedUpdate = PlayniteApi.Database.Platforms.BufferedUpdate();
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
            _logger.Debug($"Saving version after upgrade to {latestVersion}");
            _settings.Settings.Version = latestVersion;
            SavePluginSettings(_settings.Settings);
        }
    }
}
