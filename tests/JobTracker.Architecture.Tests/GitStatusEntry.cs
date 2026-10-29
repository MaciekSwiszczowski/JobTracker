namespace JobTracker.Architecture.Tests;

/// <summary>
/// One line of <c>git status --porcelain</c>: a path and its two status characters. <paramref name="Index"/> is
/// what the index says about the path, <paramref name="Worktree"/> what the working tree says.
/// </summary>
/// <param name="Path">Repository-relative path, forward-slashed as git reports it.</param>
/// <param name="Index">Status in the index: <c>A</c>, <c>M</c>, <c>D</c>, <c>R</c>, <c>?</c>, or a space.</param>
/// <param name="Worktree">Status in the working tree, using the same characters.</param>
internal sealed record GitStatusEntry(string Path, char Index, char Worktree)
{
    /// <summary>
    /// True when the index holds something the working tree no longer agrees with — <c>AD</c> (staged, then
    /// deleted on disk), <c>AM</c> and <c>MM</c> (staged, then edited further). Committing in this state commits
    /// the staged version, not the one on disk.
    /// </summary>
    /// <remarks>
    /// Untracked paths (<c>??</c>) are not stale: nothing is staged for them, so there is nothing to disagree
    /// with. A clean staged path (<c>A </c>, <c>M </c>) is not stale either — the tree matches the index.
    /// </remarks>
    internal bool IsStaleInIndex =>
        Index is not (' ' or '?')
        && Worktree is not (' ' or '?');

    /// <summary>Renders as git does, so a failure message can be pasted into a search.</summary>
    public override string ToString() => $"{Index}{Worktree} {Path}";
}
