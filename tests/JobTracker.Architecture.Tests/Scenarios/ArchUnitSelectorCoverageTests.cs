using System.Reflection;

namespace JobTracker.Architecture.Tests.Scenarios;

/// <summary>
/// Guards the architecture rules against the one failure mode they cannot report themselves: a selector or a banned
/// name that matches nothing, and therefore passes for ever without checking anything.
/// </summary>
/// <remarks>
/// <para>
/// Outside <c>ArchUnitRules/</c> because both tests use reflection, which that folder does not admit — the first
/// enumerates <see cref="SolutionArchitecture"/>'s own fields, the second compares banned strings against
/// compile-time members. The ratchet still covers them: it applies to everything in this project, and only the
/// folder's ArchUnitNET-only <em>style</em> rule stops at its boundary.
/// </para>
/// <para>
/// ArchUnitNET already fails a rule whose subject is empty. It does not notice an empty <em>forbidden</em> set, or a
/// banned name nothing could ever match, and both turn a rule into one that checks nothing while staying green.
/// </para>
/// </remarks>
public sealed class ArchUnitSelectorCoverageTests
{
    // Trigger: a project is renamed, or its last type is moved out, and a selector in SolutionArchitecture goes on
    //   compiling while matching nothing.
    // Behavior: the empty selector is reported here, rather than every rule that forbids it turning green and staying
    //   green.
    // Assertion: every SolutionArchitecture selector and every allow-list provider the layer rules use matches at
    //   least one type in the loaded model.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void EverySelector_MatchesAtLeastOneType()
    {
        // The named selectors are read off SolutionArchitecture by reflection rather than listed here, so one added
        // there is covered without anyone remembering to extend a list — a hand-kept list would drift in exactly the
        // way this test exists to catch, one indirection up. This is not the reflection test-antipatterns §5 bans:
        // SolutionArchitecture is part of the harness, not the code under test.
        //
        // The allow-list providers below are built by a method rather than held in a field, so they cannot be
        // enumerated and are named explicitly. They matter as much as the selectors: they are the *forbidden* halves
        // of the layer rules, and a forbidden set that empties out is a layer rule that checks nothing.
        // Arrange
        var named = typeof(SolutionArchitecture)
            .GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(static field => typeof(IObjectProvider<IType>).IsAssignableFrom(field.FieldType))
            .Select(static field => (field.Name, Selector: (IObjectProvider<IType>)field.GetValue(null)!));

        (string Name, IObjectProvider<IType> Selector)[] allowListProviders =
        [
            ("EveryLayerOutsideReachOf(the wire format)",
                SolutionArchitecture.EveryLayerOutsideReachOf(
                    "the wire format",
                    SolutionArchitecture.ContractsAssembly)),
            ("EveryLayerOutsideReachOf(the UI)",
                SolutionArchitecture.EveryLayerOutsideReachOf(
                    "the UI",
                    SolutionArchitecture.UiAssembly,
                    SolutionArchitecture.ContractsAssembly)),
            ("EveryLayerOutsideReachOf(the service)",
                SolutionArchitecture.EveryLayerOutsideReachOf(
                    "the service",
                    SolutionArchitecture.ApiAssembly,
                    SolutionArchitecture.ContractsAssembly,
                    SolutionArchitecture.DomainAssembly,
                    SolutionArchitecture.ApplicationAssembly,
                    SolutionArchitecture.InfrastructureAssembly)),
        ];

        // Act
        var empty = named
            .Concat(allowListProviders)
            .Where(static selector => !selector.Selector.GetObjects(SolutionArchitecture.Loaded).Any())
            .Select(static selector => selector.Name)
            .Order(StringComparer.Ordinal)
            .ToList();

        // Assert
        empty.ShouldBeEmpty(
            "These selectors match no type, so every rule written against one of them passes without checking "
            + "anything. Either the assembly is no longer loaded, the types moved, or an allow-list was widened until "
            + "nothing was left to forbid. Fix the selector — do not delete the rules that depend on it.");
    }

    // Trigger: a banned clock member in ExternalPackages is mistyped, or a ban is added and spelled from memory.
    // Behavior: the mismatch is reported here, instead of the ban matching nothing and the rule passing for ever.
    // Assertion: the wall-clock group is exactly the getters of DateTime.Now, UtcNow and Today, and of
    //   DateTimeOffset.Now and UtcNow.
    [Fact]
    public void EveryBannedMemberName_NamesAMemberThatExists()
    {
        // This ban matches nothing in the solution today and is meant to: it stops something arriving rather than
        // report something already here. That makes it exactly the ban a typo could silence with no visible effect.
        //
        // Sequence equality, not ShouldContain: containment only proves each member named here is in the array, so a
        // mistyped extra entry would sail through — which is the failure mode this test is named for.
        //
        // MongoDB is absent because it is banned by namespace prefix, not by member; EveryBannedPrefix_... covers it.
        // No Timeout: this reads in-memory metadata and cannot wait on anything (§2).
        // Assert
        ExternalPackages.WallClock.ShouldBe(
        [
            Getter(typeof(DateTime), nameof(DateTime.Now)),
            Getter(typeof(DateTime), nameof(DateTime.UtcNow)),
            Getter(typeof(DateTime), nameof(DateTime.Today)),
            Getter(typeof(DateTimeOffset), nameof(DateTimeOffset.Now)),
            Getter(typeof(DateTimeOffset), nameof(DateTimeOffset.UtcNow)),
        ]);
    }

    // Trigger: the MongoDB prefix in ExternalPackages is mistyped ("MongoDb.") or the driver renames its namespaces.
    // Behavior: the mismatch is reported here, instead of the data-layer ban matching nothing and passing for ever.
    // Assertion: the full names of BsonDocument, BsonElementAttribute and IMongoCollection<T> all start with the
    //   banned prefix.
    [Fact]
    public void EveryBannedPrefix_MatchesTheLibraryItBans()
    {
        // The ban's subject is non-empty, but that proves nothing about the prefix: a typo would leave every type in
        // the subject passing. These three are the shapes the ban exists for — the untyped document, a mapping
        // attribute, and the collection a port must not take. The driver reaches this project transitively, through
        // its reference to Infrastructure. No Timeout: compile-time metadata only (§2).
        // Arrange
        string[] driverTypes =
        [
            typeof(MongoDB.Bson.BsonDocument).FullName!,
            typeof(MongoDB.Bson.Serialization.Attributes.BsonElementAttribute).FullName!,
            typeof(MongoDB.Driver.IMongoCollection<>).FullName!,
        ];

        // Act
        var unmatched = driverTypes
            .Where(static name => !ExternalPackages.MongoDb.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .ToList();

        // Assert
        unmatched.ShouldBeEmpty("The MongoDB ban's prefix no longer matches these driver types, so it bans nothing.");
    }

    /// <summary>
    /// The ArchUnitNET name of a static property's getter, as <see cref="ExternalPackages.CallsTo"/> builds it: the
    /// declaring type's full name, the getter's name, and its empty parameter list.
    /// </summary>
    private static string Getter(Type type, string property) =>
        $"{type.FullName}.{type.GetProperty(property, BindingFlags.Public | BindingFlags.Static)!.GetMethod!.Name}()";
}
