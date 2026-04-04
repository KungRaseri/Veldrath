using FluentValidation;
using MediatR;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Tilemap.Queries;

/// <summary>Query that loads a <see cref="TileMapDefinition"/> for the given zone.</summary>
/// <param name="ZoneId">Identifier of the zone whose map to load (e.g. <c>"fenwick-crossing"</c>).</param>
public record GetTileMapQuery(string ZoneId) : IRequest<TileMapDefinition?>;

/// <summary>Handles <see cref="GetTileMapQuery"/> by loading the map from the tilemap repository.</summary>
public class GetTileMapQueryHandler : IRequestHandler<GetTileMapQuery, TileMapDefinition?>
{
    private readonly ITileMapRepository _repository;

    /// <summary>Initializes a new instance of <see cref="GetTileMapQueryHandler"/>.</summary>
    /// <param name="repository">The tilemap repository used to load zone map data.</param>
    public GetTileMapQueryHandler(ITileMapRepository repository) =>
        _repository = repository;

    /// <inheritdoc/>
    public Task<TileMapDefinition?> Handle(GetTileMapQuery request, CancellationToken cancellationToken) =>
        _repository.GetByZoneIdAsync(request.ZoneId);
}

/// <summary>Validates <see cref="GetTileMapQuery"/> inputs.</summary>
public class GetTileMapQueryValidator : AbstractValidator<GetTileMapQuery>
{
    /// <summary>Initializes a new instance of <see cref="GetTileMapQueryValidator"/>.</summary>
    public GetTileMapQueryValidator()
    {
        RuleFor(q => q.ZoneId)
            .NotEmpty()
            .MaximumLength(128);
    }
}
