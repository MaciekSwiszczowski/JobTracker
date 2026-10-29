using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JobTracker.Contracts;
using JobTracker.UI.Services;

namespace JobTracker.UI.ViewModels;

/// <summary>
/// The main window: the list of applications on the left, the selected one on the right, and a banner for
/// when the service cannot be reached or refuses.
/// </summary>
public sealed partial class MainViewModel(IJobTrackerClient client, TimeProvider timeProvider) : ViewModelBase
{
    /// <summary>What the banner says when the service does not answer at all.</summary>
    internal const string ServiceUnreachableMessage =
        "Cannot reach the JobTracker service. Start it with: dotnet run --project src/JobTracker.Api";

    private readonly List<ApplicationSummary> _all = [];
    private bool _isRefreshingList;

    /// <summary>The applications that match the search: open ones first, then by applied date, newest first.</summary>
    public ObservableCollection<ApplicationSummary> Applications { get; } = [];

    /// <summary>Text to match against company and title.</summary>
    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    /// <summary>The application picked in the list.</summary>
    [ObservableProperty]
    public partial ApplicationSummary? SelectedApplication { get; set; }

    /// <summary>The selected application in full. Null while nothing is selected.</summary>
    [ObservableProperty]
    public partial ApplicationDetailViewModel? Detail { get; set; }

    /// <summary>Why the last call to the service failed; null when it succeeded.</summary>
    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ApplicationSummary> items = [];
        if (!await TryCallAsync(async () => items = await client.GetApplicationsAsync(cancellationToken)))
        {
            return;
        }

        _all.Clear();
        _all.AddRange(items);
        RefreshList();
    }

    // Concurrent on purpose: clicking a second application while the first is still loading must start loading
    // the second, not be ignored. The check after the await throws away whichever answer is no longer wanted.
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task OpenAsync(ApplicationSummary summary, CancellationToken cancellationToken)
    {
        ApplicationDetail? application = null;
        var loaded = await TryCallAsync(async () => application = await client.GetApplicationAsync(summary.Id, cancellationToken));
        if (!string.Equals(SelectedApplication?.Id, summary.Id, StringComparison.Ordinal))
        {
            return;
        }

        Detail = loaded && application is not null
            ? new ApplicationDetailViewModel(application, Today(), closure => CloseAsync(summary, closure))
            : null;
    }

    private async Task CloseAsync(ApplicationSummary summary, Closure closure)
    {
        if (!await TryCallAsync(() => client.CloseApplicationAsync(summary.Id, closure, CancellationToken.None)))
        {
            return;
        }

        await RefreshAsync(CancellationToken.None);
        if (SelectedApplication is { } selected)
        {
            await OpenAsync(selected, CancellationToken.None);
        }
    }

    partial void OnSearchTextChanged(string value) => RefreshList();

    partial void OnSelectedApplicationChanged(ApplicationSummary? value)
    {
        // Rebuilding the list makes the ListBox drop and re-take its selection. That is not the user picking
        // something, so it must not reload or close the detail.
        if (_isRefreshingList)
        {
            return;
        }

        if (value is null)
        {
            Detail = null;
        }
        else
        {
            OpenCommand.Execute(value);
        }
    }

    private void RefreshList()
    {
        var selectedId = SelectedApplication?.Id;
        _isRefreshingList = true;
        try
        {
            Applications.Clear();
            var visible = _all
                .Where(Matches)
                .OrderBy(static a => a.Status == ApplicationStatus.Open ? 0 : 1)
                .ThenByDescending(static a => a.AppliedOn);
            foreach (var application in visible)
            {
                Applications.Add(application);
            }

            // By id, not by value: after a refresh the row for the same application is a new record.
            SelectedApplication = Applications.FirstOrDefault(a => string.Equals(a.Id, selectedId, StringComparison.Ordinal));
        }
        finally
        {
            _isRefreshingList = false;
        }

        if (SelectedApplication is null)
        {
            Detail = null;
        }
    }

    private bool Matches(ApplicationSummary application) =>
        string.IsNullOrWhiteSpace(SearchText)
        || application.Company.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase)
        || application.Title.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase);

    // Every call to the service goes through here, so the banner always tells the truth about the last one.
    private async Task<bool> TryCallAsync(Func<Task> call)
    {
        try
        {
            await call();
            ErrorMessage = null;
            return true;
        }
        catch (HttpRequestException failure)
        {
            // No status means no answer at all; with one, the message is the service's own explanation.
            ErrorMessage = failure.StatusCode is null ? ServiceUnreachableMessage : failure.Message;
        }

        return false;
    }

    private DateOnly Today() => DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
}
