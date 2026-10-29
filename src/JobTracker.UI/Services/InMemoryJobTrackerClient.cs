using System.Net;
using JobTracker.Contracts;

namespace JobTracker.UI.Services;

/// <summary>
/// The service's stand-in, in memory: the sample mode of the app, and the client the view model tests run on.
/// It answers the way the service is documented to (docs/api.md): list rows are a projection of the stored
/// applications, an unknown id is 404, closing a closed application is 409. The view model tests depend on
/// exactly that, so keep it in step with the documentation.
/// </summary>
public sealed class InMemoryJobTrackerClient(IEnumerable<ApplicationDetail> applications) : IJobTrackerClient
{
    private readonly List<ApplicationDetail> _applications = [.. applications];

    /// <inheritdoc />
    public Task<IReadOnlyList<ApplicationSummary>> GetApplicationsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ApplicationSummary>>([.. _applications.Select(ToSummary)]);

    /// <inheritdoc />
    public Task<ApplicationDetail> GetApplicationAsync(string id, CancellationToken cancellationToken) =>
        Task.FromResult(_applications[IndexOf(id)]);

    /// <inheritdoc />
    public Task CloseApplicationAsync(string id, Closure closure, CancellationToken cancellationToken)
    {
        var index = IndexOf(id);
        if (_applications[index].Status == ApplicationStatus.Closed)
        {
            throw new HttpRequestException("The application is already closed.", null, HttpStatusCode.Conflict);
        }

        _applications[index] = _applications[index] with { Status = ApplicationStatus.Closed, Closure = closure };
        return Task.CompletedTask;
    }

    private int IndexOf(string id)
    {
        var index = _applications.FindIndex(application => string.Equals(application.Id, id, StringComparison.Ordinal));
        return index >= 0
            ? index
            : throw new HttpRequestException($"There is no application {id}.", null, HttpStatusCode.NotFound);
    }

    private static ApplicationSummary ToSummary(ApplicationDetail application) =>
        new(
            application.Id,
            application.Company.Name,
            application.Title,
            application.AppliedOn,
            application.Status,
            application.Events.Count == 0 ? null : application.Events.Max(static e => e.At));
}
