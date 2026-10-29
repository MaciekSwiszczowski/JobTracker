using ArchUnitNET.Loader;
using JobTracker.Contracts;
using JobTracker.UI;

namespace JobTracker.Architecture.Tests;

/// <summary>
/// The production assemblies as one lazily-built ArchUnitNET model, with a named type selector per populated project
/// and the allow-list helper the layer rules are written against.
/// </summary>
/// <remarks>
/// A rule only sees the assemblies loaded here. Drop one and every rule about it turns into a silent pass, so
/// <see cref="AllAssemblies"/> and the <c>ProjectReference</c> list in the <c>.csproj</c> must stay in step —
/// <c>ProjectStructureTests.EveryProductionProject_IsLoadedByTheArchitectureRules</c> makes that mechanical.
/// </remarks>
internal static class SolutionArchitecture
{
    internal static readonly Assembly ContractsAssembly = typeof(NewApplication).Assembly;

    // Domain, Application and Infrastructure are loaded by name because they hold no types yet, so there is nothing
    // to take typeof of. They are loaded anyway rather than left out, because the forbidden half of a layer rule has
    // to name them — otherwise "the UI reaches only Contracts" would quietly stop covering Domain the moment Domain
    // gained a type. The project reference puts each file beside the test host, and its deps.json entry lets the
    // default load context find it by name. Swap each for a typeof anchor when its first type lands.
    internal static readonly Assembly DomainAssembly = Assembly.Load("JobTracker.Domain");

    internal static readonly Assembly ApplicationAssembly = Assembly.Load("JobTracker.Application");

    internal static readonly Assembly InfrastructureAssembly = Assembly.Load("JobTracker.Infrastructure.MongoDb");

    // The API's Program is the compiler-generated top-level-statements class, in the global namespace.
    internal static readonly Assembly ApiAssembly = typeof(global::Program).Assembly;

    internal static readonly Assembly UiAssembly = typeof(App).Assembly;

    internal static readonly Assembly[] AllAssemblies =
    [
        ContractsAssembly,
        DomainAssembly,
        ApplicationAssembly,
        InfrastructureAssembly,
        ApiAssembly,
        UiAssembly,
    ];

    /// <summary>Every type in the solution's production code, which is the subject of the solution-wide bans.</summary>
    internal static readonly IObjectProvider<IType> Production = TypesIn(AllAssemblies, "production code");

    internal static readonly IObjectProvider<IType> Contracts =
        Types().That().ResideInAssembly(ContractsAssembly).As("the wire format");

    internal static readonly IObjectProvider<IType> Api =
        Types().That().ResideInAssembly(ApiAssembly).As("the service");

    internal static readonly IObjectProvider<IType> Ui =
        Types().That().ResideInAssembly(UiAssembly).As("the UI");

    /// <summary>
    /// Every production type outside the data layer: the subject of the MongoDB ban. It is non-empty today through
    /// Contracts, the API and the UI, and it takes in Domain and Application from their first type onward, with
    /// nobody needing to come back and add them.
    /// </summary>
    /// <remarks>
    /// Computed from <see cref="AllAssemblies"/> rather than listed, so a project added later is inside the ban from
    /// the start.
    /// </remarks>
    internal static readonly IObjectProvider<IType> OutsideTheDataLayer =
        TypesIn(AllAssemblies.Except([InfrastructureAssembly]), "production code outside the data layer");

    // Deliberately no selector for Domain, Application or Infrastructure. Each holds no types yet, so a selector
    // would match nothing, and ArchUnitNET fails a rule whose subject is empty - ArchUnitSelectorCoverageTests reports
    // one the moment it is added, which is the point at which its first rule should be too (test-antipatterns §12).
    //
    // What matters about those three is enforced without a selector: ProjectStructureTests holds each project to an
    // allow-list of references, and the MongoDB ban above is written over everything outside the data layer, so
    // Domain and Application are forbidden MongoDB from their first type onward.

    // Loading is the expensive half of an architecture test, so it happens once per test process and only when a
    // rule asks for it. Lazy rather than an eager field because a type initializer runs every field initializer
    // together: an eager one would make a test that wants nothing but an Assembly out of this class pay for the
    // whole model. Fully qualified because the enclosing JobTracker.Architecture namespace shadows the type name.
    private static readonly Lazy<ArchUnitNET.Domain.Architecture> LoadedModel =
        new(static () => new ArchLoader().LoadAssemblies(AllAssemblies).Build());

    /// <summary>Every production assembly as one ArchUnitNET model, built on first use and shared from then on.</summary>
    internal static ArchUnitNET.Domain.Architecture Loaded => LoadedModel.Value;

    /// <summary>
    /// Every production assembly except the ones passed in, as one selector — the "forbidden" half of a layer rule
    /// written as an allow-list.
    /// </summary>
    /// <remarks>
    /// Stated as an allow-list on purpose: everything a layer may reach is named, and every other production assembly
    /// is forbidden. A project added to the solution later is therefore off limits until someone widens the allowed
    /// set, rather than being reachable because no rule happens to mention it yet.
    /// </remarks>
    internal static IObjectProvider<IType> EveryLayerOutsideReachOf(string layerName, params Assembly[] allowed) =>
        TypesIn(AllAssemblies.Except(allowed), $"production layers that {layerName} may not depend on");

    private static IObjectProvider<IType> TypesIn(IEnumerable<Assembly> assemblies, string description)
    {
        Assembly[] selected = [.. assemblies];
        return Types().That().ResideInAssembly(selected[0], selected[1..]).As(description);
    }
}
