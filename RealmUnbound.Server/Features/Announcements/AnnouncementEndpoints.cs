using Veldrath.Contracts.Announcements;
using Veldrath.Server.Data.Repositories;

namespace Veldrath.Server.Features.Announcements;

/// <summary>Minimal API endpoints for the announcements news feed.</summary>
public static class AnnouncementEndpoints
{
    /// <summary>Maps announcement routes under <c>/api/announcements</c>.</summary>
    public static IEndpointRouteBuilder MapAnnouncementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/announcements")
                       .WithTags("Announcements")
                       .AllowAnonymous();

        group.MapGet("/", GetActiveAsync);

        return app;
    }

    private static async Task<IResult> GetActiveAsync(
        IAnnouncementRepository repository,
        CancellationToken ct)
    {
        var announcements = await repository.GetActiveAsync(ct);
        var dtos = announcements
            .Select(a => new AnnouncementDto(a.Id, a.Title, a.Body, a.Category, a.IsPinned, a.PublishedAt))
            .ToList();
        return Results.Ok(dtos);
    }
}
