namespace JobTracker.Architecture.Tests;

/// <summary>
/// A throwaway git repository with the aliases <see cref="GitTests"/> needs, built through <see cref="Git"/> so that
/// setup is bounded and reports failures the same way the rules do.
/// </summary>
/// <remarks>
/// <para>
/// The aliases exist because a plain git command has no descendants worth killing. <c>spawn-detached</c> is the case
/// that matters: git starts a process that inherits its output pipe, then git and everything between it and that
/// process exit, leaving a survivor no parent-id walk can reach. Both aliases begin with <c>!</c>, so git runs them
/// through the bash that ships with Git for Windows.
/// </para>
/// <para>
/// The repository is built on first use rather than in the constructor, and that is load-bearing rather than
/// tidiness. xUnit creates a class fixture while it initialises the class, which is before it evaluates any test's
/// <see cref="FactAttribute.SkipUnless"/> gate — so setup running here would run on a machine that has no git at
/// all, and <see cref="GitTests.HasGit"/> would never get the chance to skip (docs/conventions/test-antipatterns.md
/// §6). Deferring setup to <see cref="RepositoryPath"/> puts the work behind the gate: a skipped test never asks for
/// the path, so nothing ever starts git.
/// </para>
/// </remarks>
public sealed class GitTestsFixture : IDisposable
{
    /// <summary>File the <c>spawn-detached</c> alias writes the surviving process id into.</summary>
    internal const string OrphanProcessIdFileName = "orphan.pid";

    /// <summary>Alias that sleeps for far longer than any test waits, without leaving anything behind.</summary>
    internal const string SleepAlias = "sleep-forever";

    /// <summary>Alias that starts a process outliving git, then exits.</summary>
    internal const string SpawnDetachedAlias = "spawn-detached";

    // Long enough that the started process is still there when a test looks for it, short enough that a leak during
    // development clears itself rather than accumulating across runs.
    private const int SleepSeconds = 120;

    // ping waits a second between echoes, so it runs for one second less than the count it is given.
    private const int OrphanPingCount = SleepSeconds + 1;

    // git init and two git config calls, on a directory that already exists.
    private const int SetupTimeoutMilliseconds = 20_000;

    // Lazy caches the failure as well as the value, so four tests on a machine that cannot run git report the one
    // setup failure rather than attempting setup four times over.
    private readonly Lazy<TempDirectoryFixture> _directory = new(CreateRepository);

    /// <summary>Full path of the repository the aliases are configured in, built on first read.</summary>
    internal string RepositoryPath => _directory.Value.RootPath;

    public void Dispose()
    {
        // Nothing was built if no test got past its skip gate, and asking for the value here would build it.
        if (_directory.IsValueCreated)
        {
            _directory.Value.Dispose();
        }
    }

    /// <summary>
    /// Builds the <c>spawn-detached</c> alias: start a process that outlives git, and record the Windows process id
    /// it can be found by.
    /// </summary>
    /// <param name="shellSafeRoot">The repository root with forward slashes, for the redirection target.</param>
    /// <remarks>
    /// <para>Three details carry the test:</para>
    /// <list type="bullet">
    /// <item>
    /// The background job has to be a native Windows program. bash signals its own MSYS children with SIGHUP as it
    /// exits, and an MSYS <c>sleep</c> can be killed that way; <c>ping</c> is started through CreateProcess and never
    /// sees the signal.
    /// </item>
    /// <item>
    /// Its output is deliberately not redirected. Inheriting git's redirected standard output and error is the whole
    /// point — the survivor holds both open after git has exited, and that is what leaves a pipe read waiting for
    /// ever.
    /// </item>
    /// <item>
    /// <c>$!</c> is the job's MSYS process id, which neither a job object nor
    /// <see cref="System.Diagnostics.Process.GetProcessById(int)"/> understands. <c>/proc/&lt;pid&gt;/winpid</c> is
    /// the MSYS mapping to the Windows id, and it can be read as soon as bash has forked.
    /// </item>
    /// </list>
    /// </remarks>
    private static string SpawnDetachedCommand(string shellSafeRoot) =>
        $"!f() {{ ping -n {OrphanPingCount} 127.0.0.1 & "
        + $"cat /proc/$!/winpid > \"{shellSafeRoot}/{OrphanProcessIdFileName}\"; }}; f";

    private static TempDirectoryFixture CreateRepository()
    {
        var directory = new TempDirectoryFixture();

        try
        {
            // git aliases beginning with ! run through sh, which reads a backslash as an escape character.
            var shellSafeRoot = directory.RootPath.Replace('\\', '/');

            RunSetup(directory, "init");
            RunSetup(directory, "config", $"alias.{SleepAlias}", $"!sleep {SleepSeconds}");
            RunSetup(directory, "config", $"alias.{SpawnDetachedAlias}", SpawnDetachedCommand(shellSafeRoot));

            return directory;
        }
        catch
        {
            // Dispose never runs for a value Lazy did not publish, so a half-built repository would be left on disk
            // for every run on a machine where setup cannot succeed.
            TempDirectoryFixture.DisposeAfterFailedSetup(directory);
            throw;
        }
    }

    private static void RunSetup(TempDirectoryFixture directory, params string[] arguments)
    {
        // CancellationToken.None: fixture setup has no test to be cancelled with, and the setup timeout bounds it.
        Git.Run(directory.RootPath, SetupTimeoutMilliseconds, CancellationToken.None, [0], arguments);
    }
}
