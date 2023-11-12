using System;
using System.Collections.Specialized;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace OculusLibrary.DataExtraction
{
    public interface IWebClient: IDisposable
    {
        WebHeaderCollection Headers { get; }
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

        public WebHeaderCollection Headers => this.WebClient.Headers;

        public async Task<string> UploadValuesAsync(string address, string method, NameValueCollection data)
        {
            var bytes = await WebClient.UploadValuesTaskAsync(address, method, data);
            WebClient.Headers.Clear();
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
