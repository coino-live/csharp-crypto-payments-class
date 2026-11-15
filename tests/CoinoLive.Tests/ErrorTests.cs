using System.Net;
using Xunit;

namespace CoinoLive.Tests;

public class ErrorTests
{
    private static async Task<CoinoLiveException> Fail(int status, string body, string contentType = "application/json")
    {
        var handler = new FakeHandler().Always((_, _) => Task.FromResult(FakeHandler.Response(status, body, contentType)));
        using var client = Fixtures.Client(handler, maxRetries: 0);
        return await Assert.ThrowsAnyAsync<CoinoLiveException>(() => client.Payments.RetrieveAsync("01pay"));
    }

    [Fact]
    public async Task Status401IsAnAuthenticationException()
    {
        var e = await Fail(401, """{"message":"Invalid API key."}""");

        Assert.IsType<AuthenticationException>(e);
        Assert.Equal(401, e.Status);
        Assert.Equal("Invalid API key.", e.Message);
    }

    [Fact]
    public async Task Status404WithJsonIsANotFoundException()
    {
        var e = await Fail(404, """{"message":"Payment not found."}""");

        Assert.IsType<NotFoundException>(e);
        Assert.Equal(404, e.Status);
        Assert.Equal("Payment not found.", e.Message);
    }

    [Fact]
    public async Task Status422IsAValidationExceptionWithFieldErrors()
    {
        var e = await Fail(422, """
            {"message":"amount must be a decimal string with at most 2 decimals, e.g. \"25.00\".",
             "errors":{"amount":["amount must be a decimal string with at most 2 decimals, e.g. \"25.00\"."],"currency":["bad","worse"]}}
            """);

        var validation = Assert.IsType<ValidationException>(e);
        Assert.Equal(422, validation.Status);
        Assert.StartsWith("amount must be", validation.Message);
        Assert.Equal(new[] { "amount", "currency" }, validation.Errors.Keys.OrderBy(k => k));
        Assert.Equal(new[] { "bad", "worse" }, validation.Errors["currency"]);
    }

    [Fact]
    public async Task Status422WithoutFieldsHasNoErrors()
    {
        var e = await Fail(422, """{"message":"Payments with a deposit cannot be cancelled."}""");

        Assert.IsType<ValidationException>(e);
        Assert.Empty(e.Errors);
    }

    [Fact]
    public async Task Status429IsARateLimitException()
    {
        var e = await Fail(429, """{"message":"Too Many Attempts."}""");

        Assert.IsType<RateLimitException>(e);
        Assert.Equal(429, e.Status);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(403)]
    [InlineData(409)]
    [InlineData(500)]
    [InlineData(503)]
    public async Task OtherStatusesAreTheParentException(int status)
    {
        var e = await Fail(status, """{"message":"API withdrawals are turned off.","errors":{"ip":["not allowed"]}}""");

        Assert.Equal(typeof(CoinoLiveException), e.GetType());
        Assert.Equal(status, e.Status);
        Assert.Equal("API withdrawals are turned off.", e.Message);
        Assert.Equal(new[] { "not allowed" }, e.Errors["ip"]);
    }

    [Fact]
    public async Task AnAnswerWithoutAMessageSaysTheStatus()
    {
        var e = await Fail(500, "<html>Server Error</html>", "text/html");

        Assert.Equal(typeof(CoinoLiveException), e.GetType());
        Assert.Equal("HTTP 500", e.Message);
        Assert.Empty(e.Errors);
    }

    [Fact]
    public async Task AnEmptyMessageSaysTheStatus()
    {
        var e = await Fail(401, """{"message":""}""");

        Assert.IsType<AuthenticationException>(e);
        Assert.Equal("HTTP 401", e.Message);
    }

    [Fact]
    public async Task AnHtml404SaysThereIsNoApiAtTheBaseUrl()
    {
        var e = await Fail(404, "<html>not here</html>", "text/html");

        Assert.Equal(typeof(CoinoLiveException), e.GetType());
        Assert.Equal(404, e.Status);
        Assert.Equal("There is no Coino Live API at https://coino.live/api/v2 (HTTP 404). Check BaseUrl.", e.Message);
    }

    [Theory]
    [InlineData(301)]
    [InlineData(302)]
    [InlineData(307)]
    public async Task ARedirectSaysThereIsNoApiAtTheBaseUrl(int status)
    {
        var handler = new FakeHandler().Always((_, _) =>
        {
            var response = new HttpResponseMessage((HttpStatusCode)status);
            response.Headers.Location = new Uri("https://example.test/");
            return Task.FromResult(response);
        });
        using var client = Fixtures.Client(handler);

        var e = await Assert.ThrowsAnyAsync<CoinoLiveException>(() => client.AssetsAsync());

        Assert.Equal(typeof(CoinoLiveException), e.GetType());
        Assert.Contains("Check BaseUrl", e.Message);
        Assert.Single(handler.Seen);
    }

    [Fact]
    public async Task ASuccessThatIsNotJsonIsAnError()
    {
        var e = await Fail(200, "<html>Welcome</html>", "text/html");

        Assert.Equal(typeof(CoinoLiveException), e.GetType());
        Assert.Equal(200, e.Status);
        Assert.Equal("HTTP 200", e.Message);
    }

    [Fact]
    public async Task ASuccessWithoutDataIsAnError()
    {
        var e = await Fail(200, """{"ok":true}""");

        Assert.Equal(typeof(CoinoLiveException), e.GetType());
    }

    [Fact]
    public async Task ASuccessOfTheWrongShapeIsAnError()
    {
        var e = await Fail(200, """{"data":{"id":5,"created_at":"not a date"}}""");

        Assert.Equal(typeof(CoinoLiveException), e.GetType());
        Assert.Contains("cannot read", e.Message);
    }

    [Fact]
    public void EveryClientErrorSharesOneParent()
    {
        Assert.True(typeof(CoinoLiveException).IsAssignableFrom(typeof(AuthenticationException)));
        Assert.True(typeof(CoinoLiveException).IsAssignableFrom(typeof(NotFoundException)));
        Assert.True(typeof(CoinoLiveException).IsAssignableFrom(typeof(ValidationException)));
        Assert.True(typeof(CoinoLiveException).IsAssignableFrom(typeof(RateLimitException)));
        Assert.True(typeof(CoinoLiveException).IsAssignableFrom(typeof(ConnectionException)));
        Assert.False(typeof(CoinoLiveException).IsAssignableFrom(typeof(SignatureVerificationException)));
    }
}
