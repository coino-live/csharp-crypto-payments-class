using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CoinoLive
{
    /// <summary>
    /// Webhook signatures: <c>X-Coino-Signature: t=&lt;unix time&gt;,v1=&lt;hex HMAC-SHA256(secret, "&lt;t&gt;.&lt;raw body&gt;")&gt;</c>.
    /// Pass the raw body, not parsed JSON.
    /// <code>
    /// var evt = Webhook.Verify(body, request.Headers["X-Coino-Signature"], secret);
    /// var payment = await coino.Payments.RetrieveAsync(evt.Data.GetProperty("id").GetString()!); // Read the payment before acting on it.
    /// </code>
    /// </summary>
    public static class Webhook
    {
        /// <summary>The header that carries the signature.</summary>
        public const string SignatureHeader = "X-Coino-Signature";

        /// <summary>Checks the signature and returns the decoded event.</summary>
        /// <param name="payload">The raw request body.</param>
        /// <param name="signatureHeader">The X-Coino-Signature header.</param>
        /// <param name="secret">The gateway's signing secret.</param>
        /// <param name="tolerance">Seconds a signature stays valid (300).</param>
        /// <exception cref="SignatureVerificationException">The signature is missing, wrong or older than <paramref name="tolerance"/> seconds.</exception>
        public static WebhookEvent Verify(string payload, string? signatureHeader, string secret, int tolerance = 300)
        {
            if (payload == null)
            {
                throw new SignatureVerificationException("Pass the raw request body (a string or byte array), not parsed JSON.");
            }
            return Verify(Encoding.UTF8.GetBytes(payload), signatureHeader, secret, tolerance, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }

        /// <summary>Checks the signature and returns the decoded event.</summary>
        /// <param name="payload">The raw request body.</param>
        /// <param name="signatureHeader">The X-Coino-Signature header.</param>
        /// <param name="secret">The gateway's signing secret.</param>
        /// <param name="tolerance">Seconds a signature stays valid (300).</param>
        /// <exception cref="SignatureVerificationException">The signature is missing, wrong or older than <paramref name="tolerance"/> seconds.</exception>
        public static WebhookEvent Verify(byte[] payload, string? signatureHeader, string secret, int tolerance = 300)
        {
            return Verify(payload, signatureHeader, secret, tolerance, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }

        /// <summary>The check, at a given time (unix seconds).</summary>
        internal static WebhookEvent Verify(byte[] payload, string? signatureHeader, string secret, int tolerance, long now)
        {
            if (string.IsNullOrEmpty(secret))
            {
                throw new ArgumentException("Pass the gateway's signing secret.", nameof(secret));
            }
            if (payload == null)
            {
                throw new SignatureVerificationException("Pass the raw request body (a string or byte array), not parsed JSON.");
            }

            var parts = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string pair in (signatureHeader ?? "").Split(','))
            {
                int at = pair.IndexOf('=');
                if (at > 0)
                {
                    parts[pair.Substring(0, at).Trim()] = pair.Substring(at + 1).Trim();
                }
            }
            parts.TryGetValue("t", out string? t);
            parts.TryGetValue("v1", out string? v1);
            if (string.IsNullOrEmpty(t) || string.IsNullOrEmpty(v1) || !IsDigits(t))
            {
                throw new SignatureVerificationException("The X-Coino-Signature header is missing or malformed.");
            }
            if (!long.TryParse(t, out long timestamp) || Math.Abs(now - timestamp) > tolerance)
            {
                throw new SignatureVerificationException("The signature is too old.");
            }

            byte[] prefix = Encoding.UTF8.GetBytes(t + ".");
            byte[] signed = new byte[prefix.Length + payload.Length];
            Buffer.BlockCopy(prefix, 0, signed, 0, prefix.Length);
            Buffer.BlockCopy(payload, 0, signed, prefix.Length, payload.Length);
            byte[] expected;
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
            {
                expected = Encoding.ASCII.GetBytes(Convert.ToHexString(hmac.ComputeHash(signed)).ToLowerInvariant());
            }
            if (!CryptographicOperations.FixedTimeEquals(expected, Encoding.UTF8.GetBytes(v1)))
            {
                throw new SignatureVerificationException("The signature does not match.");
            }

            try
            {
                using var document = JsonDocument.Parse(payload);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    throw new SignatureVerificationException("The payload is not JSON.");
                }
                return WebhookEvent.From(document.RootElement);
            }
            catch (Exception e) when (e is JsonException || e is FormatException || e is InvalidOperationException)
            {
                throw new SignatureVerificationException("The payload is not JSON.", e);
            }
        }

        private static bool IsDigits(string value)
        {
            foreach (char c in value)
            {
                if (c < '0' || c > '9')
                {
                    return false;
                }
            }
            return true;
        }
    }

    /// <summary>A verified webhook: <c>{ id, event, created_at, data }</c>.</summary>
    public sealed class WebhookEvent
    {
        private WebhookEvent(string id, string @event, DateTimeOffset? createdAt, JsonElement data, JsonElement json)
        {
            Id = id;
            Event = @event;
            CreatedAt = createdAt;
            Data = data;
            Json = json;
        }

        /// <summary>The delivery ID.</summary>
        public string Id { get; }

        /// <summary>payment.&lt;status&gt;, custody.deposit.detected, custody.deposit.credited, withdrawal.&lt;status&gt;</summary>
        public string Event { get; }

        /// <summary>When the event happened.</summary>
        public DateTimeOffset? CreatedAt { get; }

        /// <summary>The object the event is about, as JSON. <c>Data.GetProperty("id").GetString()</c> is its ID.</summary>
        public JsonElement Data { get; }

        /// <summary>The whole event, as JSON.</summary>
        public JsonElement Json { get; }

        /// <summary>
        /// <see cref="Data"/> read into one of this library's types, e.g. <c>DataAs&lt;Payment&gt;()</c> for a payment.*
        /// event. Read the object from the API before acting on it.
        /// </summary>
        /// <typeparam name="T">The type to read the data into.</typeparam>
        public T? DataAs<T>()
        {
            return Data.ValueKind == JsonValueKind.Undefined ? default : Data.Deserialize<T>(CoinoLiveJson.Options);
        }

        internal static WebhookEvent From(JsonElement root)
        {
            var json = root.Clone();
            string id = json.TryGetProperty("id", out var i) && i.ValueKind == JsonValueKind.String ? i.GetString()! : "";
            string name = json.TryGetProperty("event", out var e) && e.ValueKind == JsonValueKind.String ? e.GetString()! : "";
            DateTimeOffset? createdAt = json.TryGetProperty("created_at", out var c) && c.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(c.GetString(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var at)
                ? at
                : null;
            JsonElement data = json.TryGetProperty("data", out var d) ? d : default;
            return new WebhookEvent(id, name, createdAt, data, json);
        }
    }
}
