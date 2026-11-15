using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CoinoLive
{
    /// <summary>Payment statuses.</summary>
    public static class PaymentStatus
    {
        /// <summary>"open"</summary>
        public const string Open = "open";

        /// <summary>"confirming"</summary>
        public const string Confirming = "confirming";

        /// <summary>"partially_paid"</summary>
        public const string PartiallyPaid = "partially_paid";

        /// <summary>"paid": ship the order when a fresh read says so.</summary>
        public const string Paid = "paid";

        /// <summary>"expired"</summary>
        public const string Expired = "expired";

        /// <summary>"cancelled"</summary>
        public const string Cancelled = "cancelled";
    }

    /// <summary>What <see cref="Payments.CreateAsync"/> sends. Only <see cref="Amount"/> is required.</summary>
    public sealed class CreatePaymentParams
    {
        /// <summary>A decimal string with at most 2 decimals, e.g. "25.00".</summary>
        public required string Amount { get; set; }

        /// <summary>Any code from <see cref="CoinoLiveClient.CurrenciesAsync"/>; USD by default.</summary>
        public string? Currency { get; set; }

        /// <summary>Your order number.</summary>
        public string? OrderId { get; set; }

        /// <summary>A description of the order.</summary>
        public string? Description { get; set; }

        /// <summary>The customer's email address.</summary>
        public string? CustomerEmail { get; set; }

        /// <summary>Where the customer goes after paying.</summary>
        public string? SuccessUrl { get; set; }

        /// <summary>Where the customer goes when they leave the checkout.</summary>
        public string? CancelUrl { get; set; }

        /// <summary>Where the webhooks for this payment go.</summary>
        public string? NotifyUrl { get; set; }

        /// <summary>Your own data, returned with the payment (at most 20 keys, 4 KB).</summary>
        public IDictionary<string, object?>? Metadata { get; set; }

        /// <summary>Lock the payment to one coin, e.g. "usdt-tron".</summary>
        public string? Asset { get; set; }

        /// <summary>Minutes, 15 to 10080.</summary>
        public int? ExpiresIn { get; set; }
    }

    /// <summary>A payment. Amounts are decimal strings (never floats).</summary>
    public sealed class Payment
    {
        /// <summary>The payment ID.</summary>
        public string Id { get; set; } = "";

        /// <summary>One of <see cref="PaymentStatus"/>.</summary>
        public string Status { get; set; } = "";

        /// <summary>The price in USD.</summary>
        public string Amount { get; set; } = "";

        /// <summary>The currency of <see cref="Amount"/> (USD).</summary>
        public string Currency { get; set; } = "";

        /// <summary>The price as requested, e.g. "25.00" with <see cref="RequestedCurrency"/> "EUR".</summary>
        public string RequestedAmount { get; set; } = "";

        /// <summary>The currency the payment was priced in.</summary>
        public string RequestedCurrency { get; set; } = "";

        /// <summary>USD per unit of <see cref="RequestedCurrency"/>, or null.</summary>
        public string? ExchangeRateUsd { get; set; }

        /// <summary>Received so far, in USD.</summary>
        public string ReceivedUsd { get; set; } = "";

        /// <summary>Still to pay, in USD.</summary>
        public string RemainingUsd { get; set; } = "";

        /// <summary>Your order number.</summary>
        public string? OrderId { get; set; }

        /// <summary>A description of the order.</summary>
        public string? Description { get; set; }

        /// <summary>The customer's email address.</summary>
        public string? CustomerEmail { get; set; }

        /// <summary>Your own data, as you sent it.</summary>
        public JsonElement? Metadata { get; set; }

        /// <summary>Send the customer here to pay.</summary>
        public string CheckoutUrl { get; set; } = "";

        /// <summary>Where the customer goes after paying.</summary>
        public string? SuccessUrl { get; set; }

        /// <summary>Where the customer goes when they leave the checkout.</summary>
        public string? CancelUrl { get; set; }

        /// <summary>The coin the payment is locked to, if any.</summary>
        public string? Asset { get; set; }

        /// <summary>The latest quote: the coin, address and amount to pay.</summary>
        public PaymentQuote? Quote { get; set; }

        /// <summary>Credited deposits, oldest first.</summary>
        public IReadOnlyList<PaymentDeposit> Deposits { get; set; } = Array.Empty<PaymentDeposit>();

        /// <summary>When the payment expires.</summary>
        public DateTimeOffset ExpiresAt { get; set; }

        /// <summary>When it was paid.</summary>
        public DateTimeOffset? PaidAt { get; set; }

        /// <summary>When it was created.</summary>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>Fields this version of the library does not know yet.</summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }
    }

    /// <summary>The coin, address and amount to pay.</summary>
    public sealed class PaymentQuote
    {
        /// <summary>The coin, e.g. "usdt-tron".</summary>
        public string Asset { get; set; } = "";

        /// <summary>e.g. "USDT".</summary>
        public string Symbol { get; set; } = "";

        /// <summary>The network name.</summary>
        public string Network { get; set; } = "";

        /// <summary>The deposit address.</summary>
        public string Address { get; set; } = "";

        /// <summary>The amount in coins.</summary>
        public string Amount { get; set; } = "";

        /// <summary>The network fee in coins.</summary>
        public string NetworkFee { get; set; } = "";

        /// <summary>USD per coin.</summary>
        public string RateUsd { get; set; } = "";

        /// <summary>When the quote expires.</summary>
        public DateTimeOffset ExpiresAt { get; set; }

        /// <summary>Fields this version of the library does not know yet.</summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }
    }

    /// <summary>A deposit credited to a payment.</summary>
    public sealed class PaymentDeposit
    {
        /// <summary>The coin, e.g. "usdt-tron".</summary>
        public string Asset { get; set; } = "";

        /// <summary>e.g. "USDT".</summary>
        public string Symbol { get; set; } = "";

        /// <summary>The network name.</summary>
        public string Network { get; set; } = "";

        /// <summary>The transaction hash.</summary>
        public string TxHash { get; set; } = "";

        /// <summary>The transaction in a block explorer, when there is one.</summary>
        public string? ExplorerUrl { get; set; }

        /// <summary>The amount received, in coins.</summary>
        public string Amount { get; set; } = "";

        /// <summary>The network fee, in coins.</summary>
        public string NetworkFee { get; set; } = "";

        /// <summary>The platform fee, in coins.</summary>
        public string PlatformFee { get; set; } = "";

        /// <summary>What was credited, in coins.</summary>
        public string Net { get; set; } = "";

        /// <summary>The USD value when it was credited.</summary>
        public string UsdValue { get; set; } = "";

        /// <summary>When it was credited.</summary>
        public DateTimeOffset CreditedAt { get; set; }

        /// <summary>Fields this version of the library does not know yet.</summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }
    }
}
