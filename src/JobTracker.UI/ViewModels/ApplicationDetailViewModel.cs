using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JobTracker.Contracts;
using JobTracker.UI.Formatting;

namespace JobTracker.UI.ViewModels;

/// <summary>
/// The right-hand side: one application, read-only. Standard properties first, then the timeline, then the
/// free-form properties, then the original posting. The one thing it lets the user do is close the application.
/// </summary>
public sealed partial class ApplicationDetailViewModel : ViewModelBase
{
    private readonly Func<Closure, Task> _close;

    /// <summary>Shows <paramref name="application"/>; <paramref name="close"/> is called when the user closes it.</summary>
    public ApplicationDetailViewModel(ApplicationDetail application, DateOnly today, Func<Closure, Task> close)
    {
        _close = close;
        Id = application.Id;
        CompanyName = application.Company.Name;
        Title = application.Title;
        IsOpen = application.Status == ApplicationStatus.Open;
        StandardFields = StandardFieldsOf(application);
        Events = [.. application.Events.OrderByDescending(static e => e.At).Select(static e => new EventItem(e))];
        ExtraFields = PropertyFlattener.Flatten(application.Extra);
        Description = application.Description;
        RawText = application.RawText;
        CloseOn = today.ToDateTime(TimeOnly.MinValue);
    }

    /// <summary>The reasons offered when closing. The service accepts any text; these are the common ones.</summary>
    public static IReadOnlyList<string> CloseReasons { get; } =
        ["Rejected", "Withdrawn", "No response", "Accepted offer", "Declined offer"];

    /// <summary>The application's id.</summary>
    public string Id { get; }

    /// <summary>Who the application went to.</summary>
    public string CompanyName { get; }

    /// <summary>The job title.</summary>
    public string Title { get; }

    /// <summary>True until the application is closed; the close controls show only while it is.</summary>
    public bool IsOpen { get; }

    /// <summary>The typed core of the document, in a fixed order, leaving out what is not known.</summary>
    public IReadOnlyList<FieldRow> StandardFields { get; }

    /// <summary>Everything that happened, the most recent first.</summary>
    public IReadOnlyList<EventItem> Events { get; }

    /// <summary>Whether there is a timeline to show.</summary>
    public bool HasEvents => Events.Count > 0;

    /// <summary>The free-form part of the document, whatever its shape.</summary>
    public IReadOnlyList<PropertyRow> ExtraFields { get; }

    /// <summary>Whether there are free-form properties to show.</summary>
    public bool HasExtraFields => ExtraFields.Count > 0;

    /// <summary>The role description from the posting.</summary>
    public string? Description { get; }

    /// <summary>The posting as it was pasted.</summary>
    public string? RawText { get; }

    /// <summary>The reason picked for closing; nothing until the user picks one.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CloseCommand))]
    public partial string? CloseReason { get; set; }

    /// <summary>The day to close it on; today unless the user picks another. A date picker binds a <see cref="DateTime"/>.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CloseCommand))]
    public partial DateTime? CloseOn { get; set; }

    [RelayCommand(CanExecute = nameof(CanClose))]
    private Task CloseAsync() => _close(new Closure(DateOnly.FromDateTime(CloseOn!.Value), CloseReason!));

    private bool CanClose() => IsOpen && !string.IsNullOrWhiteSpace(CloseReason) && CloseOn is not null;

    private static List<FieldRow> StandardFieldsOf(ApplicationDetail application)
    {
        var fields = new List<FieldRow>
        {
            new("Applied on", Iso(application.AppliedOn)),
            new("Status", application.Closure is { } closure
                ? $"Closed on {Iso(closure.ClosedOn)}: {closure.Reason}"
                : application.Status == ApplicationStatus.Open ? "Open" : "Closed"),
        };

        if (!string.IsNullOrWhiteSpace(application.Company.Website))
        {
            fields.Add(new FieldRow("Company website", application.Company.Website));
        }

        if (!string.IsNullOrWhiteSpace(application.SourceUrl))
        {
            fields.Add(new FieldRow("Job posting", application.SourceUrl));
        }

        return fields;
    }

    private static string Iso(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
