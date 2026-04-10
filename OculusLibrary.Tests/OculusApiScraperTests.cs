using OculusLibrary.DataExtraction;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace OculusLibrary.Tests;

public class OculusApiScraperTests
{
    [Fact]
    public void Asgards_Wrath_Parses_Correctly()
    {
        var subject = Setup("asgards-wrath", Branding.Oculus, out var settings);
        string appId = "1180401875303371";

        var data = subject.GetMetadata(appId, settings);
        Assert.Equal("Asgard's Wrath", data.Name);
        Assert.NotNull(data.Description);
        Assert.Equal(appId, data.GameId);
        Assert.NotNull(data.BackgroundImage?.Path);
        Assert.Equal(new MetadataNameProperty("Oculus"), data.Source);
        MetadataPropertyCollectionsMatch(data.Platforms, ["Oculus Rift", "Oculus Rift S"], ["pc_windows"]);
        MetadataPropertyCollectionsMatch(data.Developers, ["Sanzaru"]);
        MetadataPropertyCollectionsMatch(data.Publishers, ["Oculus"]);
        MetadataPropertyCollectionsMatch(data.Genres, ["Action", "Adventure", "RPG"]);
        Assert.NotNull(data.CoverImage?.Path);
        Assert.NotNull(data.BackgroundImage?.Path);

        Assert.Equal("1.6.0", data.Version);
        ReleaseDateEquals(2019, 10, 9, data.ReleaseDate);
        MetadataPropertyCollectionsMatch(data.Features, ["Single Player", "VR", "VR Standing", "VR Seated", "VR Room-Scale", "VR Motion Controllers"]);
        MetadataPropertyCollectionsMatch(data.Tags, ["VR Comfort: Moderate"]);
        Assert.Equal(89, data.CommunityScore);
    }

    [Fact]
    public void Sprint_Vector_Parses_Correctly()
    {
        var subject = Setup("sprint-vector", Branding.Meta, out var settings);
        string appId = "1425858557493354";

        var data = subject.GetMetadata(appId, settings);
        Assert.Equal("Sprint Vector", data.Name);
        Assert.NotNull(data.Description);
        Assert.Equal(appId, data.GameId);
        Assert.NotNull(data.BackgroundImage?.Path);
        Assert.Equal(new MetadataNameProperty("Meta"), data.Source);
        MetadataPropertyCollectionsMatch(data.Platforms, ["Oculus Rift", "Oculus Rift S"], ["pc_windows"]);
        MetadataPropertyCollectionsMatch(data.Developers, ["Survios"]);
        MetadataPropertyCollectionsMatch(data.Publishers, ["Survios"]);
        MetadataPropertyCollectionsMatch(data.Genres, ["Action", "Racing", "Sports"]);
        Assert.NotNull(data.CoverImage?.Path);
        Assert.NotNull(data.BackgroundImage?.Path);

        Assert.Equal("0.0.0.302317", data.Version);
        ReleaseDateEquals(2018, 2, 2, data.ReleaseDate);
        MetadataPropertyCollectionsMatch(data.Features, ["Single Player", "Multiplayer", "VR", "VR Standing", "VR Seated", "VR Room-Scale", "VR Motion Controllers"]);
        MetadataPropertyCollectionsMatch(data.Tags, ["VR Comfort: Intense"]);
        Assert.Equal(83, data.CommunityScore);
    }

    [Fact]
    public void ChronostrikeDoesNotCrash()
    {
        var subject = Setup("chronostrike", Branding.Meta, out var settings);
        string appId = "24697269806585974";

        var data = subject.GetMetadata(appId, settings);
        Assert.Equal("Chronostrike", data.Name);
    }

    [Fact]
    public void Description_images_are_parsed()
    {
        var subject = Setup("flight-74", Branding.Oculus, out var settings);

        var data = subject.GetMetadata("1234", settings);
        Assert.Equal("Flight 74", data.Name);
        Assert.DoesNotContain("![{", data.Description);
        Assert.Contains(
            "<img src=\"https://scontent.oculuscdn.com/v/t64.5771-25/39035449_2750905201734369_6818732176437659600_n.webp?stp=dst-webp&_nc_cat=103&ccb=1-7&_nc_sid=6e7a0a&_nc_ohc=sLmjODHRfHgQ7kNvwGQLZ5V&_nc_oc=Adkm-BeD7sBbs0ALeY8-XNxUMUc6Gc9sKODywa2s72gaoWbThPwmNe31tgn7aWOHCcQ&_nc_zt=3&_nc_ht=scontent.oculuscdn.com&_nc_ss=8&oh=00_Afx_pwS2yDrjDDLC6OnSGjbm8ZJGR4xuKS1cJMUaMVgtrg&oe=69BE6A5B\"/>",
            data.Description);
    }

    [Fact]
    public void AgeRatingsAreParsed()
    {
        var subject = Setup("flight-74", Branding.Oculus, out var settings);

        var data = subject.GetMetadata("1234", settings);

        MetadataPropertyCollectionsMatch(data.AgeRatings, ["PEGI 7"]);
    }

    [Fact]
    public void Description_videos_are_removed()
    {
        var subject = Setup("taiko-frenzy", Branding.Meta, out var settings);

        var data = subject.GetMetadata("1234", settings);
        Assert.Equal("Taiko Frenzy", data.Name);
        Assert.DoesNotContain("![{", data.Description);
        Assert.DoesNotContain(".mp4", data.Description);
    }

    private static void ReleaseDateEquals(int expectedYear, int expectedMonth, int expectedDay, ReleaseDate? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expectedYear, actual.Value.Year);
        Assert.Equal(expectedMonth, actual.Value.Month);
        Assert.Equal(expectedDay, actual.Value.Day);
    }

    private static void MetadataPropertyCollectionsMatch(HashSet<MetadataProperty> metadataProperties, string[] namePropertyValues, string[] specPropertyValues = null)
    {
        var expectedStrings = namePropertyValues.ToList();
        expectedStrings.AddRange(specPropertyValues ?? []);
        string expected = string.Join(", ", expectedStrings);
        string actual = string.Join(", ", metadataProperties);

        if (expectedStrings.Count != metadataProperties.Count)
            Assert.Fail($"Expected {expectedStrings.Count} items [{expected}], found {metadataProperties.Count} items [{actual}]");
        Assert.Equal(namePropertyValues.Length + (specPropertyValues?.Length ?? 0), metadataProperties.Count);
        foreach (var nameProp in namePropertyValues)
        {
            bool contains = metadataProperties.OfType<MetadataNameProperty>().Any(x => x.Name == nameProp);
            if (!contains)
                Assert.Fail($"Name property {nameProp} not found in collection [{actual}]");
        }

        if (specPropertyValues == null)
            return;

        foreach (var specProp in specPropertyValues)
        {
            bool contains = metadataProperties.OfType<MetadataSpecProperty>().Any(x => x.Id == specProp);
            if (!contains)
                Assert.Fail($"Spec property {specProp} not found in collection [{actual}]");
        }
    }

    private static OculusApiScraper Setup(string fileNameNoExt, Branding branding, out OculusLibrarySettings settings)
    {
        var webclient = new FakeGraphQLClient(fileNameNoExt);

        settings = new OculusLibrarySettings { BackgroundSource = BackgroundSource.Hero, Branding = branding };

        return new OculusApiScraper(webclient);
    }

    private class FakeGraphQLClient(string fileNameNoExt) : IGraphQLClient
    {
        public void Dispose()
        {
        }

        public async Task<OculusMetadataRaw> GetMetadataAsync(string appId, CancellationToken cancellationToken = default) => new()
        {
            PageSource = File.ReadAllText($"./testdata/{fileNameNoExt}.html"),
            XhrResponse = File.ReadAllText($"./testdata/{fileNameNoExt}.json"),
        };

        public OculusLibraryGames GetGames(OculusLibrarySettings settings, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
