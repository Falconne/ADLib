using ADLib.Util;
using System;
using System.IO;
using System.Net;
using System.Net.Http;

namespace ADLib.Net
{
    public static class Client
    {
        public static string GetString(string fromUrl)
        {
            using (var client = new HttpClient())
            using (var response = client.GetAsync(fromUrl).Result)
            using (var content = response.Content)
            {
                return content.ReadAsStringAsync().Result;
            }
        }

        public static string DownloadFile(string url, string destination)
        {
            var destinationDirectory = Path.GetDirectoryName(destination);
            FileSystem.CreateDirectory(destinationDirectory);

            Retry.OnException(
                () => { DownloadFileOrFail(url, destination); },

                $"Downloading {url} to {destination}");

            return destination;
        }

        private static void DownloadFileOrFail(string url, string destination)
        {
            using (var client = new WebClient())
            {
                client.DownloadFile(url, destination);
            }

            if (!File.Exists(destination))
            {
                throw new Exception($"Could not download {url}");
            }
        }
    }
}
