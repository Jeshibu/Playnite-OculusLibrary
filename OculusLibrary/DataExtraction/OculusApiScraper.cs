﻿using Newtonsoft.Json;
using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace OculusLibrary.DataExtraction
{
    public class OculusApiScraper
    {
        private readonly ILogger logger = LogManager.GetLogger();

        public OculusApiScraper(IGraphQLClient webClient = null)
        {
            WebClient = webClient;
        }

        private IGraphQLClient WebClient { get; }

        private static string GetStoreUrl(string appId) => $"https://www.meta.com/experiences/{appId}/";

        public IEnumerable<GameMetadata> GetGames(OculusLibrarySettings settings, CancellationToken cancellationToken)
        {
            if (!settings.ImportAnyOnline || cancellationToken.IsCancellationRequested)
                return new GameMetadata[0];

            var accessToken = WebClient.GetAccessToken(cancellationToken);
            if (accessToken == null)
                throw new NotAuthenticatedException();

            var output = new List<GameMetadata>();

            if (settings.ImportRiftOnline && !cancellationToken.IsCancellationRequested)
                output.AddRange(GetGames(settings, accessToken, "6549375561785664", u => u.ActivePcEntitlements, new MetadataSpecProperty("pc_windows")));

            if (settings.ImportQuestOnline && !cancellationToken.IsCancellationRequested)
                output.AddRange(GetGames(settings, accessToken, "6260775224011087", u => u.ActiveAndroidEntitlements, new MetadataNameProperty("Meta Quest")));

            if (settings.ImportGearGoOnline && !cancellationToken.IsCancellationRequested)
                output.AddRange(GetGames(settings, accessToken, "6040003812794294", u => u.ActiveAndroidEntitlements, new MetadataNameProperty("Oculus Go")));

            return output;
        }

        private IEnumerable<GameMetadata> GetGames(OculusLibrarySettings settings, string accessToken, string docId, Func<OculusLibraryResponseUser, OculusLibraryResponseEntitlements> entitlementsSelector, params MetadataProperty[] platforms)
        {
            var responseString = WebClient.GetLibrary(accessToken, docId);
            var responseObj = JsonConvert.DeserializeObject<OculusLibraryResponseModel>(responseString);
            var entitlements = entitlementsSelector(responseObj.Data.Viewer.User);
            var items = entitlements.Edges.Select(e => e.Node.Item).ToList();
            return items.Select(i => ToGameMetadata(i, settings, platforms));
        }

        public ExtendedGameMetadata GetMetadata(string appId, OculusLibrarySettings settings, bool setLocale, ExtendedGameMetadata data = null)
        {
            var json = GetOculusMetadata(appId, setLocale);
            if (json?.Data?.Item == null) return data;
            var metadata = ToGameMetadata(json.Data.Item, settings, data);
            return metadata;
        }

        public ExtendedGameMetadata ToGameMetadata(OculusJsonResponseDataNode json, OculusLibrarySettings settings, ExtendedGameMetadata data = null)
        {
            data = data ?? OculusLibraryPlugin.GetBaseMetadata(settings);
            data.Features.Add(new MetadataNameProperty("VR"));

            data.GameId = json.Id;
            data.Name = json.DisplayName;
            data.Description = ParseDescription(json.DisplayLongDescription);

            data.CommunityScore = GetAverageRating(json.RatingAggregates);
            logger.Info($"parsing release date: {json.ReleaseInfo?.DisplayDate}");
            data.ReleaseDate = ParseReleaseDate(json.ReleaseInfo?.DisplayDate);
            data.Version = json.LatestSupportedBinary?.Version;
            data.Links.Add(new Link($"{settings.Branding} Store", GetStoreUrl(json.Id)));
            if (!string.IsNullOrEmpty(json.WebsiteUrl))
                data.Links.Add(new Link("Website", json.WebsiteUrl));

            string rating = json.IarcCert?.IarcRating?.AgeRatingText;
            if (!string.IsNullOrEmpty(rating))
                data.AgeRatings.Add(new MetadataNameProperty(rating));

            //platforms
            if (json.Platform == "PC") //the other options are ANDROID (Go, GearVR) and ANDROID_6DOF (Quest, Quest 2) but those are covered via HMD
                data.Platforms.Add(new MetadataSpecProperty("pc_windows"));
            SetPropertiesForCollection(json.SupportedHmdPlatforms, data.Platforms, GetHmdPlatformName);

            if (settings.BackgroundSource == BackgroundSource.TrailerThumbnail && json.Trailer?.Thumbnail?.Uri != null)
                data.BackgroundImage = new MetadataFile(json.Trailer.Thumbnail?.Uri);

            if (json.Screenshots?.Count > 0
                && (settings.BackgroundSource == BackgroundSource.Screenshots
                || (settings.BackgroundSource == BackgroundSource.TrailerThumbnail && data.BackgroundImage == null)))
            {
                var random = new Random();
                var uri = json.Screenshots.OrderBy(s => random.Next(int.MaxValue)).First().Uri;
                data.BackgroundImage = new MetadataFile(uri);
            }

            if ((data.BackgroundImage == null || settings.BackgroundSource == BackgroundSource.Hero) && json.Hero?.Uri != null)
                data.BackgroundImage = new MetadataFile(json.Hero.Uri);

            SetPropertiesForCollection(json.UserInteractionModeNames, data.Features, GetFeatureFromInteractionMode);
            SetPropertiesForCollection(json.SupportedPlayerModes, data.Features, GetFeatureFromPlayerMode);
            SetPropertiesForCollection(json.SupportedInputDevicesList, data.Features, GetFeatureMetadataPropertyFromInputDevice);
            SetPropertiesForCollection(SplitCompanies(json.DeveloperName), data.Developers);
            SetPropertiesForCollection(SplitCompanies(json.PublisherName), data.Publishers);
            SetPropertiesForCollection(json.GenreNames, data.Genres);

            var comfortRating = GetComfortRating(json.ComfortRating);
            if (comfortRating != null)
                data.Tags.Add(new MetadataNameProperty(comfortRating));

            if (ulong.TryParse(json.LatestSupportedBinary?.TotalInstalledSpace, out ulong size))
                data.InstallSize = size;

            if (!string.IsNullOrWhiteSpace(json.IconImage?.Uri))
                data.Icon = new MetadataFile(json.IconImage.Uri);

            return data;
        }

        private OculusMetadataJsonResponse GetOculusMetadata(string appId, bool setLocale)
        {
            var jsonStr = WebClient.GetMetadata(appId, setLocale);
            var data = JsonConvert.DeserializeObject<OculusMetadataJsonResponse>(jsonStr);
            return data;
        }

        private static GameMetadata ToGameMetadata(OculusLibraryResponseItem item, OculusLibrarySettings settings, params MetadataProperty[] platforms)
        {
            var output = OculusLibraryPlugin.GetBaseMetadata(settings);
            output.Name = item.DisplayName;
            output.GameId = item.Id;
            foreach (var platform in platforms)
                output.Platforms.Add(platform);
            return output;
        }

        private static int GetAverageRating(IEnumerable<StarRatingAggregate> aggregates)
        {
            long totalRating = 0;
            int totalCount = 0;
            foreach (var agg in aggregates)
            {
                totalRating += (long)agg.StarRating * (long)agg.Count;
                totalCount += agg.Count;
            }
            if (totalCount == 0) return 0;
            return (int)(totalRating * 20 / totalCount);
        }

        private static string GetComfortRating(string jsonComfortRating)
        {
            switch (jsonComfortRating)
            {
                case "COMFORTABLE_FOR_MOST": return "VR Comfort: Comfortable";
                case "COMFORTABLE_FOR_SOME": return "VR Comfort: Moderate";
                case "COMFORTABLE_FOR_FEW": return "VR Comfort: Intense";
                case "NOT_RATED": return "VR Comfort: Unrated";
                default: return null;
            }
        }

        /// <summary>
        /// The developer field sometimes contains multiple developers. This splits them while also discarding the ", Ltd." in "Initech, Ltd."
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private static IEnumerable<string> SplitCompanies(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                yield break;

            string[] splitValues = value.Split(new[] { ", ", " / " }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var val in splitValues)
            {
                if (Regex.IsMatch(val, @"^\s*(llc|ltd|inc|gmbh)\.?\s*$", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture))
                    continue;

                yield return val;
            }
        }

        private static ReleaseDate? ParseReleaseDate(string dateString)
        {
            if (DateTime.TryParse(dateString, out var date))
                return new ReleaseDate(date);

            return null;
        }

        private string GetHmdPlatformName(string oculusJsonName)
        {
            if (oculusJsonName.StartsWith("Rift"))
                return $"Oculus {oculusJsonName}";

            else return oculusJsonName;
        }

        private string GetFeatureFromInteractionMode(string interactionMode)
        {
            switch (interactionMode.ToLowerInvariant())
            {
                case "single user":
                case "eén speler": //until I figure out how to actually consistently get en-us localization this is a workaround
                    return "Single Player";
                default:
                    logger.Warn("Unknown interaction mode: " + interactionMode);
                    return interactionMode;
            }
        }

        private string GetFeatureFromPlayerMode(string playerMode)
        {
            switch (playerMode)
            {
                case "SITTING": return "VR Seated";
                case "STANDING": return "VR Standing";
                case "ROOM_SCALE": return "VR Room-Scale";
                default:
                    logger.Warn("Unknown player mode: " + playerMode);
                    return null;
            }
        }

        private string GetFeatureFromInputDevice(TagItem inputDevice)
        {
            switch (inputDevice.Tag)
            {
                case "GAMEPAD": return "VR Gamepad";
                case "OCULUS_TOUCH": return "VR Motion Controllers";
                case "GAMEPAD_VIA_TOUCH": return "VR Motion Controllers (As Gamepad)";
                case "RACING_WHEEL": return "Racing Wheel Support"; //found on Dirt Rally
                case "FLIGHT_STICK": return "Flight Stick Support"; //found on End Space
                case "KEYBOARD_MOUSE": return "Input: Keyboard & Mouse";
                case "HAND_TRACKING": return "Hand Tracking";

                default: //localized and unknown input devices
                    logger.Info($"Unknown input device: {inputDevice.Tag} | {inputDevice.Name}");
                    return $"Input: {inputDevice.Name}";
            }
        }

        private MetadataProperty GetFeatureMetadataPropertyFromInputDevice(TagItem inputDevice)
        {
            var str = GetFeatureFromInputDevice(inputDevice);
            if (str == null)
                return null;

            return new MetadataNameProperty(str);
        }

        private static void SetPropertiesForCollection<T>(IEnumerable<T> input, HashSet<MetadataProperty> target, Func<T, MetadataProperty> nameParser)
        {
            foreach (var i in input)
            {
                var prop = nameParser(i);
                if (prop != null)
                    target.Add(prop);
            }
        }

        private static void SetPropertiesForCollection(IEnumerable<string> input, HashSet<MetadataProperty> target, Func<string, string> nameParser = null)
        {
            nameParser = nameParser ?? ((string x) => x);
            Func<string, MetadataProperty> func = jsonName =>
            {
                string name = nameParser(jsonName);

                if (name == null)
                    return null;

                return new MetadataNameProperty(name);
            };
            SetPropertiesForCollection(input, target, func);
        }

        private string ParseDescription(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
                return description;

            var output = Regex.Replace(description, @"\!\[\{(""(?<key>\w+)"":""?(?<value>\w+)""?,?)+\}\]\((?<url>[^)]+)\)(?<linebreaks>(\s*\r?\n)*)", m =>
            {
                string type = null;
                var keyCaptures = m.Groups["key"].Captures;
                for (int i = 0; i < keyCaptures.Count; i++)
                {
                    var keyCapture = keyCaptures[i];
                    if (keyCapture.Value != "type")
                        continue;

                    type = m.Groups["value"].Captures[i].Value;
                }

                if (type != "image")
                    return string.Empty;

                var url = m.Groups["url"].Value;
                string linebreaks = m.Groups["linebreaks"]?.Value;

                return $"<img src=\"{url}\"/>{linebreaks}";
            }, RegexOptions.ExplicitCapture);

            output = Regex.Replace(output, @"##\\?", "");
            output = Regex.Replace(output, @"\*\*(?<text>[^*]+)\*\*", m =>
            {
                var textMatch = m.Groups["text"].Value.Trim();
                return $"<b>{textMatch}</b>";
            });

            return Regex.Replace(output, "\r?\n", "<br>$0");
        }
    }
}
