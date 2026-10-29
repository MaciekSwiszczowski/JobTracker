namespace JobTracker.Infrastructure.MongoDb.Tests;

/// <summary>
/// Proves Docker, Testcontainers and the driver work together on this machine. Keep it: when this test fails and
/// the others fail with it, the problem is the environment, not the data layer.
/// </summary>
public sealed class MongoDbFixtureTests(MongoDbFixture mongo)
{
    // Trigger: the integration tests start on a machine with Docker running.
    // Behavior: a MongoDB server is up and accepts commands from the C# driver.
    // Assertion: the server answers the ping command with ok = 1.
    [Fact(Timeout = TestTimeouts.ContainerMilliseconds)]
    public async Task Ping_WhenTheContainerIsRunning_ReturnsOk()
    {
        // ping is the documented liveness check: https://www.mongodb.com/docs/manual/reference/command/ping/
        // Arrange
        var admin = new MongoClient(mongo.ConnectionString).GetDatabase("admin");

        // Act
        var reply = await admin.RunCommandAsync<BsonDocument>(
            new BsonDocument("ping", 1),
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        reply["ok"].ToDouble().ShouldBe(1.0);
    }
}
