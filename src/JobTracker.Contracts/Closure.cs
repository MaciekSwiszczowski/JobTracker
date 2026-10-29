namespace JobTracker.Contracts;

/// <summary>
/// How an application ended. Both the body of <c>POST /applications/{id}/close</c> and
/// <see cref="ApplicationDetail.Closure"/>.
/// </summary>
/// <param name="ClosedOn">The day it ended.</param>
/// <param name="Reason">Why, in a few words: "Rejected", "Withdrawn", "Accepted offer".</param>
public sealed record Closure(DateOnly ClosedOn, string Reason);
