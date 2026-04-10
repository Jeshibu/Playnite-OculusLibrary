using AngleSharp.Parser.Html;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OculusLibrary.DataExtraction.Models;
using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace OculusLibrary.DataExtraction;

public class OculusApiScraper(IGraphQLClient graphQLClient = null)
{
    private readonly ILogger _logger = LogManager.GetLogger();
    private readonly Random _random = new();

    private static string GetStoreUrl(string appId) => $"https://www.meta.com/experiences/{appId}/";

    public IEnumerable<GameMetadata> GetGames(OculusLibrarySettings settings, CancellationToken cancellationToken)
    {
        var response = graphQLClient.GetGames(settings, cancellationToken);

        var output = new List<GameMetadata>();
        output.AddRange(response.RiftGames.Select(i => ToGameMetadata(i, settings, new MetadataSpecProperty("pc_windows"))));
        output.AddRange(response.QuestGames.Select(i => ToGameMetadata(i, settings, new MetadataNameProperty("Meta Quest"))));
        output.AddRange(response.GearGames.Select(i => ToGameMetadata(i, settings, new MetadataNameProperty("Oculus Go"))));
        return output;
    }

    public ExtendedGameMetadata GetMetadata(string appId, OculusLibrarySettings settings, ExtendedGameMetadata data = null)
    {
        var json = GetOculusMetadata(appId);
        if (json?.AppDetails?.display_name == null && json?.LdJsonProduct?.name == null)
            return data;
        var metadata = ToGameMetadata(json, settings, data);
        return metadata;
    }

    private ExtendedGameMetadata ToGameMetadata(MetadataBundled oculusMetadata, OculusLibrarySettings settings, ExtendedGameMetadata data = null)
    {
        data ??= OculusLibraryPlugin.GetBaseMetadata(settings);
        data.Features.Add(new MetadataNameProperty("VR"));

        data.GameId = oculusMetadata.AppDetails?.id ?? oculusMetadata.LdJsonProduct?.sku;
        data.Name = oculusMetadata.AppDetails?.display_name ?? oculusMetadata.LdJsonProduct?.name;
        data.Description = ParseDescription(oculusMetadata.Description?.app_store_item.display_long_description ?? oculusMetadata.LdJsonProduct?.description);

        var starRating = oculusMetadata.Reviews?.quality_rating_score ?? oculusMetadata.OnPageAppStoreItem?.quality_rating_aggregate;
        if (starRating > 0)
            data.CommunityScore = (int)(starRating * 20);

        _logger.Info($"parsing release date: {oculusMetadata.AppDetails?.release_info?.display_date}");
        data.ReleaseDate = ParseReleaseDate(oculusMetadata.AppDetails?.release_info?.display_date);
        data.Version = oculusMetadata.AppDetails?.latest_supported_binary?.version;
        data.Links.Add(new($"{settings.Branding} Store", oculusMetadata.LdJsonProduct?.url ?? GetStoreUrl(data.GameId)));
        if (!string.IsNullOrEmpty(oculusMetadata.AppDetails?.website_url))
            data.Links.Add(new("Website", oculusMetadata.AppDetails.website_url));

        string rating = oculusMetadata.OnPageAppStoreItem?.content_rating?.age_rating_text;
        if (!string.IsNullOrEmpty(rating))
            data.AgeRatings.Add(new MetadataNameProperty(rating));

        SetPropertiesForCollection(oculusMetadata.AppDetails?.supported_platforms_i18n ?? oculusMetadata.LdJsonProduct?.availableOnDevice, data.Platforms, GetHmdPlatformName);

        //the other platform options are ANDROID (Go, GearVR) and ANDROID_6DOF (Quest, Quest 2) but those are covered via HMD
        if (oculusMetadata.Reviews?.platform == "PC" || data.Platforms.OfType<MetadataNameProperty>().Any(p => p.Name.Contains("Rift")))
            data.Platforms.Add(new MetadataSpecProperty("pc_windows"));

        List<string> backgroundImageUrls = [];

        if (settings.BackgroundSource == BackgroundSource.TrailerThumbnail)
        {
            if (oculusMetadata?.LdJsonProduct?.image?.Length > 0)
                backgroundImageUrls.Add(oculusMetadata.LdJsonProduct.image[0]._id);

            else if (oculusMetadata.OnPageAppStoreItem?.trailer?.thumbnail?.Uri != null)
                backgroundImageUrls.Add(oculusMetadata.OnPageAppStoreItem.trailer.thumbnail.Uri);
        }

        if (settings.BackgroundSource == BackgroundSource.Screenshots || backgroundImageUrls.Count == 0)
        {
            if (oculusMetadata.LdJsonProduct?.image?.Length > 3)
                backgroundImageUrls.AddRange(oculusMetadata.LdJsonProduct.image.Skip(3).Select(s => s._id));

            else if (oculusMetadata.OnPageAppStoreItem?.screenshots?.Count > 0)
                backgroundImageUrls.AddRange(oculusMetadata.OnPageAppStoreItem.screenshots.Select(s => s.Uri));
        }

        if ((data.BackgroundImage == null || settings.BackgroundSource == BackgroundSource.Hero) && oculusMetadata.OnPageAppStoreItem?.hero_image?.Uri != null)
            backgroundImageUrls.Add(oculusMetadata.OnPageAppStoreItem.hero_image.Uri);

        data.BackgroundImage = backgroundImageUrls?.Count switch
        {
            null or 0 => null,
            1 => new(backgroundImageUrls[0]),
            _ => new(backgroundImageUrls[_random.Next(backgroundImageUrls.Count)])
        };

        if (oculusMetadata.LdJsonProduct?.image?.Length > 2)
            data.CoverImage = new(oculusMetadata.LdJsonProduct.image.Skip(2).First()._id);

        SetPropertiesForCollection(oculusMetadata.AppDetails?.user_interaction_mode_names, data.Features, GetFeatureFromInteractionMode);
        SetPropertiesForCollection(oculusMetadata.AppDetails?.supported_player_modes, data.Features, GetFeatureFromPlayerMode);
        SetPropertiesForCollection(oculusMetadata.AppDetails?.supported_input_device_names, data.Features, GetInputFeature);
        SetPropertiesForCollection(SplitCompanies(oculusMetadata.AppDetails?.developer_name ?? TrimOrganizationSchema(oculusMetadata.LdJsonProduct?.creator?._id)), data.Developers);
        SetPropertiesForCollection(SplitCompanies(oculusMetadata.AppDetails?.publisher_name ?? TrimOrganizationSchema(oculusMetadata.LdJsonProduct?.publisher?._id)), data.Publishers);
        SetPropertiesForCollection(oculusMetadata.AppDetails?.genre_names ?? oculusMetadata.LdJsonProduct?.applicationSubCategory, data.Genres);

        var comfortRating = GetComfortRating(oculusMetadata.AppDetails?.comfort_rating);
        if (comfortRating != null)
            data.Tags.Add(new MetadataNameProperty(comfortRating));

        if (ulong.TryParse(oculusMetadata.AppDetails?.latest_supported_binary?.total_installed_space, out ulong size))
            data.InstallSize = size;

        return data;
    }

    private static string TrimOrganizationSchema(string organizationSchemaUrl)
    {
        if (string.IsNullOrEmpty(organizationSchemaUrl))
            return null;

        const string organizationUrlRoot = "https://www.meta.com/#/schema/Organization/";
        if (organizationSchemaUrl.StartsWith(organizationUrlRoot))
            return organizationSchemaUrl.Substring(organizationUrlRoot.Length);

        return organizationSchemaUrl;
    }

    private class MetadataBundled
    {
        public PageSourceAppStoreItem OnPageAppStoreItem { get; set; }
        public DescriptionDataRoot Description { get; set; }
        public AppDetailsData AppDetails { get; set; }
        public ReviewData Reviews { get; set; }
        public LdJsonProduct LdJsonProduct { get; set; }
    }

    private MetadataBundled GetOculusMetadata(string appId)
    {
        var metadataRaw = Task.Run(async () => await graphQLClient.GetMetadataAsync(appId)).GetAwaiter().GetResult();

        if (metadataRaw == null)
            return null;

        var output = new MetadataBundled
        {
            OnPageAppStoreItem = GetPageSourceAppStoreItem(metadataRaw.PageSource),
            LdJsonProduct = GetLdJsonData(metadataRaw.PageSource),
        };

        if (metadataRaw.XhrResponse != null)
        {
            var jsonLines = metadataRaw.XhrResponse.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
            output.Description = DeserializeJsonLine<DescriptionDataRoot>(jsonLines, null);
            output.AppDetails = DeserializeJsonLine<AppDetailsData>(jsonLines, "MDCAppStoreV2ParityAppPDPInfo_app$defer$MDCAppStoreV2ParityAppDetails_app");
            output.Reviews = DeserializeJsonLine<ReviewData>(jsonLines, "MDCAppStoreV2ParityAppPDPInfo_app$defer$MDCAppStoreV2ParityAppPDPReviews_app");
        }

        return output;
    }

    private static LdJsonProduct GetLdJsonData(string pageSource)
    {
        var doc = new HtmlParser().Parse(pageSource);

        string ldJson = doc.QuerySelector("script[type='application/ld+json']")?.InnerHtml;
        if (ldJson == null)
            return null;

        var ldObj = JObject.Parse(ldJson);
        if (ldObj["@graph"] is not JArray graphObjects)
            return null;

        foreach (var graphObject in graphObjects)
        {
            if (graphObject["@type"] is not JArray types)
                continue;

            var typeList = types.ToObject<List<string>>();
            if (typeList.Contains("Product"))
                return graphObject.ToObject<LdJsonProduct>();
        }

        return null;
    }

    private static PageSourceAppStoreItem GetPageSourceAppStoreItem(string pageSource)
    {
        var match = Regex.Match(pageSource, """
                                            "app_store_item":(?<app>\{.+\}),"viewer":\{"user":
                                            """);
        if (!match.Success)
            return null;

        string str = match.Groups["app"].Value;
        return JsonConvert.DeserializeObject<PageSourceAppStoreItem>(str);
    }

    private static TData DeserializeJsonLine<TData>(List<string> lines, string label) where TData : class
    {
        foreach (string line in lines)
        {
            var labeledObj = JsonConvert.DeserializeObject<AppStoreLabeledObject>(line);
            if (labeledObj.label == label)
                return JsonConvert.DeserializeObject<AppStoreRootObject<TData>>(line).data;
        }

        return null;
    }

    private static GameMetadata ToGameMetadata(OculusLibraryResponseItem item, OculusLibrarySettings settings, params MetadataProperty[] platforms)
    {
        var output = OculusLibraryPlugin.GetBaseMetadata(settings);
        output.Name = item.DisplayName;
        output.GameId = item.Id;
        foreach (var platform in platforms)
            output.Platforms.Add(platform);
        return output;
    }

    private static int GetAverageRating(Quality_rating_histogram_aggregate_all[] aggregates)
    {
        long totalRating = 0;
        int totalCount = 0;
        foreach (var agg in aggregates)
        {
            totalRating += (long)agg.star_rating * (long)agg.count;
            totalCount += agg.count;
        }

        if (totalCount == 0) return 0;
        return (int)(totalRating * 20 / totalCount);
    }

    private static string GetComfortRating(string jsonComfortRating) => jsonComfortRating switch
    {
        "COMFORTABLE_FOR_MOST" => "VR Comfort: Comfortable",
        "COMFORTABLE_FOR_SOME" => "VR Comfort: Moderate",
        "COMFORTABLE_FOR_FEW" => "VR Comfort: Intense",
        "NOT_RATED" => "VR Comfort: Unrated",
        _ => null
    };

    private static string GetInputFeature(string inputFeature) => inputFeature.ToLowerInvariant() switch
    {
        "touch controllers" => "VR Motion Controllers",
        _ => inputFeature
    };

    /// <summary>
    /// The developer field sometimes contains multiple developers. This splits them while also discarding the ", Ltd." in "Initech, Ltd."
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    private static IEnumerable<string> SplitCompanies(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            yield break;

        string[] splitValues = value.Split([", ", " / "], StringSplitOptions.RemoveEmptyEntries);
        foreach (var val in splitValues)
        {
            if (Regex.IsMatch(val, @"^\s*(llc|ltd|inc|gmbh)\.?\s*$", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture))
                continue;

            yield return val;
        }
    }

    private static ReleaseDate? ParseReleaseDate(string dateString)
    {
        if (DateTime.TryParse(dateString, out var date))
            return new ReleaseDate(date);

        return null;
    }

    private static string GetHmdPlatformName(string oculusJsonName)
    {
        if (oculusJsonName.StartsWith("Rift"))
            return $"Oculus {oculusJsonName}";

        return oculusJsonName;
    }

    private string GetFeatureFromInteractionMode(string interactionMode)
    {
        switch (interactionMode.ToLowerInvariant())
        {
            case "single user":
            case "eén speler": //until I figure out how to actually consistently get en-us localization this is a workaround
                return "Single Player";
            default:
                _logger.Warn("Unknown interaction mode: " + interactionMode);
                return interactionMode;
        }
    }

    private string GetFeatureFromPlayerMode(string playerMode)
    {
        switch (playerMode)
        {
            case "SITTING": return "VR Seated";
            case "STANDING": return "VR Standing";
            case "ROOM_SCALE": return "VR Room-Scale";
            default:
                _logger.Warn("Unknown player mode: " + playerMode);
                return null;
        }
    }

    private static void SetPropertiesForCollection<T>(IEnumerable<T> input, HashSet<MetadataProperty> target, Func<T, MetadataProperty> nameParser)
    {
        foreach (var i in input)
        {
            var prop = nameParser(i);
            if (prop != null)
                target.Add(prop);
        }
    }

    private static void SetPropertiesForCollection<T>(IEnumerable<T> input, HashSet<MetadataProperty> target, Func<T, string> nameParser)
    {
        MetadataProperty PropertySelector(T i)
        {
            string name = nameParser(i);

            return name == null ? null : new MetadataNameProperty(name);
        }

        SetPropertiesForCollection(input, target, PropertySelector);
    }

    private static void SetPropertiesForCollection(IEnumerable<string> input, HashSet<MetadataProperty> target, Func<string, string> nameParser = null)
    {
        if (input == null)
            return;

        nameParser ??= x => x;
        SetPropertiesForCollection<string>(input, target, nameParser);
    }

    private static string ParseDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return description;

        var output = Regex.Replace(description, @"\!\[\{(""(?<key>\w+)"":""?(?<value>\w+)""?,?)+\}\]\((?<url>[^)]+)\)(?<linebreaks>(\s*\r?\n)*)", m =>
        {
            string type = null;
            var keyCaptures = m.Groups["key"].Captures;
            for (int i = 0; i < keyCaptures.Count; i++)
            {
                var keyCapture = keyCaptures[i];
                if (keyCapture.Value != "type")
                    continue;

                type = m.Groups["value"].Captures[i].Value;
            }

            if (type != "image")
                return string.Empty;

            var url = m.Groups["url"].Value;
            string linebreaks = m.Groups["linebreaks"]?.Value;

            return $"<img src=\"{url}\"/>{linebreaks}";
        }, RegexOptions.ExplicitCapture);

        output = Regex.Replace(output, @"##\\?", "");
        output = Regex.Replace(output, @"\*\*(?<text>[^*]+)\*\*", m =>
        {
            var textMatch = m.Groups["text"].Value.Trim();
            return $"<b>{textMatch}</b>";
        });

        return Regex.Replace(output, "\r?\n", "<br>$0");
    }
}
