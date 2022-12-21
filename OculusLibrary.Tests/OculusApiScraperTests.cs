using OculusLibrary.DataExtraction;
using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
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
            ReleaseDateEquals(2019, 10, 10, data.ReleaseDate.Value);
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
            Assert.Equal("0.0.0.111496", data.Version);
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

        [Theory]
        [InlineData("Asgard's Wrath")]
        [InlineData("Sprint Vector")]
        public async Task Names_Parse_Correctly(string gameName)
        {
            var subject = Setup(gameName, out var webClient);

            var output = await subject.GetGameName("1234");
            Assert.Equal(gameName, output);
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
            var htmlContent = File.ReadAllText($@".\{gameName}.html");
            var jsonContent = File.ReadAllText($@".\{gameName}.json");

            webclient = new FakeWebclient(htmlContent, jsonContent);

            return new OculusApiScraper(webclient);
        }

        private class FakeWebclient:IWebClient
        {
            public FakeWebclient(string htmlContent, string jsonContent)
            {
                HtmlContent = htmlContent;
                JsonContent = jsonContent;
            }

            public string HtmlContent { get; }
            public string JsonContent { get; }

            public void Dispose()
            {
            }

            public string DownloadString(string address)
            {
                return HtmlContent;
            }

            public async Task<string> DownloadStringAsync(string address)
            {
                return HtmlContent;
            }

            public string UploadValues(string address, string method, NameValueCollection data)
            {
                return JsonContent;
            }

            public async Task<string> UploadValuesAsync(string address, string method, NameValueCollection data)
            {
                return JsonContent;
            }
        }
    }
}
