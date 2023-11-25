using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;

namespace OculusLibrary.DataExtraction
{
    public interface IGraphQLClient: IDisposable
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="appId"></param>
        /// <param name="setLocale">Set the locale (an extra request potentially slowing things down) so that things like player modes and dates are en-US instead of IP-localized</param>
        /// <returns></returns>
        string GetMetadata(string appId, bool setLocale);
        string GetLibrary(string accessToken, string docId);
        string GetAccessToken();
    }

    public class GraphQLClient : IGraphQLClient
    {
        private class WebResponse
        {
            public string Url { get; set; }
            public Cookie[] Cookies { get; set; }
            public string Content { get; set; }
        }

        private IPlayniteAPI PlayniteAPI { get; }
        private WebClient WebClient { get; }

        public GraphQLClient(IPlayniteAPI playniteAPI)
        {
            PlayniteAPI = playniteAPI;
            WebClient = new WebClient();
        }

        public string GetLibrary(string accessToken, string docId)
        {
            var body = new Dictionary<string, string>
            {
                { "access_token", accessToken },
                { "variables", "{}" },
                { "doc_id", docId },
            };
            return PostWithWebclient("https://graph.oculus.com/graphql?locale=en_US", body, new Cookie[0]);
        }

        public string GetMetadata(string appId, bool setLocale)
        {
            var response = GetBrowserResponse(GetStoreUrl(appId));

            if (setLocale)
                SetLocale(response.Cookies);

            var body = new Dictionary<string, string>
            {
                { "variables", $@"{{""itemId"":""{appId}"",""hmdType"":""RIFT"",""requestPDPAssetsAsPNG"":false}}" },
                { "doc_id", "7101363079925397" },
            };

            return PostWithWebclient("https://www.meta.com/ocapi/graphql?forced_locale=en_US", body, response.Cookies);
        }

        public string GetAccessToken()
        {
            var response = GetBrowserResponse("https://secure.oculus.com/my/profile/");
            var accessTokenCookie = response.Cookies.SingleOrDefault(c => c.Domain == ".oculus.com" && c.Name == "oc_ac_at");
            return accessTokenCookie?.Value;
        }

        private static string GetStoreUrl(string appId) => $"https://www.meta.com/en-us/experiences/{appId}/";

        private string SetLocale(Cookie[] cookies)
        {
            var data = new Dictionary<string, string>()
            {
                { "variables", @"{""input"":{""non_ecomm_locale"":""en_US"",""site_type"":""DOLLY"",""actor_id"":""0"",""client_mutation_id"":""2""}}" },
                { "doc_id", "5141701172554610" },
            };
            return PostWithWebclient("https://www.meta.com/api/graphql/", data, cookies);
        }

        private WebResponse GetBrowserResponse(string storePageUrl)
        {
            var webview = PlayniteAPI.WebViews.CreateOffscreenView(new WebViewSettings { JavaScriptEnabled = true });

            webview.NavigateAndWait(storePageUrl);

            var response = new WebResponse { Content = webview.GetPageSource() };

            response.Cookies = webview.GetCookies()
                .Select(c => new Cookie(c.Name, c.Value, c.Path, c.Domain))
                .ToArray();

            return response;
        }

        private string PostWithWebclient(string address, IDictionary<string, string> data, Cookie[] cookies)
        {
            var nameValueCollection = new NameValueCollection();
            foreach (var kvp in data)
                nameValueCollection.Add(kvp.Key, kvp.Value);

            WebClient.Headers.Clear();
            WebClient.Headers[HttpRequestHeader.Cookie] = GetCookieHeader(address, cookies);
            WebClient.Headers[HttpRequestHeader.UserAgent] = "PostmanRuntime/7.35.0"; //why does this work and a regular browser user agent string doesn't
            WebClient.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";

            var bytes = WebClient.UploadValues(address, "POST", nameValueCollection);
            WebClient.Headers.Clear();
            return Encoding.UTF8.GetString(bytes);
        }

        private string GetCookieHeader(string url, Cookie[] globalCookies)
        {
            var host = "." + new Uri(url).GetComponents(UriComponents.Host, UriFormat.SafeUnescaped);

            var cookies = globalCookies.Where(c => host.Contains(c.Domain)).ToArray();
            var cookieStrings = cookies.Select(c => $"{HttpUtility.UrlEncode(c.Name)}={HttpUtility.UrlEncode(c.Value)}");
            return string.Join("&", cookieStrings);
        }

        public void Dispose()
        {
            WebClient.Dispose();
        }
    }
}
