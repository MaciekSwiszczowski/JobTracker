using System.Text.RegularExpressions;

namespace JobTracker.Architecture.Tests.Scenarios;

/// <summary>
/// Rules about the working tree itself, for the mistakes that survive a green build: content staged and then
/// changed, files committed that an ignore rule also matches, build output or personal data committed, and
/// unresolved merge markers.
/// </summary>
/// <remarks>
/// <para>
/// These are local-only, via <see cref="FactAttribute.SkipUnless"/> and <see cref="HasLocalWorkingTree"/>. On a
/// build agent the tree is a fresh checkout with nothing staged, so the rules are vacuously green and say nothing; on
/// a developer or agent machine mid-change they are the only place the question gets asked.
/// </para>
/// <para>
/// The index rule is aimed at automation rather than at people. An agent that writes a file, stages it, then deletes
/// or rewrites it leaves an <c>AD</c> or <c>AM</c> entry, and a later commit takes the staged version — silently,
/// because the working tree looks correct. A person who staged a hunk with <c>git add -p</c> and kept editing will
/// also see this fail; that is the accepted cost, and <c>git add</c> or <c>git restore --staged</c> clears it.
/// </para>
/// </remarks>
public sealed class GitWorkingTreeTests
{
    private const string SkipReason = "Needs a local git working tree on Windows; a fresh CI checkout has nothing to say.";

    private static readonly Regex BuildOutputPath = new(@"(^|/)(bin|obj)/", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));

    // Anything the build or the suite reads. A missing one of these breaks a fresh clone but not this machine.
    //
    // The first half is extensions: C#, project and solution files, and .axaml, which Avalonia compiles into the UI
    // assembly. The second half is names rather than extensions, because each one is an input whose extension is not:
    // NativeMethods.txt is what CsWin32 generates Git's P/Invokes from; BannedSymbols.txt feeds the Debug_Analyzers
    // gate; global.json routes `dotnet test` through Microsoft.Testing.Platform; xunit.runner.json carries the
    // parallel mode; the docs/api examples are what JobTracker.Contracts.Tests checks the wire format against.
    // Extending the first list to `.txt` or `.json` instead would match every note and settings file in the tree.
    private static readonly Regex BuildInputPath = new(
        @"\.(cs|csproj|props|targets|slnx|axaml)$"
        + @"|(^|/)(NativeMethods\.txt|BannedSymbols\.txt|global\.json|xunit\.runner\.json)$"
        + @"|^docs/api/[^/]+\.example\.json$",
        RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(1));

    // inbox/ is where the job-applications skill queues real job offers - recruiter names, salaries. It is ignored,
    // so this only fires if someone force-adds it or the ignore rule is lost. See AGENTS.md, Gotchas.
    private static readonly Regex PersonalDataPath = new(@"^inbox/", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));

    /// <summary>Gate for <see cref="FactAttribute.SkipUnless"/>: a real working tree, and not a build agent.</summary>
    /// <remarks>The CI check comes first so a build agent never starts git just to be told to skip.</remarks>
    public static bool HasLocalWorkingTree => !IsContinuousIntegration && Git.IsWorkingTreeAvailable;

    private static bool IsContinuousIntegration =>
        Environment.GetEnvironmentVariable("CI") is not null
        || Environment.GetEnvironmentVariable("GITHUB_ACTIONS") is not null
        || Environment.GetEnvironmentVariable("TF_BUILD") is not null;

    // Trigger: a file is staged and then changed or deleted again, which is what an agent editing its own output
    //   leaves behind.
    // Behavior: the discrepancy is reported before the commit, instead of the commit shipping the staged version
    //   while the working tree looks right.
    // Assertion: no path is staged with content the working tree no longer agrees with.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds, Skip = SkipReason, SkipUnless = nameof(HasLocalWorkingTree))]
    public void CommitIndex_MatchesIntendedWorkingTree()
    {
        // Arrange
        var entries = Git.Status();

        // Act
        var stale = entries
            .Where(static entry => entry.IsStaleInIndex)
            .Select(static entry => entry.ToString())
            .Order(StringComparer.Ordinal)
            .ToList();

        // Assert
        stale.ShouldBeEmpty(
            "The index holds content the working tree no longer agrees with, so a commit would ship the staged "
            + "version rather than what is on disk. Run `git add <path>` to take the current content, or "
            + "`git restore --staged <path>` to drop the staged version.");
    }

    // Trigger: a new source file is written but never added to git — a type replaced, its replacement forgotten.
    // Behavior: it is reported here, where the build still passes because the file exists on this machine.
    // Assertion: no untracked file the build or the suite reads is left out of the index.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds, Skip = SkipReason, SkipUnless = nameof(HasLocalWorkingTree))]
    public void CompilableSource_IsTracked()
    {
        // Arrange
        var untracked = Git.UntrackedFiles();

        // Act
        var missingFromGit = untracked
            .Where(static path => BuildInputPath.IsMatch(path) && !BuildOutputPath.IsMatch(path))
            .Order(StringComparer.Ordinal)
            .ToList();

        // Assert
        missingFromGit.ShouldBeEmpty(
            "These look like build inputs and git does not know about them, so a fresh clone may not build. This is "
            + "what a commit that deletes a type and forgets to `git add` its replacement looks like: it builds here, "
            + "and only here. Run `git add <path>`, or add an ignore rule if the file is genuinely local.");
    }

    // Trigger: an ignore rule is widened until it also matches files that are already committed.
    // Behavior: the contradiction is reported now, rather than when a sibling added later is silently ignored.
    // Assertion: no tracked path is also matched by an ignore rule.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds, Skip = SkipReason, SkipUnless = nameof(HasLocalWorkingTree))]
    public void TrackedFiles_AreNotAlsoIgnored()
    {
        // Act
        var contradictory = Git.TrackedButIgnoredFiles().Order(StringComparer.Ordinal).ToList();

        // Assert
        contradictory.ShouldBeEmpty(
            "These paths are committed and also matched by an ignore rule. Tracking wins, so they keep working, but a "
            + "sibling added later is ignored without warning. Add a `!` negation in .gitignore if the file belongs "
            + "in the repository, or remove it with `git rm --cached`.");
    }

    // Trigger: a bin or obj file is committed, usually by staging everything at once.
    // Behavior: it is reported so it can be removed before it starts conflicting on every build.
    // Assertion: no tracked path lies under a bin or obj folder.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds, Skip = SkipReason, SkipUnless = nameof(HasLocalWorkingTree))]
    public void BuildOutput_IsNotTracked()
    {
        // Arrange
        var tracked = Git.TrackedFiles();

        // Act
        var committed = tracked
            .Where(static path => BuildOutputPath.IsMatch(path))
            .Order(StringComparer.Ordinal)
            .ToList();

        // Assert
        committed.ShouldBeEmpty(
            "Build output is committed. Remove it with `git rm -r --cached` and check .gitignore covers it.");
    }

    // Trigger: a queued job offer is force-added, or the inbox/ ignore rule is lost and everything is staged at once.
    // Behavior: it is reported before the commit, while the recruiter's name is still only on this machine.
    // Assertion: no tracked path lies under inbox/.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds, Skip = SkipReason, SkipUnless = nameof(HasLocalWorkingTree))]
    public void Inbox_IsNotTracked()
    {
        // Arrange
        var tracked = Git.TrackedFiles();

        // Act
        var committed = tracked
            .Where(static path => PersonalDataPath.IsMatch(path))
            .Order(StringComparer.Ordinal)
            .ToList();

        // Assert
        committed.ShouldBeEmpty(
            "inbox/ holds real job offers and recruiter names and must never be committed. Remove it with "
            + "`git rm -r --cached inbox` and check .gitignore still lists it.");
    }

    // Trigger: a merge is finished without resolving every file, leaving conflict markers in the text.
    // Behavior: they are reported here rather than found later in a file nobody reopened.
    // Assertion: no tracked file still holds merge conflict markers.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds, Skip = SkipReason, SkipUnless = nameof(HasLocalWorkingTree))]
    public void TrackedFiles_HaveNoConflictMarkers()
    {
        // Act
        var unresolved = Git.FilesWithConflictMarkers().Order(StringComparer.Ordinal).ToList();

        // Assert
        unresolved.ShouldBeEmpty("These tracked files still hold merge conflict markers.");
    }
}
