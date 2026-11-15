# Coino Live .NET library

Accept crypto payments with the [Coino Live API](https://coino.live/api-guide). C# for .NET 8+, no dependencies
(only `System.Net.Http` and `System.Text.Json` from the framework).

This library replaces the old `CoinoLive.cs` class, which called API v1. API v1 has been removed.

## Install

The library is not on NuGet yet. Download the source (**Code → Download ZIP**) or a zip from
[Releases](https://github.com/coino-live/csharp-crypto-payments-class/releases), unzip it and reference the project
from yours:

```bash
dotnet add reference path/to/csharp-crypto-payments-class/src/CoinoLive/CoinoLive.csproj
```

Or build a package and install it from a local folder:

```bash
dotnet pack path/to/csharp-crypto-payments-class/src/CoinoLive -c Release -o /absolute/path/to/packages
dotnet nuget add source /absolute/path/to/packages --name coinolive-local
dotnet add package CoinoLive --version 1.0.0
```

## Take a payment

```csharp
using CoinoLive;

var coino = new CoinoLiveClient("ck_live_…"); // Dashboard → Developers

var payment = await coino.Payments.CreateAsync(new CreatePaymentParams
{
    Amount = "25.00",          // a string, at most 2 decimals
    Currency = "EUR",          // any code from coino.CurrenciesAsync(); USD by default
    OrderId = "1001",
    SuccessUrl = "https://shop.example/thanks",
    CancelUrl = "https://shop.example/cart",
    NotifyUrl = "https://shop.example/coino-webhook",
});

return Redirect(payment.CheckoutUrl);
```

Ship the order when a fresh read says `paid`:

```csharp
var payment = await coino.Payments.RetrieveAsync(paymentId);
if (payment.Status == PaymentStatus.Paid)
{
    // …
}
```

Statuses: `open`, `confirming`, `partially_paid`, `paid`, `expired`, `cancelled` (constants in `PaymentStatus`).

One client can serve the whole application and is safe to share between threads. In ASP.NET Core, register it once:

```csharp
builder.Services.AddSingleton(new CoinoLiveClient(builder.Configuration["Coino:ApiKey"]!));
```

## Webhooks

The signature covers the raw body, so read it unparsed (ASP.NET Core minimal API):

```csharp
using CoinoLive;

app.MapPost("/coino-webhook", async (HttpRequest request, CoinoLiveClient coino) =>
{
    using var reader = new StreamReader(request.Body);
    string body = await reader.ReadToEndAsync();

    WebhookEvent evt;
    try
    {
        evt = Webhook.Verify(body, request.Headers["X-Coino-Signature"], signingSecret);
    }
    catch (SignatureVerificationException)
    {
        return Results.StatusCode(401);
    }
    if (evt.Event.StartsWith("payment."))
    {
        // act on what the API says, not on the webhook body
        var payment = await coino.Payments.RetrieveAsync(evt.Data.GetProperty("id").GetString()!);
    }
    return Results.Ok();
});
```

`Webhook.Verify` also takes the body as a `byte[]`. It refuses signatures older than 300 seconds; pass another
number of seconds as the fourth argument to change that. The event has `Id`, `Event` (`payment.<status>`,
`custody.deposit.detected`, `custody.deposit.credited`, `withdrawal.<status>`), `CreatedAt` and `Data` (JSON);
`evt.DataAs<Payment>()` reads `Data` into a library type.

## Everything else

```csharp
await coino.Payments.ListAsync(status: "paid", orderId: "1001"); // Page<Payment>: .Data, .Meta.NextCursor, .Meta.PrevCursor
await foreach (var payment in coino.Payments.AllAsync(status: "paid")) { /* every page */ }
await coino.Payments.CancelAsync(paymentId);           // only while nothing was received

await coino.CurrenciesAsync();                         // prices can be in these
await coino.AssetsAsync();                             // customers can pay with these
await coino.BalancesAsync();                           // your balance per coin, with UsdValue

var user = await coino.Custody.CreateAsync(new CreateCustodyUserParams { ExternalId = "user-42" }); // lifetime deposit addresses for your users
await coino.Custody.RetrieveAsync(user.Id);
await coino.Custody.ListAsync(externalId: "user-42");
await coino.Custody.DepositsAsync(user.Id);            // .Data, .NextCursor
await coino.Custody.TransferAsync(user.Id, "usdt-tron"); // move the user's balance to yours

await coino.Withdrawals.CreateAsync(new CreateWithdrawalParams { Asset = "usdt-tron", Amount = "50", Address = "T…", Reference = "payout-77" });
await coino.Withdrawals.RetrieveAsync(withdrawalId);
```

Every method:

| Method | Request | Returns |
| --- | --- | --- |
| `Payments.CreateAsync(params, idempotencyKey?)` | `POST /payments` | `Payment` |
| `Payments.RetrieveAsync(id)` | `GET /payments/{id}` | `Payment` |
| `Payments.ListAsync(status?, orderId?, cursor?)` | `GET /payments` | `Page<Payment>`, 50 per page, newest first |
| `Payments.AllAsync(status?, orderId?)` | `GET /payments`, page after page | `IAsyncEnumerable<Payment>` |
| `Payments.CancelAsync(id)` | `POST /payments/{id}/cancel` | `Payment` |
| `Custody.CreateAsync(params)` | `POST /custody/users` | `CustodyUser` (the existing one for a known `ExternalId`) |
| `Custody.RetrieveAsync(id)` | `GET /custody/users/{id}` | `CustodyUser` |
| `Custody.ListAsync(externalId?, cursor?)` | `GET /custody/users` | `Page<CustodyUser>`, 50 per page, newest first |
| `Custody.DepositsAsync(id, cursor?)` | `GET /custody/users/{id}/deposits` | `CustodyDepositPage`, 50 per page, newest first |
| `Custody.TransferAsync(id, asset, amount?)` | `POST /custody/users/{id}/transfers` | `CustodyTransfer`; all of the balance unless `amount` is given |
| `Withdrawals.CreateAsync(params)` | `POST /withdrawals` | `Withdrawal` |
| `Withdrawals.RetrieveAsync(id)` | `GET /withdrawals/{id}` | `Withdrawal` |
| `CurrenciesAsync()` | `GET /currencies` | `IReadOnlyList<Currency>` |
| `AssetsAsync()` | `GET /assets` | `IReadOnlyList<Asset>` |
| `BalancesAsync()` | `GET /balances` | `IReadOnlyList<Balance>` |

Every method also takes a `CancellationToken` as its last argument. Amounts are decimal strings, as the API sends them
(never floats). Fields this version does not know yet are kept in each object's `ExtensionData`. API withdrawals must
be turned on in the gateway settings first.

## Errors and retries

Every failure throws one of these (all in the `CoinoLive` namespace):

| Exception | When |
| --- | --- |
| `AuthenticationException` | Wrong or revoked key, or the gateway is suspended (401) |
| `NotFoundException` | Unknown ID, or it belongs to another gateway (404) |
| `ValidationException` | The request was refused; `e.Errors` names the fields (422) |
| `RateLimitException` | Too many requests (429) |
| `ConnectionException` | The API could not be reached, or the request timed out |
| `CoinoLiveException` | Anything else; the parent of all of the above, with `e.Status` |

```csharp
try
{
    await coino.Payments.CreateAsync(new CreatePaymentParams { Amount = "5.123" });
}
catch (ValidationException e)
{
    // e.Message: what was wrong; e.Errors["amount"]: the messages for that field
}
```

Network errors, 429 and 5xx answers are retried twice with a short pause, but only for requests that are safe to repeat:
reads, `Payments.CreateAsync()` (it always sends an idempotency key, random unless you pass one as the second argument),
`Custody.CreateAsync()` (same `ExternalId`, same user) and `Withdrawals.CreateAsync()` with a `Reference`.

```csharp
var coino = new CoinoLiveClient("ck_live_…", new CoinoLiveClientOptions
{
    Timeout = TimeSpan.FromSeconds(30),   // per request
    MaxRetries = 2,
    BaseUrl = "https://coino.live/api/v2",
    HttpClient = null,                    // or your own, e.g. from IHttpClientFactory; it must not follow redirects
});
```

## Tests

```bash
dotnet test
```

The tests run offline, against a stand-in for the API.

## License

MIT, see [LICENSE](LICENSE).
