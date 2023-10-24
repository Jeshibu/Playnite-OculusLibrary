using System;
using System.Collections.Specialized;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace OculusLibrary.DataExtraction
{
    public interface IWebClient: IDisposable
    {
        Task<string> UploadValuesAsync(string address, string method, NameValueCollection data);
    }

    public class WebClientWrapper : IWebClient
    {
        public WebClientWrapper()
        {
            this.WebClient = new WebClient();
        }

        private WebClient WebClient { get; }

        public void Dispose()
        {
            WebClient.Dispose();
        }

        public async Task<string> UploadValuesAsync(string address, string method, NameValueCollection data)
        {
            WebClient.Headers[HttpRequestHeader.UserAgent] = "PostmanRuntime/7.33.0"; //why does this work and a regular browser user agent string doesn't
            WebClient.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
            WebClient.Headers[HttpRequestHeader.Cookie] = "locale=en_GB";
            var bytes = await WebClient.UploadValuesTaskAsync(address, method, data);
            WebClient.Headers.Clear();
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
