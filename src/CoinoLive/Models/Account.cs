using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CoinoLive
{
    /// <summary>A currency a payment can be priced in.</summary>
    public sealed class Currency
    {
        /// <summary>e.g. "EUR".</summary>
        public string Code { get; set; } = "";

        /// <summary>USD per unit, e.g. "1.09".</summary>
        public string UsdPerUnit { get; set; } = "";

        /// <summary>Fields this version of the library does not know yet.</summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }
    }

    /// <summary>A coin customers can pay with.</summary>
    public sealed class Asset
    {
        /// <summary>e.g. "usdt-tron".</summary>
        public string Code { get; set; } = "";

        /// <summary>e.g. "USDT".</summary>
        public string Symbol { get; set; } = "";

        /// <summary>The coin's name.</summary>
        public string Name { get; set; } = "";

        /// <summary>The network name.</summary>
        public string Network { get; set; } = "";

        /// <summary>Decimal places.</summary>
        public int Decimals { get; set; }

        /// <summary>USD per coin, or null.</summary>
        public string? PriceUsd { get; set; }

        /// <summary>Fields this version of the library does not know yet.</summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }
    }

    /// <summary>Your balance of one coin, after fees.</summary>
    public sealed class Balance
    {
        /// <summary>The coin, e.g. "usdt-tron".</summary>
        public string Asset { get; set; } = "";

        /// <summary>e.g. "USDT".</summary>
        public string Symbol { get; set; } = "";

        /// <summary>The network name.</summary>
        public string Network { get; set; } = "";

        /// <summary>The balance in coins (the API's <c>balance</c> field).</summary>
        [JsonPropertyName("balance")]
        public string Amount { get; set; } = "";

        /// <summary>The balance in USD, or null when there is no fresh price.</summary>
        public string? UsdValue { get; set; }

        /// <summary>Fields this version of the library does not know yet.</summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }
    }

    /// <summary>One page of a list (50), newest first: <c>{ data, meta: { next_cursor, prev_cursor } }</c>.</summary>
    /// <typeparam name="T">What is listed.</typeparam>
    public sealed class Page<T>
    {
        /// <summary>The items on this page.</summary>
        public IReadOnlyList<T> Data { get; set; } = Array.Empty<T>();

        /// <summary>The cursors to the pages around this one.</summary>
        public PageMeta Meta { get; set; } = new PageMeta();

        /// <summary>Fields this version of the library does not know yet (such as <c>links</c>).</summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }
    }

    /// <summary>The cursors of a <see cref="Page{T}"/>.</summary>
    public sealed class PageMeta
    {
        /// <summary>Pass it as <c>cursor</c> for the next page; null on the last page.</summary>
        public string? NextCursor { get; set; }

        /// <summary>Pass it as <c>cursor</c> for the previous page; null on the first page.</summary>
        public string? PrevCursor { get; set; }

        /// <summary>Fields this version of the library does not know yet (such as <c>per_page</c>).</summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }
    }
}
