using Newtonsoft.Json;
using OculusLibrary.DataExtraction.Models;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace OculusLibrary.DataExtraction;

public class GraphQLClient(IWebViewFactory webViewFactory) : IGraphQLClient
{
    private class WebResponse
    {
        public string Url { get; set; }
        public Cookie[] Cookies { get; set; }
        public string Content { get; set; }
    }

    private WebClient WebClient { get; } = new();
    private readonly ILogger _logger = LogManager.GetLogger();

    private string GetLibraryString(string accessToken, string docId)
    {
        var body = new Dictionary<string, string>
        {
            { "access_token", accessToken },
            { "doc_id", docId },
        };
        return PostWithWebclient("https://graph.oculus.com/graphql?locale=en_US", body, []);
    }

    private List<OculusLibraryResponseItem> GetLibraryJson(string accessToken, string docId, Func<OculusLibraryResponseUser, OculusLibraryResponseEntitlements> entitlementsSelector)
    {
        var responseString = GetLibraryString(accessToken, docId);
        var responseObj = JsonConvert.DeserializeObject<OculusLibraryResponseModel>(responseString);
        var entitlements = entitlementsSelector(responseObj.Data.Viewer.User);
        var items = entitlements.Edges.Select(e => e.Node.Item).ToList();
        return items;
    }

    public OculusLibraryGames GetGames(OculusLibrarySettings settings, CancellationToken cancellationToken = default)
    {
        var output = new OculusLibraryGames();

        if (!settings.ImportAnyOnline || cancellationToken.IsCancellationRequested)
            return output;

        var accessToken = GetAccessToken(cancellationToken);
        if (accessToken == null)
            throw new NotAuthenticatedException();

        if (settings.ImportRiftOnline && !cancellationToken.IsCancellationRequested)
            output.RiftGames.AddRange(GetLibraryJson(accessToken, "9431935310238631", u => u.ActivePcEntitlements));

        if (settings.ImportQuestOnline && !cancellationToken.IsCancellationRequested)
            output.QuestGames.AddRange(GetLibraryJson(accessToken, "29383114651302983", u => u.ActiveAndroidEntitlements));

        if (settings.ImportGearGoOnline && !cancellationToken.IsCancellationRequested)
            output.GearGames.AddRange(GetLibraryJson(accessToken, "29143116735333849", u => u.ActiveAndroidEntitlements));

        return output;
    }

    public async Task<OculusMetadataRaw> GetMetadataAsync(string appId, CancellationToken cancellationToken = default)
    {
        TaskCompletionSource<string> metadataXhr = new();
        var s = new WebViewSettings
        {
            JavaScriptEnabled = true,
            PassResourceContentStreamToCallback = true,
            ResourceLoadedCallback = call =>
            {
                try
                {
                    if (call.Request.Method != "POST"
                        || call.Response.StatusCode != 200
                        || !call.Request.Url.StartsWith("https://www.meta.com/ocapi/graphql")
                        || !call.Request.Headers.TryGetValue("X-FB-Friendly-Name", out string friendlyName)
                        || friendlyName != "MDCAppStoreAppPDPBelowFoldRootQuery")
                    {
                        return;
                    }

                    if (!call.ResponseContent.CanSeek || !call.ResponseContent.CanRead)
                    {
                        _logger.Error($"Can't read/seek response content for {call.Request.Url}, friendly name: {friendlyName}");
                        return;
                    }

                    call.ResponseContent.Seek(0, SeekOrigin.Begin);
                    using var streamReader = new StreamReader(call.ResponseContent, Encoding.UTF8);
                    string responseContent = streamReader.ReadToEnd();

                    if (!string.IsNullOrWhiteSpace(responseContent))
                        metadataXhr.SetResult(responseContent);
                }
                catch (Exception e)
                {
                    _logger.Error(e, $"Error getting metadata for app {appId}");
                }
            }
        };
        using var webView = webViewFactory.CreateOffscreenView(s);
        webView.Navigate(GetStoreUrl(appId));

        var completedTask = await Task.WhenAny(metadataXhr.Task, Task.Delay(TimeSpan.FromSeconds(10), cancellationToken));
        if (completedTask != metadataXhr.Task)
            return null;

        string pageSource = await webView.GetPageSourceAsync();

        return new()
        {
            PageSource = pageSource,
            XhrResponse = metadataXhr.Task.Result,
        };
    }

    public string GetAccessToken(CancellationToken cancellationToken = default)
    {
        var response = GetBrowserResponse("https://secure.oculus.com/my/profile/", cancellationToken);
        var accessTokenCookie = response.Cookies.SingleOrDefault(c => c.Domain == ".oculus.com" && c.Name == "oc_ac_at" && !c.Expired);
        return accessTokenCookie?.Value;
    }

    private static string GetStoreUrl(string appId) => $"https://www.meta.com/experiences/-/{appId}/";

    private WebResponse GetBrowserResponse(string storePageUrl, CancellationToken cancellationToken)
    {
        _logger.Info($"GetBrowserResponse: {storePageUrl}");

        using var webView = webViewFactory.CreateOffscreenView(new() { JavaScriptEnabled = true });
        webView.NavigateAndWait(storePageUrl);

        var response = new WebResponse();

        response.Content = Timeout(webView.GetPageSourceAsync(), cancellationToken, timeoutSeconds: 30).GetAwaiter().GetResult();

        response.Cookies = webView.GetCookies()
                                  .Where(c => c.Value != null)
                                  .Select(c => new Cookie(c.Name, c.Value, c.Path, c.Domain))
                                  .ToArray();

        _logger.Info($"GetBrowserResponse complete with {response.Cookies.Length} cookies: {storePageUrl}");

        return response;
    }

    private string PostWithWebclient(string address, IDictionary<string, string> data, Cookie[] cookies)
    {
        _logger.Info($"""
                      PostWithWebclient: {address} with {cookies.Length} cookies
                      data: {DictionaryToString(data)}
                      """);

        var nameValueCollection = new NameValueCollection();
        foreach (var kvp in data)
            nameValueCollection.Add(kvp.Key, kvp.Value);

        WebClient.Headers.Clear();
        WebClient.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";

        var bytes = WebClient.UploadValues(address, "POST", nameValueCollection);
        WebClient.Headers.Clear();

        _logger.Info($"PostWithWebclient complete: {address}");

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

    private static string DictionaryToString(IDictionary<string, string> dictionary)
    {
        var stringBuilder = new StringBuilder();
        foreach (var kvp in dictionary)
        {
            stringBuilder.AppendLine($"{kvp.Key}: {kvp.Value}");
        }

        return stringBuilder.ToString();
    }

    private static async Task<T> Timeout<T>(Task<T> task, CancellationToken cancellationToken, uint? timeoutSeconds = null)
    {
        uint elapsed = 0;
        while (!cancellationToken.IsCancellationRequested
               && (!timeoutSeconds.HasValue || elapsed < timeoutSeconds))
        {
            if (task.IsCompleted)
                return task.Result;

            await Task.Delay(1000, cancellationToken);
            elapsed++;
        }

        return default;
    }

    ~GraphQLClient() => Dispose();
}
