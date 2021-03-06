﻿using ADLib.Util;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

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

        public static async Task<string> DownloadFileAsync(string url, string destination,
            CancellationToken cancellationToken)
        {
            var destinationDirectory = Path.GetDirectoryName(destination);
            FileSystem.CreateDirectory(destinationDirectory);

            await Retry.OnExceptionAsync(
                async () => { await DownloadFileOrFailAsync(url, destination, cancellationToken); },

                $"Downloading {url} to {destination}",
                cancellationToken);

            return destination;
        }

        public static void DownloadFileOrFail(string url, string destination)
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

        public static async Task DownloadFileOrFailAsync(
            string url, string destination, CancellationToken cancellationToken)
        {
            await Task.Run(() =>
                DownloadFileOrFail(url, destination), cancellationToken);
        }
    }
}
