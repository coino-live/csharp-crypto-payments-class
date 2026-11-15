using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CoinoLive
{
    /// <summary>
    /// Coino Live API v2 client.
    /// <code>
    /// var coino = new CoinoLiveClient("ck_live_…");
    /// var payment = await coino.Payments.CreateAsync(new CreatePaymentParams { Amount = "25.00", Currency = "EUR", OrderId = "1001" });
    /// return Redirect(payment.CheckoutUrl);
    /// </code>
    /// One client can be shared by the whole application; it is safe to use from several threads.
    /// </summary>
    public sealed class CoinoLiveClient : IDisposable
    {
        /// <summary>The library version.</summary>
        public const string Version = "1.0.0";

        /// <summary>The API every request goes to unless <see cref="CoinoLiveClientOptions.BaseUrl"/> says otherwise.</summary>
        public const string DefaultBaseUrl = "https://coino.live/api/v2";

        private static readonly Regex ApiKeyPattern =
            new Regex(@"^ck_(live|test)_[A-Za-z0-9]+\z", RegexOptions.CultureInvariant);

        private static readonly Regex LocalHttpPattern =
            new Regex(@"^http://(localhost|127\.0\.0\.1)(:[0-9]+)?(/|\z)", RegexOptions.CultureInvariant);

        private static readonly string UserAgent = "coinolive-dotnet/" + Version + " .NET/" + Environment.Version;

        private readonly string _apiKey;
        private readonly string _baseUrl;
        private readonly TimeSpan _timeout;
        private readonly int _maxRetries;
        private readonly HttpClient _http;
        private readonly bool _ownsHttp;

        /// <summary>Creates a client.</summary>
        /// <param name="apiKey">ck_live_… (Dashboard → Developers)</param>
        /// <param name="options">Base URL, timeout, retries and an optional HttpClient.</param>
        /// <exception cref="ArgumentException">The key or the base URL is not usable.</exception>
        public CoinoLiveClient(string apiKey, CoinoLiveClientOptions? options = null)
        {
            if (apiKey == null || !ApiKeyPattern.IsMatch(apiKey))
            {
                throw new ArgumentException("An API key looks like ck_live_… (create one in your dashboard under Developers).", nameof(apiKey));
            }
            options ??= new CoinoLiveClientOptions();

            _apiKey = apiKey;
            _baseUrl = (string.IsNullOrEmpty(options.BaseUrl) ? DefaultBaseUrl : options.BaseUrl).TrimEnd('/');
            if (!_baseUrl.StartsWith("https://", StringComparison.Ordinal) && !LocalHttpPattern.IsMatch(_baseUrl))
            {
                throw new ArgumentException("BaseUrl must start with https://.", nameof(options));
            }
            if (!Uri.TryCreate(_baseUrl, UriKind.Absolute, out _))
            {
                throw new ArgumentException("BaseUrl is not a valid URL.", nameof(options));
            }
            if (options.Timeout <= TimeSpan.Zero && options.Timeout != System.Threading.Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentOutOfRangeException(nameof(options), "Timeout must be more than zero.");
            }
            _timeout = options.Timeout;
            _maxRetries = Math.Max(0, options.MaxRetries);

            if (options.HttpClient != null)
            {
                _http = options.HttpClient;
                _ownsHttp = false;
            }
            else
            {
                var handler = new SocketsHttpHandler
                {
                    AllowAutoRedirect = false,
                    ConnectTimeout = TimeSpan.FromSeconds(10),
                    PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                };
                // The per-request timeout below applies; this one must not cut in first.
                _http = new HttpClient(handler) { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
                _ownsHttp = true;
            }

            Payments = new Payments(this);
            Custody = new Custody(this);
            Withdrawals = new Withdrawals(this);
        }

        /// <summary>Payments: create one per order, send the customer to its checkout_url, read it back before shipping.</summary>
        public Payments Payments { get; }

        /// <summary>Custody users: lifetime deposit addresses and balances for the users of your app.</summary>
        public Custody Custody { get; }

        /// <summary>API withdrawals. Turn them on in the gateway settings first.</summary>
        public Withdrawals Withdrawals { get; }

        /// <summary>The pause between attempts. Tests replace it so they do not wait.</summary>
        internal Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = Task.Delay;

        /// <summary>Currencies a payment can be priced in: <c>[{ code: "EUR", usd_per_unit: "1.09" }, …]</c>.</summary>
        public async Task<IReadOnlyList<Currency>> CurrenciesAsync(CancellationToken cancellationToken = default)
        {
            return await DataAsync<IReadOnlyList<Currency>>(HttpMethod.Get, "/currencies", cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Coins customers can pay with right now.</summary>
        public async Task<IReadOnlyList<Asset>> AssetsAsync(CancellationToken cancellationToken = default)
        {
            return await DataAsync<IReadOnlyList<Asset>>(HttpMethod.Get, "/assets", cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Your account balance per coin, after fees.</summary>
        public async Task<IReadOnlyList<Balance>> BalancesAsync(CancellationToken cancellationToken = default)
        {
            return await DataAsync<IReadOnlyList<Balance>>(HttpMethod.Get, "/balances", cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Disposes the HttpClient the client made itself (not one passed in the options).</summary>
        public void Dispose()
        {
            if (_ownsHttp)
            {
                _http.Dispose();
            }
        }

        /// <summary>The <c>data</c> of the answer.</summary>
        internal async Task<T> DataAsync<T>(
            HttpMethod method,
            string path,
            CancellationToken cancellationToken,
            object? body = null,
            string? idempotencyKey = null,
            bool repeatable = false)
        {
            var envelope = await RequestAsync<DataEnvelope<T>>(method, path, body, idempotencyKey, repeatable, cancellationToken).ConfigureAwait(false);
            if (envelope.Data == null)
            {
                throw new CoinoLiveException("Coino Live answered without data.");
            }
            return envelope.Data;
        }

        /// <summary>Sends the request, retrying it when that is safe, and returns the decoded JSON answer.</summary>
        internal async Task<T> RequestAsync<T>(
            HttpMethod method,
            string path,
            object? body,
            string? idempotencyKey,
            bool repeatable,
            CancellationToken cancellationToken)
        {
            // Retrying a POST is only safe when repeating it returns the same object (an idempotency key, a custody
            // external_id, a withdrawal reference).
            bool retriable = method == HttpMethod.Get || !string.IsNullOrEmpty(idempotencyKey) || repeatable;
            byte[]? json = body == null ? null : JsonSerializer.SerializeToUtf8Bytes(body, body.GetType(), CoinoLiveJson.Options);

            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    return await SendAsync<T>(method, path, json, idempotencyKey, cancellationToken).ConfigureAwait(false);
                }
                catch (CoinoLiveException e) when (retriable && IsTemporary(e) && attempt < _maxRetries)
                {
                    await Delay(Backoff(attempt), cancellationToken).ConfigureAwait(false);
                }
            }
        }

        /// <summary>0.5 s, 1 s, 2 s, … at most 8 s.</summary>
        internal static TimeSpan Backoff(int attempt)
        {
            return TimeSpan.FromMilliseconds(Math.Min(8000, 500 * Math.Pow(2, attempt)));
        }

        /// <summary>A random idempotency key: 32 hex characters.</summary>
        internal static string NewIdempotencyKey()
        {
            return Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        }

        private static bool IsTemporary(CoinoLiveException e)
        {
            return e is ConnectionException || e is RateLimitException || e.Status >= 500;
        }

        private async Task<T> SendAsync<T>(HttpMethod method, string path, byte[]? json, string? idempotencyKey, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(method, _baseUrl + path);
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + _apiKey);
            request.Headers.TryAddWithoutValidation("Accept", "application/json");
            request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
            if (!string.IsNullOrEmpty(idempotencyKey))
            {
                request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
            }
            if (json != null)
            {
                request.Content = new ByteArrayContent(json);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (_timeout != System.Threading.Timeout.InfiniteTimeSpan)
            {
                timeout.CancelAfter(_timeout);
            }

            int status;
            string text;
            try
            {
                using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
                status = (int)response.StatusCode;
                text = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException e)
            {
                throw new ConnectionException("Could not reach Coino Live: the request timed out.", e);
            }
            catch (Exception e) when (e is HttpRequestException || e is IOException)
            {
                throw new ConnectionException("Could not reach Coino Live: " + e.Message, e);
            }

            JsonDocument? document = null;
            try
            {
                document = JsonDocument.Parse(text);
            }
            catch (JsonException)
            {
                document = null;
            }

            using (document)
            {
                bool isObject = document != null && document.RootElement.ValueKind == JsonValueKind.Object;

                if (status >= 200 && status < 300 && isObject)
                {
                    try
                    {
                        return document!.RootElement.Deserialize<T>(CoinoLiveJson.Options)
                            ?? throw new CoinoLiveException("Coino Live answered without data.", status);
                    }
                    catch (JsonException e)
                    {
                        throw new CoinoLiveException("Coino Live sent an answer this library cannot read: " + e.Message, status, null, e);
                    }
                }
                if ((status >= 300 && status < 400) || (status == 404 && !isObject))
                {
                    throw new CoinoLiveException($"There is no Coino Live API at {_baseUrl} (HTTP {status}). Check BaseUrl.", status);
                }

                var root = isObject ? document!.RootElement : default;
                string message = isObject ? MessageOf(root) ?? "HTTP " + status : "HTTP " + status;
                var errors = isObject ? ErrorsOf(root) : null;

                throw status switch
                {
                    401 => new AuthenticationException(message, status, errors),
                    404 => new NotFoundException(message, status, errors),
                    422 => new ValidationException(message, status, errors),
                    429 => new RateLimitException(message, status, errors),
                    _ => new CoinoLiveException(message, status, errors),
                };
            }
        }

        private static string? MessageOf(JsonElement root)
        {
            if (!root.TryGetProperty("message", out var message))
            {
                return null;
            }
            switch (message.ValueKind)
            {
                case JsonValueKind.String:
                    string? text = message.GetString();
                    return string.IsNullOrEmpty(text) ? null : text;
                case JsonValueKind.Null:
                case JsonValueKind.False:
                case JsonValueKind.Undefined:
                    return null;
                default:
                    return message.GetRawText();
            }
        }

        private static IReadOnlyDictionary<string, IReadOnlyList<string>>? ErrorsOf(JsonElement root)
        {
            if (!root.TryGetProperty("errors", out var errors) || errors.ValueKind != JsonValueKind.Object)
            {
                return null;
            }
            var result = new Dictionary<string, IReadOnlyList<string>>();
            foreach (var field in errors.EnumerateObject())
            {
                var messages = new List<string>();
                if (field.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in field.Value.EnumerateArray())
                    {
                        messages.Add(item.ValueKind == JsonValueKind.String ? item.GetString()! : item.GetRawText());
                    }
                }
                else
                {
                    messages.Add(field.Value.ValueKind == JsonValueKind.String ? field.Value.GetString()! : field.Value.GetRawText());
                }
                result[field.Name] = messages;
            }
            return result;
        }

        /// <summary>"?a=1&amp;b=2" from the non-empty values, or "".</summary>
        internal static string Query(params (string Key, string? Value)[] parameters)
        {
            var builder = new StringBuilder();
            foreach (var (key, value) in parameters)
            {
                if (string.IsNullOrEmpty(value))
                {
                    continue;
                }
                builder.Append(builder.Length == 0 ? '?' : '&')
                    .Append(FormEncode(key))
                    .Append('=')
                    .Append(FormEncode(value));
            }
            return builder.ToString();
        }

        /// <summary>A path segment: every reserved character escaped.</summary>
        internal static string Id(string value, string name)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("An ID is required.", name);
            }
            return Uri.EscapeDataString(value);
        }

        private static string FormEncode(string value)
        {
            return Uri.EscapeDataString(value).Replace("%20", "+");
        }

        private sealed class DataEnvelope<T>
        {
            public T? Data { get; set; }
        }
    }
}
