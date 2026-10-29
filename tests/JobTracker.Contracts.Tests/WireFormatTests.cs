namespace JobTracker.Contracts.Tests;

/// <summary>
/// Pins the rules of the wire format itself: what is refused on the way in, and the exact text on the way out.
/// </summary>
public sealed class WireFormatTests
{
    // Trigger: the skill posts an application with a required field missing or null.
    // Behavior: deserializing refuses it, rather than handing the service a record with a null where none may be.
    // Assertion: a JsonException, for every row.
    [Theory]
    [InlineData("no company", """{ "title": "Developer", "appliedOn": "2026-10-20" }""")]
    [InlineData("company without a name", """{ "company": {}, "title": "Developer", "appliedOn": "2026-10-20" }""")]
    [InlineData("no title", """{ "company": { "name": "Contoso" }, "appliedOn": "2026-10-20" }""")]
    [InlineData("null title", """{ "company": { "name": "Contoso" }, "title": null, "appliedOn": "2026-10-20" }""")]
    [InlineData("no applied date", """{ "company": { "name": "Contoso" }, "title": "Developer" }""")]
    public void NewApplication_WhenARequiredFieldIsMissingOrNull_IsRefused(string description, string json)
    {
        // Assert
        Should.Throw<JsonException>(
            () => JsonSerializer.Deserialize<NewApplication>(json, JobTrackerJson.Options),
            description);
    }

    // Trigger: the skill posts an application with only the required fields.
    // Behavior: every optional field is simply absent.
    // Assertion: description, source URL, extra and raw text are all null.
    [Fact]
    public void NewApplication_WhenOnlyRequiredFieldsAreSent_LeavesTheRestNull()
    {
        // Arrange
        const string json = """{ "company": { "name": "Contoso" }, "title": "Developer", "appliedOn": "2026-10-20" }""";

        // Act
        var application = JsonSerializer.Deserialize<NewApplication>(json, JobTrackerJson.Options).ShouldNotBeNull();

        // Assert
        application.Company.Website.ShouldBeNull();
        application.Description.ShouldBeNull();
        application.SourceUrl.ShouldBeNull();
        application.Extra.ShouldBeNull();
        application.RawText.ShouldBeNull();
    }

    // Trigger: the service writes a list row for an open application.
    // Behavior: names are camelCase, the status is the lower-case word, the date has no time part.
    // Assertion: the JSON text is exactly the literal.
    [Fact]
    public void ApplicationSummary_WhenWritten_UsesCamelCaseAndLowerCaseStatus()
    {
        // Arrange
        var summary = new ApplicationSummary("a1", "Contoso", "Developer", new DateOnly(2026, 10, 20),
            ApplicationStatus.Open, null);

        // Act
        var json = JsonSerializer.Serialize(summary, JobTrackerJson.Options);

        // Assert
        json.ShouldBe("""{"id":"a1","company":"Contoso","title":"Developer","appliedOn":"2026-10-20","status":"open","lastEventAt":null}""");
    }

    // Trigger: the user closes an application in the UI, which posts the closure.
    // Behavior: the body is the documented shape.
    // Assertion: the JSON text is exactly the literal.
    [Fact]
    public void Closure_WhenWritten_IsTheDocumentedShape()
    {
        // Act
        var json = JsonSerializer.Serialize(new Closure(new DateOnly(2026, 10, 28), "Withdrawn"), JobTrackerJson.Options);

        // Assert
        json.ShouldBe("""{"closedOn":"2026-10-28","reason":"Withdrawn"}""");
    }
}
