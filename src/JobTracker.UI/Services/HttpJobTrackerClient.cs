using System.Net.Http.Json;
using System.Text.Json;
using JobTracker.Contracts;

namespace JobTracker.UI.Services;

/// <summary>
/// Talks to JobTracker.Api. The <see cref="HttpClient"/> carries the base address; the routes come from
/// <see cref="ApiRoutes"/> and the JSON options from <see cref="JobTrackerJson"/>, the same ones the service uses.
/// </summary>
public sealed class HttpJobTrackerClient(HttpClient http) : IJobTrackerClient
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<ApplicationSummary>> GetApplicationsAsync(CancellationToken cancellationToken) =>
        await GetAsync<ApplicationSummary[]>(ApiRoutes.Applications, cancellationToken);

    /// <inheritdoc />
    public Task<ApplicationDetail> GetApplicationAsync(string id, CancellationToken cancellationToken) =>
        GetAsync<ApplicationDetail>(ApiRoutes.Application(id), cancellationToken);

    /// <inheritdoc />
    public async Task CloseApplicationAsync(string id, Closure closure, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(
            new Uri(ApiRoutes.Close(id), UriKind.Relative), closure, JobTrackerJson.Options, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private async Task<T> GetAsync<T>(string route, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(new Uri(route, UriKind.Relative), cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(JobTrackerJson.Options, cancellationToken)
            ?? throw new HttpRequestException("The JobTracker service answered with an empty body.", null, response.StatusCode);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var message = ProblemText(body)
            ?? $"The JobTracker service answered {(int)response.StatusCode} {response.ReasonPhrase}.";
        throw new HttpRequestException(message, null, response.StatusCode);
    }

    // ASP.NET Core answers errors as RFC 9457 problem details; "detail" is the specific sentence, "title" the
    // general one: https://learn.microsoft.com/aspnet/core/fundamentals/error-handling-api
    private static string? ProblemText(string body)
    {
        try
        {
            using var problem = JsonDocument.Parse(body);
            if (problem.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            foreach (var name in (ReadOnlySpan<string>)["detail", "title"])
            {
                if (problem.RootElement.TryGetProperty(name, out var text)
                    && text.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(text.GetString()))
                {
                    return text.GetString();
                }
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
