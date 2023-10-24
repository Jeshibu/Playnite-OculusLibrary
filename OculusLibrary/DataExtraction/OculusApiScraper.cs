using Newtonsoft.Json;
using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OculusLibrary.DataExtraction
{
    public class OculusApiScraper
    {
        private readonly ILogger logger = LogManager.GetLogger();

        public OculusApiScraper(IWebClient webClient = null)
        {
            WebClient = webClient ?? new WebClientWrapper();
        }

        private IWebClient WebClient { get; }

        public static string GetStoreUrl(string appId) => $"https://www.meta.com/en-us/experiences/pcvr/{appId}/";

        private async Task<OculusJsonResponse> GetJsonData(string appId)
        {
            NameValueCollection values = new NameValueCollection
            {
                { "variables", $@"{{""itemId"":""{appId}"",""hmdType"":""RIFT"",""requestPDPAssetsAsPNG"":false}}" },
                { "doc_id", "7101363079925397" },
            };
            string jsonStr = await WebClient.UploadValuesAsync("https://www.meta.com/ocapi/graphql?forced_locale=en_GB", "POST", values);
            var data = JsonConvert.DeserializeObject<OculusJsonResponse>(jsonStr);
            return data;
        }

        public async Task<GameMetadata> GetMetadata(string appId, GameMetadata data = null)
        {
            var json = await GetJsonData(appId);
            if (json?.Data?.Item == null) return data;
            var metadata = ToGameMetadata(json.Data.Item, data);
            return metadata;
        }

        public GameMetadata ToGameMetadata(OculusJsonResponseDataNode json, GameMetadata data = null)
        {
            data = data ?? OculusLibraryPlugin.GetBaseMetadata();
            data.Features.Add(new MetadataNameProperty("VR"));

            data.GameId = json.Id;
            data.Name = json.DisplayName;
            if (!string.IsNullOrWhiteSpace(json.DisplayLongDescription))
                data.Description = Regex.Replace(json.DisplayLongDescription, "\r?\n", "<br>$0");

            data.CommunityScore = GetAverageRating(json.RatingAggregates);
            data.ReleaseDate = ParseReleaseDate(json.ReleaseInfo?.DisplayDate);
            data.Version = json.LatestSupportedBinary?.Version;
            data.Links.Add(new Link("Oculus Store", GetStoreUrl(json.Id)));
            if (!string.IsNullOrEmpty(json.WebsiteUrl))
                data.Links.Add(new Link("Website", json.WebsiteUrl));

            string rating = json.IarcCert?.IarcRating?.AgeRatingText;
            if (!string.IsNullOrEmpty(rating))
                data.AgeRatings.Add(new MetadataNameProperty(rating));

            //platforms
            if (json.Platform == "PC") //the other options are ANDROID (Go, GearVR) and ANDROID_6DOF (Quest, Quest 2) but those are covered via HMD
                data.Platforms.Add(new MetadataSpecProperty("pc_windows"));
            SetPropertiesForCollection(json.SupportedHmdPlatforms, data.Platforms, GetHmdPlatformName);

            //var backgroundImageUrls = new List<string>();
            //if (json.Hero?.Uri != null)
            //    backgroundImageUrls.Add(json.Hero.Uri);
            //if (json.Screenshots?.Count > 0)
            //    backgroundImageUrls.AddRange(json.Screenshots.Select(s => s.Uri));

            string backgroundImageUrl = json.Hero?.Uri;
            if (!string.IsNullOrEmpty(backgroundImageUrl))
                data.BackgroundImage = new MetadataFile(backgroundImageUrl);

            SetPropertiesForCollection(json.UserInteractionModeNames, data.Features, GetFeatureFromInteractionMode);
            SetPropertiesForCollection(json.SupportedPlayerModes, data.Features, GetFeatureFromPlayerMode);
            SetPropertiesForCollection(json.SupportedInputDeviceNames, data.Features, GetFeatureFromInputDevice);
            SetPropertiesForCollection(SplitCompanies(json.DeveloperName), data.Developers);
            SetPropertiesForCollection(SplitCompanies(json.PublisherName), data.Publishers);
            SetPropertiesForCollection(json.GenreNames, data.Genres);

            var comfortRating = GetComfortRating(json.ComfortRating);
            if (comfortRating != null)
                data.Tags.Add(new MetadataNameProperty(comfortRating));

            if (ulong.TryParse(json.LatestSupportedBinary?.TotalInstalledSpace, out ulong size))
                data.InstallSize = size;

            return data;
        }

        private static int GetAverageRating(IEnumerable<StarRatingAggregate> aggregates)
        {
            long totalRating = 0;
            int totalCount = 0;
            foreach (var agg in aggregates)
            {
                totalRating += (long)agg.StarRating * (long)agg.Count;
                totalCount += agg.Count;
            }
            if (totalCount == 0) return 0;
            return (int)(totalRating * 20 / totalCount);
        }

        private static string GetComfortRating(string jsonComfortRating)
        {
            switch (jsonComfortRating)
            {
                case "COMFORTABLE_FOR_MOST": return "VR Comfort: Comfortable";
                case "COMFORTABLE_FOR_SOME": return "VR Comfort: Moderate";
                case "COMFORTABLE_FOR_FEW": return "VR Comfort: Intense";
                case "NOT_RATED": return "VR Comfort: Unrated";
                default: return null;
            }
        }

        /// <summary>
        /// The developer field sometimes contains multiple developers. This splits them while also discarding the ", Ltd." in "Initech, Ltd."
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private static IEnumerable<string> SplitCompanies(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                yield break;

            string[] splitValues = value.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var val in splitValues)
            {
                if (Regex.IsMatch(val, @"^\s*(llc|ltd|inc)\.?\s*$", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture))
                    continue;

                yield return val;
            }
        }

        private static ReleaseDate ParseReleaseDate(string dateString)
        {
            if (DateTime.TryParseExact(dateString, new[] { "MMM d, yyyy", "d MMM yyyy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                return new ReleaseDate(date);

            return default;
        }

        private string GetHmdPlatformName(string oculusJsonName)
        {
            return $"Oculus {oculusJsonName}";
        }

        private string GetFeatureFromInteractionMode(string interactionMode)
        {
            switch (interactionMode)
            {
                case "Single User": return "Single Player";
                case "Multiplayer": return "Multiplayer";
                case "Co-op": return "Co-Op";
                default:
                    logger.Warn("Unknown interaction mode: " + interactionMode);
                    return null;
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
                    logger.Warn("Unknown player mode: " + playerMode);
                    return null;
            }
        }

        private string GetFeatureFromInputDevice(string inputDevice)
        {
            switch (inputDevice)
            {
                case "Gamepad": return "VR Gamepad";
                case "Touch Controllers":
                case "Oculus Touch": return "VR Motion Controllers";
                case "Touch (as Gamepad)": return "VR Motion Controllers";
                case "Racing Wheel": return "Racing Wheel Support"; //found on Dirt Rally
                case "Flight Stick": return "Flight Stick Support"; //found on End Space

                case "Keyboard & Mouse":
                case "Oculus Remote":
                case "Other Device":
                    return null; //Unsure if these should be features, disregard for now

                default:
                    logger.Warn("Unknown input device: " + inputDevice);
                    return null;
            }
        }

        private void SetPropertiesForCollection(IEnumerable<string> input, HashSet<MetadataProperty> target, Func<string, MetadataProperty> nameParser)
        {
            foreach (var i in input)
            {
                var prop = nameParser(i);
                if (prop != null)
                    target.Add(prop);
            }
        }

        private void SetPropertiesForCollection(IEnumerable<string> input, HashSet<MetadataProperty> target, Func<string, string> nameParser = null)
        {
            nameParser = nameParser ?? ((string x) => x);
            Func<string, MetadataProperty> func = jsonName =>
            {
                string name = nameParser(jsonName);

                if (name == null)
                    return null;

                return new MetadataNameProperty(name);
            };
            SetPropertiesForCollection(input, target, func);
        }
    }
}
