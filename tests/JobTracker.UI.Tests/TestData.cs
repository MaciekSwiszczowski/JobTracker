namespace JobTracker.UI.Tests;

/// <summary>Builders for the contract records the UI shows, dated from a fixed day so no test reads the clock.</summary>
internal static class TestData
{
    /// <summary>The "today" every UI test runs on. Fixed, so a test passes on any day of the year (§1).</summary>
    public static readonly DateOnly Today = new(2026, 10, 28);

    /// <summary>An application with only the required fields set, applied <paramref name="appliedDaysAgo"/> days before <see cref="Today"/>.</summary>
    public static ApplicationDetail Application(
        string company = "Contoso",
        string title = "Developer",
        int appliedDaysAgo = 1,
        ApplicationStatus status = ApplicationStatus.Open,
        string? id = null) =>
        new(
            id ?? $"{company}-{title}".ToLowerInvariant().Replace(' ', '-'),
            new Company(company),
            title,
            Today.AddDays(-appliedDaysAgo),
            status,
            []);

    /// <summary>The list row the service shows for <paramref name="application"/> while it has no events.</summary>
    public static ApplicationSummary Summary(ApplicationDetail application) =>
        new(application.Id, application.Company.Name, application.Title, application.AppliedOn, application.Status, null);

    /// <summary>Parses a JSON literal into the element a contract record carries.</summary>
    public static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
