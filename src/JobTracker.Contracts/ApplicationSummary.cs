namespace JobTracker.Contracts;

/// <summary>One row of <c>GET /applications</c>: enough for the list, nothing more.</summary>
/// <param name="Id">The application's id.</param>
/// <param name="Company">The company name.</param>
/// <param name="Title">The job title.</param>
/// <param name="AppliedOn">The day the application was sent.</param>
/// <param name="Status">Open or closed.</param>
/// <param name="LastEventAt">When the latest event happened, or null when nothing has happened yet.</param>
public sealed record ApplicationSummary(
    string Id,
    string Company,
    string Title,
    DateOnly AppliedOn,
    ApplicationStatus Status,
    DateTimeOffset? LastEventAt);
