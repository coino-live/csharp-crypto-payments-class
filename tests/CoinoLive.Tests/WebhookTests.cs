using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace CoinoLive.Tests;

public class WebhookTests
{
    private const string Secret = "whsec_test";
    private const string Body = """{"id":"01evt","event":"payment.paid","created_at":"2026-10-31T10:00:00+00:00","data":{"id":"01pay","status":"paid"}}""";

    // Computed with sdk/php/src/Webhook.php's algorithm: hash_hmac('sha256', $t . '.' . $payload, $secret).
    private const long VectorTime = 1793440800;
    private const string VectorHeader = "t=1793440800,v1=1f18c280b7fcdf81894537b2cc31974bb16e0290ce1daed0262b6355f8374819";
    // "description" holds JSON escapes (backslash, u, 00e9 …), "note" the same text as raw UTF-8 bytes.
    private const string Backslash = "\\";
    private const string UnicodeBody =
        """{"id":"01evt2","event":"payment.paid","created_at":"2026-10-31T10:00:00+00:00","data":{"id":"01pay","description":"Caf"""
        + Backslash + "u00e9 " + Backslash + """u20ac","note":"Café €"}}""";
    private const string UnicodeHeader = "t=1793440800,v1=779c01f516e6a30ff4a77d880f5059842ce5ec0d84bede82a7a8802eb926264a";

    private static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static string Sign(long t, string body = Body, string secret = Secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return "t=" + t + ",v1=" + Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(t + "." + body))).ToLowerInvariant();
    }

    [Fact]
    public void ThePhpVectorVerifies()
    {
        var evt = Webhook.Verify(Encoding.UTF8.GetBytes(Body), VectorHeader, Secret, 300, VectorTime + 10);

        Assert.Equal("01evt", evt.Id);
        Assert.Equal("payment.paid", evt.Event);
        Assert.Equal("01pay", evt.Data.GetProperty("id").GetString());
    }

    [Fact]
    public void ThePhpVectorWithNonAsciiTextVerifiesAsAStringAndAsBytes()
    {
        var fromString = Webhook.Verify(UnicodeBody, UnicodeHeader, Secret, int.MaxValue);
        var fromBytes = Webhook.Verify(Encoding.UTF8.GetBytes(UnicodeBody), UnicodeHeader, Secret, int.MaxValue);

        Assert.Equal("Café €", fromString.Data.GetProperty("description").GetString());
        Assert.Equal("Café €", fromBytes.Data.GetProperty("note").GetString());
    }

    [Fact]
    public void OurSignatureMatchesThePhpVector()
    {
        Assert.Equal(
            "7b226964223a22303165767432222c226576656e74223a227061796d656e742e70616964222c22637265617465645f6174223a22323032362d31302d33315431303a30303a30302b30303a3030222c2264617461223a7b226964223a223031706179222c226465736372697074696f6e223a224361665c7530306539205c7532306163222c226e6f7465223a22436166c3a920e282ac227d7d",
            Convert.ToHexString(Encoding.UTF8.GetBytes(UnicodeBody)).ToLowerInvariant());
        Assert.Equal(VectorHeader, Sign(VectorTime));
        Assert.Equal(UnicodeHeader, Sign(VectorTime, UnicodeBody));
    }

    [Fact]
    public void ThePhpVectorIsTooOldToday()
    {
        var e = Assert.Throws<SignatureVerificationException>(() => Webhook.Verify(Body, VectorHeader, Secret));

        Assert.Equal("The signature is too old.", e.Message);
    }

    [Fact]
    public void AGoodSignatureReturnsTheEvent()
    {
        var evt = Webhook.Verify(Body, Sign(Now), Secret);

        Assert.Equal("01evt", evt.Id);
        Assert.Equal("payment.paid", evt.Event);
        Assert.Equal(new DateTimeOffset(2026, 10, 31, 10, 0, 0, TimeSpan.Zero), evt.CreatedAt);
        Assert.Equal("01pay", evt.Data.GetProperty("id").GetString());
        Assert.Equal("01evt", evt.Json.GetProperty("id").GetString());
    }

    [Fact]
    public void AByteBodyWorks()
    {
        var evt = Webhook.Verify(Encoding.UTF8.GetBytes(Body), Sign(Now), Secret);

        Assert.Equal("payment.paid", evt.Event);
    }

    [Fact]
    public void TheDataReadsIntoALibraryType()
    {
        var evt = Webhook.Verify(Body, Sign(Now), Secret);

        var payment = evt.DataAs<Payment>();

        Assert.Equal("01pay", payment!.Id);
        Assert.Equal(PaymentStatus.Paid, payment.Status);
    }

    [Fact]
    public void SpacesAroundThePartsAreAllowed()
    {
        string header = Sign(Now).Replace(",", " , ");

        Assert.Equal("01evt", Webhook.Verify(Body, " " + header + " ", Secret).Id);
    }

    [Fact]
    public void UnknownPartsAreIgnored()
    {
        Assert.Equal("01evt", Webhook.Verify(Body, Sign(Now) + ",v0=abc,x", Secret).Id);
    }

    [Fact]
    public void AWrongSecretIsRefused()
    {
        var e = Assert.Throws<SignatureVerificationException>(() => Webhook.Verify(Body, Sign(Now), "other"));

        Assert.Equal("The signature does not match.", e.Message);
    }

    [Fact]
    public void AChangedBodyIsRefused()
    {
        var e = Assert.Throws<SignatureVerificationException>(() => Webhook.Verify(Body.Replace("paid", "open"), Sign(Now), Secret));

        Assert.Equal("The signature does not match.", e.Message);
    }

    [Fact]
    public void UpperCaseHexIsRefusedLikeThePhpLibrary()
    {
        string header = "t=" + VectorTime + ",v1=" + VectorHeader.Split("v1=")[1].ToUpperInvariant();

        Assert.Throws<SignatureVerificationException>(() => Webhook.Verify(Encoding.UTF8.GetBytes(Body), header, Secret, 300, VectorTime));
    }

    [Fact]
    public void AnOldTimestampIsRefused()
    {
        var e = Assert.Throws<SignatureVerificationException>(() => Webhook.Verify(Body, Sign(Now - 301), Secret));

        Assert.Equal("The signature is too old.", e.Message);
    }

    [Fact]
    public void AFutureTimestampIsRefused()
    {
        Assert.Throws<SignatureVerificationException>(() => Webhook.Verify(Body, Sign(Now + 301), Secret));
    }

    [Fact]
    public void TheToleranceIsInclusiveAndConfigurable()
    {
        var bytes = Encoding.UTF8.GetBytes(Body);

        Assert.Equal("01evt", Webhook.Verify(bytes, VectorHeader, Secret, 300, VectorTime + 300).Id);
        Assert.Throws<SignatureVerificationException>(() => Webhook.Verify(bytes, VectorHeader, Secret, 300, VectorTime + 301));
        Assert.Equal("01evt", Webhook.Verify(bytes, VectorHeader, Secret, 600, VectorTime + 600).Id);
        Assert.Throws<SignatureVerificationException>(() => Webhook.Verify(bytes, VectorHeader, Secret, 10, VectorTime - 11));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("t=abc,v1=00")]
    [InlineData("t=1793440800")]
    [InlineData("v1=1f18c280b7fcdf81894537b2cc31974bb16e0290ce1daed0262b6355f8374819")]
    [InlineData("t=,v1=00")]
    [InlineData("t=-5,v1=00")]
    [InlineData("t=1793440800,v1=")]
    [InlineData("garbage")]
    public void AMissingOrMalformedHeaderIsRefused(string? header)
    {
        var e = Assert.Throws<SignatureVerificationException>(() => Webhook.Verify(Body, header, Secret));

        Assert.Equal("The X-Coino-Signature header is missing or malformed.", e.Message);
    }

    [Fact]
    public void AHugeTimestampIsTooOld()
    {
        var e = Assert.Throws<SignatureVerificationException>(() => Webhook.Verify(Body, "t=99999999999999999999999,v1=00", Secret));

        Assert.Equal("The signature is too old.", e.Message);
    }

    [Fact]
    public void ANullBodyIsRefused()
    {
        Assert.Throws<SignatureVerificationException>(() => Webhook.Verify((string)null!, Sign(Now), Secret));
        Assert.Throws<SignatureVerificationException>(() => Webhook.Verify((byte[])null!, Sign(Now), Secret));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("\"text\"")]
    [InlineData("")]
    public void ASignedBodyThatIsNotAJsonObjectIsRefused(string body)
    {
        var e = Assert.Throws<SignatureVerificationException>(() => Webhook.Verify(body, Sign(Now, body), Secret));

        Assert.Equal("The payload is not JSON.", e.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void AnEmptySecretIsAnArgumentError(string? secret)
    {
        Assert.Throws<ArgumentException>(() => Webhook.Verify(Body, Sign(Now), secret!));
    }

    [Fact]
    public void TheHeaderNameIsExposed()
    {
        Assert.Equal("X-Coino-Signature", Webhook.SignatureHeader);
    }
}
