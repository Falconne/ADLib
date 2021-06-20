using ADLib.Logging;
using HtmlAgilityPack;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ADLib.Net
{
    public class ThrottledWebClient
    {
        private readonly HtmlWeb _webClient = new();

        public int MinDelayMilliseconds = 20;

        private DateTime _lastCallTime = DateTime.MinValue;


        public async Task<HtmlDocument> GetPageAsync(string url, CancellationToken cancellationToken)
        {
            var timeSinceLastCall = (DateTime.Now - _lastCallTime).Milliseconds;
            if (timeSinceLastCall < MinDelayMilliseconds)
            {
                var sleepTime = MinDelayMilliseconds - timeSinceLastCall;
                await Task.Delay(sleepTime, cancellationToken);
            }

            GenLog.Debug($"Fetching {url}");
            _lastCallTime = DateTime.Now;
            return await _webClient.LoadFromWebAsync(url, cancellationToken);
        }
    }
}