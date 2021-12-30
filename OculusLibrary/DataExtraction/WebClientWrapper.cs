using System;
using System.Collections.Specialized;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace OculusLibrary.DataExtraction
{
    public interface IWebClient: IDisposable
    {
        string DownloadString(string address);
        Task<string> DownloadStringAsync(string address);
        string UploadValues(string address, string method, NameValueCollection data);
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

        public string DownloadString(string address)
        {
            return WebClient.DownloadString(address);
        }

        public Task<string> DownloadStringAsync(string address)
        {
            return WebClient.DownloadStringTaskAsync(address);
        }

        public string UploadValues(string address, string method, NameValueCollection data)
        {
            var bytes = WebClient.UploadValues(address, method, data);
            return Encoding.UTF8.GetString(bytes);
        }

        public async Task<string> UploadValuesAsync(string address, string method, NameValueCollection data)
        {
            var bytes = await WebClient.UploadValuesTaskAsync(address, method, data);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
