using System.Collections.Generic;

namespace OculusLibrary.DataExtraction.Models;

public class PageSourceAppStoreItem
{
    public string __typename { get; set; }
    public string id { get; set; }
    public string fallback_ranking_trace { get; set; }
    public string __isAppStoreItem { get; set; }
    public bool is_coming_soon { get; set; }
    public string display_name { get; set; }
    public ReleaseInfo release_info { get; set; }
    public string __isVRDeeplinkTarget { get; set; }
    public bool is_purchase_restricted { get; set; }
    public bool is_get_button_disabled { get; set; }
    public double? quality_rating_aggregate { get; set; }
    public bool is_linked_fb_account_required { get; set; }
    public string __isWishlistableItem { get; set; }
    public bool is_viewer_entitled { get; set; }
    public bool is_salsa_appropriate { get; set; }
    public bool is_viewer_subscribed_coming_soon { get; set; }
    public bool can_viewer_purchase { get; set; }
    public string canonical_name { get; set; }
    public bool viewer_has_preorder { get; set; }
    public string quality_rating_i18n_score_string { get; set; }
    public string quality_rating_i18n_count_string { get; set; }
    public string[] genre_names { get; set; }
    public Content_rating content_rating { get; set; }
    public string user_data_disclosure_translated { get; set; }
    public bool is_giftable { get; set; }
    public bool is_early_access { get; set; }
    public string __isWithHeroMediaCarousel { get; set; }
    public UriItem hero_image { get; set; }
    public List<UriItem> screenshots { get; set; } = [];
    public Trailer trailer { get; set; }
    public List<UriItem> screenshotsThumbnail { get; set; } = [];
    public UriItem heroThumbnail { get; set; }
    public bool is_pdp_override_ongoing_for_viewer { get; set; }
}

public class Content_rating
{
    public string __typename { get; set; }
    public string age_rating_text { get; set; }
    public string rating_definition_uri { get; set; }
    public string __isNode { get; set; }
    public string id { get; set; }
    public UriItem large_image { get; set; }
    public UriItem medium_image { get; set; }
    public UriItem small_image { get; set; }
}

public class Trailer: UriItem
{
    public UriItem thumbnail { get; set; }
}
