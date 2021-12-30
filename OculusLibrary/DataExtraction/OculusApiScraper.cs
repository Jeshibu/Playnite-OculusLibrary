using Newtonsoft.Json;
using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OculusLibrary.DataExtraction
{
    public class OculusApiScraper
    {
        private readonly ILogger logger;

        public OculusApiScraper(ILogger logger, IWebClient webClient = null)
        {
            this.logger = logger;
            WebClient = webClient ?? new WebClientWrapper();
        }

        private IWebClient WebClient { get; }

        private static string GetStoreUrl(string appId)
        {
            return $"https://www.oculus.com/experiences/rift/{appId}";
        }

        public string GetToken(string appId)
        {
            string url = GetStoreUrl(appId);
            var src = WebClient.DownloadString(url);

            //this is part of HTML in a <script> tag, so that's why it's not being HTML parsed
            var match = Regex.Match(src, @"\bid=""OC_ACCESS_TOKEN""\s+value=""(?<token>OC\|[0-9]+\|)""");
            if (!match.Success)
            {
                logger.Error($"Couldn't find token in {url}");
                return null;
            }
            return match.Groups["token"].Value;
        }

        public OculusJsonResponse GetJsonData(string token, string appId)
        {
            NameValueCollection values = new NameValueCollection();
            values.Add("access_token", token);
            values.Add("variables", $"{{\"itemId\":\"{appId}\",\"first\":5,\"last\":null,\"after\":null,\"before\":null,\"forward\":true,\"ordering\":null,\"ratingScores\":null,\"hmdType\":\"RIFT\"}}");
            values.Add("doc_id", "4282918028433524"); //hardcoded?
            string jsonStr = WebClient.UploadValues("https://graph.oculus.com/graphql?forced_locale=en_US", "POST", values);
            var data = JsonConvert.DeserializeObject<OculusJsonResponse>(jsonStr);
            return data;
        }

        public GameMetadata GetMetaData(string appId, GameMetadata data = null)
        {
            string token = GetToken(appId);
            var json = GetJsonData(token, appId);
            if (json?.Data?.Node == null) return null;
            var metadata = ToGameMetadata(json.Data.Node, data);
            return metadata;
        }

        public GameMetadata ToGameMetadata(OculusJsonResponseDataNode json, GameMetadata data = null)
        {
            data = data ?? new GameMetadata
            {
                Features = new HashSet<MetadataProperty>(),
                Platforms = new HashSet<MetadataProperty>(),
                Developers = new HashSet<MetadataProperty>(),
                Publishers = new HashSet<MetadataProperty>(),
                Genres = new HashSet<MetadataProperty>(),
                AgeRatings = new HashSet<MetadataProperty>(),
                Links = new List<Link>(),
            };
            data.Features.Add(new MetadataNameProperty("VR"));

            data.GameId = json.Id;
            data.Name = json.DisplayName;
            data.Description = json.DisplayLongDescription;
            data.CommunityScore = (int)(json.QualityRatingAggregate * 20); //from max 5 to max 100
            data.Version = json.LatestSupportedBinary?.Version;
            data.ReleaseDate = new ReleaseDate(DateTimeOffset.FromUnixTimeSeconds(json.ReleaseDate).DateTime);
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

            string backgroundImageUrl = json.Hero?.Uri;
            if (!string.IsNullOrEmpty(backgroundImageUrl))
                data.BackgroundImage = new MetadataFile(backgroundImageUrl);

            SetPropertiesForCollection(json.UserInteractionModeNames, data.Features, GetFeatureFromInteractionMode);
            SetPropertiesForCollection(json.SupportedPlayerModes, data.Features, GetFeatureFromPlayerMode);
            SetPropertiesForCollection(json.SupportedInputDeviceNames, data.Features, GetFeatureFromInputDevice);
            SetPropertiesForCollection(SplitCompanies(json.DeveloperName), data.Developers);
            SetPropertiesForCollection(SplitCompanies(json.PublisherName), data.Publishers);
            SetPropertiesForCollection(json.GenreNames, data.Genres);

            return data;
        }

        private static IEnumerable<string> SplitCompanies(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                yield break;

            string[] splitValues = value.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var val in splitValues)
            {
                if (!Regex.IsMatch(val, @"^\s*(llc|ltd|inc)\.?\s*$", RegexOptions.IgnoreCase))
                    yield return val;
            }
        }

        private string GetHmdPlatformName(string oculusJsonName)
        {
            switch (oculusJsonName)
            {
                case "RIFT": return "Oculus Rift";
                case "LAGUNA": return "Oculus Rift S";
                case "MONTEREY": return "Oculus Quest";
                case "HOLLYWOOD": return "Oculus Quest 2";
                case "GEARVR": return "Oculus Gear VR";
                case "PACIFIC": return "Oculus Go";
                default:
                    logger.Info("Unknown HMD: " + oculusJsonName);
                    return null;
            }
        }

        private string GetFeatureFromInteractionMode(string interactionMode)
        {
            switch (interactionMode)
            {
                case "Single User": return "Single Player";
                case "Multiplayer": return "Multiplayer";
                case "Co-op": return "Co-Op";
                default:
                    logger.Info("Unknown interaction mode: " + interactionMode);
                    return null;
            }
        }

        private string GetFeatureFromPlayerMode(string playerMode)
        {
            switch (playerMode)
            {
                case "Sitting": return "VR Seated";
                case "Standing": return "VR Standing";
                case "Roomscale": return "VR Room-Scale";
                default:
                    logger.Info("Unknown player mode: " + playerMode);
                    return null;
            }
        }

        private string GetFeatureFromInputDevice(string inputDevice)
        {
            switch (inputDevice)
            {
                case "Gamepad": return "VR Gamepad";
                case "Oculus Touch": return "VR Motion Controllers";
                case "Touch (as Gamepad)": return "VR Motion Controllers";
                case "Racing Wheel": return "Racing Wheel Support"; //found on Dirt Rally
                case "Flight Stick": return "Flight Stick Support"; //found on End Space
                default:
                    logger.Info("Unknown input device: " + inputDevice);
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

    public interface IWebClient
    {
        string DownloadString(string address);
        Task<string> DownloadStringAsync(string address);
        string UploadValues(string address, string method, NameValueCollection data);
        Task<string> UploadValuesAsync(string address, string method, NameValueCollection data);
    }
    public class WebClientWrapper : IWebClient
    {
        public WebClientWrapper()
        {
            this.WebClient = new WebClient();
        }

        private WebClient WebClient { get; }

        public string DownloadString(string address)
        {
            return WebClient.DownloadString(address);
        }

        public Task<string> DownloadStringAsync(string address)
        {
            return WebClient.DownloadStringTaskAsync(address);
        }

        public string UploadValues(string address, string method, NameValueCollection data)
        {
            var bytes = WebClient.UploadValues(address, method, data);
            return Encoding.UTF8.GetString(bytes);
        }

        public async Task<string> UploadValuesAsync(string address, string method, NameValueCollection data)
        {
            var bytes = await WebClient.UploadValuesTaskAsync(address, method, data);
            return Encoding.UTF8.GetString(bytes);
        }
    }

    public class OculusJsonResponse
    {
        public OculusJsonResponseData Data { get; set; }
    }

    public class OculusJsonResponseData
    {
        public OculusJsonResponseDataNode Node { get; set; }
    }

    public class OculusJsonResponseDataNode
    {
        public string Id { get; set; }

        [JsonProperty("display_name")]
        public string DisplayName { get; set; }
        public string Platform { get; set; }

        [JsonProperty("iarc_cert")]
        public IarcCertification IarcCert { get; set; }
        public UriItem Hero { get; set; }

        [JsonProperty("display_long_description")]
        public string DisplayLongDescription { get; set; }

        [JsonProperty("supported_player_modes")]
        public List<string> SupportedPlayerModes { get; set; }

        [JsonProperty("publisher_name")]
        public string PublisherName { get; set; }

        [JsonProperty("developer_name")]
        public string DeveloperName { get; set; }

        [JsonProperty("supported_in_app_languages")]
        public List<NameItem> SupportedInAppLanguages { get; set; }

        [JsonProperty("supported_hmd_platforms")]
        public List<string> SupportedHmdPlatforms { get; set; }

        [JsonProperty("supported_input_device_names")]
        public List<string> SupportedInputDeviceNames { get; set; }

        [JsonProperty("user_interaction_mode_names")]
        public List<string> UserInteractionModeNames { get; set; }

        [JsonProperty("genre_names")]
        public List<string> GenreNames { get; set; }

        [JsonProperty("latest_supported_binary")]
        public VersionData LatestSupportedBinary { get; set; }

        [JsonProperty("website_url")]
        public string WebsiteUrl { get; set; }

        [JsonProperty("release_date")]
        public long ReleaseDate { get; set; }

        [JsonProperty("comfort_rating")]
        public string ComfortRating { get; set; }
        public string CanonicalName { get; set; }
        public string AppName { get; set; }

        [JsonProperty("quality_rating_aggregate")]
        public double QualityRatingAggregate { get; set; }

    }

    public class IarcCertification
    {
        [JsonProperty("iarc_rating")]
        public IarcRating IarcRating { get; set; }
    }

    public class IarcRating
    {
        [JsonProperty("age_rating_text")]
        public string AgeRatingText { get; set; }
    }

    public class UriItem
    {
        public string Uri { get; set; }
    }

    public class NameItem
    {
        public string Name { get; set; }
    }

    public class VersionData
    {
        public string Version { get; set; }

        [JsonProperty("change_log")]
        public string ChangeLog { get; set; }

        [JsonProperty("required_space_adjusted")]
        public string RequiredSpaceAdjusted { get; set; }
    }
}
