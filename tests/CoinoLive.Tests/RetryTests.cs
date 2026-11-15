using Xunit;

namespace CoinoLive.Tests;

public class RetryTests
{
    [Theory]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    [InlineData(429)]
    public async Task AReadIsRetriedTwiceForTemporaryAnswersThenThrows(int status)
    {
        var pauses = new List<TimeSpan>();
        var handler = new FakeHandler().Always(status, """{"message":"Try later."}""");
        using var client = Fixtures.Client(handler, pauses);

        var e = await Assert.ThrowsAnyAsync<CoinoLiveException>(() => client.CurrenciesAsync());

        Assert.Equal(status, e.Status);
        Assert.Equal(3, handler.Seen.Count);
        Assert.Equal(new[] { TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1) }, pauses);
    }

    [Fact]
    public async Task ARateLimitIsRetriedThenThrownAsRateLimitException()
    {
        var handler = new FakeHandler().Always(429, """{"message":"Too many requests."}""");
        using var client = Fixtures.Client(handler);

        var e = await Assert.ThrowsAsync<RateLimitException>(() => client.CurrenciesAsync());

        Assert.Equal("Too many requests.", e.Message);
        Assert.Equal(3, handler.Seen.Count);
    }

    [Fact]
    public async Task ANetworkErrorIsRetriedThenThrownAsConnectionException()
    {
        var handler = new FakeHandler().Always((_, _) => Task.FromException<HttpResponseMessage>(new HttpRequestException("No such host is known.")));
        using var client = Fixtures.Client(handler);

        var e = await Assert.ThrowsAsync<ConnectionException>(() => client.AssetsAsync());

        Assert.Equal(0, e.Status);
        Assert.Equal("Could not reach Coino Live: No such host is known.", e.Message);
        Assert.IsType<HttpRequestException>(e.InnerException);
        Assert.Equal(3, handler.Seen.Count);
    }

    [Fact]
    public async Task ARetrySucceedsAfterANetworkError()
    {
        var handler = new FakeHandler()
            .ThenThrow(new HttpRequestException("Connection reset"))
            .Then(200, """{"data":[]}""");
        using var client = Fixtures.Client(handler);

        var balances = await client.BalancesAsync();

        Assert.Empty(balances);
        Assert.Equal(2, handler.Seen.Count);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(409)]
    [InlineData(422)]
    public async Task OtherErrorsAreNotRetried(int status)
    {
        var handler = new FakeHandler().Always(status, """{"message":"No."}""");
        using var client = Fixtures.Client(handler);

        var e = await Assert.ThrowsAnyAsync<CoinoLiveException>(() => client.Payments.RetrieveAsync("01pay"));

        Assert.Equal(status, e.Status);
        Assert.Single(handler.Seen);
    }

    [Fact]
    public async Task APostWithoutASafeRepeatIsNotRetriedEvenOnANetworkError()
    {
        var handler = new FakeHandler().Always((_, _) => Task.FromException<HttpResponseMessage>(new HttpRequestException("Connection reset")));
        using var client = Fixtures.Client(handler);

        await Assert.ThrowsAsync<ConnectionException>(() => client.Custody.TransferAsync("01usr", "usdt-tron"));
        await Assert.ThrowsAsync<ConnectionException>(() => client.Withdrawals.CreateAsync(
            new CreateWithdrawalParams { Asset = "usdt-tron", Amount = "1", Address = "T" }));

        Assert.Equal(2, handler.Seen.Count);
    }

    [Fact]
    public async Task EverySafeRequestIsRetried()
    {
        var handler = new FakeHandler().Always(503, """{"message":"Busy"}""");
        using var client = Fixtures.Client(handler, maxRetries: 1);

        await Assert.ThrowsAnyAsync<CoinoLiveException>(() => client.Payments.CreateAsync(new CreatePaymentParams { Amount = "1.00" }));
        await Assert.ThrowsAnyAsync<CoinoLiveException>(() => client.Payments.RetrieveAsync("p"));
        await Assert.ThrowsAnyAsync<CoinoLiveException>(() => client.Payments.ListAsync());
        await Assert.ThrowsAnyAsync<CoinoLiveException>(() => client.Payments.CancelAsync("p"));
        await Assert.ThrowsAnyAsync<CoinoLiveException>(() => client.Custody.CreateAsync(new CreateCustodyUserParams { ExternalId = "u" }));
        await Assert.ThrowsAnyAsync<CoinoLiveException>(() => client.Custody.RetrieveAsync("u"));
        await Assert.ThrowsAnyAsync<CoinoLiveException>(() => client.Custody.ListAsync());
        await Assert.ThrowsAnyAsync<CoinoLiveException>(() => client.Custody.DepositsAsync("u"));
        await Assert.ThrowsAnyAsync<CoinoLiveException>(() => client.Withdrawals.CreateAsync(
            new CreateWithdrawalParams { Asset = "a", Amount = "1", Address = "T", Reference = "r" }));
        await Assert.ThrowsAnyAsync<CoinoLiveException>(() => client.Withdrawals.RetrieveAsync("w"));
        await Assert.ThrowsAnyAsync<CoinoLiveException>(() => client.CurrenciesAsync());
        await Assert.ThrowsAnyAsync<CoinoLiveException>(() => client.AssetsAsync());
        await Assert.ThrowsAnyAsync<CoinoLiveException>(() => client.BalancesAsync());

        Assert.Equal(13 * 2, handler.Seen.Count);
    }

    [Fact]
    public async Task MaxRetriesZeroSendsOnce()
    {
        var pauses = new List<TimeSpan>();
        var handler = new FakeHandler().Always(503, """{"message":"Busy"}""");
        using var client = Fixtures.Client(handler, pauses, maxRetries: 0);

        await Assert.ThrowsAnyAsync<CoinoLiveException>(() => client.AssetsAsync());

        Assert.Single(handler.Seen);
        Assert.Empty(pauses);
    }

    [Fact]
    public async Task ANegativeMaxRetriesCountsAsZero()
    {
        var handler = new FakeHandler().Always(503, """{"message":"Busy"}""");
        using var client = Fixtures.Client(handler, maxRetries: -3);

        await Assert.ThrowsAnyAsync<CoinoLiveException>(() => client.AssetsAsync());

        Assert.Single(handler.Seen);
    }

    [Fact]
    public async Task ThePauseDoublesUpToEightSeconds()
    {
        var pauses = new List<TimeSpan>();
        var handler = new FakeHandler().Always(503, """{"message":"Busy"}""");
        using var client = Fixtures.Client(handler, pauses, maxRetries: 6);

        await Assert.ThrowsAnyAsync<CoinoLiveException>(() => client.AssetsAsync());

        Assert.Equal(7, handler.Seen.Count);
        Assert.Equal(new[] { 0.5, 1, 2, 4, 8, 8 }, pauses.Select(p => p.TotalSeconds));
    }

    [Fact]
    public async Task ATimeoutIsAConnectionException()
    {
        var handler = new FakeHandler().Always(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return FakeHandler.Response(200, """{"data":[]}""");
        });
        using var client = Fixtures.Client(handler, maxRetries: 0, timeout: TimeSpan.FromMilliseconds(100));

        var e = await Assert.ThrowsAsync<ConnectionException>(() => client.AssetsAsync());

        Assert.Contains("timed out", e.Message);
    }

    [Fact]
    public async Task ATimeoutIsRetried()
    {
        var handler = new FakeHandler()
            .Then(async (_, token) =>
            {
                await Task.Delay(Timeout.Infinite, token);
                return FakeHandler.Response(200, """{"data":[]}""");
            })
            .Then(200, """{"data":[]}""");
        using var client = Fixtures.Client(handler, timeout: TimeSpan.FromMilliseconds(100));

        await client.AssetsAsync();

        Assert.Equal(2, handler.Seen.Count);
    }

    [Fact]
    public async Task YourCancellationIsNotRetriedOrWrapped()
    {
        using var cancel = new CancellationTokenSource();
        var handler = new FakeHandler().Always(async (_, token) =>
        {
            cancel.Cancel();
            await Task.Delay(Timeout.Infinite, token);
            return FakeHandler.Response(200, """{"data":[]}""");
        });
        using var client = Fixtures.Client(handler);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.AssetsAsync(cancel.Token));

        Assert.Single(handler.Seen);
    }
}
