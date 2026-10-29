namespace JobTracker.Architecture.Tests;

/// <summary>
/// A throwaway directory under <c>%TEMP%\JobTracker_Tests</c> for the tests that need a real git repository on
/// disk.
/// </summary>
/// <remarks>
/// One instance owns one uniquely named root and deletes it on <see cref="Dispose"/>. A git repository fills its
/// own <c>.git</c> directory with read-only objects, so every delete attempt first clears the read-only attribute
/// from every entry — <c>Directory.Delete</c> otherwise refuses a read-only file outright. If the root still cannot
/// be removed after a few attempts, the leftover path is written to the test's output rather than swallowed.
/// Nothing asserts on the cleanup, so the retry cannot make a test pass or fail; that is why the short waits
/// between attempts are not the wall-clock synchronisation banned in <c>docs/conventions/test-antipatterns.md</c>
/// §1. It lives here rather than in <c>JobTracker.TestSupport</c> because this is the only project that needs it
/// (§3).
/// </remarks>
internal sealed class TempDirectoryFixture : IDisposable
{
    private const int DeleteAttempts = 4;

    private static readonly TimeSpan DelayBetweenDeleteAttempts = TimeSpan.FromMilliseconds(50);

    public TempDirectoryFixture()
    {
        RootPath = Path.Combine(Path.GetTempPath(), "JobTracker_Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootPath);
    }

    /// <summary>Full path of this instance's root directory.</summary>
    public string RootPath { get; }

    /// <summary>
    /// Disposes <paramref name="directory"/> from inside a <c>catch</c> block, without letting a failure here
    /// replace the exception already being thrown for a different reason.
    /// </summary>
    /// <remarks>
    /// A fixture that builds on top of a <see cref="TempDirectoryFixture"/> and fails partway through is already
    /// throwing the exception that explains why. <see cref="Dispose"/> reports rather than throws for the retryable
    /// case, but an exception outside <see cref="IOException"/> or <see cref="UnauthorizedAccessException"/> still
    /// escapes it — and if it did, it would become the exception the run reports, burying the setup failure.
    /// </remarks>
    public static void DisposeAfterFailedSetup(TempDirectoryFixture directory)
    {
        try
        {
            directory.Dispose();
        }
        // Deliberately not narrowed: anything at all coming out of cleanup is less informative than the setup
        // failure it would displace, so there is no exception worth letting past.
        catch (Exception cleanupFailure)
        {
            TestContext.Current.TestOutputHelper?.WriteLine(
                $"Temp directory could not be removed after fixture setup failed and is left behind: "
                + $"{directory.RootPath} ({cleanupFailure.GetType().Name}: {cleanupFailure.Message})");
        }
    }

    public void Dispose()
    {
        var failure = TryDeleteRoot();
        if (failure is null)
        {
            return;
        }

        // The test itself is over and its verdict stands; a leftover directory is reported, never thrown.
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"Temp directory could not be removed and is left behind: {RootPath} ({failure.GetType().Name}: {failure.Message})");
    }

    private static void ClearReadOnlyAttributes(string root)
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories))
        {
            var attributes = File.GetAttributes(path);
            if (attributes.HasFlag(FileAttributes.ReadOnly))
            {
                File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
            }
        }
    }

    /// <returns>The last failure, or <c>null</c> once the root is gone.</returns>
    private Exception? TryDeleteRoot()
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                ClearReadOnlyAttributes(RootPath);
                Directory.Delete(RootPath, true);
                return null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt == DeleteAttempts)
                {
                    return ex;
                }

                // A watcher or a handle git briefly held is usually released a moment later.
                Thread.Sleep(DelayBetweenDeleteAttempts);
            }
        }
    }
}
