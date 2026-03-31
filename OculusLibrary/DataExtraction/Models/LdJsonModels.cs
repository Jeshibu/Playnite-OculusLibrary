using Newtonsoft.Json;

namespace OculusLibrary.DataExtraction.Models;

public class LdJsonProduct: IdObject
{
    [JsonProperty("@type")]
    public string[] _type { get; set; }
    public string name { get; set; }
    public string url { get; set; }
    public string description { get; set; }
    public string sku { get; set; }
    public IdObject[] image { get; set; }
    public string applicationCategory { get; set; }
    public string[] applicationSubCategory { get; set; }
    public string[] availableOnDevice { get; set; }
    public AggregateRating aggregateRating { get; set; }
    public IdObject publisher { get; set; }
    public IdObject creator { get; set; }
}

public class IdObject
{
    [JsonProperty("@id")]
    public string _id { get; set; }
}

public class AggregateRating
{
    public string _type { get; set; }
    public int ratingValue { get; set; }
    public int ratingCount { get; set; }
    public int bestRating { get; set; }
    public int worstRating { get; set; }
}

