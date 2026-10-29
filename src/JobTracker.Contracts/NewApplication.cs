using System.Text.Json;

namespace JobTracker.Contracts;

/// <summary>
/// The body of <c>POST /applications</c>: one application, as the skill extracts it from a pasted job offer.
/// </summary>
/// <param name="Company">Who the application went to.</param>
/// <param name="Title">The job title from the posting.</param>
/// <param name="AppliedOn">The day the application was sent.</param>
/// <param name="Description">The role description, in the posting's words.</param>
/// <param name="SourceUrl">Where the posting was found.</param>
/// <param name="Extra">
/// Everything else the posting says, as a JSON object of any shape: salary, location, tech stack, recruiter.
/// The service stores it as it comes; docs/api.md lists the keys the skill uses by convention.
/// </param>
/// <param name="RawText">The pasted posting, verbatim, so nothing the skill missed is lost.</param>
public sealed record NewApplication(
    Company Company,
    string Title,
    DateOnly AppliedOn,
    string? Description = null,
    string? SourceUrl = null,
    JsonElement? Extra = null,
    string? RawText = null);
