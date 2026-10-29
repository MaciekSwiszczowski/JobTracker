using System.Net;

namespace JobTracker.UI.Tests;

/// <summary>
/// Pins the HTTP side of the UI: which routes it calls, what it sends, how it reads answers, and how it turns a
/// refusal into a message the banner can show.
/// </summary>
public sealed class HttpJobTrackerClientTests
{
    // Trigger: the app starts and the service answers the list request.
    // Behavior: the client calls GET /applications and reads every row.
    // Assertion: the request line, and the one row with its literal values.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task GetApplicationsAsync_WhenTheServiceAnswers_ReadsTheRows()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK,
            """[{ "id": "a1", "company": "Contoso", "title": "Developer", "appliedOn": "2026-10-20", "status": "open", "lastEventAt": "2026-10-22T14:30:00+02:00" }]""");
        var client = CreateClient(handler);

        // Act
        var rows = await client.GetApplicationsAsync(TestContext.Current.CancellationToken);

        // Assert
        handler.LastRequest.ShouldBe("GET /applications");
        rows.ShouldBe([new ApplicationSummary("a1", "Contoso", "Developer", new DateOnly(2026, 10, 20),
            ApplicationStatus.Open, new DateTimeOffset(2026, 10, 22, 14, 30, 0, TimeSpan.FromHours(2)))]);
    }

    // Trigger: the user closes application a1 as rejected.
    // Behavior: the client posts the closure to that application's close route, in the documented shape.
    // Assertion: the request line and the exact JSON body.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task CloseApplicationAsync_WhenCalled_PostsTheClosure()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.NoContent, "");
        var client = CreateClient(handler);

        // Act
        await client.CloseApplicationAsync("a1", new Closure(new DateOnly(2026, 10, 28), "Rejected"),
            TestContext.Current.CancellationToken);

        // Assert
        handler.LastRequest.ShouldBe("POST /applications/a1/close");
        handler.LastRequestBody.ShouldBe("""{"closedOn":"2026-10-28","reason":"Rejected"}""");
    }

    // Trigger: the user opens an application, and the service refuses, with or without problem details.
    // Behavior: the client throws with the service's own sentence when there is one, a generic one otherwise.
    // Assertion: the exception's status and message are the row's literals.
    [Theory(Timeout = TestTimeouts.DefaultMilliseconds)]
    [InlineData(HttpStatusCode.NotFound, """{ "title": "Not Found", "detail": "There is no application a1." }""", "There is no application a1.")]
    [InlineData(HttpStatusCode.Conflict, """{ "title": "The application is already closed." }""", "The application is already closed.")]
    [InlineData(HttpStatusCode.InternalServerError, "oops", "The JobTracker service answered 500 Internal Server Error.")]
    public async Task GetApplicationAsync_WhenTheServiceRefuses_ThrowsWithItsReason(
        HttpStatusCode status, string body, string expectedMessage)
    {
        // Arrange
        var client = CreateClient(new StubHttpMessageHandler(status, body));

        // Assert
        var refused = await Should.ThrowAsync<HttpRequestException>(
            client.GetApplicationAsync("a1", TestContext.Current.CancellationToken));
        refused.StatusCode.ShouldBe(status);
        refused.Message.ShouldBe(expectedMessage);
    }

    private static HttpJobTrackerClient CreateClient(StubHttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5080") });
}
