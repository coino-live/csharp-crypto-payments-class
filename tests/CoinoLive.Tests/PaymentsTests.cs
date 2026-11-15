using Xunit;

namespace CoinoLive.Tests;

public class PaymentsTests
{
    [Fact]
    public async Task CreatePostsTheParamsAsJsonWithAnIdempotencyKey()
    {
        var handler = new FakeHandler().Then(201, Fixtures.Data(Fixtures.Payment));
        using var client = Fixtures.Client(handler);

        var payment = await client.Payments.CreateAsync(new CreatePaymentParams { Amount = "25.00", Currency = "EUR", OrderId = "1001" });

        var seen = Assert.Single(handler.Seen);
        Assert.Equal("POST", seen.Method);
        Assert.Equal(Fixtures.Base + "/payments", seen.Url);
        Assert.Equal("application/json", seen.ContentType);
        Assert.Equal("""{"amount":"25.00","currency":"EUR","order_id":"1001"}""", seen.Body);
        Assert.Matches("^[0-9a-f]{32}$", seen.Headers["Idempotency-Key"]);
        Assert.Equal("Bearer ck_test_mock", seen.Headers["Authorization"]);
        Assert.Equal("01pay", payment.Id);
        Assert.Equal("https://coino.live/pay/01pay", payment.CheckoutUrl);
    }

    [Fact]
    public async Task CreateSendsEveryFieldInSnakeCase()
    {
        var handler = new FakeHandler().Then(201, Fixtures.Data(Fixtures.Payment));
        using var client = Fixtures.Client(handler);

        await client.Payments.CreateAsync(new CreatePaymentParams
        {
            Amount = "25.00",
            Currency = "EUR",
            OrderId = "1001",
            Description = "Café €",
            CustomerEmail = "ada+shop@example.com",
            SuccessUrl = "https://shop.example/thanks",
            CancelUrl = "https://shop.example/cart",
            NotifyUrl = "https://shop.example/coino-webhook",
            Metadata = new Dictionary<string, object?> { ["cart"] = 7, ["note"] = "gift", ["none"] = null },
            Asset = "usdt-tron",
            ExpiresIn = 60,
        });

        Assert.Equal(
            """{"amount":"25.00","currency":"EUR","order_id":"1001","description":"Café €","customer_email":"ada+shop@example.com","success_url":"https://shop.example/thanks","cancel_url":"https://shop.example/cart","notify_url":"https://shop.example/coino-webhook","metadata":{"cart":7,"note":"gift","none":null},"asset":"usdt-tron","expires_in":60}""",
            handler.Seen[0].Body);
    }

    [Fact]
    public async Task CreateUsesAGivenIdempotencyKey()
    {
        var handler = new FakeHandler().Then(200, Fixtures.Data(Fixtures.Payment));
        using var client = Fixtures.Client(handler);

        await client.Payments.CreateAsync(new CreatePaymentParams { Amount = "1.00" }, "order-1001-1");

        Assert.Equal("order-1001-1", handler.Seen[0].Headers["Idempotency-Key"]);
    }

    [Fact]
    public async Task CreateMakesANewKeyForEveryCall()
    {
        var handler = new FakeHandler().Always(201, Fixtures.Data(Fixtures.Payment));
        using var client = Fixtures.Client(handler);

        await client.Payments.CreateAsync(new CreatePaymentParams { Amount = "1.00" });
        await client.Payments.CreateAsync(new CreatePaymentParams { Amount = "1.00" });
        await client.Payments.CreateAsync(new CreatePaymentParams { Amount = "1.00" }, "");

        Assert.Equal(3, handler.Seen.Select(s => s.Headers["Idempotency-Key"]).Distinct().Count());
    }

    [Fact]
    public async Task CreateRetriesA503WithTheSameIdempotencyKeyAndBody()
    {
        var pauses = new List<TimeSpan>();
        var handler = new FakeHandler()
            .Then(503, """{"message":"Busy"}""")
            .Then(503, """{"message":"Busy"}""")
            .Then(201, Fixtures.Data(Fixtures.Payment));
        using var client = Fixtures.Client(handler, pauses);

        var payment = await client.Payments.CreateAsync(new CreatePaymentParams { Amount = "25.00", Currency = "EUR", OrderId = "1001" });

        Assert.Equal("01pay", payment.Id);
        Assert.Equal(3, handler.Seen.Count);
        Assert.Single(handler.Seen.Select(s => s.Headers["Idempotency-Key"]).Distinct());
        Assert.Single(handler.Seen.Select(s => s.Body).Distinct());
        Assert.Equal(new[] { TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1) }, pauses);
    }

    [Fact]
    public async Task CreateKeepsAGivenKeyAcrossRetries()
    {
        var handler = new FakeHandler()
            .ThenThrow(new HttpRequestException("Connection refused"))
            .Then(201, Fixtures.Data(Fixtures.Payment));
        using var client = Fixtures.Client(handler);

        await client.Payments.CreateAsync(new CreatePaymentParams { Amount = "1.00" }, "order-1001-1");

        Assert.Equal(new[] { "order-1001-1", "order-1001-1" }, handler.Seen.Select(s => s.Headers["Idempotency-Key"]));
    }

    [Fact]
    public async Task CreateRefusesNullParams()
    {
        using var client = Fixtures.Client(new FakeHandler());

        await Assert.ThrowsAsync<ArgumentNullException>(() => client.Payments.CreateAsync(null!));
    }

    [Fact]
    public async Task RetrieveReadsEveryField()
    {
        var handler = new FakeHandler().Then(200, Fixtures.Data(Fixtures.Payment));
        using var client = Fixtures.Client(handler);

        var p = await client.Payments.RetrieveAsync("01pay");

        var seen = Assert.Single(handler.Seen);
        Assert.Equal("GET", seen.Method);
        Assert.Equal(Fixtures.Base + "/payments/01pay", seen.Url);
        Assert.Null(seen.Body);
        Assert.False(seen.Headers.ContainsKey("Idempotency-Key"));

        Assert.Equal(PaymentStatus.Open, p.Status);
        Assert.Equal("27.25", p.Amount);
        Assert.Equal("USD", p.Currency);
        Assert.Equal("25.00", p.RequestedAmount);
        Assert.Equal("EUR", p.RequestedCurrency);
        Assert.Equal("1.09", p.ExchangeRateUsd);
        Assert.Equal("0.00", p.ReceivedUsd);
        Assert.Equal("27.25", p.RemainingUsd);
        Assert.Equal("1001", p.OrderId);
        Assert.Equal("Order 1001", p.Description);
        Assert.Null(p.CustomerEmail);
        Assert.Equal(7, p.Metadata!.Value.GetProperty("cart").GetInt32());
        Assert.Equal("https://shop.example/thanks", p.SuccessUrl);
        Assert.Null(p.CancelUrl);
        Assert.Null(p.Asset);
        Assert.Equal("TXyz", p.Quote!.Address);
        Assert.Equal("usdt-tron", p.Quote.Asset);
        Assert.Equal("1", p.Quote.RateUsd);
        var deposit = Assert.Single(p.Deposits);
        Assert.Equal("abc", deposit.TxHash);
        Assert.Equal("9.95", deposit.Net);
        Assert.Equal("0.05", deposit.PlatformFee);
        Assert.Equal("10.00", deposit.UsdValue);
        Assert.Null(deposit.ExplorerUrl);
        Assert.Equal(new DateTimeOffset(2026, 10, 31, 10, 5, 0, TimeSpan.Zero), deposit.CreditedAt);
        Assert.Equal(new DateTimeOffset(2026, 10, 31, 11, 0, 0, TimeSpan.Zero), p.ExpiresAt);
        Assert.Null(p.PaidAt);
        Assert.Equal(new DateTimeOffset(2026, 10, 31, 10, 0, 0, TimeSpan.Zero), p.CreatedAt);
        Assert.True(p.ExtensionData!["something_new"].GetBoolean());
    }

    [Fact]
    public async Task RetrieveEscapesTheId()
    {
        var handler = new FakeHandler().Then(200, Fixtures.Data(Fixtures.Payment));
        using var client = Fixtures.Client(handler);

        await client.Payments.RetrieveAsync("a/b c?d");

        Assert.Equal(Fixtures.Base + "/payments/a%2Fb%20c%3Fd", handler.Seen[0].Url);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task RetrieveRefusesAnEmptyId(string? id)
    {
        using var client = Fixtures.Client(new FakeHandler());

        await Assert.ThrowsAsync<ArgumentException>(() => client.Payments.RetrieveAsync(id!));
    }

    [Fact]
    public async Task ListSendsTheFiltersAndReturnsThePage()
    {
        var handler = new FakeHandler().Then(200, """
            {"data":[{"id":"p1"},{"id":"p2"}],"links":{"first":null,"last":null,"prev":null,"next":"https://x/?cursor=c2"},
             "meta":{"path":"https://x","per_page":50,"next_cursor":"c2","prev_cursor":null}}
            """);
        using var client = Fixtures.Client(handler);

        var page = await client.Payments.ListAsync(status: PaymentStatus.Paid, orderId: "order 1001");

        Assert.Equal("GET", handler.Seen[0].Method);
        Assert.Equal(Fixtures.Base + "/payments?status=paid&order_id=order+1001", handler.Seen[0].Url);
        Assert.Equal(new[] { "p1", "p2" }, page.Data.Select(p => p.Id));
        Assert.Equal("c2", page.Meta.NextCursor);
        Assert.Null(page.Meta.PrevCursor);
        Assert.Equal(50, page.Meta.ExtensionData!["per_page"].GetInt32());
        Assert.True(page.ExtensionData!.ContainsKey("links"));
    }

    [Fact]
    public async Task ListWithoutFiltersHasNoQuery()
    {
        var handler = new FakeHandler().Then(200, """{"data":[],"meta":{"next_cursor":null,"prev_cursor":null}}""");
        using var client = Fixtures.Client(handler);

        await client.Payments.ListAsync(status: "", cursor: null);

        Assert.Equal(Fixtures.Base + "/payments", handler.Seen[0].Url);
    }

    [Fact]
    public async Task ListSendsTheCursor()
    {
        var handler = new FakeHandler().Then(200, """{"data":[],"meta":{"next_cursor":null,"prev_cursor":"c1"}}""");
        using var client = Fixtures.Client(handler);

        await client.Payments.ListAsync(cursor: "eyJpZCI6MX0=");

        Assert.Equal(Fixtures.Base + "/payments?cursor=eyJpZCI6MX0%3D", handler.Seen[0].Url);
    }

    [Fact]
    public async Task AllFollowsNextCursor()
    {
        var handler = new FakeHandler()
            .Then(200, """{"data":[{"id":"p1"},{"id":"p2"}],"meta":{"next_cursor":"c2","prev_cursor":null}}""")
            .Then(200, """{"data":[{"id":"p3"}],"meta":{"next_cursor":null,"prev_cursor":"c1"}}""");
        using var client = Fixtures.Client(handler);

        var ids = new List<string>();
        await foreach (var payment in client.Payments.AllAsync(status: PaymentStatus.Paid))
        {
            ids.Add(payment.Id);
        }

        Assert.Equal(new[] { "p1", "p2", "p3" }, ids);
        Assert.Equal(
            new[] { Fixtures.Base + "/payments?status=paid", Fixtures.Base + "/payments?status=paid&cursor=c2" },
            handler.Seen.Select(s => s.Url));
    }

    [Fact]
    public async Task AllFetchesPagesOnlyAsYouGo()
    {
        var handler = new FakeHandler()
            .Then(200, """{"data":[{"id":"p1"}],"meta":{"next_cursor":"c2","prev_cursor":null}}""");
        using var client = Fixtures.Client(handler);

        await foreach (var payment in client.Payments.AllAsync())
        {
            break;
        }

        Assert.Single(handler.Seen);
    }

    [Fact]
    public async Task CancelPostsWithoutABody()
    {
        var handler = new FakeHandler().Then(200, Fixtures.Data(Fixtures.Payment.Replace("\"open\"", "\"cancelled\"")));
        using var client = Fixtures.Client(handler);

        var payment = await client.Payments.CancelAsync("01pay");

        var seen = Assert.Single(handler.Seen);
        Assert.Equal("POST", seen.Method);
        Assert.Equal(Fixtures.Base + "/payments/01pay/cancel", seen.Url);
        Assert.Null(seen.Body);
        Assert.Null(seen.ContentType);
        Assert.False(seen.Headers.ContainsKey("Idempotency-Key"));
        Assert.Equal(PaymentStatus.Cancelled, payment.Status);
    }

    [Fact]
    public async Task CancelIsRetried()
    {
        var handler = new FakeHandler()
            .Then(502, "<html>Bad gateway</html>")
            .Then(200, Fixtures.Data(Fixtures.Payment));
        using var client = Fixtures.Client(handler);

        await client.Payments.CancelAsync("01pay");

        Assert.Equal(2, handler.Seen.Count);
    }
}
