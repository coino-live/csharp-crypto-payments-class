using Xunit;

namespace CoinoLive.Tests;

public class CustodyTests
{
    [Fact]
    public async Task CreatePostsTheUser()
    {
        var handler = new FakeHandler().Then(201, Fixtures.Data(Fixtures.CustodyUser));
        using var client = Fixtures.Client(handler);

        var user = await client.Custody.CreateAsync(new CreateCustodyUserParams
        {
            ExternalId = "user-42",
            Name = "Ada",
            Metadata = new Dictionary<string, object?> { ["plan"] = "pro" },
        });

        var seen = Assert.Single(handler.Seen);
        Assert.Equal("POST", seen.Method);
        Assert.Equal(Fixtures.Base + "/custody/users", seen.Url);
        Assert.Equal("application/json", seen.ContentType);
        Assert.Equal("""{"external_id":"user-42","name":"Ada","metadata":{"plan":"pro"}}""", seen.Body);
        Assert.False(seen.Headers.ContainsKey("Idempotency-Key"));

        Assert.Equal("01usr", user.Id);
        Assert.Equal("user-42", user.ExternalId);
        Assert.Equal("Ada", user.Name);
        Assert.Null(user.Email);
        Assert.Null(user.Metadata);
        Assert.Equal("active", user.Status);
        var address = Assert.Single(user.Addresses);
        Assert.Equal("TAbc", address.Address);
        Assert.Equal(new[] { "tron" }, address.Networks);
        Assert.Equal(new[] { "trx-tron", "usdt-tron" }, address.Assets);
        Assert.Equal("12.5", Assert.Single(user.Balances).Balance);
    }

    [Fact]
    public async Task CreateIsRetriedBecauseTheSameExternalIdReturnsTheSameUser()
    {
        var handler = new FakeHandler()
            .Then(500, """{"message":"Server Error"}""")
            .Then(200, Fixtures.Data(Fixtures.CustodyUser));
        using var client = Fixtures.Client(handler);

        await client.Custody.CreateAsync(new CreateCustodyUserParams { ExternalId = "user-42" });

        Assert.Equal(2, handler.Seen.Count);
        Assert.All(handler.Seen, s => Assert.Equal("""{"external_id":"user-42"}""", s.Body));
    }

    [Fact]
    public async Task RetrieveReadsTheUser()
    {
        var handler = new FakeHandler().Then(200, Fixtures.Data(Fixtures.CustodyUser));
        using var client = Fixtures.Client(handler);

        var user = await client.Custody.RetrieveAsync("01usr");

        Assert.Equal("GET", handler.Seen[0].Method);
        Assert.Equal(Fixtures.Base + "/custody/users/01usr", handler.Seen[0].Url);
        Assert.Equal("01usr", user.Id);
    }

    [Fact]
    public async Task ListSendsTheFilters()
    {
        var handler = new FakeHandler().Then(200, "{\"data\":[" + Fixtures.CustodyUser + "],\"meta\":{\"next_cursor\":null,\"prev_cursor\":null}}");
        using var client = Fixtures.Client(handler);

        var page = await client.Custody.ListAsync(externalId: "user-42", cursor: "c2");

        Assert.Equal(Fixtures.Base + "/custody/users?external_id=user-42&cursor=c2", handler.Seen[0].Url);
        Assert.Equal("01usr", Assert.Single(page.Data).Id);
        Assert.Null(page.Meta.NextCursor);
    }

    [Fact]
    public async Task ListWithoutFiltersHasNoQuery()
    {
        var handler = new FakeHandler().Then(200, """{"data":[],"meta":{"next_cursor":null,"prev_cursor":null}}""");
        using var client = Fixtures.Client(handler);

        await client.Custody.ListAsync();

        Assert.Equal(Fixtures.Base + "/custody/users", handler.Seen[0].Url);
    }

    [Fact]
    public async Task DepositsReadsOnePageWithATopLevelCursor()
    {
        var handler = new FakeHandler()
            .Then(200, """
                {"data":[{"asset":"usdt-tron","amount":"10","network_fee":"0","platform_fee":"0.05","net":"9.95","tx_hash":"abc",
                          "credited_at":"2026-10-31T10:05:00+00:00"}],"next_cursor":"c2"}
                """)
            .Then(200, """{"data":[],"next_cursor":null}""");
        using var client = Fixtures.Client(handler);

        var first = await client.Custody.DepositsAsync("01usr");
        var second = await client.Custody.DepositsAsync("01usr", first.NextCursor);

        Assert.Equal(Fixtures.Base + "/custody/users/01usr/deposits", handler.Seen[0].Url);
        Assert.Equal(Fixtures.Base + "/custody/users/01usr/deposits?cursor=c2", handler.Seen[1].Url);
        var deposit = Assert.Single(first.Data);
        Assert.Equal("9.95", deposit.Net);
        Assert.Equal("abc", deposit.TxHash);
        Assert.Equal("0.05", deposit.PlatformFee);
        Assert.Equal("c2", first.NextCursor);
        Assert.Empty(second.Data);
        Assert.Null(second.NextCursor);
    }

    [Fact]
    public async Task TransferSendsTheAssetAndOnlyAGivenAmount()
    {
        const string transfer = """
            {"data":{"id":"01trf","asset":"usdt-tron","amount":"12.5","custody_user":"01usr","reference":null,"created_at":"2026-10-31T10:00:00+00:00"}}
            """;
        var handler = new FakeHandler().Then(201, transfer).Then(201, transfer);
        using var client = Fixtures.Client(handler);

        var all = await client.Custody.TransferAsync("01usr", "usdt-tron");
        await client.Custody.TransferAsync("01usr", "usdt-tron", "2.5");

        Assert.Equal("POST", handler.Seen[0].Method);
        Assert.Equal(Fixtures.Base + "/custody/users/01usr/transfers", handler.Seen[0].Url);
        Assert.Equal("""{"asset":"usdt-tron"}""", handler.Seen[0].Body);
        Assert.Equal("""{"asset":"usdt-tron","amount":"2.5"}""", handler.Seen[1].Body);
        Assert.Equal("01trf", all.Id);
        Assert.Equal("12.5", all.Amount);
        Assert.Equal("01usr", all.CustodyUser);
    }

    [Fact]
    public async Task TransferIsNotRetried()
    {
        var handler = new FakeHandler().Always(503, """{"message":"Busy"}""");
        using var client = Fixtures.Client(handler);

        var e = await Assert.ThrowsAsync<CoinoLiveException>(() => client.Custody.TransferAsync("01usr", "usdt-tron"));

        Assert.Equal(503, e.Status);
        Assert.Single(handler.Seen);
    }
}

public class WithdrawalsTests
{
    [Fact]
    public async Task CreatePostsTheWithdrawal()
    {
        var handler = new FakeHandler().Then(201, Fixtures.Data(Fixtures.Withdrawal));
        using var client = Fixtures.Client(handler);

        var w = await client.Withdrawals.CreateAsync(new CreateWithdrawalParams
        {
            Asset = "usdt-tron",
            Amount = "50",
            Address = "TDest",
            CustodyUser = "01aaaaaaaaaaaaaaaaaaaaaaaa",
            Reference = "payout-77",
        });

        var seen = Assert.Single(handler.Seen);
        Assert.Equal("POST", seen.Method);
        Assert.Equal(Fixtures.Base + "/withdrawals", seen.Url);
        Assert.Equal(
            """{"asset":"usdt-tron","amount":"50","address":"TDest","custody_user":"01aaaaaaaaaaaaaaaaaaaaaaaa","reference":"payout-77"}""",
            seen.Body);
        Assert.False(seen.Headers.ContainsKey("Idempotency-Key"));
        Assert.Equal("01wdr", w.Id);
        Assert.Equal("pending_review", w.Status);
        Assert.Equal("1", w.Fee);
        Assert.Equal("payout-77", w.Reference);
        Assert.Null(w.TxHash);
        Assert.Null(w.CompletedAt);
    }

    [Fact]
    public async Task CreateWithAReferenceIsRetried()
    {
        var handler = new FakeHandler()
            .Then(503, """{"message":"Busy"}""")
            .Then(200, Fixtures.Data(Fixtures.Withdrawal));
        using var client = Fixtures.Client(handler);

        await client.Withdrawals.CreateAsync(new CreateWithdrawalParams { Asset = "usdt-tron", Amount = "50", Address = "TDest", Reference = "payout-77" });

        Assert.Equal(2, handler.Seen.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task CreateWithoutAReferenceIsNotRetried(string? reference)
    {
        var handler = new FakeHandler().Always(503, """{"message":"Busy"}""");
        using var client = Fixtures.Client(handler);

        await Assert.ThrowsAsync<CoinoLiveException>(() => client.Withdrawals.CreateAsync(
            new CreateWithdrawalParams { Asset = "usdt-tron", Amount = "50", Address = "TDest", Reference = reference }));

        Assert.Single(handler.Seen);
    }

    [Fact]
    public async Task RetrieveReadsTheWithdrawal()
    {
        var handler = new FakeHandler().Then(200, Fixtures.Data(Fixtures.Withdrawal));
        using var client = Fixtures.Client(handler);

        var w = await client.Withdrawals.RetrieveAsync("01wdr");

        Assert.Equal("GET", handler.Seen[0].Method);
        Assert.Equal(Fixtures.Base + "/withdrawals/01wdr", handler.Seen[0].Url);
        Assert.Equal("TDest", w.Address);
        Assert.Equal(new DateTimeOffset(2026, 10, 31, 10, 0, 0, TimeSpan.Zero), w.CreatedAt);
    }
}
