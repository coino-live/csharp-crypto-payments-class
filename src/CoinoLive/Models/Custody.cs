using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CoinoLive
{
    /// <summary>What <see cref="Custody.CreateAsync"/> sends. Only <see cref="ExternalId"/> is required.</summary>
    public sealed class CreateCustodyUserParams
    {
        /// <summary>Your ID for the user. The same external_id returns the same user.</summary>
        public required string ExternalId { get; set; }

        /// <summary>The user's name.</summary>
        public string? Name { get; set; }

        /// <summary>The user's email address.</summary>
        public string? Email { get; set; }

        /// <summary>Your own data (at most 20 keys, 4 KB).</summary>
        public IDictionary<string, object?>? Metadata { get; set; }
    }

    /// <summary>A custody user: lifetime deposit addresses and balances.</summary>
    public sealed class CustodyUser
    {
        /// <summary>The custody user ID.</summary>
        public string Id { get; set; } = "";

        /// <summary>Your ID for the user.</summary>
        public string ExternalId { get; set; } = "";

        /// <summary>The user's name.</summary>
        public string? Name { get; set; }

        /// <summary>The user's email address.</summary>
        public string? Email { get; set; }

        /// <summary>Your own data, as you sent it.</summary>
        public JsonElement? Metadata { get; set; }

        /// <summary>The user's status.</summary>
        public string Status { get; set; } = "";

        /// <summary>The user's deposit addresses, with the networks and coins each accepts.</summary>
        public IReadOnlyList<CustodyAddress> Addresses { get; set; } = Array.Empty<CustodyAddress>();

        /// <summary>The user's balance per coin.</summary>
        public IReadOnlyList<CustodyBalance> Balances { get; set; } = Array.Empty<CustodyBalance>();

        /// <summary>When the user was created.</summary>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>Fields this version of the library does not know yet.</summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }
    }

    /// <summary>A lifetime deposit address of a custody user.</summary>
    public sealed class CustodyAddress
    {
        /// <summary>The address.</summary>
        public string Address { get; set; } = "";

        /// <summary>The network codes it works on.</summary>
        public IReadOnlyList<string> Networks { get; set; } = Array.Empty<string>();

        /// <summary>The coins it accepts, e.g. "usdt-tron".</summary>
        public IReadOnlyList<string> Assets { get; set; } = Array.Empty<string>();

        /// <summary>Fields this version of the library does not know yet.</summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }
    }

    /// <summary>A custody user's balance of one coin.</summary>
    public sealed class CustodyBalance
    {
        /// <summary>The coin, e.g. "usdt-tron".</summary>
        public string Asset { get; set; } = "";

        /// <summary>The balance in coins.</summary>
        public string Balance { get; set; } = "";

        /// <summary>Fields this version of the library does not know yet.</summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }
    }

    /// <summary>A deposit credited to a custody user.</summary>
    public sealed class CustodyDeposit
    {
        /// <summary>The coin, e.g. "usdt-tron".</summary>
        public string Asset { get; set; } = "";

        /// <summary>The amount received, in coins.</summary>
        public string Amount { get; set; } = "";

        /// <summary>The network fee, in coins.</summary>
        public string NetworkFee { get; set; } = "";

        /// <summary>The platform fee, in coins.</summary>
        public string PlatformFee { get; set; } = "";

        /// <summary>What was credited, in coins.</summary>
        public string Net { get; set; } = "";

        /// <summary>The transaction hash.</summary>
        public string TxHash { get; set; } = "";

        /// <summary>When it was credited.</summary>
        public DateTimeOffset CreditedAt { get; set; }

        /// <summary>Fields this version of the library does not know yet.</summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }
    }

    /// <summary>One page of a custody user's credited deposits: <c>{ data, next_cursor }</c>.</summary>
    public sealed class CustodyDepositPage
    {
        /// <summary>Up to 50 deposits, newest first.</summary>
        public IReadOnlyList<CustodyDeposit> Data { get; set; } = Array.Empty<CustodyDeposit>();

        /// <summary>Pass it to <see cref="Custody.DepositsAsync"/> for the next page; null on the last page.</summary>
        public string? NextCursor { get; set; }

        /// <summary>Fields this version of the library does not know yet.</summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }
    }

    /// <summary>A move of a custody user's balance to your main balance.</summary>
    public sealed class CustodyTransfer
    {
        /// <summary>The transfer ID.</summary>
        public string Id { get; set; } = "";

        /// <summary>The coin, e.g. "usdt-tron".</summary>
        public string Asset { get; set; } = "";

        /// <summary>The amount moved, in coins.</summary>
        public string Amount { get; set; } = "";

        /// <summary>The custody user ID.</summary>
        public string CustodyUser { get; set; } = "";

        /// <summary>The transfer's reference, if it has one.</summary>
        public string? Reference { get; set; }

        /// <summary>When it was made.</summary>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>Fields this version of the library does not know yet.</summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }
    }
}
