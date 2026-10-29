namespace JobTracker.Architecture.Tests;

/// <summary>
/// Locates files in the working tree for the tests that read project files or ask git about the repository.
/// </summary>
/// <remarks>
/// Tests run from <c>bin</c>, so every path has to be resolved from the repository root upwards. The root is found
/// by walking up to the solution file rather than by counting <c>..</c> segments, which would break the moment
/// this project moved.
/// </remarks>
internal static class RepositoryLayout
{
    private const string SolutionFileName = "JobTracker.slnx";

    /// <summary>The repository root: the directory holding the solution file.</summary>
    /// <exception cref="InvalidOperationException">Thrown when no ancestor of the test output directory holds
    /// the solution file.</exception>
    internal static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException($"{SolutionFileName} not found above {AppContext.BaseDirectory}");
    }

    /// <summary>Every production project file: every <c>.csproj</c> under <c>src/</c>.</summary>
    internal static List<string> ProductionProjectFiles() => ProjectFilesUnder("src");

    /// <summary>Every test-tier project file: every <c>.csproj</c> under <c>tests/</c>.</summary>
    internal static List<string> TestProjectFiles() => ProjectFilesUnder("tests");

    private static List<string> ProjectFilesUnder(string folder)
    {
        return Directory
            .EnumerateFiles(Path.Combine(Root(), folder), "*.csproj", SearchOption.AllDirectories)
            .Where(static path => !IsBuildOutput(path))
            .ToList();
    }

    private static bool IsBuildOutput(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
}
