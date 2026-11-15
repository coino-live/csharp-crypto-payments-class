using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CoinoLive
{
    /// <summary>The JSON settings the API uses: snake_case names (<c>order_id</c>), null fields left out of requests.</summary>
    internal static class CoinoLiveJson
    {
        internal static JsonSerializerOptions Options { get; } = Create();

        private static JsonSerializerOptions Create()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            };
            options.MakeReadOnly(populateMissingResolver: true);
            return options;
        }
    }
}
