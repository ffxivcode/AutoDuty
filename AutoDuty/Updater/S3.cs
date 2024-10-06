using System.Threading.Tasks;
using ECommons.DalamudServices;
using System.IO;
using System.Net.Http;

namespace AutoDuty.Updater
{
    public static class S3
    {
        private static readonly HttpClient httpClient = new();

        public static void Dispose() => httpClient.Dispose();

        public static async Task<string> ListObjectsAsync(string requestUri)
        {
            try
            {
                HttpResponseMessage response = await httpClient.GetAsync(requestUri);

                response.EnsureSuccessStatusCode();
                var responseBody = await response.Content.ReadAsStringAsync();

                return responseBody;
            }
            catch (HttpRequestException ex)
            {
                Svc.Log.Error($"Request error: {ex.Message}");
                return string.Empty;
            }
        }

        public static async Task<bool> DownloadFileAsync(string requestUri, string filePath)
        {
            try
            {
                HttpResponseMessage response = await httpClient.GetAsync(requestUri);

                response.EnsureSuccessStatusCode();
                using Stream responseStream = await response.Content.ReadAsStreamAsync();
                using FileStream fileStream = new(filePath, FileMode.Create, FileAccess.Write);
                await responseStream.CopyToAsync(fileStream);
                return true;
            }
            catch (HttpRequestException e)
            {
                Svc.Log.Error($"Request error: {e.Message}");
                return false;
            }
        }
    }
}
