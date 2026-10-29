using System.Text.Json;

namespace JobTracker.Contracts;

/// <summary>The body of <c>GET /applications/{id}</c>: the whole application document.</summary>
/// <param name="Id">The application's id.</param>
/// <param name="Company">Who the application went to.</param>
/// <param name="Title">The job title.</param>
/// <param name="AppliedOn">The day the application was sent.</param>
/// <param name="Status">Open or closed.</param>
/// <param name="Events">Everything that happened, in the order it happened.</param>
/// <param name="Description">The role description.</param>
/// <param name="SourceUrl">Where the posting was found.</param>
/// <param name="Closure">How it ended; null while the application is open.</param>
/// <param name="Extra">The free-form part, exactly as stored.</param>
/// <param name="RawText">The original posting.</param>
public sealed record ApplicationDetail(
    string Id,
    Company Company,
    string Title,
    DateOnly AppliedOn,
    ApplicationStatus Status,
    IReadOnlyList<ApplicationEvent> Events,
    string? Description = null,
    string? SourceUrl = null,
    Closure? Closure = null,
    JsonElement? Extra = null,
    string? RawText = null);
