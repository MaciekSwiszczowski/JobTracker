using System.Net;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace JobTracker.UI.Tests;

/// <summary>
/// Pins the main window: the list and its order, the search, opening an application, closing one, and what the
/// banner says when the service is down or refuses. Runs on the in-memory client, or on a Moq mock where one
/// test needs the service to refuse, to be unreachable, or to answer late.
/// </summary>
public sealed class MainViewModelTests
{
    private readonly FakeTimeProvider _time = CreateTime();

    // Trigger: the app starts while the service holds two open applications and one closed one.
    // Behavior: open applications come first, newest first, then the closed one, however recent.
    // Assertion: the list is Newer, Older, Closed.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task Refresh_WhenTheServiceHasApplications_ListsOpenOnesFirstThenNewest()
    {
        // Arrange
        var older = TestData.Application(company: "Older", appliedDaysAgo: 10);
        var newer = TestData.Application(company: "Newer", appliedDaysAgo: 2);
        var closed = TestData.Application(company: "Closed", appliedDaysAgo: 1, status: ApplicationStatus.Closed);

        // Act
        var viewModel = await LoadedWithAsync(TestContext.Current.CancellationToken, older, closed, newer);

        // Assert
        viewModel.Applications.Select(static a => a.Company).ShouldBe(["Newer", "Older", "Closed"]);
    }

    // Trigger: the user types into the search box, in any letter case and with stray spaces.
    // Behavior: only applications whose company or title contains the text stay in the list.
    // Assertion: the list holds just the matching application.
    [Theory(Timeout = TestTimeouts.DefaultMilliseconds)]
    [InlineData("contoso")]
    [InlineData("  CONTOSO ")]
    [InlineData("backend")]
    public async Task SearchText_WhenTyped_MatchesCompanyOrTitleIgnoringCase(string search)
    {
        // Arrange
        var match = TestData.Application(company: "Contoso", title: "Backend Developer");
        var other = TestData.Application(company: "Fabrikam", title: "Tester");
        var viewModel = await LoadedWithAsync(TestContext.Current.CancellationToken, match, other);

        // Act
        viewModel.SearchText = search;

        // Assert
        viewModel.Applications.ShouldBe([TestData.Summary(match)]);
    }

    // Trigger: the user clicks an application in the list.
    // Behavior: the right-hand side shows that application in full.
    // Assertion: the detail is for Contoso's "Backend Developer".
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task SelectedApplication_WhenSet_OpensItsDetail()
    {
        // Arrange
        var viewModel = await LoadedWithAsync(TestContext.Current.CancellationToken,
            TestData.Application(company: "Contoso", title: "Backend Developer"));

        // Act
        await SelectAsync(viewModel, viewModel.Applications[0]);

        // Assert
        var detail = viewModel.Detail.ShouldNotBeNull();
        detail.CompanyName.ShouldBe("Contoso");
        detail.Title.ShouldBe("Backend Developer");
    }

    // Trigger: the user clears the selection in the list.
    // Behavior: the right-hand side empties.
    // Assertion: there is no detail.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task SelectedApplication_WhenCleared_ClosesTheDetail()
    {
        // Arrange
        var viewModel = await LoadedWithAsync(TestContext.Current.CancellationToken, TestData.Application());
        await SelectAsync(viewModel, viewModel.Applications[0]);

        // Act
        viewModel.SelectedApplication = null;

        // Assert
        viewModel.Detail.ShouldBeNull();
    }

    // Trigger: the user has an application open and types a search that still matches it.
    // Behavior: the detail stays as it is - rebuilding the list must neither close nor reload it.
    // Assertion: the detail is the same instance as before, and the selection is the same application.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task SearchText_WhenTypedWithAnApplicationOpen_KeepsTheDetail()
    {
        // Arrange
        var viewModel = await LoadedWithAsync(TestContext.Current.CancellationToken, TestData.Application(company: "Contoso"));
        await SelectAsync(viewModel, viewModel.Applications[0]);
        var detail = viewModel.Detail;

        // Act
        viewModel.SearchText = "Cont";

        // Assert
        viewModel.Detail.ShouldBeSameAs(detail);
        viewModel.SelectedApplication.ShouldNotBeNull().Id.ShouldBe("contoso-developer");
    }

    // Trigger: the user clicks Contoso, then Fabrikam before Contoso's detail has arrived, and Contoso's answer
    //   arrives last.
    // Behavior: the detail shows Fabrikam, the application the user picked last, not the answer that came last.
    // Assertion: once both answers are in, the detail is Fabrikam's.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task SelectedApplication_WhenChangedBeforeTheFirstDetailArrives_ShowsTheLastPicked()
    {
        // Arrange
        var contoso = TestData.Application(company: "Contoso");
        var fabrikam = TestData.Application(company: "Fabrikam");
        var slowContoso = new TaskCompletionSource<ApplicationDetail>();
        var client = new Mock<IJobTrackerClient>();
        client.Setup(c => c.GetApplicationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([TestData.Summary(contoso), TestData.Summary(fabrikam)]);
        client.Setup(c => c.GetApplicationAsync(contoso.Id, It.IsAny<CancellationToken>())).Returns(slowContoso.Task);
        client.Setup(c => c.GetApplicationAsync(fabrikam.Id, It.IsAny<CancellationToken>())).ReturnsAsync(fabrikam);
        var viewModel = await LoadedWithAsync(client.Object, TestContext.Current.CancellationToken);
        viewModel.SelectedApplication = viewModel.Applications.Single(static a => a.Company == "Contoso");
        var contosoLoad = viewModel.OpenCommand.ExecutionTask.ShouldNotBeNull();
        await SelectAsync(viewModel, viewModel.Applications.Single(static a => a.Company == "Fabrikam"));

        // Act
        slowContoso.SetResult(contoso);
        await contosoLoad.WaitAsync(TestContext.Current.CancellationToken);

        // Assert
        viewModel.Detail.ShouldNotBeNull().CompanyName.ShouldBe("Fabrikam");
    }

    // Trigger: the user picks a reason and presses Close on an open application.
    // Behavior: the service is told, the list shows the application closed, and the detail shows it closed and
    //   stays on the same application.
    // Assertion: the stored application is closed with today's date and the reason, the list row is Closed, and
    //   the detail is the same id with IsOpen false.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task Close_WhenConfirmed_ClosesItInTheServiceListAndDetail()
    {
        // Arrange
        var client = new InMemoryJobTrackerClient([TestData.Application(company: "Contoso")]);
        var viewModel = await LoadedWithAsync(client, TestContext.Current.CancellationToken);
        await SelectAsync(viewModel, viewModel.Applications[0]);
        var detail = viewModel.Detail.ShouldNotBeNull();
        detail.CloseReason = "Withdrawn";

        // Act
        await detail.CloseCommand.ExecuteAsync(null).WaitAsync(TestContext.Current.CancellationToken);

        // Assert
        var stored = await client.GetApplicationAsync("contoso-developer", TestContext.Current.CancellationToken);
        stored.Status.ShouldBe(ApplicationStatus.Closed);
        stored.Closure.ShouldBe(new Closure(new DateOnly(2026, 10, 28), "Withdrawn"));
        viewModel.Applications.ShouldHaveSingleItem().Status.ShouldBe(ApplicationStatus.Closed);
        var reopened = viewModel.Detail.ShouldNotBeNull();
        reopened.Id.ShouldBe("contoso-developer");
        reopened.IsOpen.ShouldBeFalse();
    }

    // Trigger: the user presses Close, and the service refuses with its own reason.
    // Behavior: the banner shows the service's reason, and the application stays open on screen.
    // Assertion: the error message is the service's sentence, and the list row is still Open.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task Close_WhenTheServiceRefuses_ShowsItsReason()
    {
        // Arrange
        var application = TestData.Application();
        var client = new Mock<IJobTrackerClient>();
        client.Setup(c => c.GetApplicationsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([TestData.Summary(application)]);
        client.Setup(c => c.GetApplicationAsync(application.Id, It.IsAny<CancellationToken>())).ReturnsAsync(application);
        client.Setup(c => c.CloseApplicationAsync(application.Id, It.IsAny<Closure>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("The closing date is before the applied date.", null, HttpStatusCode.BadRequest));
        var viewModel = await LoadedWithAsync(client.Object, TestContext.Current.CancellationToken);
        await SelectAsync(viewModel, viewModel.Applications[0]);
        var detail = viewModel.Detail.ShouldNotBeNull();
        detail.CloseReason = "Rejected";

        // Act
        await detail.CloseCommand.ExecuteAsync(null).WaitAsync(TestContext.Current.CancellationToken);

        // Assert
        viewModel.ErrorMessage.ShouldBe("The closing date is before the applied date.");
        viewModel.Applications.ShouldHaveSingleItem().Status.ShouldBe(ApplicationStatus.Open);
    }

    // Trigger: the app starts while the service is not running.
    // Behavior: the banner says how to start it, and the list is empty rather than stale.
    // Assertion: the error message is the start-the-service sentence, and there are no applications.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task Refresh_WhenTheServiceIsNotRunning_SaysHowToStartIt()
    {
        // Arrange
        var client = new Mock<IJobTrackerClient>();
        client.Setup(c => c.GetApplicationsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused (localhost:5080)"));

        // Act
        var viewModel = await LoadedWithAsync(client.Object, TestContext.Current.CancellationToken);

        // Assert
        viewModel.ErrorMessage.ShouldBe("Cannot reach the JobTracker service. Start it with: dotnet run --project src/JobTracker.Api");
        viewModel.Applications.ShouldBeEmpty();
    }

    // Trigger: the service was down at start-up; the user starts it and presses Refresh.
    // Behavior: the banner goes away and the applications appear.
    // Assertion: no error message, and one application listed.
    [Fact(Timeout = TestTimeouts.DefaultMilliseconds)]
    public async Task Refresh_WhenTheServiceAnswersAgain_ClearsTheBanner()
    {
        // Arrange
        var client = new Mock<IJobTrackerClient>();
        client.SetupSequence(c => c.GetApplicationsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused (localhost:5080)"))
            .ReturnsAsync([TestData.Summary(TestData.Application())]);
        var viewModel = await LoadedWithAsync(client.Object, TestContext.Current.CancellationToken);

        // Act
        await viewModel.RefreshCommand.ExecuteAsync(null).WaitAsync(TestContext.Current.CancellationToken);

        // Assert
        viewModel.ErrorMessage.ShouldBeNull();
        viewModel.Applications.ShouldHaveSingleItem();
    }

    private static FakeTimeProvider CreateTime()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 28, 9, 0, 0, TimeSpan.Zero));
        time.SetLocalTimeZone(TimeZoneInfo.Utc);
        return time;
    }

    // Selecting starts loading the detail in the background, as a click in the list does; this waits for it.
    private static Task SelectAsync(MainViewModel viewModel, ApplicationSummary application)
    {
        viewModel.SelectedApplication = application;
        return viewModel.OpenCommand.ExecutionTask.ShouldNotBeNull().WaitAsync(TestContext.Current.CancellationToken);
    }

    private Task<MainViewModel> LoadedWithAsync(CancellationToken cancellationToken, params ApplicationDetail[] applications) =>
        LoadedWithAsync(new InMemoryJobTrackerClient(applications), cancellationToken);

    private async Task<MainViewModel> LoadedWithAsync(IJobTrackerClient client, CancellationToken cancellationToken)
    {
        var viewModel = new MainViewModel(client, _time);
        await viewModel.RefreshCommand.ExecuteAsync(null).WaitAsync(cancellationToken);
        return viewModel;
    }
}
