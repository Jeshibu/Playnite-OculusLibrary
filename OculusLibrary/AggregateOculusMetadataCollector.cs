using OculusLibrary.DataExtraction;
using Playnite.SDK;
using Playnite.SDK.Models;
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

        public AggregateOculusMetadataCollector(OculusManifestScraper manifestScraper, OculusApiScraper apiScraper, IPlayniteAPI api)
        {
            this.manifestScraper = manifestScraper;
            this.apiScraper = apiScraper;
            this.api = api;
        }

        public override GameMetadata GetMetadata(Game game)
        {
            var task = apiScraper.GetMetadata(game?.GameId);
            task.Wait();
            return task.Result;
        }

        public IEnumerable<GameMetadata> GetGames(CancellationToken cancellationToken)
        {
            var manifestGames = manifestScraper.GetGames(cancellationToken);
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
                    //for new games, we have to immediately set the name, because game name isn't overridden by a post-import metadata pass
                    var nameTask = apiScraper.GetGameName(game.GameId);
                    nameTask.Wait();
                    game.Name = nameTask.Result;
                    yield return game;
                }
            }
        }
    }
}