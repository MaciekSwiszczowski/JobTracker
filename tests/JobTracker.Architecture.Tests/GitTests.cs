using System.Diagnostics;
using System.Globalization;
using xRetry.v3;

namespace JobTracker.Architecture.Tests;

/// <summary>
/// Rules about <see cref="Git"/> itself: a git command must not be able to hang the suite, and nothing it starts may
/// outlive it.
/// </summary>
/// <remarks>
/// These tests start real processes, so they are gated on the same working tree the other git rules need. They are
/// the only place the cleanup guarantee is checked — everywhere else git exits on its own and the job object never
/// has to do anything.
/// </remarks>
public sealed class GitTests : IClassFixture<GitTestsFixture>
{
    private const string SkipReason = "Needs Windows, git on PATH and a real working tree.";

    // Attempts for Run_WhenTheRunIsStopped, the one test here that retries; see the note on that method.
    private const int StoppedRunAttempts = 3;

    // Bound for the two commands that are meant to outlast it, so it is also how long those tests take. Shorter than
    // the attribute's TestTimeouts.DefaultMilliseconds on purpose: the command deadline is the thing under test, and
    // it has to expire well inside the deadline that fails the test. It also has to outlast the spawn-detached chain
    // — git, bash and the background ping, about 110 ms measured — because a survivor whose ancestors are still alive
    // is one Process.Kill can reach, so killing that would prove nothing.
    private const int HangingCommandTimeoutMilliseconds = 2_000;

    // Bound for the one command that is meant to finish, and the same bound Git gives an ordinary rule query.
    // Generous against the work, because the number is not part of what that test checks and every test in this
    // assembly runs in parallel, so a tight bound would report a busy machine as a git failure.
    private const int CompletingCommandTimeoutMilliseconds = 15_000;

    private readonly GitTestsFixture _fixture;

    public GitTests(GitTestsFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>Gate for <see cref="FactAttribute.SkipUnless"/>: a Windows machine that can run git at all.</summary>
    public static bool HasGit => Git.IsWorkingTreeAvailable;

    // Trigger: a git command starts a background process that inherits git's output pipe — a hook or an alias that
    //   launches something and returns — and git exits while that process keeps running.
    // Behavior: the helper gives up on its own timeout instead of waiting for a pipe nobody will close, and kills the
    //   survivor even though every process between it and git has already exited.
    // Assertion: the process id in orphan.pid is no longer running once the call returns, and the failure reads
    //   exactly "git spawn-detached (PID nnn) did not finish in 2000 ms."
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds, Skip = SkipReason, SkipUnless = nameof(HasGit))]
    public async Task Run_WhenBackgroundDescendantOutlivesGit_KillsItBeforeReturning()
    {
        // Arrange
        var runnerToken = TestContext.Current.CancellationToken;
        var orphanProcessIdFile = Path.Combine(_fixture.RepositoryPath, GitTestsFixture.OrphanProcessIdFileName);
        File.Delete(orphanProcessIdFile);

        // Assert
        var failure = await Should.ThrowAsync<TimeoutException>(
            // A statement lambda, not an expression one: that is what puts Task.Run on its Action overload and so
            // hands Should.ThrowAsync the Task it takes. Written `() => Git.Run(…)` the overload resolves to
            // Func<string>, and AsyncFixer rejects the silently dropped result. The discard says the same thing to a
            // reader: the exception is what this test reads, not the output.
            () => Task.Run(
                () =>
                {
                    _ = Git.Run(
                        _fixture.RepositoryPath,
                        HangingCommandTimeoutMilliseconds,
                        runnerToken,
                        [0],
                        GitTestsFixture.SpawnDetachedAlias);
                },
                runnerToken));

        // Reading the file also proves the alias got far enough to start the process; if it had not, there would be
        // nothing to assert about and a green test would mean nothing.
        var orphanProcessId = int.Parse(
            await File.ReadAllTextAsync(orphanProcessIdFile, runnerToken),
            CultureInfo.InvariantCulture);
        IsRunning(orphanProcessId).ShouldBeFalse(
            $"The process started by {GitTestsFixture.SpawnDetachedAlias} (PID {orphanProcessId}) outlived the git "
            + "command that created it, so closing the job object did not reach it.");
        failure.Message.ShouldMatch(
            $@"^git {GitTestsFixture.SpawnDetachedAlias} \(PID \d+\) did not finish in {HangingCommandTimeoutMilliseconds} ms\.$");
    }

    // Trigger: the test run is stopped from the runner while a git command is still going.
    // Behavior: the command is abandoned and reported as a cancellation, so a stopped run is not mistaken for a git
    //   command that hangs.
    // Assertion: an OperationCanceledException reading exactly
    //   "git sleep-forever (PID nnn) was cancelled by the test runner."
    //
    // [RetryFact]: a rare, non-recurring race in Git's own cleanup, not a defect in what this test checks. The !sleep
    // alias runs through sh, and sh or sleep can be created in the window before the job object claims git — the
    // "known gap" Git documents as deliberately accepted. When they escape the job they hold the inherited output pipe
    // past the drain grace, so Git throws TimeoutException and hides the OperationCanceledException a stopped run
    // should report. The escape does not repeat on a rerun. Each attempt is bounded by the three-second drain grace,
    // not by Timeout — the token is already cancelled — so three attempts stay well inside the per-test bound (§10).
    [RetryFact(StoppedRunAttempts, Timeout = TestTimeouts.DefaultMilliseconds, Skip = SkipReason, SkipUnless = nameof(HasGit))]
    public async Task Run_WhenTheRunIsStopped_ReportsCancellationRatherThanTimeout()
    {
        // Arrange
        // Already cancelled, so the test does not have to decide when the command has run for long enough.
        using var stopped = new CancellationTokenSource();
        await stopped.CancelAsync();

        // Assert
        var failure = await Should.ThrowAsync<OperationCanceledException>(
            // Statement lambda for the reason on Run_WhenBackgroundDescendant… above.
            () => Task.Run(
                () =>
                {
                    // The command bound is never reached - the token is cancelled before the wait starts - but it
                    // still has to sit inside the attribute deadline, or a regression in the cancellation path would
                    // surface as a runner timeout instead of the named message this test asserts.
                    _ = Git.Run(
                        _fixture.RepositoryPath,
                        CompletingCommandTimeoutMilliseconds,
                        stopped.Token,
                        [0],
                        GitTestsFixture.SleepAlias);
                },
                TestContext.Current.CancellationToken));

        failure.Message.ShouldMatch(
            $@"^git {GitTestsFixture.SleepAlias} \(PID \d+\) was cancelled by the test runner\.$");
    }

    // Trigger: a git command stops making progress and never exits — a stale index lock, or a credential prompt on a
    //   console that has been redirected.
    // Behavior: the helper gives up on its own timeout and says which command and which process it gave up on,
    //   rather than blocking the suite.
    // Assertion: a TimeoutException reading exactly "git sleep-forever (PID nnn) did not finish in 2000 ms."
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds, Skip = SkipReason, SkipUnless = nameof(HasGit))]
    public async Task Run_WhenCommandOutlastsItsTimeout_NamesTheCommandAndProcess()
    {
        // Arrange
        var runnerToken = TestContext.Current.CancellationToken;

        // Assert
        var failure = await Should.ThrowAsync<TimeoutException>(
            // Statement lambda for the reason on Run_WhenBackgroundDescendant… above.
            () => Task.Run(
                () =>
                {
                    _ = Git.Run(
                        _fixture.RepositoryPath,
                        HangingCommandTimeoutMilliseconds,
                        runnerToken,
                        [0],
                        GitTestsFixture.SleepAlias);
                },
                runnerToken));

        failure.Message.ShouldMatch(
            $@"^git {GitTestsFixture.SleepAlias} \(PID \d+\) did not finish in {HangingCommandTimeoutMilliseconds} ms\.$");
    }

    // Trigger: a rule asks git for something that is not there — a reference name that was never created.
    // Behavior: the helper fails immediately with the command and the exit code, rather than handing back the empty
    //   output a caller would read as "nothing matched".
    // Assertion: an InvalidOperationException reading exactly
    //   "git rev-parse --verify no-such-ref (PID nnn) exited with 128."
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds, Skip = SkipReason, SkipUnless = nameof(HasGit))]
    public void Run_WhenGitExitsWithUnexpectedCode_NamesTheCommandAndCode()
    {
        // Assert
        var failure = Should.Throw<InvalidOperationException>(
            () => Git.Run(
                _fixture.RepositoryPath,
                CompletingCommandTimeoutMilliseconds,
                TestContext.Current.CancellationToken,
                [0],
                "rev-parse",
                "--verify",
                "no-such-ref"));

        failure.Message.ShouldMatch(@"^git rev-parse --verify no-such-ref \(PID \d+\) exited with 128\.$");
    }

    /// <remarks>
    /// GetProcessById throws once the process is gone from the process list, so both outcomes mean "not running":
    /// https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.getprocessbyid
    /// </remarks>
    private static bool IsRunning(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
