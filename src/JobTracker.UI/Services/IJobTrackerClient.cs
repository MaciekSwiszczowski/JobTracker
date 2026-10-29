using JobTracker.Contracts;

namespace JobTracker.UI.Services;

/// <summary>
/// Everything the UI asks of the JobTracker service. Reads, plus the one write: closing.
/// Every method throws <see cref="HttpRequestException"/> on failure: with a null
/// <see cref="HttpRequestException.StatusCode"/> when the service could not be reached, and with the status and the
/// service's own explanation as the message when it answered but refused.
/// </summary>
public interface IJobTrackerClient
{
    /// <summary>All applications, as list rows.</summary>
    Task<IReadOnlyList<ApplicationSummary>> GetApplicationsAsync(CancellationToken cancellationToken);

    /// <summary>One application in full.</summary>
    Task<ApplicationDetail> GetApplicationAsync(string id, CancellationToken cancellationToken);

    /// <summary>Marks the application closed.</summary>
    Task CloseApplicationAsync(string id, Closure closure, CancellationToken cancellationToken);
}
