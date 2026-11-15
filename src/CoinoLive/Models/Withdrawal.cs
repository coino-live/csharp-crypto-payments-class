using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CoinoLive
{
    /// <summary>What <see cref="Withdrawals.CreateAsync"/> sends.</summary>
    public sealed class CreateWithdrawalParams
    {
        /// <summary>The coin, e.g. "usdt-tron".</summary>
        public required string Asset { get; set; }

        /// <summary>A decimal string in coins, e.g. "50".</summary>
        public required string Amount { get; set; }

        /// <summary>Where the coins go.</summary>
        public required string Address { get; set; }

        /// <summary>Pay from this custody user's balance instead of yours.</summary>
        public string? CustodyUser { get; set; }

        /// <summary>
        /// Your ID for the withdrawal. The same reference returns the same withdrawal, so a retried request never
        /// pays twice; without one the request is not retried.
        /// </summary>
        public string? Reference { get; set; }
    }

    /// <summary>A withdrawal.</summary>
    public sealed class Withdrawal
    {
        /// <summary>The withdrawal ID.</summary>
        public string Id { get; set; } = "";

        /// <summary>The withdrawal status.</summary>
        public string Status { get; set; } = "";

        /// <summary>The coin, e.g. "usdt-tron".</summary>
        public string Asset { get; set; } = "";

        /// <summary>The amount, in coins.</summary>
        public string Amount { get; set; } = "";

        /// <summary>The fee, in coins.</summary>
        public string Fee { get; set; } = "";

        /// <summary>Where the coins go.</summary>
        public string Address { get; set; } = "";

        /// <summary>Your ID for the withdrawal.</summary>
        public string? Reference { get; set; }

        /// <summary>The custody user it was paid from, if any.</summary>
        public string? CustodyUser { get; set; }

        /// <summary>The transaction hash, once sent.</summary>
        public string? TxHash { get; set; }

        /// <summary>Why it was rejected, if it was.</summary>
        public string? Error { get; set; }

        /// <summary>When it was requested.</summary>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>When it was completed.</summary>
        public DateTimeOffset? CompletedAt { get; set; }

        /// <summary>Fields this version of the library does not know yet.</summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }
    }
}
