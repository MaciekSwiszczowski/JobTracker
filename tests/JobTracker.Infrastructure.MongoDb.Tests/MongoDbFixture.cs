using Testcontainers.MongoDb;

[assembly: AssemblyFixture(typeof(JobTracker.Infrastructure.MongoDb.Tests.MongoDbFixture))]

namespace JobTracker.Infrastructure.MongoDb.Tests;

/// <summary>
/// Starts one MongoDB container for the whole test assembly and removes it at the end. A test class asks for it in
/// its constructor: <c>public MyTests(MongoDbFixture mongo)</c>.
/// </summary>
/// <remarks>
/// <para>
/// The container runs as a single-node replica set, because MongoDB transactions only work on a replica set (or a
/// sharded cluster), never on a standalone server:
/// https://www.mongodb.com/docs/manual/core/transactions/
/// </para>
/// <para>
/// Docker is a stated prerequisite, not a discovered one (docs/conventions/test-antipatterns.md §6): when the
/// container cannot start, every test here fails with a message naming Docker, rather than being skipped and
/// turning the whole data-layer suite into a vacuous pass.
/// </para>
/// <para>
/// One server is shared by every test class, so a test class must not share a database with another one. Give each
/// class its own database name (a new <see cref="Guid"/> is enough) - see §8.
/// </para>
/// </remarks>
public sealed class MongoDbFixture : IAsyncLifetime
{
    // mongod 8 refuses to start on Linux kernels 6.19+ (GitHub's runners are on 7.0), because TCMalloc's use of
    // restartable sequences breaks there: https://jira.mongodb.org/browse/SERVER-121912 tracks the incompatibility.
    // Testcontainers then reports a missing mongosh, not the crash.
    // The image sets glibc.pthread.rseq=0 to hand rseq to TCMalloc; =1, glibc's default, keeps it in glibc and
    // TCMalloc falls back to per-thread caches. Revisit when a mongo:8.0 release fixes this upstream.
    private readonly MongoDbContainer _container = new MongoDbBuilder("mongo:8.0")
        .WithReplicaSet()
        .WithEnvironment("GLIBC_TUNABLES", "glibc.pthread.rseq=1")
        .Build();

    /// <summary>The connection string of the running container.</summary>
    public string ConnectionString => _container.GetConnectionString();

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        try
        {
            await _container.StartAsync(TestContext.Current.CancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new InvalidOperationException(
                "The MongoDB test container did not start. These tests need Docker running on this machine (Docker "
                + "Desktop on Windows); if it is running, the container itself failed. Cause: " + exception.Message,
                exception);
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
