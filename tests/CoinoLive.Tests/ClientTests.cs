using Xunit;

namespace CoinoLive.Tests;

public class ClientTests
{
    [Theory]
    [InlineData("sk_123")]
    [InlineData("ck_live_")]
    [InlineData("ck_prod_abc")]
    [InlineData("ck_live_abc-def")]
    [InlineData("ck_live_abc\n")]
    [InlineData("")]
    public void RefusesAKeyThatIsNotCk(string key)
    {
        var e = Assert.Throws<ArgumentException>(() => new CoinoLiveClient(key));
        Assert.Contains("ck_live_", e.Message);
    }

    [Fact]
    public void RefusesANullKey()
    {
        Assert.Throws<ArgumentException>(() => new CoinoLiveClient(null!));
    }

    [Theory]
    [InlineData("ck_live_abc123")]
    [InlineData("ck_test_ABCxyz789")]
    public void AcceptsLiveAndTestKeys(string key)
    {
        using var client = new CoinoLiveClient(key);
        Assert.NotNull(client.Payments);
        Assert.NotNull(client.Custody);
        Assert.NotNull(client.Withdrawals);
    }

    [Theory]
    [InlineData("http://coino.live/api/v2")]
    [InlineData("ftp://coino.live/api/v2")]
    [InlineData("http://localhost.evil.test/api/v2")]
    [InlineData("coino.live/api/v2")]
    public void RefusesABaseUrlThatIsNotHttps(string baseUrl)
    {
        var e = Assert.Throws<ArgumentException>(() => new CoinoLiveClient("ck_test_abc", new CoinoLiveClientOptions { BaseUrl = baseUrl }));
        Assert.Contains("https://", e.Message);
    }

    [Theory]
    [InlineData("http://127.0.0.1:9/api/v2")]
    [InlineData("http://localhost/api/v2")]
    [InlineData("http://localhost:8000")]
    public void AllowsHttpOnLocalhost(string baseUrl)
    {
        using var client = new CoinoLiveClient("ck_test_abc", new CoinoLiveClientOptions { BaseUrl = baseUrl });
        Assert.NotNull(client);
    }

    [Fact]
    public void TheDefaultsMatchTheOtherLibraries()
    {
        var options = new CoinoLiveClientOptions();
        Assert.Equal("https://coino.live/api/v2", CoinoLiveClient.DefaultBaseUrl);
        Assert.Equal(CoinoLiveClient.DefaultBaseUrl, options.BaseUrl);
        Assert.Equal(TimeSpan.FromSeconds(30), options.Timeout);
        Assert.Equal(2, options.MaxRetries);
        Assert.Equal("1.0.0", CoinoLiveClient.Version);
    }

    [Fact]
    public async Task SendsTheKeyAsABearerTokenWithJsonAcceptAndALibraryUserAgent()
    {
        var handler = new FakeHandler().Then(200, """{"data":[]}""");
        using var client = Fixtures.Client(handler);

        await client.BalancesAsync();

        var seen = Assert.Single(handler.Seen);
        Assert.Equal("Bearer ck_test_mock", seen.Headers["Authorization"]);
        Assert.Equal("application/json", seen.Headers["Accept"]);
        Assert.Equal("coinolive-dotnet/1.0.0 .NET/" + Environment.Version, seen.Headers["User-Agent"]);
        Assert.False(seen.Headers.ContainsKey("Idempotency-Key"));
        Assert.Null(seen.Body);
        Assert.Null(seen.ContentType);
    }

    [Fact]
    public async Task UsesTheDefaultBaseUrl()
    {
        var handler = new FakeHandler().Then(200, """{"data":[]}""");
        using var client = Fixtures.Client(handler);

        await client.AssetsAsync();

        Assert.Equal("https://coino.live/api/v2/assets", handler.Seen[0].Url);
    }

    [Fact]
    public async Task TrimsSlashesFromTheBaseUrl()
    {
        var handler = new FakeHandler().Then(200, """{"data":[]}""");
        using var client = Fixtures.Client(handler, baseUrl: "https://staging.example/api/v2//");

        await client.CurrenciesAsync();

        Assert.Equal("https://staging.example/api/v2/currencies", handler.Seen[0].Url);
    }

    [Fact]
    public async Task CurrenciesAssetsAndBalancesReturnTheirData()
    {
        var handler = new FakeHandler()
            .Then(200, """{"data":[{"code":"EUR","usd_per_unit":"1.09"},{"code":"USD","usd_per_unit":"1"}]}""")
            .Then(200, """{"data":[{"code":"usdt-tron","symbol":"USDT","name":"Tether","network":"Tron","decimals":6,"price_usd":"1"}]}""")
            .Then(200, """{"data":[{"asset":"usdt-tron","symbol":"USDT","network":"Tron","balance":"12.500000","usd_value":null}]}""");
        using var client = Fixtures.Client(handler);

        var currencies = await client.CurrenciesAsync();
        var assets = await client.AssetsAsync();
        var balances = await client.BalancesAsync();

        Assert.Equal(new[] { "GET", "GET", "GET" }, handler.Seen.Select(s => s.Method));
        Assert.Equal(
            new[] { Fixtures.Base + "/currencies", Fixtures.Base + "/assets", Fixtures.Base + "/balances" },
            handler.Seen.Select(s => s.Url));
        Assert.Equal("EUR", currencies[0].Code);
        Assert.Equal("1.09", currencies[0].UsdPerUnit);
        Assert.Equal(2, currencies.Count);
        Assert.Equal("usdt-tron", assets[0].Code);
        Assert.Equal(6, assets[0].Decimals);
        Assert.Equal("1", assets[0].PriceUsd);
        Assert.Equal("Tether", assets[0].Name);
        Assert.Equal("12.500000", balances[0].Amount);
        Assert.Null(balances[0].UsdValue);
    }

    [Fact]
    public async Task DisposeLeavesAGivenHttpClientUsable()
    {
        var handler = new FakeHandler().Always(200, """{"data":[]}""");
        var http = new HttpClient(handler);
        new CoinoLiveClient(Fixtures.Key, new CoinoLiveClientOptions { HttpClient = http }).Dispose();

        using var response = await http.GetAsync("https://example.test/");

        Assert.Equal(200, (int)response.StatusCode);
    }

    [Fact]
    public async Task AnUnreachableApiIsAConnectionException()
    {
        // The client's own HttpClient, against a closed local port: nothing leaves the machine.
        using var client = new CoinoLiveClient(Fixtures.Key, new CoinoLiveClientOptions
        {
            BaseUrl = "http://127.0.0.1:9/api/v2",
            MaxRetries = 0,
            Timeout = TimeSpan.FromSeconds(3),
        });

        var e = await Assert.ThrowsAsync<ConnectionException>(() => client.AssetsAsync());

        Assert.StartsWith("Could not reach Coino Live: ", e.Message);
        Assert.Equal(0, e.Status);
    }

    [Fact]
    public void IdempotencyKeysAre32RandomHexCharacters()
    {
        string a = CoinoLiveClient.NewIdempotencyKey();
        string b = CoinoLiveClient.NewIdempotencyKey();

        Assert.Matches("^[0-9a-f]{32}$", a);
        Assert.NotEqual(a, b);
    }
}
