namespace JobTracker.Contracts.Tests;

/// <summary>
/// Pins the read direction of the wire format: each example in docs/api deserializes into the contract records
/// with the values written in it.
/// </summary>
public sealed class ExampleDocumentTests
{
    // Trigger: the skill posts a new application shaped like docs/api/new-application.example.json.
    // Behavior: every field, including the free-form extra object, arrives in the record.
    // Assertion: company, title, date, links and two extra values are the literals in the example.
    [Fact]
    public void NewApplication_WhenReadFromTheExample_HasEveryField()
    {
        // Act
        var application = Examples.Read<NewApplication>("new-application");

        // Assert
        application.Company.ShouldBe(new Company("Contoso", "https://contoso.example"));
        application.Title.ShouldBe("Senior .NET Developer");
        application.AppliedOn.ShouldBe(new DateOnly(2026, 10, 20));
        application.SourceUrl.ShouldBe("https://jobs.contoso.example/4711");
        application.Description.ShouldNotBeNull().ShouldStartWith("Build and run the payments platform");
        application.RawText.ShouldNotBeNull().ShouldStartWith("Senior .NET Developer at Contoso\n");
        var extra = application.Extra.ShouldNotBeNull();
        extra.GetProperty("salary").GetProperty("min").GetInt32().ShouldBe(24_000);
        extra.GetProperty("techStack")[2].GetString().ShouldBe("MongoDB");
    }

    // Trigger: the skill posts an update shaped like docs/api/event.example.json.
    // Behavior: the time keeps its offset, and the details object arrives as it was written.
    // Assertion: 22 October 14:30 at +02:00, type recruiterCall, the summary, and the details' format "video".
    [Fact]
    public void ApplicationEvent_WhenReadFromTheExample_KeepsTheOffsetAndDetails()
    {
        // Act
        var applicationEvent = Examples.Read<ApplicationEvent>("event");

        // Assert
        applicationEvent.At.ShouldBe(new DateTimeOffset(2026, 10, 22, 14, 30, 0, TimeSpan.FromHours(2)));
        applicationEvent.At.Offset.ShouldBe(TimeSpan.FromHours(2));
        applicationEvent.Type.ShouldBe("recruiterCall");
        applicationEvent.Summary.ShouldBe("Anna called: first interview booked for Tuesday.");
        applicationEvent.Details.ShouldNotBeNull().GetProperty("format").GetString().ShouldBe("video");
    }

    // Trigger: an application is closed with a body shaped like docs/api/closure.example.json.
    // Behavior: the day and the reason arrive.
    // Assertion: 28 October 2026, "Rejected".
    [Fact]
    public void Closure_WhenReadFromTheExample_HasTheDayAndReason()
    {
        // Act
        var closure = Examples.Read<Closure>("closure");

        // Assert
        closure.ShouldBe(new Closure(new DateOnly(2026, 10, 28), "Rejected"));
    }

    // Trigger: the UI opens an application the service returns shaped like docs/api/application-detail.example.json.
    // Behavior: the closed status, both events in order, the closure and the extra object all arrive.
    // Assertion: status Closed, event types recruiterCall then rejection, the second without details, the closure
    //   literal, and the extra workMode.
    [Fact]
    public void ApplicationDetail_WhenReadFromTheExample_HasStatusEventsAndClosure()
    {
        // Act
        var detail = Examples.Read<ApplicationDetail>("application-detail");

        // Assert
        detail.Id.ShouldBe("6720f1a2c3d4e5f6a7b8c9d0");
        detail.Status.ShouldBe(ApplicationStatus.Closed);
        detail.Events.Select(static e => e.Type).ShouldBe(["recruiterCall", "rejection"]);
        detail.Events[1].Details.ShouldBeNull();
        detail.Closure.ShouldBe(new Closure(new DateOnly(2026, 10, 28), "Rejected"));
        detail.Extra.ShouldNotBeNull().GetProperty("workMode").GetString().ShouldBe("hybrid");
    }

    // Trigger: the UI loads the list the service returns shaped like docs/api/application-list.example.json.
    // Behavior: both rows arrive, and an application with no events has no last-event time.
    // Assertion: Contoso closed with a last event on 28 October, Fabrikam open with none.
    [Fact]
    public void ApplicationSummaries_WhenReadFromTheExample_HaveStatusAndLastEvent()
    {
        // Act
        var summaries = Examples.Read<ApplicationSummary[]>("application-list");

        // Assert
        summaries.ShouldBe(
        [
            new ApplicationSummary("6720f1a2c3d4e5f6a7b8c9d0", "Contoso", "Senior .NET Developer",
                new DateOnly(2026, 10, 20), ApplicationStatus.Closed,
                new DateTimeOffset(2026, 10, 28, 9, 15, 0, TimeSpan.FromHours(1))),
            new ApplicationSummary("6720f1a2c3d4e5f6a7b8c9d1", "Fabrikam", "Backend Engineer",
                new DateOnly(2026, 10, 24), ApplicationStatus.Open, null),
        ]);
    }
}
