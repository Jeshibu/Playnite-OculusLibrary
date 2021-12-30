using AngleSharp;
using Newtonsoft.Json;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

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

        public string GetToken(string appId)
        {
            string url = $"https://www.oculus.com/experiences/rift/{appId}";
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

        public OculusJsonResponseDataNode GetJsonData(string appId)
        {
            string token = GetToken(appId);
            var data = GetJsonData(token, appId);
            return data?.Data?.Node;
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
