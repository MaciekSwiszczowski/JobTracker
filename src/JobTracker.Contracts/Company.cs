namespace JobTracker.Contracts;

/// <summary>The company the application went to.</summary>
/// <param name="Name">The name as the posting gives it. Required.</param>
/// <param name="Website">The company's own site, when the posting names it.</param>
public sealed record Company(string Name, string? Website = null);
