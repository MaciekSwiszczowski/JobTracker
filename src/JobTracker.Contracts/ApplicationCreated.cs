namespace JobTracker.Contracts;

/// <summary>The body of the <c>201 Created</c> answer to <c>POST /applications</c>.</summary>
/// <param name="Id">The new application's id, for the events and close endpoints.</param>
public sealed record ApplicationCreated(string Id);
