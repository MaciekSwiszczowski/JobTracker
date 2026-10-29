namespace JobTracker.TestSupport;

/// <summary>
/// The ceiling a test passes to <c>[Fact(Timeout = …)]</c> when it awaits, does real I/O, talks to MongoDB, or
/// waits on an event or another thread. One value shared across every test project.
/// </summary>
public static class TestTimeouts
{
    /// <summary>Milliseconds. The default ceiling for a test that can wait.</summary>
    public const int DefaultMilliseconds = 30_000;

    /// <summary>
    /// Milliseconds. For the MongoDB integration tests only: the first test in a run also pays for pulling the
    /// image and starting the container.
    /// </summary>
    public const int ContainerMilliseconds = 120_000;
}
