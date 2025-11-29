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

    private DateTimeOffset _lastCallTime = DateTimeOffset.MinValue;

    public async Task<HtmlNode> GetPageDocNodeOrFailAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        FailIfBadUrl(url);

        var doc = new HtmlDocument();
        doc.LoadHtml(await GetPageContentOrFailAsync(url, cancellationToken).ConfigureAwait(false));

        return doc.DocumentNode;
    }

    public async Task<string> GetPageContentOrFailAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        FailIfBadUrl(url);

        await DoThrottle(cancellationToken).ConfigureAwait(false);
        HttpResponseMessage? response = null;
        await Retry.OnExceptionAsync(
                async () => { response = await GetAsync(url, cancellationToken).ConfigureAwait(false); },
                null,
                cancellationToken)
            .ConfigureAwait(false);

        if (response == null)
        {
            throw new Exception($"GET returned empty result for {url}");
        }

        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<HttpResponseMessage> PostAndFailIfNotOk(
        string url,
        Dictionary<string, string> parameters,
        CancellationToken cancellationToken = default)
    {
        FailIfBadUrl(url);

        var encodedContent = new FormUrlEncodedContent(parameters);

        GenLog.Debug($"POSTing {url}");
        await DoThrottle(cancellationToken).ConfigureAwait(false);
        var response = await Client.PostAsync(url, encodedContent, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.OK)
        {
            return response;
        }

        GenLog.Error(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        throw new Exception($"Bad status code from POST: {response.StatusCode}");
    }

    public async Task DownloadFileAsync(
        string url,
        string path,
        CancellationToken cancellationToken = default,
        int retries = 3,
        int delay = 3000)
    {
        if (File.Exists(path))
        {
            throw new InvalidOperationException($"File already exists: {path}");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await DoThrottle(cancellationToken).ConfigureAwait(false);
        var tempPath = path + "_temp";
        await FileSystem.DeleteAsync(tempPath).ConfigureAwait(false);
        try
        {
            byte[]? bytes = null;
            await Retry.OnExceptionAsync(
                    async () =>
                    {
                        bytes = await Client.GetByteArrayAsync(url, cancellationToken).ConfigureAwait(false);
                    },
                    $"Downloading {url} to {path}",
                    cancellationToken,
                    retries,
                    delay)
                .ConfigureAwait(false);

            if (bytes == null)
            {
                throw new Exception($"Download returned empty result: {url}");
            }

            await File.WriteAllBytesAsync(tempPath, bytes, cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, path);
        }
        finally
        {
            await FileSystem.DeleteAsync(tempPath).ConfigureAwait(false);
        }
    }

    public async Task<(bool result, Exception? error)> TryDownloadFileAsync(
        string url,
        string path,
        Func<Exception, bool>? shouldAbortEarly = null,
        CancellationToken cancellationToken = default,
        int retries = 3,
        int delay = 3000)
    {
        if (File.Exists(path))
        {
            throw new InvalidOperationException($"File already exists: {path}");
        }

        FailIfBadUrl(url);
        var tempPath = path + "_temp";
        await FileSystem.DeleteAsync(tempPath).ConfigureAwait(false);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await DoThrottle(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                GenLog.Info($"Attempt download {url} to {path}");
                var bytes = await Client.GetByteArrayAsync(url, cancellationToken).ConfigureAwait(false);
                await File.WriteAllBytesAsync(tempPath, bytes, cancellationToken).ConfigureAwait(false);
                File.Move(tempPath, path);
                GenLog.Info("Success");
                return (true, null);
            }
            catch (OperationCanceledException e)
            {
                GenLog.Warning("Cancel download on request");
                return (false, e);
            }
            catch (Exception e)
            {
                GenLog.Info($"Failed: {e.GetType()}: {e.Message}");
                if (shouldAbortEarly?.Invoke(e) == true)
                {
                    GenLog.Info("Aborting early on this type of exception");
                    return (false, e);
                }

                if (retries-- <= 0)
                {
                    GenLog.Error("No more retries left");
                    return (false, e);
                }

                GenLog.Info($"Retries remaining: {retries}");
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                await FileSystem.DeleteAsync(tempPath).ConfigureAwait(false);
            }
        }
    }

    public static bool AbortOnForbiddenHandler(Exception e)
    {
        return e is HttpRequestException hre && hre.Message.Contains("403");
    }

    public async Task<HttpResponseMessage> GetAsync(string url, CancellationToken cancellationToken = default)
    {
        FailIfBadUrl(url);
        await DoThrottle(cancellationToken).ConfigureAwait(false);
        while (true)
        {
            GenLog.Debug($"GETing {url}");
            var response = await Client.GetAsync(url, cancellationToken).ConfigureAwait(false);
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
        await DoThrottle(cancellationToken).ConfigureAwait(false);
        var tempFile = Path.GetTempFileName();
        await DownloadFileAsync(link, tempFile, cancellationToken).ConfigureAwait(false);
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
        var timeSinceLastCall = (DateTimeOffset.UtcNow - _lastCallTime).Milliseconds;
        if (timeSinceLastCall < MinDelayMilliseconds)
        {
            var sleepTime = MinDelayMilliseconds - timeSinceLastCall;
            await Task.Delay(sleepTime, cancellationToken).ConfigureAwait(false);
        }

        _lastCallTime = DateTimeOffset.UtcNow;
    }
}