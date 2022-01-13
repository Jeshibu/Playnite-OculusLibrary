using Microsoft.Win32;
using Newtonsoft.Json;
using OculusLibrary.DataExtraction;
using OculusLibrary.OS;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace OculusLibrary
{
    public partial class OculusLibraryPlugin : LibraryPlugin
    {
        public static Guid PluginId = new Guid("77346DD6-B0CC-4F7D-80F0-C1D138CCAE58");
        public override Guid Id { get; } = PluginId;

        public override string Name { get; } = "Oculus";

        private readonly IOculusPathSniffer pathSniffer;
        private readonly AggregateOculusMetadataCollector metadataCollector;
        private readonly OculusApiScraper apiScraper;
        private readonly OculusManifestScraper manifestScraper;
        private readonly ILogger logger;

        public OculusLibraryPlugin(IPlayniteAPI api) : base(api)
        {
            logger = LogManager.GetLogger();
            pathSniffer = new OculusPathSniffer(new RegistryValueProvider(), new PathNormaliser(new WMODriveQueryProvider()), logger);
            apiScraper = new OculusApiScraper(logger);
            manifestScraper = new OculusManifestScraper(pathSniffer, logger);
            metadataCollector = new AggregateOculusMetadataCollector(manifestScraper, apiScraper, api);
        }

        public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        {
            return metadataCollector.GetGames(args.CancelToken);
        }

        public static GameMetadata GetBaseMetadata()
        {
            return new GameMetadata
            {
                Source = new MetadataNameProperty("Oculus"),
                Features = new HashSet<MetadataProperty> { new MetadataNameProperty("VR") },
                Platforms = new HashSet<MetadataProperty>(),
                Developers = new HashSet<MetadataProperty>(),
                Publishers = new HashSet<MetadataProperty>(),
                Genres = new HashSet<MetadataProperty>(),
                AgeRatings = new HashSet<MetadataProperty>(),
                Links = new List<Link>(),
                Tags = new HashSet<MetadataProperty>(),
            };
        }



        }

        public override LibraryMetadataProvider GetMetadataDownloader()
        {
            return metadataCollector;
        }
    }
}