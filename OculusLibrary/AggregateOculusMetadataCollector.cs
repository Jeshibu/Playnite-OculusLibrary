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
        private readonly ILogger logger = LogManager.GetLogger();

        public AggregateOculusMetadataCollector(OculusManifestScraper manifestScraper, OculusApiScraper apiScraper, IPlayniteAPI api)
        {
            this.manifestScraper = manifestScraper;
            this.apiScraper = apiScraper;
            this.api = api;
        }

        public override GameMetadata GetMetadata(Game game)
        {
            try
            {
                var manifestData = manifestScraper.GetGames(minimal: false).FirstOrDefault(g => g.GameId == game.GameId);
                var task = apiScraper.GetMetadata(game?.GameId, manifestData);
                task.Wait();
                var apiData = task.Result;
                return apiData;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error fetching metadata");
                return null;
            }
        }

        public IEnumerable<GameMetadata> GetGames(CancellationToken cancellationToken)
        {
            var manifestGames = manifestScraper.GetGames(minimal: true);
            foreach (var game in manifestGames)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }

                if (api.Database.Games.Any(g => g.PluginId == OculusLibraryPlugin.PluginId && g.GameId == game.GameId))
                {
                    yield return game;
                }
                else
                {
                    //for new games, we have to immediately set the name, because game name isn't overridden by a post-import metadata pass (by default)
                    var nameTask = apiScraper.GetMetadata(game.GameId);
                    nameTask.Wait();

                    if (nameTask.Result != null)
                        game.Name = nameTask.Result.Name;

                    yield return game;
                }
            }
        }
    }
}