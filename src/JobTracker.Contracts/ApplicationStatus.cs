using System.Text.Json.Serialization;

namespace JobTracker.Contracts;

/// <summary>Whether an application is still in progress. Written as <c>"open"</c> or <c>"closed"</c>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ApplicationStatus>))]
public enum ApplicationStatus
{
    /// <summary>Sent and not yet finished, whatever stage it is in.</summary>
    [JsonStringEnumMemberName("open")]
    Open,

    /// <summary>Finished: rejected, withdrawn, accepted, or given up on. See <see cref="Closure"/>.</summary>
    [JsonStringEnumMemberName("closed")]
    Closed,
}
