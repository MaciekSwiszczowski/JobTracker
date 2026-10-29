namespace JobTracker.UI.Tests;

/// <summary>
/// Pins how a free-form JSON object becomes the rows of "Other details" and of an event's details: nesting,
/// lists, scalars, and readable names.
/// </summary>
public sealed class PropertyFlattenerTests
{
    // Trigger: an application's extra part holds a nested salary object and a flag.
    // Behavior: the nested object heads its own lines, which are indented one level; the flag reads as a word.
    // Assertion: the four rows, with depth, readable name and value text, in document order.
    [Fact]
    public void Flatten_WhenAnObjectIsNested_IndentsItsProperties()
    {
        // Arrange
        var extra = TestData.Json("""{ "salary": { "min": 24000, "currency": "PLN" }, "relocation": true }""");

        // Act
        var rows = PropertyFlattener.Flatten(extra);

        // Assert
        rows.ShouldBe(
        [
            new PropertyRow(0, "Salary", null),
            new PropertyRow(1, "Min", "24000"),
            new PropertyRow(1, "Currency", "PLN"),
            new PropertyRow(0, "Relocation", "yes"),
        ]);
    }

    // Trigger: the extra part lists a tech stack.
    // Behavior: a list of plain values is one line, the values joined.
    // Assertion: one row, "Tech stack": "C#, MongoDB, 3".
    [Fact]
    public void Flatten_WhenAListHoldsPlainValues_JoinsThemOnOneLine()
    {
        // Act
        var rows = PropertyFlattener.Flatten(TestData.Json("""{ "techStack": ["C#", "MongoDB", 3] }"""));

        // Assert
        rows.ShouldBe([new PropertyRow(0, "Tech stack", "C#, MongoDB, 3")]);
    }

    // Trigger: the extra part lists two contact people, each an object.
    // Behavior: each item gets a numbered heading line, with its properties indented under it.
    // Assertion: the heading, then #1 and #2 one level in, each with its name two levels in.
    [Fact]
    public void Flatten_WhenAListHoldsObjects_NumbersEachItem()
    {
        // Act
        var rows = PropertyFlattener.Flatten(TestData.Json("""{ "contacts": [{ "name": "Anna" }, { "name": "Piotr" }] }"""));

        // Assert
        rows.ShouldBe(
        [
            new PropertyRow(0, "Contacts", null),
            new PropertyRow(1, "#1", null),
            new PropertyRow(2, "Name", "Anna"),
            new PropertyRow(1, "#2", null),
            new PropertyRow(2, "Name", "Piotr"),
        ]);
    }

    // Trigger: an application or event has no free-form part, or an empty one, or an explicit null.
    // Behavior: there is nothing to show, so "Other details" stays hidden.
    // Assertion: no rows, for every row of the table.
    [Theory]
    [InlineData("absent", null)]
    [InlineData("JSON null", "null")]
    [InlineData("empty object", "{}")]
    public void Flatten_WhenThereIsNothing_GivesNoRows(string description, string? json)
    {
        // Arrange
        JsonElement? element = json is null ? null : TestData.Json(json);

        // Act
        var rows = PropertyFlattener.Flatten(element);

        // Assert
        rows.ShouldBeEmpty(description);
    }

    // Trigger: the skill stores keys in camelCase, sometimes with an acronym, sometimes snake_case.
    // Behavior: the view shows them as plain words, first letter capital, acronyms kept.
    // Assertion: each key reads as the literal on its row.
    [Theory]
    [InlineData("remote", "Remote")]
    [InlineData("techStack", "Tech stack")]
    [InlineData("officeDaysPerWeek", "Office days per week")]
    [InlineData("sourceURL", "Source URL")]
    [InlineData("salary_max", "Salary max")]
    [InlineData("recruiterCall", "Recruiter call")]
    public void Humanize_WhenGivenAKey_ReadsAsWords(string key, string expected)
    {
        // Act
        var text = PropertyFlattener.Humanize(key);

        // Assert
        text.ShouldBe(expected);
    }
}
