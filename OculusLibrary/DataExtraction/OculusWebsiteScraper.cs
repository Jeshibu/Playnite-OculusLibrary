using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Parser.Html;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace OculusLibrary.DataExtraction
{
    public class OculusWebsiteScraper
    {
        private readonly JavaScriptSerializer serialiser;
        private readonly ILogger logger;
        private readonly Func<IBrowsingContext> GetBrowsingContext;

        public OculusWebsiteScraper(ILogger logger, Func<IBrowsingContext> getBrowsingContext = null)
        {
            serialiser = new JavaScriptSerializer();
            this.logger = logger;
            GetBrowsingContext = getBrowsingContext ?? GetDefaultBrowsingContext;
        }

        private static IBrowsingContext GetDefaultBrowsingContext()
        {
            //TODO: maybe add "locale" cookie and set it to en_US
            var config = Configuration.Default.WithDefaultLoader();
            var context = BrowsingContext.New(config);

            return context;
        }

        public OculusWebsiteJson ScrapeDataForApplicationId(IWebView view, string appId)
        {
            logger.Debug($"Trying to scrape {appId}");

            // robo recall 1081190428622821
            try
            {
                view.NavigateAndWait($"https://www.oculus.com/experiences/rift/{appId}/");
                var source = view.GetPageSource();

                // get the json block from the source which contains the games meta data

                //Regex regex = new Regex(@"<script type=""application\/ld\+json"">([\s\S]*?)<\/script>");
                Regex regex = new Regex(@"<meta name=""json-ld"" content=""(.*?)"">", RegexOptions.Multiline);
                var json = regex.Match(source);

                if (json == null)
                {
                    logger.Error($"json file was null");
                    return null;
                }
                if (json.Groups.Count < 2)
                {
                    logger.Error($"json had {json.Groups.Count} regex match groups- was expecting 2 or more");
                    return null;
                }

                String reencoded_json = json.Groups[1].Value.Replace("&quot;", "\"").Replace("&amp;", "&");

                var manifest = serialiser.Deserialize<OculusWebsiteJson>(reencoded_json);

                return manifest;
            }
            catch (Exception ex)
            {
                logger.Error($"Exception trying to scrape {appId} : {ex}");
                return null;
            }
        }

        public OculusGameData GetGameData(IWebView view, string appId)
        {
            logger.Debug($"Trying to scrape {appId}");

            // robo recall 1081190428622821
            try
            {
                //var browsingContext = GetBrowsingContext();
                //var document = await browsingContext.OpenAsync($"https://www.oculus.com/experiences/rift/{appId}/");
                string url = $"https://www.oculus.com/experiences/rift/{appId}/";
                view.NavigateAndWait(url);
                System.Threading.Thread.Sleep(1700);
                var source = view.GetPageSource();
                var document = new HtmlParser().Parse(source);

                var jsonData = GetJsonData(document);
                var output = new OculusGameData { AppId = appId, StoreUrl = url };
                if (jsonData != null)
                {
                    output.Name = jsonData.Name;
                    output.AverageRating = jsonData.AggregateRating?.RatingValue;
                    output.Description = jsonData.Description;
                    output.BackgroundImageUrl = jsonData.Image?.FirstOrDefault();
                }
                return ParseDetails(document, output);
            }
            catch (Exception ex)
            {
                logger.Error($"Exception trying to scrape {appId} : {ex}");
                return null;
            }
        }

        private OculusWebsiteJson GetJsonData(IDocument page)
        {
            var jsonDataElement = page.QuerySelector("meta[name=json-ld]");
            if (jsonDataElement == null || !jsonDataElement.HasAttribute("content"))
            {
                logger.Error($"json data not present on page");
                return null;
            }
            var jsonStr = jsonDataElement.GetAttribute("content");
            jsonStr = jsonStr.Replace("&quot;", "\"").Replace("&amp;", "&");

            var data = serialiser.Deserialize<OculusWebsiteJson>(jsonStr);

            return data;
        }

        private OculusGameData ParseDetails(IDocument page, OculusGameData addTo = null)
        {
            var data = addTo ?? new OculusGameData();
            var dataRows = page.QuerySelectorAll("div.app-details-row");

            if (!dataRows.Any())
            {
                logger.Error("Details section not present on page");
                return data;
            }

            foreach (var row in dataRows)
            {
                var key = row.QuerySelector("div.app-details-row__left")?.TextContent;
                var value = row.QuerySelector("div.app-details-row__right")?.TextContent;
                switch (key)
                {
                    case "Game Modes":
                        data.GameModes = Split(value);
                        break;
                    case "Supported Player Modes":
                        data.PlayerModes = Split(value);
                        break;
                    case "Supported Tracking Modes":
                        data.TrackingModes = Split(value);
                        break;
                    case "Supported Controllers":
                        data.SupportedControllers = Split(value);
                        break;
                    case "Supported Platforms":
                        data.SupportedPlatforms = Split(value);
                        break;
                    case "Genres":
                        data.Genres = Split(value);
                        break;
                    case "Languages":
                        data.Languages = Split(value);
                        break;
                    case "Version":
                    case "Version + Release Notes":
                        data.Version = value;
                        break;
                    case "Developer":
                        data.Developers = SplitCompanies(value);
                        break;
                    case "Publisher":
                        data.Publishers = SplitCompanies(value);
                        break;
                    case "Website":
                        data.Website = value;
                        break;
                    case "Release Date":
                        if(DateTime.TryParse(value, out DateTime date))
                        {
                            data.ReleaseDate = date;
                        }
                        break;
                    case "Space Required":
                        data.SpaceRequired = value;
                        break;
                    default:
                        break;
                }
            }

            var ageRatings = page.QuerySelectorAll("div.app-age-rating-icon__text");
            data.AgeRatings = ageRatings.Select(x => x.TextContent).ToArray();

            return data;
        }

        private static string[] SplitCompanies(string value)
        {
            var splitValues = new List<string>(Split(value));
            splitValues.RemoveAll(c => Regex.IsMatch(c, @"^(llc|ltd|inc)\.?$", RegexOptions.IgnoreCase));
            return splitValues.ToArray();
        }

        private static string[] Split(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return new string[0];

            return value.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
        }
    }

    public class OculusGameData
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string[] GameModes { get; set; } = new string[0];
        public string[] PlayerModes { get; set; } = new string[0];
        public string[] TrackingModes { get; set; } = new string[0];
        public string[] SupportedControllers { get; set; } = new string[0];
        public string[] SupportedPlatforms { get; set; } = new string[0];
        public string[] Genres { get; set; } = new string[0];
        public string[] Languages { get; set; } = new string[0];
        public string Version { get; set; }
        public string[] Developers { get; set; }
        public string[] Publishers { get; set; }
        public string Website { get; set; }
        public DateTime? ReleaseDate { get; set; }
        public string SpaceRequired { get; set; }
        public string BackgroundImageUrl { get; set; }
        /// <summary>
        /// Average rating (out of 5)
        /// </summary>
        public decimal? AverageRating { get; set; }
        public string StoreUrl { get; set; }
        public string AppId { get; set; }
        public string[] AgeRatings { get; set; } = new string[0];
    }
}
