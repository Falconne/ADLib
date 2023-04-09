using ADLib.Exceptions;
using ADLib.Logging;
using ADLib.Util;
using HtmlAgilityPack;
using System.Net;
using System.Text.RegularExpressions;

namespace ADLib.Net
{
    public class ThrottledWebClient
    {
        public int MinDelayMilliseconds;

        private DateTime _lastCallTime = DateTime.MinValue;

        private readonly CookieContainer _cookies = new();

        private readonly HttpClient _client;

        private static readonly Regex _urlChecker = new(@"^https*://[^\s""']+$", RegexOptions.Singleline);

        public ThrottledWebClient(int defaultDelayMilliseconds = 50)
        {
            HttpClientHandler handler = new() { CookieContainer = _cookies };
            _client = new HttpClient(handler);
            MinDelayMilliseconds = defaultDelayMilliseconds;
        }

        public async Task<HtmlDocument> GetPageDocOrFailAsync(string url)
        {
            return await GetPageDocOrFailAsync(url, CancellationToken.None);
        }

        public async Task<HtmlDocument> GetPageDocOrFailAsync(string url, CancellationToken cancellationToken)
        {
            FailIfBadUrl(url);

            var doc = new HtmlDocument();
            doc.LoadHtml(await GetPageContentOrFailAsync(url, cancellationToken));

            return doc;
        }

        public async Task<string?> GetPageContentOrFailAsync(string url)
        {
            return await GetPageContentOrFailAsync(url, CancellationToken.None);
        }

        public async Task<string?> GetPageContentOrFailAsync(string url, CancellationToken cancellationToken)
        {
            FailIfBadUrl(url);

            await DoThrottle(cancellationToken);
            string? result = null;
            GenLog.Debug($"GETting {url}");
            await Retry.OnExceptionAsync(
                async () => { result = await _client.GetStringAsync(url, cancellationToken); },
                null,
                cancellationToken);

            return result;
        }

        public async Task<HttpResponseMessage> PostAndFailIfNotOk(string url, Dictionary<string, string> parameters)
        {
            return await PostAndFailIfNotOk(url, parameters, CancellationToken.None);
        }

        public async Task<HttpResponseMessage> PostAndFailIfNotOk(
            string url,
            Dictionary<string, string> parameters,
            CancellationToken cancellationToken)
        {
            FailIfBadUrl(url);

            var encodedContent = new FormUrlEncodedContent(parameters);

            await DoThrottle(cancellationToken);
            GenLog.Debug($"POSTing {url}");
            var response = await _client.PostAsync(url, encodedContent, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.OK)
                return response;

            GenLog.Error(await response.Content.ReadAsStringAsync(cancellationToken));
            throw new Exception($"Bad status code from POST: {response.StatusCode}");
        }

        public async Task DownloadFileAsync(string url, string path)
        {
            await DownloadFileAsync(url, path, CancellationToken.None);
        }

        public async Task DownloadFileAsync(string url, string path, CancellationToken cancellationToken)
        {
            var dir = Path.GetDirectoryName(path);
            if (dir.IsNotEmpty() && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            await DoThrottle(cancellationToken);
            byte[]? bytes = null;
            await Retry.OnExceptionAsync(
                async () => { bytes = await _client.GetByteArrayAsync(url, cancellationToken); },
                $"Downloading {url} to {path}",
                cancellationToken);

            if (bytes == null)
                throw new Exception($"Download returned empty result: {url}");
            await File.WriteAllBytesAsync(path, bytes, cancellationToken);
        }

        public async Task<bool> TryDownloadFileAsync(string url, string path, CancellationToken cancellationToken)
        {
            FailIfBadUrl(url);

            await DoThrottle(cancellationToken);
            GenLog.Info($"Attempt download {url} to {path}");
            try
            {
                var bytes = await _client.GetByteArrayAsync(url, cancellationToken);
                await File.WriteAllBytesAsync(path, bytes, cancellationToken);
                GenLog.Info("Success");
                return true;
            }
            catch (Exception e)
            {
                GenLog.Info($"Failed: {e.Message}");
                return false;
            }
        }

        private static void FailIfBadUrl(string url)
        {

            if (!IsValidUrl(url))
                throw new InvalidAssumptionException($"Invalid URL: {url}");
        }

        public static bool IsValidUrl(string url)
        {
            return url.IsNotEmpty() && _urlChecker.IsMatch(url);
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