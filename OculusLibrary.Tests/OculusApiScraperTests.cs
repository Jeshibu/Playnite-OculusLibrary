using OculusLibrary.DataExtraction;
using Playnite.SDK.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace OculusLibrary.Tests
{
    public class OculusApiScraperTests
    {
        [Fact]
        public async Task Asgards_Wrath_Parses_Correctly()
        {
            var subject = Setup("Asgard's Wrath", Branding.Oculus, out var settings);
            string appId = "1180401875303371";

            var data = subject.GetMetadata(appId, settings, true);
            Assert.Equal("Asgard's Wrath", data.Name);
            Assert.NotNull(data.Description);
            Assert.Equal("1.6.0", data.Version);
            ReleaseDateEquals(2019, 10, 9, data.ReleaseDate.Value);
            Assert.Equal(appId, data.GameId);
            Assert.NotNull(data.BackgroundImage?.Path);
            Assert.Equal(new MetadataNameProperty("Oculus"), data.Source);
            MetadataPropertyCollectionsMatch(data.Features, new[] { "Single Player", "VR", "VR Standing", "VR Seated", "VR Room-Scale", "VR Motion Controllers" });
            MetadataPropertyCollectionsMatch(data.Platforms, new[] { "Meta Quest", "Meta Quest 2", "Meta Quest 3", "Meta Quest Pro" }, new[] { "pc_windows" });
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
            var subject = Setup("Sprint Vector", Branding.Meta, out var settings);
            string appId = "1425858557493354";

            var data = subject.GetMetadata(appId, settings, true);
            Assert.Equal("Sprint Vector", data.Name);
            Assert.NotNull(data.Description);
            Assert.Equal("0.0.0.302317", data.Version);
            ReleaseDateEquals(2018, 2, 2, data.ReleaseDate.Value);
            Assert.Equal(appId, data.GameId);
            Assert.NotNull(data.BackgroundImage?.Path);
            Assert.Equal(new MetadataNameProperty("Meta"), data.Source);
            MetadataPropertyCollectionsMatch(data.Features, new[] { "Single Player", "Multiplayer", "VR", "VR Standing", "VR Seated", "VR Room-Scale", "VR Motion Controllers" });
            MetadataPropertyCollectionsMatch(data.Platforms, new[] { "Oculus Rift", "Oculus Rift S" }, new[] { "pc_windows" });
            MetadataPropertyCollectionsMatch(data.Tags, new[] { "VR Comfort: Intense" });
            MetadataPropertyCollectionsMatch(data.Developers, new[] { "Survios" });
            MetadataPropertyCollectionsMatch(data.Publishers, new[] { "Survios" });
            MetadataPropertyCollectionsMatch(data.AgeRatings, new[] { "PEGI 3" });
            MetadataPropertyCollectionsMatch(data.Genres, new[] { "Action", "Racing", "Sports" });
            Assert.Equal(82, data.CommunityScore);
        }

        [Fact]
        public async Task Description_images_are_parsed()
        {
            var subject = Setup("Flight 74", Branding.Oculus, out var settings);

            var data = subject.GetMetadata("1234", settings, true);
            Assert.Equal("Flight 74", data.Name);
            Assert.DoesNotContain("![{", data.Description);
            Assert.Contains("<img src=\"https://scontent.oculuscdn.com/v/t64.5771-25/39035449_2750905201734369_6818732176437659600_n.png?_nc_cat=103&ccb=1-7&_nc_sid=6e7a0a&_nc_ohc=1RXPKwkUeocQ7kNvgG-Ve8i&_nc_ht=scontent.oculuscdn.com&oh=00_AYCvsPGrnJKa24TIuuEQAqKigYHQZ4m0mXE1kmPLuin9Mg&oe=66E276D2\"/>", data.Description);
        }

        [Fact]
        public async Task Description_videos_are_removed()
        {
            var subject = Setup("Taiko Frenzy", Branding.Meta, out var settings);

            var data = subject.GetMetadata("1234", settings, true);
            Assert.Equal("Taiko Frenzy", data.Name);
            Assert.DoesNotContain("![{", data.Description);
            Assert.DoesNotContain(".mp4", data.Description);
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

        private OculusApiScraper Setup(string gameName, Branding branding, out OculusLibrarySettings settings)
        {
            var jsonContent = File.ReadAllText($@".\{gameName}.json");

            var webclient = new FakeWebclient(jsonContent);

            settings = new OculusLibrarySettings { BackgroundSource = BackgroundSource.Hero, Branding = branding };

            return new OculusApiScraper(webclient);
        }

        private class FakeWebclient : IGraphQLClient
        {
            public FakeWebclient(string jsonContent)
            {
                JsonContent = jsonContent;
            }
            public string JsonContent { get; }

            public void Dispose()
            {
            }

            public string GetAccessToken()
            {
                throw new System.NotImplementedException();
            }

            public string GetLibrary(string accessToken, string docId)
            {
                throw new System.NotImplementedException();
            }

            public string GetMetadata(string appId, bool setLocale)
            {
                return JsonContent;
            }
        }
    }
}
