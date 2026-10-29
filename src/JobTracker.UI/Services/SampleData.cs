using System.Text.Json;
using JobTracker.Contracts;

namespace JobTracker.UI.Services;

/// <summary>
/// A few applications for the sample mode, shaped like the ones the skill stores: a typed core, a timeline, and
/// a free-form extra part that differs from one application to the next. Dated relative to "today" so the
/// sample never looks stale.
/// </summary>
internal static class SampleData
{
    /// <summary>Builds the sample applications, the most recent one applied two days before <paramref name="today"/>.</summary>
    public static IReadOnlyList<ApplicationDetail> Create(DateOnly today)
    {
        var offset = TimeSpan.FromHours(1);
        DateTimeOffset At(int daysAgo, int hour) => new(today.AddDays(-daysAgo).ToDateTime(new TimeOnly(hour, 0)), offset);

        return
        [
            new ApplicationDetail(
                "sample-contoso",
                new Company("Contoso", "https://contoso.example"),
                "Senior .NET Developer",
                today.AddDays(-12),
                ApplicationStatus.Open,
                [
                    new ApplicationEvent(At(10, 14), "recruiterCall", "Anna called: first interview booked.",
                        Json("""{ "format": "video", "interviewers": ["Tech lead", "Engineering manager"] }""")),
                    new ApplicationEvent(At(5, 10), "interview", "First interview. Went well; take-home task next.",
                        Json("""{ "durationMinutes": 60, "topics": ["MongoDB indexing", "async/await", "system design"] }""")),
                    new ApplicationEvent(At(3, 9), "assignment", "Take-home task received, due in a week."),
                ],
                Description: "Build and run the payments platform in a team of six. C# on .NET, MongoDB, Kafka, Azure.",
                SourceUrl: "https://jobs.contoso.example/4711",
                Extra: Json("""
                    {
                      "salary": { "min": 24000, "max": 30000, "currency": "PLN", "period": "month", "contract": "B2B" },
                      "location": "Warsaw",
                      "workMode": "hybrid",
                      "officeDaysPerWeek": 2,
                      "techStack": ["C#", ".NET", "MongoDB", "Kafka", "Azure"],
                      "recruiter": { "name": "Anna Nowak", "email": "anna.nowak@contoso.example" }
                    }
                    """),
                RawText: "Senior .NET Developer at Contoso\nWarsaw (hybrid, 2 days a week in the office)\n24 000 - 30 000 PLN net/month on B2B\n\nYou will build and run our payments platform..."),
            new ApplicationDetail(
                "sample-fabrikam",
                new Company("Fabrikam"),
                "Backend Engineer (Go or C#)",
                today.AddDays(-2),
                ApplicationStatus.Open,
                [],
                SourceUrl: "https://www.linkedin.example/jobs/12345",
                Extra: Json("""{ "workMode": "remote", "timeZone": "CET ± 2h", "languages": ["English"], "source": "LinkedIn" }""")),
            new ApplicationDetail(
                "sample-northwind",
                new Company("Northwind Traders", "https://northwind.example"),
                "Software Engineer, Data Platform",
                today.AddDays(-30),
                ApplicationStatus.Closed,
                [
                    new ApplicationEvent(At(20, 16), "followUp", "Sent a follow-up e-mail to the recruiter."),
                    new ApplicationEvent(At(9, 11), "rejection", "They went with a candidate with more Spark experience."),
                ],
                Closure: new Closure(today.AddDays(-9), "Rejected"),
                Extra: Json("""{ "salary": { "max": 160000, "currency": "EUR", "period": "year" }, "location": "Berlin", "relocation": true }""")),
        ];
    }

    private static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
