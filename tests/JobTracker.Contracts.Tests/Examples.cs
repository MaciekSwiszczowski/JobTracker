namespace JobTracker.Contracts.Tests;

/// <summary>
/// Reads the examples in docs/api, which the project file copies next to the tests. They are the documents the
/// Claude skill imitates, so reading them here is what keeps the skill's instructions honest.
/// </summary>
internal static class Examples
{
    /// <summary>Deserializes <c>docs/api/<paramref name="name"/>.example.json</c> with the wire options.</summary>
    public static T Read<T>(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "examples", $"{name}.example.json");
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JobTrackerJson.Options)
            ?? throw new InvalidOperationException($"{path} holds the JSON literal null.");
    }
}
