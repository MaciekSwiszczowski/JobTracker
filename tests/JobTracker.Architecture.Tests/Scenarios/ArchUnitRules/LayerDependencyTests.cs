namespace JobTracker.Architecture.Tests.Scenarios.ArchUnitRules;

/// <summary>
/// The dependency direction between the production projects, at the level of the types that actually use each
/// other: which layer may reach which, and which may reach MongoDB.
/// </summary>
/// <remarks>
/// <para>Two rules bind every class in this folder:</para>
/// <list type="bullet">
/// <item>Rules are written with ArchUnitNET alone: no reflection, no file parsing. A check that cannot be expressed
/// as a fluent rule belongs in a test outside this folder, not in here in a second style.</item>
/// <item>An agent may add a rule here. Only a human may change or delete one that is already committed. Otherwise an
/// agent whose work fails a rule can edit the rule instead of fixing the code, and the rule protects nothing.</item>
/// </list>
/// <para>
/// Domain, Application and Infrastructure hold no types yet, so no rule here can name them as its subject: ArchUnitNET
/// fails a rule that selects nothing. Their project-level boundaries are held by
/// <see cref="ProjectStructureTests"/> until then; the type-level rules for them land with their first types.
/// </para>
/// </remarks>
public sealed class LayerDependencyTests
{
    // Trigger: a domain value object is given a [BsonElement] attribute, or a repository port takes an
    //   IMongoCollection, because the type was already to hand.
    // Behavior: the build fails and names the type, so the domain stays plain C# and the stored shape stays in the
    //   one place that maps it.
    // Assertion: no production type outside JobTracker.Infrastructure.MongoDb depends on a MongoDB.* type.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void MongoDbTypes_AreReachedOnlyByTheDataLayer()
    {
        // The rule the whole layer split exists to make possible. Written over "everything outside the data layer"
        // rather than "Domain must not ..." so it binds today, through Contracts, the API and the UI, and forbids
        // Domain and Application from their first type onward.
        //
        // It also covers "no type outside Infrastructure carries a [Bson...] attribute": an attribute is a
        // dependency of the type or member it decorates, and every one of them lives under MongoDB.Bson.
        //
        // The API is inside the ban too. It is the composition root, but registering the data layer is one call,
        // AddMongoDbInfrastructure, which lives in Infrastructure; the API has no reason to name a driver type.
        // Assert
        Types()
            .That().Are(SolutionArchitecture.OutsideTheDataLayer)
            .Should().FollowCustomCondition(
                ExternalPackages.NotDependingOn(ExternalPackages.MongoDb),
                "not depend on a MongoDB.* type")
            .Because("MongoDB types never appear outside the data layer: the domain is plain C#, and the stored shape is defined in one place.")
            .Check(SolutionArchitecture.Loaded);
    }

    // Trigger: a record in JobTracker.Contracts is given a domain value object or an API helper, because it was
    //   already to hand when the record was written.
    // Behavior: the build fails, so the wire format stays a set of plain records anything can serialise.
    // Assertion: no Contracts type depends on any other production assembly.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void Contracts_ReachesNoOtherProductionLayer()
    {
        // Contracts is shared by the API and the UI, and its records are the third copy of docs/api.md. A leaf that
        // reaches back into a layer would drag that layer into the UI, and make the wire format change whenever the
        // use cases do — which is what keeping Application and Contracts apart exists to prevent.
        // Assert
        Types()
            .That().Are(SolutionArchitecture.Contracts)
            .Should().NotDependOnAny(
                SolutionArchitecture.EveryLayerOutsideReachOf(
                    "the wire format",
                    SolutionArchitecture.ContractsAssembly))
            .Because("the wire format is the seam between the service and its clients; a leaf that reaches back stops being one.")
            .Check(SolutionArchitecture.Loaded);
    }

    // Trigger: a view model is given a domain type, or calls a use case directly, to skip a round trip to the service.
    // Behavior: the build fails, and the UI goes on talking to the service over HTTP through Contracts.
    // Assertion: UI types depend on Contracts and on no other production assembly.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void Ui_ReachesOnlyContracts()
    {
        // Assert
        Types()
            .That().Are(SolutionArchitecture.Ui)
            .Should().NotDependOnAny(
                SolutionArchitecture.EveryLayerOutsideReachOf(
                    "the UI",
                    SolutionArchitecture.UiAssembly,
                    SolutionArchitecture.ContractsAssembly))
            .Because("the UI is a client of the service: it reads what the service returns, and a shortcut into the layers would bypass the HTTP contract.")
            .Check(SolutionArchitecture.Loaded);
    }

    // Trigger: an endpoint is given a view model or a UI formatting helper to shape its response.
    // Behavior: the build fails, so the service never needs Avalonia to start.
    // Assertion: no API type depends on the UI assembly.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void Api_ReachesEveryLayerButTheUi()
    {
        // The composition root may reach every layer below it. The one thing it may not reach is the other client
        // of the wire format: the dependency between the two runs over HTTP, and only one way.
        // Assert
        Types()
            .That().Are(SolutionArchitecture.Api)
            .Should().NotDependOnAny(
                SolutionArchitecture.EveryLayerOutsideReachOf(
                    "the service",
                    SolutionArchitecture.ApiAssembly,
                    SolutionArchitecture.ContractsAssembly,
                    SolutionArchitecture.DomainAssembly,
                    SolutionArchitecture.ApplicationAssembly,
                    SolutionArchitecture.InfrastructureAssembly))
            .Because("the service is the UI's server, not its host: the UI depends on the service over HTTP, never the reverse.")
            .Check(SolutionArchitecture.Loaded);
    }
}
