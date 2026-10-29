using System.Xml.Linq;

namespace JobTracker.Architecture.Tests.Scenarios;

/// <summary>
/// Checks on the solution's project files that no ArchUnitNET rule can express: which project may reference which,
/// and that the architecture rules load every production project.
/// </summary>
/// <remarks>
/// <para>
/// A project reference nobody has used yet leaves no trace in IL — the compiler drops unused references from the
/// assembly manifest — so the type rules in <c>ArchUnitRules/</c> see a clean solution until the first line of code
/// lands. Reading the project files is the only way to catch the reference itself.
/// </para>
/// <para>
/// Here that is more than a nicety. Domain, Application and Infrastructure hold no types yet, so no type rule can
/// name them; until they do, these allow-lists are the only thing holding "Domain depends on nothing", "Application
/// depends only on Domain" and "only the API references Infrastructure".
/// </para>
/// </remarks>
public sealed class ProjectStructureTests
{
    private const string Contracts = "JobTracker.Contracts";
    private const string Domain = "JobTracker.Domain";
    private const string Application = "JobTracker.Application";
    private const string Infrastructure = "JobTracker.Infrastructure.MongoDb";
    private const string Api = "JobTracker.Api";
    private const string Ui = "JobTracker.UI";
    private const string TestSupport = "JobTracker.TestSupport";

    // The architecture rules must load every production assembly, so this project alone references them all.
    private const string ArchitectureTests = "JobTracker.Architecture.Tests";

    // What each production project may reference, by project name. Every project has an entry, the composition root
    // included: an entry is the only thing that stops a reference being added and left to grow.
    private static readonly Dictionary<string, string[]> AllowedProjectReferences = new(StringComparer.Ordinal)
    {
        [Contracts] = [],
        [Domain] = [],
        [Application] = [Domain],
        [Infrastructure] = [Application, Domain],
        // The composition root reaches every layer, and maps between Contracts and Application so the two can change
        // independently. The UI is the one thing it may not reach: the UI is its client, over HTTP.
        [Api] = [Contracts, Application, Domain, Infrastructure],
        [Ui] = [Contracts],
    };

    // What each test project may reference. A test project tests one layer, so it gets that layer, whatever that
    // layer itself may reach, and TestSupport. A test whose subject is not in this set is filed in the wrong project
    // — move the test, do not widen the entry (test-antipatterns, "A test belongs to the project named after what it
    // tests").
    private static readonly Dictionary<string, string[]> AllowedTestProjectReferences = new(StringComparer.Ordinal)
    {
        // TestSupport is shared by every test project, so a production reference here drags that layer into all of
        // them. Application and Domain are allowed for one reason: the repository contract tests are written
        // once, as an abstract class in TestSupport, and run against both the in-memory fake and MongoDB, so
        // TestSupport has to name the repository port.
        [TestSupport] = [Application, Domain],
        ["JobTracker.Contracts.Tests"] = [Contracts, TestSupport],
        ["JobTracker.Domain.Tests"] = [Domain, TestSupport],
        ["JobTracker.Application.Tests"] = [Application, Domain, TestSupport],
        ["JobTracker.Infrastructure.MongoDb.Tests"] = [Infrastructure, Application, Domain, TestSupport],
        ["JobTracker.Api.Tests"] = [Api, Contracts, Application, Domain, Infrastructure, TestSupport],
        ["JobTracker.UI.Tests"] = [Ui, Contracts, TestSupport],
    };

    // Trigger: a new project is added under src/ and nobody teaches the architecture loader about it.
    // Behavior: the suite reports the unloaded project instead of passing every rule without checking it.
    // Assertion: the loaded assembly names read exactly the project names found on disk under src/.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void EveryProductionProject_IsLoadedByTheArchitectureRules()
    {
        // The guard that keeps the rest of this project honest. A rule can only fail on an assembly the loader was
        // given, so a new project under src/ would pass every architecture rule while being checked by none.
        // Arrange
        var loaded = SolutionArchitecture.AllAssemblies
            .Select(static assembly => assembly.GetName().Name!)
            .Order(StringComparer.Ordinal)
            .ToList();

        // Act
        var onDisk = ProjectNames(RepositoryLayout.ProductionProjectFiles());

        // Assert
        loaded.ShouldBe(onDisk);
    }

    // Trigger: a ProjectReference is added to a production project — Domain given Contracts "for one record", or the
    //   UI given Application to call a use case directly.
    // Behavior: a reference that crosses a layer boundary is refused while it is still unused by any code.
    // Assertion: every project under src/ has an allow-list entry, and no reference falls outside its own entry.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void ProjectReferences_MatchTheLayerAllowList()
    {
        // Act
        var (checkedProjects, violations) = Check(
            RepositoryLayout.ProductionProjectFiles(),
            AllowedProjectReferences,
            exempt: []);

        // Assert
        checkedProjects.ShouldBe(AllowedProjectReferences.Keys.Order(StringComparer.Ordinal).ToList());
        violations.ShouldBeEmpty("Project references outside the layer allow-list.");
    }

    // Trigger: a test is written against a layer its project does not test, and a ProjectReference is added to make
    //   it compile.
    // Behavior: the reference is refused while it is still unused by any code, so the test is filed with its layer.
    // Assertion: every project under tests/ has an allow-list entry, and no reference falls outside its own entry.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void TestProjectReferences_MatchTheLayerUnderTest()
    {
        // Act
        var (checkedProjects, violations) = Check(
            RepositoryLayout.TestProjectFiles(),
            AllowedTestProjectReferences,
            exempt: [ArchitectureTests]);

        // Assert
        checkedProjects.ShouldBe(AllowedTestProjectReferences.Keys.Order(StringComparer.Ordinal).ToList());
        violations.ShouldBeEmpty("Test project references outside the layer under test.");
    }

    /// <summary>
    /// Reads every project file and reports, in ordinal order, the projects that were held to an entry and every
    /// reference that falls outside its project's entry — or the project itself, when it has no entry at all.
    /// </summary>
    private static (List<string> CheckedProjects, List<string> Violations) Check(
        List<string> projectFiles,
        Dictionary<string, string[]> allowed,
        string[] exempt)
    {
        var checkedProjects = new List<string>();
        var violations = new List<string>();

        foreach (var project in projectFiles)
        {
            var name = Path.GetFileNameWithoutExtension(project);
            if (exempt.Contains(name, StringComparer.Ordinal))
            {
                continue;
            }

            checkedProjects.Add(name);
            if (!allowed.TryGetValue(name, out var entry))
            {
                violations.Add($"{name} has no allow-list entry");
                continue;
            }

            violations.AddRange(ReferencedProjectNames(project)
                .Where(referenced => !entry.Contains(referenced, StringComparer.Ordinal))
                .Select(referenced => $"{name} -> {referenced}"));
        }

        return (
            [.. checkedProjects.Order(StringComparer.Ordinal)],
            [.. violations.Order(StringComparer.Ordinal)]);
    }

    private static List<string> ProjectNames(List<string> projectFiles) =>
        [.. projectFiles.Select(static path => Path.GetFileNameWithoutExtension(path)).Order(StringComparer.Ordinal)];

    private static IEnumerable<string> ReferencedProjectNames(string projectFile)
    {
        return XDocument.Load(projectFile)
            .Descendants("ProjectReference")
            .Select(static element => element.Attribute("Include")?.Value)
            .Where(static include => !string.IsNullOrEmpty(include))
            .Select(static include => Path.GetFileNameWithoutExtension(include!.Replace('\\', Path.DirectorySeparatorChar)));
    }
}
