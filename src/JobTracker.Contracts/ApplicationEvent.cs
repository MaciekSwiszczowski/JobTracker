using System.Text.Json;

namespace JobTracker.Contracts;

/// <summary>
/// One thing that happened to an application: a recruiter call, an interview, a task, an offer. It is both the
/// body of <c>POST /applications/{id}/events</c> and an entry of <see cref="ApplicationDetail.Events"/>.
/// </summary>
/// <param name="At">When it happened, with the offset it happened in.</param>
/// <param name="Type">A short camelCase kind, from the list in docs/api.md where one fits.</param>
/// <param name="Summary">One sentence for the timeline.</param>
/// <param name="Details">Anything else worth keeping, as a JSON object of any shape.</param>
public sealed record ApplicationEvent(
    DateTimeOffset At,
    string Type,
    string Summary,
    JsonElement? Details = null);
