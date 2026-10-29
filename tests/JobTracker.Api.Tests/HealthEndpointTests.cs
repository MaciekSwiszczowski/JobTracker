using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace JobTracker.Api.Tests;

/// <summary>
/// Pins the one endpoint the UI and the skill rely on before any other exists: the liveness check.
/// </summary>
public sealed class HealthEndpointTests
{
    // Trigger: the skill checks whether the service is running before it posts an application.
    // Behavior: a running service answers the health check successfully.
    // Assertion: 200 OK with the body {"status":"ok"}.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task Health_WhenTheServiceIsRunning_AnswersOk()
    {
        // Arrange
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("""{"status":"ok"}""");
    }
}
