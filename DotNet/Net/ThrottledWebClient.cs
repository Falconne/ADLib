using ADLib.Exceptions;
using ADLib.Logging;
using ADLib.Util;
using HtmlAgilityPack;
using System.Net;
using System.Text.RegularExpressions;

namespace ADLib.Net;

public class ThrottledWebClient
{
    private static readonly Regex _urlChecker = new(@"^https*://[^\s""']+$", RegexOptions.Singleline);

    public ThrottledWebClient(int defaultDelayMilliseconds = 50, bool followRedirects = true)
    {
        HttpClientHandler handler = new() { CookieContainer = _cookies };
        if (!followRedirects)
        {
            handler.AllowAutoRedirect = false;
        }

        Client = new HttpClient(handler);
        MinDelayMilliseconds = defaultDelayMilliseconds;
        _followRedirects = followRedirects;
        Client.DefaultRequestHeaders.UserAgent.ParseAdd(
            @"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.36");
    }

    public readonly HttpClient Client;

    // TODO: Make readonly
    public int MinDelayMilliseconds;

    private readonly CookieContainer _cookies = new();

    private readonly bool _followRedirects;

    private DateTime _lastCallTime = DateTime.MinValue;

    public async Task<HtmlDocument> GetPageDocOrFailAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        FailIfBadUrl(url);

        var doc = new HtmlDocument();
        doc.LoadHtml(await GetPageContentOrFailAsync(url, cancellationToken));

        return doc;
    }

    public async Task<string> GetPageContentOrFailAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        FailIfBadUrl(url);

        await DoThrottle(cancellationToken);
        HttpResponseMessage? response = null;
        await Retry.OnExceptionAsync(
            async () => { response = await GetAsync(url, cancellationToken); },
            null,
            cancellationToken);

        if (response == null)
        {
            throw new Exception($"GET returned empty result for {url}");
        }

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    public async Task<HttpResponseMessage> PostAndFailIfNotOk(
        string url,
        Dictionary<string, string> parameters,
        CancellationToken cancellationToken = default)
    {
        FailIfBadUrl(url);

        var encodedContent = new FormUrlEncodedContent(parameters);

        GenLog.Debug($"POSTing {url}");
        await DoThrottle(cancellationToken);
        var response = await Client.PostAsync(url, encodedContent, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.OK)
        {
            return response;
        }

        GenLog.Error(await response.Content.ReadAsStringAsync(cancellationToken));
        throw new Exception($"Bad status code from POST: {response.StatusCode}");
    }

    public async Task DownloadFileAsync(
        string url,
        string path,
        CancellationToken cancellationToken = default,
        int retries = 3)
    {
        var dir = Path.GetDirectoryName(path);
        if (dir.IsNotEmpty() && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await DoThrottle(cancellationToken);
        try
        {
            byte[]? bytes = null;
            await Retry.OnExceptionAsync(
                async () => { bytes = await Client.GetByteArrayAsync(url, cancellationToken); },
                $"Downloading {url} to {path}",
                cancellationToken,
                retries);

            if (bytes == null)
            {
                throw new Exception($"Download returned empty result: {url}");
            }

            await File.WriteAllBytesAsync(path, bytes, cancellationToken);
        }
        catch (Exception)
        {
            File.Delete(path);
            throw;
        }
    }

    public async Task<bool> TryDownloadFileAsync(
        string url,
        string path,
        int numRetries = 3,
        int delay = 3000,
        Func<Exception, bool>? shouldAbortEarly = null,
        CancellationToken cancellationToken = default)
    {
        FailIfBadUrl(url);
        if (numRetries < 0)
        {
            numRetries = 0;
        }

        await DoThrottle(cancellationToken);
        while (numRetries-- > 0 && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                GenLog.Info($"Attempt download {url} to {path}");
                var bytes = await Client.GetByteArrayAsync(url, cancellationToken);
                await File.WriteAllBytesAsync(path, bytes, cancellationToken);
                GenLog.Info("Success");
                return true;
            }
            catch (Exception e)
            {
                GenLog.Info($"Failed: {e.GetType()}: {e.Message}");
                File.Delete(path);
                if (shouldAbortEarly?.Invoke(e) == true)
                {
                    GenLog.Info("Aborting early on this type of exception");
                    return false;
                }

                if (numRetries == 0)
                {
                    GenLog.Error("No more retries left");
                    return false;
                }

                GenLog.Info($"Retries remaining: {numRetries}");
                await Task.Delay(delay, cancellationToken);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return false;
    }

    public bool AbortOnForbiddenHandler(Exception e)
    {
        return e is HttpRequestException hre && hre.Message.Contains("403");
    }

    public async Task<HttpResponseMessage> GetAsync(string url, CancellationToken cancellationToken = default)
    {
        FailIfBadUrl(url);
        await DoThrottle(cancellationToken);
        while (true)
        {
            GenLog.Debug($"GETing {url}");
            var response = await Client.GetAsync(url, cancellationToken);
            if (response.StatusCode != HttpStatusCode.Found || !_followRedirects)
            {
                return response;
            }

            var redirectUrl = response.Headers.Location?.ToString();
            if (redirectUrl.IsEmpty())
            {
                throw new RemoteAccessException($"No redirect URL found for {url}");
            }

            GenLog.Info($"Following redirect to {redirectUrl}");
            url = redirectUrl;
        }
    }

    public static bool IsValidUrl(string url)
    {
        return url.IsNotEmpty() && _urlChecker.IsMatch(url);
    }

    public async Task<string> DownloadToTempFile(string link, CancellationToken cancellationToken = default)
    {
        await DoThrottle(cancellationToken);
        var tempFile = Path.GetTempFileName();
        await DownloadFileAsync(link, tempFile, cancellationToken);
        return tempFile;
    }

    private static void FailIfBadUrl(string url)
    {
        if (!IsValidUrl(url))
        {
            throw new InvalidAssumptionException($"Invalid URL: {url}");
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