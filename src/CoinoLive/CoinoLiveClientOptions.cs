using System;
using System.Net.Http;

namespace CoinoLive
{
    /// <summary>Settings for <see cref="CoinoLiveClient"/>. Every setting has a working default.</summary>
    public sealed class CoinoLiveClientOptions
    {
        /// <summary><c>https://coino.live/api/v2</c> unless you were given another.</summary>
        public string BaseUrl { get; set; } = CoinoLiveClient.DefaultBaseUrl;

        /// <summary>Time per request, including reading the answer (30 seconds).</summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>Retries for network errors, 429 and 5xx, only for requests that are safe to repeat (2).</summary>
        public int MaxRetries { get; set; } = 2;

        /// <summary>
        /// Your own <see cref="System.Net.Http.HttpClient"/>, for example from <c>IHttpClientFactory</c>. The client
        /// does not dispose it. Its handler should not follow redirects. Leave it null to let the client make one.
        /// </summary>
        public HttpClient? HttpClient { get; set; }
    }
}
