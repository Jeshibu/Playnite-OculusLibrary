using System.Collections.Generic;

namespace OculusLibrary.DataExtraction.Models;

/// <summary>
/// Only used to parse the label of an AppStoreRootObject to discern whether we consume it
/// </summary>
public class AppStoreLabeledObject
{
    public string label { get; set; }
}

public class AppStoreRootObject<TData>
{
    public string label { get; set; }
    public TData data { get; set; }
}

public class DescriptionDataRoot
{
    public AppStoreDescriptionItem app_store_item { get; set; }
}

public class AppStoreDescriptionItem
{
    public string __typename { get; set; }
    public string __isAppStoreItem { get; set; }
    public bool is_ccve_pdp_warning_enforced { get; set; }
    public bool is_cg_pdp_warning_enforced { get; set; }
    public bool is_platform_abuse_pdp_warning_enforced { get; set; }
    public bool is_dmca_pdp_warning_enforced { get; set; }
    public bool is_vrc_pdp_warning_enforced { get; set; }
    public bool is_vrc_compliant { get; set; }
    public string display_long_description { get; set; }
    public bool long_description_uses_markdown { get; set; }
    public object display_machine_translated_long_description { get; set; }
    public bool long_description_requires_machine_translation { get; set; }
    public string id { get; set; }
    public bool is_viewer_duc_admin { get; set; }
}

public class AppDetailsData
{
    public string __isAppStoreItem { get; set; }
    public string developer_privacy_policy_url { get; set; }
    public string developer_terms_of_service_url { get; set; }
    public string[] supported_player_modes { get; set; }
    public string publisher_name { get; set; }
    public Supported_in_app_languages[] supported_in_app_languages { get; set; }
    public List<string> supported_input_device_names { get; set; }
    public string[] supported_platforms_i18n { get; set; }
    public string[] user_interaction_mode_names { get; set; }
    public string category_name { get; set; }
    public string developer_name { get; set; }
    public string[] genre_names { get; set; }
    public Latest_supported_binary latest_supported_binary { get; set; }
    public ReleaseInfo release_info { get; set; }
    public object[] supported_tracking_modes { get; set; }
    public string website_url { get; set; }
    public Builder_profile builder_profile { get; set; }
    public string display_name { get; set; }
    public string id { get; set; }
    public string subscription_type { get; set; }
    public bool has_in_app_ads { get; set; }
    public string __isItemWithComfortRating { get; set; }
    public string comfort_rating { get; set; }
    public string internet_connection { get; set; }
    public string internet_connection_name { get; set; }
    public bool is_meta_cloud { get; set; }
}

public class Supported_in_app_languages
{
    public string name { get; set; }
}

public class Latest_supported_binary
{
    public string __typename { get; set; }
    public string total_installed_space { get; set; }
    public string id { get; set; }
    public string required_space_adjusted { get; set; }
    public string version { get; set; }
    public string change_log { get; set; }
    public string[] supported_hmd_types { get; set; }
}

public class Builder_profile
{
    public string id { get; set; }
    public string name { get; set; }
    public UriItem avatar { get; set; }
}

public class ReviewData
{
    public string display_name { get; set; }
    public double quality_rating_score { get; set; }
    public string platform { get; set; }
    public string id { get; set; }
    public object support_website_url { get; set; }
    public bool can_viewer_review { get; set; }
    public object[] quality_ratings_by_viewer { get; set; }
    public int? quality_rating_count { get; set; }
    public string quality_rating_i18n_score_string { get; set; }
    public string quality_rating_i18n_count_string { get; set; }
    public int? quality_review_count { get; set; }
    public Quality_rating_histogram_aggregate_all[] quality_rating_histogram_aggregate_all { get; set; }
}

public class Quality_rating_histogram_aggregate_all
{
    public int star_rating { get; set; }
    public int count { get; set; }
}
