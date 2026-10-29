using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.JobObjects;

namespace JobTracker.Architecture.Tests;

/// <summary>
/// The only type here that knows how to run git. Everything else asks it a question and gets values back, so the
/// working-tree rules read as assertions rather than as process plumbing.
/// </summary>
/// <remarks>
/// <para>
/// The git CLI is used rather than a managed git library because this repository normalises line endings through
/// <c>.gitattributes</c> (<c>* text=auto</c>). A second implementation of git would have to reproduce that
/// normalisation to agree with the index — one that hashed raw bytes would report every text file as modified, and
/// a gate that reported a normalisation difference as a stale index would be worse than no gate at all.
/// </para>
/// <para>
/// Standard error is drained through <see cref="Process.BeginErrorReadLine"/> rather than left redirected and
/// unread: git writes a warning per file for some configurations, which is more than enough to fill the pipe buffer
/// and deadlock a caller that only reads standard output.
/// </para>
/// <para>
/// Every git process runs inside a kill-on-close job object, which is what actually bounds a command. See
/// <see cref="CreateKillOnCloseJob"/> for why <see cref="Process.Kill(bool)"/> is not enough. Job objects are
/// Windows-only, so on any other OS <see cref="IsWorkingTreeAvailable"/> is false and every git rule skips. That
/// costs nothing real: the owner works on Windows, and CI runs on Ubuntu, where the working-tree rules have nothing
/// to say about a fresh checkout anyway.
/// </para>
/// </remarks>
internal static class Git
{
    private const int TimeoutMilliseconds = 15_000;

    /// <summary>
    /// Bound for the availability probe, kept separate from <see cref="TimeoutMilliseconds"/> because it runs from a
    /// <c>SkipUnless</c> gate rather than from inside a test, and a slow answer there delays every rule.
    /// </summary>
    private const int ProbeTimeoutMilliseconds = 5_000;

    /// <summary>
    /// How long the output read is given to finish after the job is closed. Closing the job removes the last writer,
    /// so this only expires if a handle survives somewhere outside the job — it exists to turn that into a named
    /// failure rather than a hung suite.
    /// </summary>
    private const int OutputDrainGraceMilliseconds = 3_000;

    // What Process.Start reports when the executable is not on PATH. Anything else is not "git is missing".
    private const int ErrorFileNotFound = 2;

    private const int ErrorPathNotFound = 3;

    // git grep exits 1 when nothing matched, which is an answer rather than a failure.
    private static readonly int[] SuccessOrNoMatch = [0, 1];

    private static readonly Lazy<bool> AvailabilityProbe = new(Probe);

    /// <summary>
    /// True when this is a real working tree on Windows. False in a source drop with no <c>.git</c>, or on another
    /// OS — in both cases the git rules have nothing to say. A working tree whose git cannot run is not a "false":
    /// the probe throws, and every gated rule reports that rather than skipping (test-antipatterns §6).
    /// </summary>
    internal static bool IsWorkingTreeAvailable => AvailabilityProbe.Value;

    /// <summary>The porcelain status of every path git considers interesting.</summary>
    internal static IReadOnlyList<GitStatusEntry> Status() => ParseStatus(Run("status", "--porcelain=v1", "-z"));

    // Every path query below passes -z. Without it git C-quotes a path holding a non-ASCII or special character
    // ("caf\303\251.cs"), and a quoted path matches none of the patterns the rules filter on, so the rule would pass
    // over exactly that file.

    /// <summary>Every path in the index.</summary>
    internal static IReadOnlyList<string> TrackedFiles() => Paths(Run("ls-files", "-z"));

    /// <summary>Files present in the working tree that git neither tracks nor ignores.</summary>
    internal static IReadOnlyList<string> UntrackedFiles() =>
        Paths(Run("ls-files", "-z", "--others", "--exclude-standard"));

    /// <summary>Paths that are both committed and matched by an ignore rule — a contradiction that silently
    /// changes what a later <c>git add</c> picks up.</summary>
    internal static IReadOnlyList<string> TrackedButIgnoredFiles() =>
        Paths(Run("ls-files", "-z", "-i", "-c", "--exclude-standard"));

    /// <summary>Tracked files holding an unresolved merge marker.</summary>
    internal static IReadOnlyList<string> FilesWithConflictMarkers() =>
        // Written with a quantifier so this source file does not contain a marker and match itself.
        Paths(Run(SuccessOrNoMatch, "grep", "-z", "--files-with-matches", "-E", "^(<{7}|>{7}) "));

    /// <summary>
    /// Runs git and returns its standard output, killing the whole process tree if the command outlives either
    /// bound.
    /// </summary>
    /// <param name="workingDirectory">Directory to run git in.</param>
    /// <param name="timeoutMilliseconds">How long the command may take before it is abandoned.</param>
    /// <param name="cancellationToken">
    /// The test runner's token. Cancelling it is reported as <see cref="OperationCanceledException"/>, which is what
    /// distinguishes "the run was stopped" from "this command is stuck".
    /// </param>
    /// <param name="okExitCodes">Exit codes that count as an answer rather than a failure.</param>
    /// <param name="arguments">Arguments passed to git, one element per argument.</param>
    /// <exception cref="PlatformNotSupportedException">Not running on Windows.</exception>
    /// <exception cref="TimeoutException">The command did not finish within <paramref name="timeoutMilliseconds"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    /// <exception cref="InvalidOperationException">git exited with a code outside <paramref name="okExitCodes"/>.</exception>
    internal static string Run(
        string workingDirectory,
        int timeoutMilliseconds,
        CancellationToken cancellationToken,
        int[] okExitCodes,
        params string[] arguments)
    {
        // A guard rather than a quiet fallback to Process.Kill: without the job object the cleanup guarantee
        // GitTests checks does not hold, and a helper that silently weakened it would make those tests lie.
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Git runs every command in a Win32 job object.");
        }

        var command = $"git {string.Join(' ', arguments)}";

        using var job = CreateKillOnCloseJob();
        using var process = new Process();
        process.StartInfo.FileName = "git";
        process.StartInfo.WorkingDirectory = workingDirectory;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.ErrorDataReceived += static (_, _) => { };

        process.Start();

        // Known gap, deliberately accepted. git is runnable the moment Process.Start returns, so a child it creates
        // before this line escapes the job permanently — assignment does not reach back. Closing the window needs
        // CREATE_SUSPENDED or PROC_THREAD_ATTRIBUTE_JOB_LIST, and both mean replacing Process.Start with a raw
        // CreateProcess call: our own pipes, our own STARTUPINFO, our own command-line quoting, our own exit-code
        // wait. That is more hand-written process plumbing than the gap is worth here, where every command run
        // through this helper (status, ls-files, grep, rev-parse, init, config) starts no background process at all.
        // AssignProcessToJobObject makes the process subject to the job's kill-on-close limit:
        // https://learn.microsoft.com/en-us/windows/win32/api/jobapi2/nf-jobapi2-assignprocesstojobobject
        if (!PInvoke.AssignProcessToJobObject(job, process.SafeHandle))
        {
            var error = Marshal.GetLastPInvokeError();

            // git is running and nothing owns it, so this is the one place the weaker cleanup is all there is.
            process.Kill(entireProcessTree: true);
            throw new Win32Exception(error, $"Could not put {command} into a job object.");
        }

        var processId = process.Id;
        process.BeginErrorReadLine();

        // Started before the wait, not after it: a full output pipe would otherwise block git while we are waiting
        // for git to exit. Do not pass a token to the read — abandoning it half way leaves the pipe undrained, and
        // the drain after the kill is what proves the writers are gone. Closing the job is what ends this read.
        // AsyncFixer04 reads this as fire-and-forget inside the process `using`. It is not: `outputTask` is drained
        // synchronously below, before this scope exits, so the process outlives the read.
#pragma warning disable AsyncFixer04
        var outputTask = process.StandardOutput.ReadToEndAsync();
#pragma warning restore AsyncFixer04

        using var timeoutSource = new CancellationTokenSource(timeoutMilliseconds);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);

        try
        {
            // WaitForExitAsync also waits for the redirected error reader to reach end of file, so a descendant
            // still holding standard error keeps us here until a token fires:
            // https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.waitforexitasync
            // VSTHRD002: this helper returns a string synchronously and runs on a plain xUnit thread with no
            // synchronization context, so the blocking waits below cannot deadlock; both are bounded by linkedSource.
#pragma warning disable VSTHRD002
            process.WaitForExitAsync(linkedSource.Token).GetAwaiter().GetResult();
            var output = outputTask.WaitAsync(linkedSource.Token).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002

            if (!okExitCodes.Contains(process.ExitCode))
            {
                throw new InvalidOperationException($"{command} (PID {processId}) exited with {process.ExitCode}.");
            }

            return output;
        }
        catch (OperationCanceledException)
        {
            var stoppedByRunner = cancellationToken.IsCancellationRequested;
            KillTreeAndDrainOutput(job, outputTask, command, processId);

            if (stoppedByRunner)
            {
                throw new OperationCanceledException(
                    $"{command} (PID {processId}) was cancelled by the test runner.", cancellationToken);
            }

            throw new TimeoutException($"{command} (PID {processId}) did not finish in {timeoutMilliseconds} ms.");
        }
    }

    private static bool Probe()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        var gitPath = Path.Combine(RepositoryLayout.Root(), ".git");

        // A worktree or submodule checkout has .git as a file pointing elsewhere, not a directory.
        if (!Directory.Exists(gitPath) && !File.Exists(gitPath))
        {
            return false;
        }

        try
        {
            // CancellationToken.None on purpose. The answer is cached in a Lazy for the whole run, so a run that is
            // being cancelled must not record a failure for every rule below. The probe's own timeout bounds it.
            Run(RepositoryLayout.Root(), ProbeTimeoutMilliseconds, CancellationToken.None, [0], "--version");
            return true;
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode is ErrorFileNotFound or ErrorPathNotFound)
        {
            // Not turned into a skip: this is a working tree, so git is a stated requirement here, and a missing one
            // would otherwise make every working-tree rule pass vacuously on the one machine meant to run them.
            throw new InvalidOperationException(
                "This is a git working tree, but git is not on PATH. Install Git for Windows or add it to PATH.",
                exception);
        }
    }

    private static string Run(params string[] arguments) => Run([0], arguments);

    private static string Run(int[] okExitCodes, params string[] arguments) =>
        Run(RepositoryLayout.Root(), TimeoutMilliseconds, TestContext.Current.CancellationToken, okExitCodes, arguments);

    /// <summary>
    /// Creates an unnamed job object that terminates everything still assigned to it when its last handle closes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is what makes cleanup a guarantee rather than an attempt. A process created by a process already in a
    /// job joins that job automatically, at every depth, so closing the handle reaches a grandchild whose own parent
    /// has already exited. <see cref="Process.Kill(bool)"/> cannot: it walks parent process ids outward from a live
    /// root handle, and the chain breaks the moment an intermediate process goes away, leaving the descendant
    /// running with git's output pipe still open.
    /// </para>
    /// <para>
    /// Breaking out needs <c>CREATE_BREAKAWAY_FROM_JOB</c> on the child *and* <c>JOB_OBJECT_LIMIT_BREAKAWAY_OK</c> on
    /// the job. The latter is deliberately not set, so an attempt to break away fails. A descendant that creates its
    /// own job nests inside this one and dies with it.
    /// https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects
    /// </para>
    /// </remarks>
    /// <returns>The job handle. The caller owns it; closing it kills every member.</returns>
    private static unsafe SafeFileHandle CreateKillOnCloseJob()
    {
        // Null attributes and no name: the handle is not inherited and nothing else can open the job.
        // https://learn.microsoft.com/en-us/windows/win32/api/jobapi2/nf-jobapi2-createjobobjectw
        var job = PInvoke.CreateJobObject(null, (string?)null);
        if (job.IsInvalid)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Could not create a job object for git.");
        }

        var limits = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            BasicLimitInformation = { LimitFlags = JOB_OBJECT_LIMIT.JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE },
        };

        // SetInformationJobObject has no SafeHandle overload, so the raw handle is passed and the wrapper is kept
        // alive across the call. `job` is a local that no other thread can see, so nothing can close it here.
        // JobObjectExtendedLimitInformation applies JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE:
        // https://learn.microsoft.com/en-us/windows/win32/api/jobapi2/nf-jobapi2-setinformationjobobject
        var configured = PInvoke.SetInformationJobObject(
            (HANDLE)job.DangerousGetHandle(),
            JOBOBJECTINFOCLASS.JobObjectExtendedLimitInformation,
            &limits,
            (uint)sizeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
        var error = Marshal.GetLastPInvokeError();
        GC.KeepAlive(job);

        if (!configured)
        {
            job.Dispose();
            throw new Win32Exception(error, "Could not make git's job object kill its members on close.");
        }

        return job;
    }

    /// <summary>
    /// Terminates git and everything it created, then waits for the output read to reach end of file.
    /// </summary>
    /// <remarks>
    /// Closing the job's last handle terminates its members, so the write end of the output pipe is gone once this
    /// returns and the pending read completes. The wait is bounded anyway, because a hung suite says far less than a
    /// named failure does.
    /// </remarks>
    /// <exception cref="TimeoutException">Output was still open after the grace period.</exception>
    private static void KillTreeAndDrainOutput(SafeFileHandle job, Task<string> outputTask, string command, int processId)
    {
        job.Dispose();

        bool drained;
        try
        {
            // VSTHRD002: synchronous helper, no synchronization context (see Run); the wait is bounded by the grace.
#pragma warning disable VSTHRD002
            drained = outputTask.Wait(OutputDrainGraceMilliseconds);
#pragma warning restore VSTHRD002
        }
        catch (AggregateException)
        {
            // The read failed as its pipe was torn down. The command's own failure is the one worth reporting.
            return;
        }

        if (drained)
        {
            return;
        }

        // Nothing will observe the read's exception now, and an unobserved one surfaces later in an unrelated test.
        _ = outputTask.ContinueWith(static task => _ = task.Exception, TaskScheduler.Default);

        throw new TimeoutException(
            $"{command} (PID {processId}) still held standard output {OutputDrainGraceMilliseconds} ms after its job was closed.");
    }

    /// <summary>
    /// Parses <c>--porcelain=v1 -z</c> output. Each record is two status characters, a space, then the path, then a
    /// NUL. A rename or copy adds the original path as a second NUL-terminated field, which must be consumed or it
    /// is read as the next record.
    /// </summary>
    private static IReadOnlyList<GitStatusEntry> ParseStatus(string output)
    {
        var fields = output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        var entries = new List<GitStatusEntry>();

        for (var i = 0; i < fields.Length; i++)
        {
            var field = fields[i];
            if (field.Length < 4)
            {
                continue;
            }

            var index = field[0];
            var worktree = field[1];
            entries.Add(new GitStatusEntry(field[3..], index, worktree));

            if (index is 'R' or 'C')
            {
                i++;
            }
        }

        return entries;
    }

    private static IReadOnlyList<string> Paths(string output) =>
        output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
}
