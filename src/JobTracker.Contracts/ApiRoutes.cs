namespace JobTracker.Contracts;

/// <summary>The service's routes, so that the API, the UI client and the tests cannot drift apart.</summary>
public static class ApiRoutes
{
    /// <summary>The address the service listens on unless configured otherwise. Loopback only.</summary>
    public const string DefaultBaseAddress = "http://localhost:5080";

    /// <summary><c>GET</c> lists summaries; <c>POST</c> creates an application.</summary>
    public const string Applications = "/applications";

    /// <summary><c>GET</c> one application, in full.</summary>
    public static string Application(string id) => $"/applications/{Uri.EscapeDataString(id)}";

    /// <summary><c>POST</c> appends an event.</summary>
    public static string Events(string id) => $"{Application(id)}/events";

    /// <summary><c>POST</c> closes the application.</summary>
    public static string Close(string id) => $"{Application(id)}/close";
}
