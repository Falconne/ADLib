﻿using ADLib.Logging;
using ADLib.Util;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ADLib.Net
{
    public class ThrottledWebClient
    {
        public int MinDelayMilliseconds;

        private DateTime _lastCallTime = DateTime.MinValue;

        private readonly CookieContainer _cookies = new();

        private readonly HttpClient _client;


        public ThrottledWebClient(int defaultDelayMilliseconds = 50)
        {
            HttpClientHandler handler = new() { CookieContainer = _cookies };
            _client = new HttpClient(handler);
            MinDelayMilliseconds = defaultDelayMilliseconds;
        }

        public async Task<HtmlDocument> GetPageDocOrFailAsync(string url, CancellationToken cancellationToken)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(await GetPageContentOrFailAsync(url, cancellationToken));

            return doc;
        }

        public async Task<string> GetPageContentOrFailAsync(string url, CancellationToken cancellationToken)
        {
            await DoThrottle(cancellationToken);
            GenLog.Debug($"GETting {url}");
            string result = null;
            await Retry.OnExceptionAsync(
                async () => { result = await _client.GetStringAsync(url); },
                $"GETting {url}",
                cancellationToken);

            return result;
        }

        public async Task<HttpResponseMessage> PostAndFailIfNotOk(string url, Dictionary<string, string> parameters,
            CancellationToken cancellationToken)
        {
            var encodedContent = new FormUrlEncodedContent(parameters);

            await DoThrottle(cancellationToken);
            GenLog.Debug($"POSTing {url}");
            var response = await _client.PostAsync(url, encodedContent, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.OK)
                return response;

            GenLog.Error(await response.Content.ReadAsStringAsync());
            throw new($"Bad status code from POST: {response.StatusCode}");
        }

        public async Task DownloadFileAsync(string url, string path, CancellationToken cancellationToken)
        {
            await DoThrottle(cancellationToken);
            byte[] bytes = null;
            await Retry.OnExceptionAsync(
                async () => { bytes = await _client.GetByteArrayAsync(url); },
                $"Downloading {url} to {path}",
                cancellationToken);

            File.WriteAllBytes(path, bytes);
        }

        public async Task<bool> TryDownloadFileAsync(string url, string path, CancellationToken cancellationToken)
        {
            await DoThrottle(cancellationToken);
            GenLog.Info($"Attempt download {url} to {path}");
            try
            {
                var bytes = await _client.GetByteArrayAsync(url);
                File.WriteAllBytes(path, bytes);
                GenLog.Info($"Success");
                return true;
            }
            catch (Exception e)
            {
                GenLog.Info($"Failed: {e.Message}");
                return false;
            }
        }

        private async Task DoThrottle(CancellationToken cancellationToken)
        {
            var timeSinceLastCall = (DateTime.Now - _lastCallTime).Milliseconds;
            if (timeSinceLastCall < MinDelayMilliseconds)
            {
                var sleepTime = MinDelayMilliseconds - timeSinceLastCall;
                await Task.Delay(sleepTime, cancellationToken);
            }

            _lastCallTime = DateTime.Now;
        }
    }
}