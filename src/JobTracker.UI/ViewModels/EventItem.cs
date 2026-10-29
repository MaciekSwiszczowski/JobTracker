using System.Globalization;
using JobTracker.Contracts;
using JobTracker.UI.Formatting;

namespace JobTracker.UI.ViewModels;

/// <summary>One entry of the timeline.</summary>
public sealed class EventItem(ApplicationEvent applicationEvent)
{
    /// <summary>When it happened, in the offset it happened in: "2026-10-22 14:30".</summary>
    public string When { get; } = applicationEvent.At.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    /// <summary>The kind, made readable: "recruiterCall" becomes "Recruiter call".</summary>
    public string Kind { get; } = PropertyFlattener.Humanize(applicationEvent.Type);

    /// <summary>The one-sentence summary.</summary>
    public string Summary { get; } = applicationEvent.Summary;

    /// <summary>The free-form details, as rows; empty when the event has none.</summary>
    public IReadOnlyList<PropertyRow> Details { get; } = PropertyFlattener.Flatten(applicationEvent.Details);

    /// <summary>Whether there are any details to show.</summary>
    public bool HasDetails => Details.Count > 0;
}
