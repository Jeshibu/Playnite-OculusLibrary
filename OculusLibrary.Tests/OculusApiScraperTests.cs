using OculusLibrary.DataExtraction;
using Playnite.SDK.Models;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace OculusLibrary.Tests
{
    public class OculusApiScraperTests
    {
        [Fact]
        public async Task Asgards_Wrath_Parses_Correctly()
        {
            var subject = Setup("Asgard's Wrath", out var webClient);
            string appId = "1180401875303371";

            var data = await subject.GetMetadata(appId);
            Assert.Equal("Asgard's Wrath", data.Name);
            Assert.NotNull(data.Description);
            Assert.Equal("1.6.0", data.Version);
            ReleaseDateEquals(2019, 10, 9, data.ReleaseDate.Value);
            Assert.Equal(appId, data.GameId);
            Assert.NotNull(data.BackgroundImage?.Path);
            Assert.Equal(new MetadataNameProperty("Oculus"), data.Source);
            MetadataPropertyCollectionsMatch(data.Features, new[] { "Single Player", "VR", "VR Standing", "VR Seated", "VR Room-Scale", "VR Motion Controllers" });
            MetadataPropertyCollectionsMatch(data.Platforms, new[] { "Oculus Rift", "Oculus Rift S" }, new[] { "pc_windows" });
            MetadataPropertyCollectionsMatch(data.Tags, new[] { "VR Comfort: Moderate" });
            MetadataPropertyCollectionsMatch(data.Developers, new[] { "Sanzaru" });
            MetadataPropertyCollectionsMatch(data.Publishers, new[] { "Oculus" });
            MetadataPropertyCollectionsMatch(data.AgeRatings, new[] { "PEGI 18" });
            MetadataPropertyCollectionsMatch(data.Genres, new[] { "Action", "Adventure", "RPG" });
            Assert.Equal(89, data.CommunityScore);
        }

        [Fact]
        public async Task Sprint_Vector_Parses_Correctly()
        {
            var subject = Setup("Sprint Vector", out var webClient);
            string appId = "1425858557493354";

            var data = await subject.GetMetadata(appId);
            Assert.Equal("Sprint Vector", data.Name);
            Assert.NotNull(data.Description);
            Assert.Equal("0.0.0.302317", data.Version);
            ReleaseDateEquals(2018, 2, 2, data.ReleaseDate.Value);
            Assert.Equal(appId, data.GameId);
            Assert.NotNull(data.BackgroundImage?.Path);
            Assert.Equal(new MetadataNameProperty("Oculus"), data.Source);
            MetadataPropertyCollectionsMatch(data.Features, new[] { "Single Player", "Multiplayer", "VR", "VR Standing", "VR Seated", "VR Room-Scale", "VR Motion Controllers" });
            MetadataPropertyCollectionsMatch(data.Platforms, new[] { "Oculus Rift", "Oculus Rift S" }, new[] { "pc_windows" });
            MetadataPropertyCollectionsMatch(data.Tags, new[] { "VR Comfort: Intense" });
            MetadataPropertyCollectionsMatch(data.Developers, new[] { "Survios" });
            MetadataPropertyCollectionsMatch(data.Publishers, new[] { "Survios" });
            MetadataPropertyCollectionsMatch(data.AgeRatings, new[] { "PEGI 3" });
            MetadataPropertyCollectionsMatch(data.Genres, new[] { "Action", "Racing", "Sports" });
            Assert.Equal(82, data.CommunityScore);
        }

        private void ReleaseDateEquals(int expectedYear, int expectedMonth, int expectedDay, ReleaseDate actual)
        {
            Assert.Equal(expectedYear, actual.Year);
            Assert.Equal(expectedMonth, actual.Month);
            Assert.Equal(expectedDay, actual.Day);
        }

        private void MetadataPropertyCollectionsMatch(HashSet<MetadataProperty> metadataProperties, string[] namePropertyValues, string[] specPropertyValues = null)
        {
            string propertyString = string.Join(", ", metadataProperties.Select(x => x.ToString()).OrderBy(x => x).ToArray());
            var expectedStrings = namePropertyValues.ToList();
            expectedStrings.AddRange(specPropertyValues ?? new string[0]);
            string expectedString = string.Join(", ", expectedStrings);

            Assert.Equal(namePropertyValues.Length + (specPropertyValues?.Length ?? 0), metadataProperties.Count);
            foreach (var nameProp in namePropertyValues)
            {
                bool contains = metadataProperties.OfType<MetadataNameProperty>().Any(x => x.Name == nameProp);
                if (!contains)
                    Assert.Fail($"Name property {nameProp} not found");
            }

            if (specPropertyValues == null)
                return;

            foreach (var specProp in specPropertyValues)
            {
                bool contains = metadataProperties.OfType<MetadataSpecProperty>().Any(x => x.Id == specProp);
                if (!contains)
                    Assert.Fail($"Spec property {specProp} not found");
            }

        }

        private OculusApiScraper Setup(string gameName, out IWebClient webclient)
        {
            var jsonContent = File.ReadAllText($@".\{gameName}.json");

            webclient = new FakeWebclient(jsonContent);

            return new OculusApiScraper(webclient);
        }

        //[Fact]
        public async Task TestRequest()
        {
            var wc = new WebClient();
            wc.Headers[HttpRequestHeader.UserAgent] = "Mozilla/5.0";
            wc.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
            wc.Headers[HttpRequestHeader.Cookie] = "locale=en_US";
            var values = new NameValueCollection
            {
                { "variables", $@"{{""itemId"":""1180401875303371"",""hmdType"":""RIFT"",""requestPDPAssetsAsPNG"":false}}" },
                { "doc_id", "7101363079925397" },
            };
            var bytes = await wc.UploadValuesTaskAsync("https://www.meta.com/ocapi/graphql?forced_locale=en_US", values);
            var contentString = Encoding.UTF8.GetString(bytes);
            Assert.NotNull(contentString);
            dynamic obj = Newtonsoft.Json.JsonConvert.DeserializeObject(contentString);
            string dateString = obj.data.item.release_info.display_date;
            Assert.Equal("Oct 9, 2019", dateString);
        }

        private class FakeWebclient : IWebClient
        {
            public FakeWebclient(string jsonContent)
            {
                JsonContent = jsonContent;
            }
            public string JsonContent { get; }

            public WebHeaderCollection Headers { get; } = new WebHeaderCollection();

            public void Dispose()
            {
            }

            public async Task<string> UploadValuesAsync(string address, string method, NameValueCollection data)
            {
                return JsonContent;
            }
        }
    }
}
