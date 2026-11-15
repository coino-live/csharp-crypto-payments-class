namespace CoinoLive.Tests;

/// <summary>Clients wired to a fake API, and answers shaped like the real API's.</summary>
public static class Fixtures
{
    public const string Key = "ck_test_mock";
    public const string Base = "https://coino.live/api/v2";

    /// <summary>A client on the fake handler that records its pauses instead of waiting.</summary>
    public static CoinoLiveClient Client(FakeHandler handler, List<TimeSpan>? pauses = null, int maxRetries = 2, string? baseUrl = null, TimeSpan? timeout = null)
    {
        var options = new CoinoLiveClientOptions
        {
            HttpClient = new HttpClient(handler),
            MaxRetries = maxRetries,
        };
        if (baseUrl != null)
        {
            options.BaseUrl = baseUrl;
        }
        if (timeout != null)
        {
            options.Timeout = timeout.Value;
        }
        var client = new CoinoLiveClient(Key, options);
        client.Delay = (pause, _) =>
        {
            pauses?.Add(pause);
            return Task.CompletedTask;
        };
        return client;
    }

    public const string Payment = """
        {
          "id": "01pay",
          "status": "open",
          "amount": "27.25",
          "currency": "USD",
          "requested_amount": "25.00",
          "requested_currency": "EUR",
          "exchange_rate_usd": "1.09",
          "received_usd": "0.00",
          "remaining_usd": "27.25",
          "order_id": "1001",
          "description": "Order 1001",
          "customer_email": null,
          "metadata": { "cart": 7 },
          "checkout_url": "https://coino.live/pay/01pay",
          "success_url": "https://shop.example/thanks",
          "cancel_url": null,
          "asset": null,
          "quote": {
            "asset": "usdt-tron", "symbol": "USDT", "network": "Tron", "address": "TXyz", "amount": "27.25",
            "network_fee": "0", "rate_usd": "1", "expires_at": "2026-10-31T10:30:00+00:00"
          },
          "deposits": [
            {
              "asset": "usdt-tron", "symbol": "USDT", "network": "Tron", "tx_hash": "abc", "explorer_url": null,
              "amount": "10", "network_fee": "0", "platform_fee": "0.05", "net": "9.95", "usd_value": "10.00",
              "credited_at": "2026-10-31T10:05:00+00:00"
            }
          ],
          "expires_at": "2026-10-31T11:00:00+00:00",
          "paid_at": null,
          "created_at": "2026-10-31T10:00:00+00:00",
          "something_new": true
        }
        """;

    public static string Data(string json) => "{\"data\":" + json + "}";

    public const string CustodyUser = """
        {
          "id": "01usr",
          "external_id": "user-42",
          "name": "Ada",
          "email": null,
          "metadata": null,
          "status": "active",
          "addresses": [ { "address": "TAbc", "networks": ["tron"], "assets": ["trx-tron", "usdt-tron"] } ],
          "balances": [ { "asset": "usdt-tron", "balance": "12.5" } ],
          "created_at": "2026-10-31T10:00:00+00:00"
        }
        """;

    public const string Withdrawal = """
        {
          "id": "01wdr", "status": "pending_review", "asset": "usdt-tron", "amount": "50", "fee": "1",
          "address": "TDest", "reference": "payout-77", "custody_user": null, "tx_hash": null, "error": null,
          "created_at": "2026-10-31T10:00:00+00:00", "completed_at": null
        }
        """;
}
