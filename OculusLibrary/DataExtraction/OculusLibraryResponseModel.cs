using Newtonsoft.Json;
using System.Collections.Generic;

namespace OculusLibrary.DataExtraction
{
    internal class OculusLibraryResponseModel
    {
        public OculusLibraryResponseData Data { get; set; }
    }

    public class OculusLibraryResponseData
    {
        public OculusLibraryResponseViewer Viewer {get;set;}
    }

    public class OculusLibraryResponseViewer
    {
        public OculusLibraryResponseUser User { get; set; }
    }

    public class OculusLibraryResponseUser
    {
        [JsonProperty("active_pc_entitlements")]
        public OculusLibraryResponseEntitlements ActivePcEntitlements { get; set; }

        [JsonProperty("active_android_entitlements")]
        public OculusLibraryResponseEntitlements ActiveAndroidEntitlements { get;set; }
    }

    public class OculusLibraryResponseEntitlements
    {
        public List<OculusLibraryResponseEdge> Edges { get; set; } = new List<OculusLibraryResponseEdge>();
    }

    public class OculusLibraryResponseEdge
    {
        public OculusLibraryResponseNode Node { get; set; }
    }

    public class OculusLibraryResponseNode
    {
        public OculusLibraryResponseItem Item { get; set; }
    }

    public class OculusLibraryResponseItem
    {
        public string Id { get; set; }

        [JsonProperty("is_released")]
        public string IsReleased { get; set; }

        [JsonProperty("display_name")]
        public string DisplayName { get; set; }

        public string Platform { get; set; }

        [JsonProperty("cover_landscape_image")]
        public UriItem Cover { get; set; }
    }
}
