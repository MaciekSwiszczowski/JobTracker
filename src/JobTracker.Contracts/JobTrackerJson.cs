using System.Text.Json;

namespace JobTracker.Contracts;

/// <summary>
/// The one set of serializer options for the wire format, used on both ends. camelCase names (the web
/// defaults), and strict: a required field that is missing or null is an error rather than a silent null.
/// </summary>
public static class JobTrackerJson
{
    /// <summary>Read-only; pass it to every (de)serialization of a contract type.</summary>
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        // RespectNullableAnnotations rejects null for a non-nullable property, and
        // RespectRequiredConstructorParameters rejects a missing one; both are off by default:
        // https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/nullable-annotations
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
        };
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
