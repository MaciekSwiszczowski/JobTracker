using System.Net;
using System.Text;

namespace JobTracker.UI.Tests;

/// <summary>
/// Stands in for the network under <see cref="HttpJobTrackerClient"/>: answers every request with one canned
/// status and body, and keeps what was sent. A real server would make these tests need a port and a process.
/// </summary>
internal sealed class StubHttpMessageHandler(HttpStatusCode status, string body) : HttpMessageHandler
{
    /// <summary>The method and path of the last request.</summary>
    public string? LastRequest { get; private set; }

    /// <summary>The body of the last request, read before the client disposes it; null when it had none.</summary>
    public string? LastRequestBody { get; private set; }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = $"{request.Method} {request.RequestUri?.PathAndQuery}";
        LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}
