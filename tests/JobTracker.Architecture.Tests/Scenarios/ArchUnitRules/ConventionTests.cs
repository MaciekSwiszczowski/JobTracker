namespace JobTracker.Architecture.Tests.Scenarios.ArchUnitRules;

/// <summary>
/// Solution-wide bans and naming conventions that no analyzer enforces under every configuration.
/// </summary>
/// <remarks>
/// <para>Two rules bind every class in this folder:</para>
/// <list type="bullet">
/// <item>Rules are written with ArchUnitNET alone: no reflection, no file parsing. A check that cannot be expressed
/// as a fluent rule belongs in a test outside this folder, not in here in a second style.</item>
/// <item>An agent may add a rule here. Only a human may change or delete one that is already committed. Otherwise an
/// agent whose work fails a rule can edit the rule instead of fixing the code, and the rule protects nothing.</item>
/// </list>
/// </remarks>
public sealed class ConventionTests
{
    // A method member's Name carries its parameter list — "GetApplicationsAsync(CancellationToken)" — so
    // HaveNameEndingWith("Async") matches nothing and any rule built on it is silently empty.
    private const string NamedAsync = @"^[^(]*Async\(";

    // Trigger: "awaiting response for 14 days" is computed from DateTime.Now in a Debug build, where the analyzer
    //   that bans it does not run.
    // Behavior: the suite fails anyway, so the rule cannot pass locally and only fail at the Debug_Analyzers gate.
    // Assertion: no production type reads DateTime.Now, DateTime.UtcNow, DateTime.Today, DateTimeOffset.Now or
    //   DateTimeOffset.UtcNow.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void ProductionCode_ReadsNoWallClock()
    {
        // A date rule tested against the wall clock passes in June and fails in July (test-antipatterns §1). The fix
        // is always the same: take a TimeProvider, and let the test pin it with FakeTimeProvider.
        // Assert
        Types()
            .That().Are(SolutionArchitecture.Production)
            .Should().FollowCustomCondition(
                ExternalPackages.NotCalling(ExternalPackages.WallClock),
                "not read the wall clock")
            .Because("the clock is an input: production code reads time through an injected TimeProvider so a test can pin it.")
            .Check(SolutionArchitecture.Loaded);
    }

    // Trigger: an async method is added under a name that gives the caller no hint it must be awaited.
    // Behavior: the build fails, so a caller cannot drop the await and never notice.
    // Assertion: every async method that returns something is named with the Async suffix.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void AsyncMethods_AreNamedAsync()
    {
        // The subject is the async keyword, which the compiler records as this attribute. Methods that hand a task
        // through without awaiting it are out of scope on purpose: selecting by name instead would need a return type
        // list, and a list can only ever be Task and ValueTask, so a method returning a custom awaitable would fail a
        // rule it satisfies. Narrow and right beats wide and wrong.
        //
        // Async void is excluded here and banned outright below: the suffix is a promise that a caller can await the
        // method, and nobody can await a void.
        //
        // The UI's async lambdas compile to methods named <RefreshAsync>b__..., which would fail the pattern; the
        // rule passes over them because ArchUnitNET's loader leaves compiler-generated members out of the model.
        //
        // VSTHRD200 says something similar, but only under Debug_Analyzers. This rule runs in every configuration the
        // suite runs in.
        // Assert
        MethodMembers()
            .That().HaveAnyAttributes(typeof(AsyncStateMachineAttribute))
            .And().DoNotHaveReturnType(typeof(void))
            .Should().HaveNameMatching(NamedAsync)
            .Because("a caller reads the suffix to know a method must be awaited.")
            .Check(SolutionArchitecture.Loaded);
    }

    // Trigger: an async void method is written — usually a UI event handler, so a call site does not have to be made
    //   async too.
    // Behavior: the build fails, because an async void that throws ends the process instead of faulting a task.
    // Assertion: no production method is async void.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public void NoProductionMethod_IsAsyncVoid()
    {
        // Unconditional, including the UI. Its commands are CommunityToolkit [RelayCommand] methods returning Task,
        // which is the form that lets a failure surface on the command instead of tearing the window down; there is
        // no event handler in it whose signature forces async void, so there is nothing to carve out.
        //
        // VSTHRD100 bans this too, but only under Debug_Analyzers.
        // Assert
        MethodMembers()
            .That().HaveReturnType(typeof(void))
            .And().HaveAnyAttributes(typeof(AsyncStateMachineAttribute))
            .Should().NotExist()
            .Because("an async void method that throws takes the process down instead of faulting a task the caller can observe.")
            .Check(SolutionArchitecture.Loaded);
    }
}
