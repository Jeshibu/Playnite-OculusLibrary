using NSubstitute;
using NUnit.Framework;
using OculusLibrary.DataExtraction;
using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OculusLibrary.Tests
{
    public class OculusApiScraperTests
    {
        [Test]
        public async Task Asgards_Wrath_Parses_Correctly()
        {
            var subject = Setup("Asgard's Wrath", out var webClient);
            string appId = "1180401875303371";

            var data = await subject.GetMetadata(appId);
            Assert.AreEqual("Asgard's Wrath", data.Name);
            Assert.NotNull(data.Description);
            Assert.AreEqual("1.6.0", data.Version);
            ReleaseDateEquals(2019, 10, 10, data.ReleaseDate.Value);
            Assert.AreEqual(appId, data.GameId);
            Assert.NotNull(data.BackgroundImage?.Path);
            Assert.AreEqual(new MetadataNameProperty("Oculus"), data.Source);
            MetadataPropertyCollectionsMatch(data.Features, new[] { "Single Player", "VR", "VR Standing", "VR Seated", "VR Room-Scale", "VR Motion Controllers" });
            MetadataPropertyCollectionsMatch(data.Platforms, new[] { "Oculus Rift", "Oculus Rift S" }, new[] { "pc_windows" });
            MetadataPropertyCollectionsMatch(data.Tags, new[] { "VR Comfort: Moderate" });
            MetadataPropertyCollectionsMatch(data.Developers, new[] { "Sanzaru" });
            MetadataPropertyCollectionsMatch(data.Publishers, new[] { "Oculus" });
            MetadataPropertyCollectionsMatch(data.AgeRatings, new[] { "PEGI 18" });
            MetadataPropertyCollectionsMatch(data.Genres, new[] { "Action", "Adventure", "RPG" });
            Assert.AreEqual(89, data.CommunityScore);
        }

        [Test]
        public async Task Sprint_Vector_Parses_Correctly()
        {
            var subject = Setup("Sprint Vector", out var webClient);
            string appId = "1425858557493354";

            var data = await subject.GetMetadata(appId);
            Assert.AreEqual("Sprint Vector", data.Name);
            Assert.NotNull(data.Description);
            Assert.AreEqual("0.0.0.111496", data.Version);
            ReleaseDateEquals(2018, 2, 2, data.ReleaseDate.Value);
            Assert.AreEqual(appId, data.GameId);
            Assert.NotNull(data.BackgroundImage?.Path);
            Assert.AreEqual(new MetadataNameProperty("Oculus"), data.Source);
            MetadataPropertyCollectionsMatch(data.Features, new[] { "Single Player", "Multiplayer", "VR", "VR Standing", "VR Seated", "VR Room-Scale", "VR Motion Controllers" });
            MetadataPropertyCollectionsMatch(data.Platforms, new[] { "Oculus Rift", "Oculus Rift S" }, new[] { "pc_windows" });
            MetadataPropertyCollectionsMatch(data.Tags, new[] { "VR Comfort: Intense" });
            MetadataPropertyCollectionsMatch(data.Developers, new[] { "Survios" });
            MetadataPropertyCollectionsMatch(data.Publishers, new[] { "Survios" });
            MetadataPropertyCollectionsMatch(data.AgeRatings, new[] { "PEGI 3" });
            MetadataPropertyCollectionsMatch(data.Genres, new[] { "Action", "Racing", "Sports" });
            Assert.AreEqual(82, data.CommunityScore);
        }

        [TestCase("Asgard's Wrath")]
        [TestCase("Sprint Vector")]
        public async Task Names_Parse_Correctly(string gameName)
        {
            var subject = Setup(gameName, out var webClient);

            var output = await subject.GetGameName("1234");
            Assert.AreEqual(gameName, output);
        }

        private void ReleaseDateEquals(int expectedYear, int expectedMonth, int expectedDay, ReleaseDate actual)
        {
            Assert.AreEqual(expectedYear, actual.Year);
            Assert.AreEqual(expectedMonth, actual.Month);
            Assert.AreEqual(expectedDay, actual.Day);
        }

        private void MetadataPropertyCollectionsMatch(HashSet<MetadataProperty> metadataProperties, string[] namePropertyValues, string[] specPropertyValues = null)
        {
            string propertyString = string.Join(", ", metadataProperties.Select(x => x.ToString()).OrderBy(x => x).ToArray());
            var expectedStrings = namePropertyValues.ToList();
            expectedStrings.AddRange(specPropertyValues ?? new string[0]);
            string expectedString = string.Join(", ", expectedStrings);

            Assert.AreEqual(namePropertyValues.Length + (specPropertyValues?.Length ?? 0), metadataProperties.Count, "Expected [{0}] but found[{1}]", expectedString, propertyString);
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
            var htmlContent = File.ReadAllText($@"{TestContext.CurrentContext.TestDirectory}\{gameName}.html");
            var jsonContent = File.ReadAllText($@"{TestContext.CurrentContext.TestDirectory}\{gameName}.json");

            webclient = Substitute.For<IWebClient>();
            webclient.DownloadString(default).ReturnsForAnyArgs(htmlContent);
            webclient.DownloadStringAsync(default).ReturnsForAnyArgs(Task.FromResult(htmlContent));
            webclient.UploadValues(default, default, default).ReturnsForAnyArgs(jsonContent);
            webclient.UploadValuesAsync(default, default, default).ReturnsForAnyArgs(Task.FromResult(jsonContent));

            var fakeLogger = Substitute.For<ILogger>();

            return new OculusApiScraper(fakeLogger, webclient);
        }
    }
}
