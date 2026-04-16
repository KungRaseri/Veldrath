namespace Veldrath.Web.Services;

/// <summary>
/// Typed HTTP client for reading content from the Strapi CMS.
/// Base address is configured via <c>Strapi:BaseUrl</c> and authenticated via <c>Strapi:ApiToken</c>.
/// </summary>
public class StrapiClient(HttpClient http)
{
    /// <summary>
    /// Fetches a collection of items from the specified Strapi content-type endpoint.
    /// Returns <see langword="null"/> when Strapi is unreachable or the response is not successful.
    /// </summary>
    /// <typeparam name="T">The DTO type to deserialise items into.</typeparam>
    /// <param name="endpoint">Relative Strapi REST endpoint, e.g. <c>/api/patch-notes</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<StrapiCollection<T>?> GetCollectionAsync<T>(string endpoint, CancellationToken ct = default)
    {
        var resp = await http.GetAsync(endpoint, ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<StrapiCollection<T>>(ct)
            : null;
    }

    /// <summary>
    /// Fetches a single item from the specified Strapi content-type endpoint.
    /// Returns <see langword="null"/> when Strapi is unreachable or the response is not successful.
    /// </summary>
    /// <typeparam name="T">The DTO type to deserialise the item into.</typeparam>
    /// <param name="endpoint">Relative Strapi REST endpoint, e.g. <c>/api/patch-notes/1</c> or <c>/api/patch-notes?filters[slug][$eq]=0-1-0</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<StrapiSingle<T>?> GetSingleAsync<T>(string endpoint, CancellationToken ct = default)
    {
        var resp = await http.GetAsync(endpoint, ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<StrapiSingle<T>>(ct)
            : null;
    }
}

/// <summary>Wrapper around a Strapi REST list response.</summary>
/// <typeparam name="T">The attributes DTO type for each item in the collection.</typeparam>
public record StrapiCollection<T>(IReadOnlyList<StrapiItem<T>> Data, StrapiMeta Meta);

/// <summary>Wrapper around a Strapi REST single-item response.</summary>
/// <typeparam name="T">The attributes DTO type for the item.</typeparam>
public record StrapiSingle<T>(StrapiItem<T> Data);

/// <summary>Represents a single entity returned by the Strapi REST API.</summary>
/// <typeparam name="T">The attributes DTO type.</typeparam>
public record StrapiItem<T>(int Id, T Attributes);

/// <summary>Pagination metadata returned by Strapi list endpoints.</summary>
public record StrapiMeta(StrapiPagination Pagination);

/// <summary>Pagination details within a Strapi collection response.</summary>
public record StrapiPagination(int Page, int PageSize, int PageCount, int Total);
