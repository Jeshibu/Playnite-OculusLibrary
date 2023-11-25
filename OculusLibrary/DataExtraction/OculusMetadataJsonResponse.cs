using Newtonsoft.Json;
using System.Collections.Generic;

namespace OculusLibrary.DataExtraction
{
    public class OculusMetadataJsonResponse
    {
        public OculusJsonResponseData Data { get; set; }
    }

    public class OculusJsonResponseData
    {
        public OculusJsonResponseDataNode Item { get; set; }
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

        [JsonProperty("long_description_uses_markdown")]
        public bool LongDescriptionUsesMarkdown { get; set; }

        [JsonProperty("supported_player_modes")]
        public List<string> SupportedPlayerModes { get; set; }

        [JsonProperty("publisher_name")]
        public string PublisherName { get; set; }

        [JsonProperty("developer_name")]
        public string DeveloperName { get; set; }

        [JsonProperty("supported_in_app_languages")]
        public List<NameItem> SupportedInAppLanguages { get; set; } = new List<NameItem>();

        [JsonProperty("supported_platforms_i18n")]
        public List<string> SupportedHmdPlatforms { get; set; } = new List<string>();

        [JsonProperty("supported_input_device_names")]
        public List<string> SupportedInputDeviceNames { get; set; } = new List<string>();

        [JsonProperty("user_interaction_mode_names")]
        public List<string> UserInteractionModeNames { get; set; } = new List<string>();

        [JsonProperty("genre_names")]
        public List<string> GenreNames { get; set; } = new List<string>();

        [JsonProperty("latest_supported_binary")]
        public VersionData LatestSupportedBinary { get; set; }

        [JsonProperty("website_url")]
        public string WebsiteUrl { get; set; }

        [JsonProperty("release_info")]
        public ReleaseInfo ReleaseInfo { get; set; }

        [JsonProperty("comfort_rating")]
        public string ComfortRating { get; set; }
        public string CanonicalName { get; set; }
        public string AppName { get; set; }

        [JsonProperty("quality_rating_histogram_aggregate_all")]
        public List<StarRatingAggregate> RatingAggregates { get; set; } = new List<StarRatingAggregate>();

        [JsonProperty("icon_image")]
        public UriItem IconImage { get; set; }

        public List<UriItem> Screenshots { get; set; } = new List<UriItem>();

        public Trailer Trailer { get; set; }
    }

    public class Trailer : UriItem
    {
        public UriItem Thumbnail { get; set; }
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

        [JsonProperty("total_installed_space")]
        public string TotalInstalledSpace { get; set; }

        [JsonProperty("required_space_adjusted")]
        public string RequiredSpaceAdjusted { get; set; }
    }

    public class ReleaseInfo
    {
        [JsonProperty("display_date")]
        public string DisplayDate { get; set; }
    }

    public class StarRatingAggregate
    {
        [JsonProperty("star_rating")]
        public int StarRating { get; set; }
        public int Count { get; set; }
    }
}
