using OculusLibrary.DataExtraction;
using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace OculusLibrary
{
    public class AggregateOculusMetadataCollector : LibraryMetadataProvider
    {
        private readonly OculusManifestScraper manifestScraper;
        private readonly OculusApiScraper apiScraper;
        private readonly IPlayniteAPI api;
        private readonly OculusLibrarySettings settings;
        private readonly ILogger logger = LogManager.GetLogger();

        public AggregateOculusMetadataCollector(OculusManifestScraper manifestScraper, OculusApiScraper apiScraper, IPlayniteAPI api, OculusLibrarySettings settings)
        {
            this.manifestScraper = manifestScraper;
            this.apiScraper = apiScraper;
            this.api = api;
            this.settings = settings;
        }

        public override GameMetadata GetMetadata(Game game)
        {
            ExtendedGameMetadata output = null;

            if (settings.ImportOculusAppGames)
            {
                try
                {
                    var manifestData = manifestScraper.GetGames(minimal: false).FirstOrDefault(g => g.GameId == game.GameId);
                    output = manifestData;
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error fetching manifest metadata");
                    return null;
                }
            }

            try
            {
                return apiScraper.GetMetadata(game?.GameId, settings, setLocale: true, data: output);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error fetching API metadata");
                return null;
            }
        }

        public IEnumerable<GameMetadata> GetGames(OculusLibrarySettings settings, CancellationToken cancellationToken)
        {
            var onlineGames = apiScraper.GetGames(settings, cancellationToken);

            var gamesById = onlineGames.ToDictionary(g => g.GameId);

            var manifestGames = manifestScraper.GetGames(minimal: true);
            foreach (var game in manifestGames)
            {
                if (cancellationToken.IsCancellationRequested)
                    return Enumerable.Empty<GameMetadata>();

                if (gamesById.TryGetValue(game.GameId, out var onlineGame))
                {
                    onlineGame.InstallDirectory = game.InstallDirectory;
                    onlineGame.IsInstalled = game.IsInstalled;
                    continue;
                }

                if (!GameExistsInLibrary(game.GameId))
                {
                    try
                    {
                        //for new games, we have to immediately set the name, because game name isn't overridden by a post-import metadata pass (by default)
                        var metadata = apiScraper.GetMetadata(game.GameId, settings, setLocale: true);

                        if (metadata != null)
                            game.Name = metadata.Name;
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "Error getting game name");
                    }

                    gamesById.Add(game.GameId, game);
                }
            }
            return gamesById.Values;
        }

        private bool GameExistsInLibrary(string gameId) =>
            api.Database.Games.Any(g => g.PluginId == OculusLibraryPlugin.PluginId && g.GameId == gameId);
    }
}