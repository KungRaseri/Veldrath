namespace RealmFoundry.Services;

/// <summary>
/// Typed HttpClient facade for calling the RealmUnbound.Server REST API.
/// Base address is configured via <c>RealmUnbound:ServerUrl</c> at startup.
/// Bearer token is set after login (Phase 5 — OAuth).
/// </summary>
public sealed class RealmFoundryApiClient(HttpClient http)
{
    public void SetBearerToken(string token) =>
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

    public Task<bool> IsServerReachableAsync(CancellationToken ct = default) =>
        http.GetAsync("/health", ct)
            .ContinueWith(t => t.IsCompletedSuccessfully && t.Result.IsSuccessStatusCode, ct,
                TaskContinuationOptions.None, TaskScheduler.Default);
}
