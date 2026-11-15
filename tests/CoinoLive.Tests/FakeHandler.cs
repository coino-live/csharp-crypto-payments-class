using System.Net;
using System.Text;

namespace CoinoLive.Tests;

/// <summary>A request as the fake API saw it.</summary>
public sealed record SeenRequest(
    string Method,
    string Url,
    Dictionary<string, string> Headers,
    string? ContentType,
    string? Body);

/// <summary>An offline stand-in for the API: answers from a list of responders and records every request.</summary>
public sealed class FakeHandler : HttpMessageHandler
{
    private readonly Queue<Func<SeenRequest, CancellationToken, Task<HttpResponseMessage>>> _answers = new();
    private Func<SeenRequest, CancellationToken, Task<HttpResponseMessage>>? _always;

    public List<SeenRequest> Seen { get; } = new();

    /// <summary>The next answer (in order). When the queue is empty, the last "Always" answer is used.</summary>
    public FakeHandler Then(int status, string body)
    {
        _answers.Enqueue((_, _) => Task.FromResult(Response(status, body)));
        return this;
    }

    public FakeHandler Then(Func<SeenRequest, CancellationToken, Task<HttpResponseMessage>> answer)
    {
        _answers.Enqueue(answer);
        return this;
    }

    public FakeHandler ThenThrow(Exception e)
    {
        _answers.Enqueue((_, _) => Task.FromException<HttpResponseMessage>(e));
        return this;
    }

    public FakeHandler Always(int status, string body)
    {
        _always = (_, _) => Task.FromResult(Response(status, body));
        return this;
    }

    public FakeHandler Always(Func<SeenRequest, CancellationToken, Task<HttpResponseMessage>> answer)
    {
        _always = answer;
        return this;
    }

    public static HttpResponseMessage Response(int status, string body, string contentType = "application/json")
    {
        return new HttpResponseMessage((HttpStatusCode)status)
        {
            Content = new StringContent(body, Encoding.UTF8, contentType),
        };
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in request.Headers.NonValidated)
        {
            headers[header.Key] = header.Value.ToString();
        }
        string? body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        var seen = new SeenRequest(
            request.Method.Method,
            request.RequestUri!.AbsoluteUri,
            headers,
            request.Content?.Headers.ContentType?.ToString(),
            body);
        Seen.Add(seen);

        var answer = _answers.Count > 0 ? _answers.Dequeue() : _always
            ?? throw new InvalidOperationException("No answer left for " + seen.Method + " " + seen.Url);
        return await answer(seen, cancellationToken);
    }
}
