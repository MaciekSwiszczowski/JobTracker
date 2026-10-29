namespace JobTracker.UI.Tests;

/// <summary>
/// Pins the detail view of one application: which standard properties it shows and how, the order of the
/// timeline, the free-form part, and when and how it lets the user close the application.
/// </summary>
public sealed class ApplicationDetailViewModelTests
{
    // Trigger: the user opens an application that has every standard property.
    // Behavior: the standard properties come first, in a fixed order, as text.
    // Assertion: applied date, status, company website and posting, with the literal values.
    [Fact]
    public void StandardFields_WhenEverythingIsKnown_ListsThemInOrder()
    {
        // Arrange
        var application = TestData.Application(appliedDaysAgo: 8) with
        {
            Company = new Company("Contoso", "https://contoso.example"),
            SourceUrl = "https://jobs.contoso.example/4711",
        };

        // Act
        var detail = Open(application);

        // Assert
        detail.StandardFields.ShouldBe(
        [
            new FieldRow("Applied on", "2026-10-20"),
            new FieldRow("Status", "Open"),
            new FieldRow("Company website", "https://contoso.example"),
            new FieldRow("Job posting", "https://jobs.contoso.example/4711"),
        ]);
    }

    // Trigger: the user opens an application the skill stored with only the required fields.
    // Behavior: properties that are not known are left out rather than shown empty.
    // Assertion: only the applied date and the status.
    [Fact]
    public void StandardFields_WhenOptionalOnesAreMissing_LeavesThemOut()
    {
        // Act
        var detail = Open(TestData.Application(appliedDaysAgo: 8));

        // Assert
        detail.StandardFields.Select(static f => f.Label).ShouldBe(["Applied on", "Status"]);
    }

    // Trigger: the user opens an application that was rejected.
    // Behavior: the status says when it ended and why, and the close controls are gone.
    // Assertion: status "Closed on 2026-10-25: Rejected", and IsOpen is false.
    [Fact]
    public void Status_WhenClosed_SaysWhenAndWhy()
    {
        // Arrange
        var application = TestData.Application(status: ApplicationStatus.Closed) with
        {
            Closure = new Closure(new DateOnly(2026, 10, 25), "Rejected"),
        };

        // Act
        var detail = Open(application);

        // Assert
        detail.StandardFields.Single(static f => f.Label == "Status").Value.ShouldBe("Closed on 2026-10-25: Rejected");
        detail.IsOpen.ShouldBeFalse();
    }

    // Trigger: the user opens an application with three updates, stored oldest first.
    // Behavior: the timeline shows the most recent update at the top, each with its time, kind and details.
    // Assertion: summaries newest first; the top one's time, readable kind, and its one detail row.
    [Fact]
    public void Events_WhenThereAreSeveral_ListsTheNewestFirst()
    {
        // Arrange
        var offset = TimeSpan.FromHours(1);
        var application = TestData.Application() with
        {
            Events =
            [
                new ApplicationEvent(new DateTimeOffset(2026, 10, 20, 9, 0, 0, offset), "applied", "Sent the CV."),
                new ApplicationEvent(new DateTimeOffset(2026, 10, 27, 10, 0, 0, offset), "interview", "First interview.",
                    TestData.Json("""{ "format": "video" }""")),
                new ApplicationEvent(new DateTimeOffset(2026, 10, 22, 14, 30, 0, offset), "recruiterCall", "Anna called."),
            ],
        };

        // Act
        var detail = Open(application);

        // Assert
        detail.Events.Select(static e => e.Summary).ShouldBe(["First interview.", "Anna called.", "Sent the CV."]);
        detail.Events[0].When.ShouldBe("2026-10-27 10:00");
        detail.Events[1].Kind.ShouldBe("Recruiter call");
        detail.Events[0].Details.ShouldBe([new PropertyRow(0, "Format", "video")]);
    }

    // Trigger: the user opens an application whose free-form part holds a location and a work mode.
    // Behavior: they are listed under "Other details", in the order they were stored.
    // Assertion: the two rows, with readable names.
    [Fact]
    public void ExtraFields_WhenTheApplicationHasThem_ListsEachOne()
    {
        // Arrange
        var application = TestData.Application() with
        {
            Extra = TestData.Json("""{ "location": "Warsaw", "workMode": "hybrid" }"""),
        };

        // Act
        var detail = Open(application);

        // Assert
        detail.ExtraFields.ShouldBe([new PropertyRow(0, "Location", "Warsaw"), new PropertyRow(0, "Work mode", "hybrid")]);
        detail.HasExtraFields.ShouldBeTrue();
    }

    // Trigger: the user looks at the close controls, before and after picking a reason, on an open or closed
    //   application.
    // Behavior: Close is enabled only for an open application with a reason picked.
    // Assertion: the close command's CanExecute matches the expected value on each row.
    [Theory]
    [InlineData(ApplicationStatus.Open, null, false)]
    [InlineData(ApplicationStatus.Open, "Rejected", true)]
    [InlineData(ApplicationStatus.Closed, "Rejected", false)]
    public void CloseCommand_WhenStatusAndReasonVary_IsEnabledOnlyWhenOpenWithAReason(ApplicationStatus status, string? reason, bool expected)
    {
        // Arrange
        var detail = Open(TestData.Application(status: status));

        // Act
        detail.CloseReason = reason;

        // Assert
        detail.CloseCommand.CanExecute(null).ShouldBe(expected);
    }

    // Trigger: the user picks "Withdrawn" and presses Close, leaving the date as it was offered.
    // Behavior: the closure handed over is dated today, with the picked reason, exactly once.
    // Assertion: the callback received one closure: 28 October 2026, "Withdrawn".
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task CloseCommand_WhenExecuted_HandsOverTodayAndTheReason()
    {
        // Arrange
        var closures = new List<Closure>();
        var detail = new ApplicationDetailViewModel(TestData.Application(), TestData.Today, closure =>
        {
            closures.Add(closure);
            return Task.CompletedTask;
        });
        detail.CloseReason = "Withdrawn";

        // Act
        await detail.CloseCommand.ExecuteAsync(null).WaitAsync(TestContext.Current.CancellationToken);

        // Assert
        closures.ShouldBe([new Closure(new DateOnly(2026, 10, 28), "Withdrawn")]);
    }

    private static ApplicationDetailViewModel Open(ApplicationDetail application) =>
        new(application, TestData.Today, static _ => Task.CompletedTask);
}
