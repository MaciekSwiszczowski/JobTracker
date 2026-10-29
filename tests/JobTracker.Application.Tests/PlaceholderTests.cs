namespace JobTracker.Application.Tests;

/// <summary>
/// Proves the test project is wired up (xUnit v3, Shouldly, the runner). Delete this file when the first real
/// application test lands - a test project with no tests fails the run, which is why it exists until then.
/// </summary>
public sealed class PlaceholderTests
{
    // Trigger: the solution is built and the test run starts, before any application code exists.
    // Behavior: the runner finds and runs this project.
    // Assertion: a literal true is true.
    [Fact]
    public void TestProject_WhenRun_IsDiscovered()
    {
        // Assert
        true.ShouldBeTrue();
    }
}
