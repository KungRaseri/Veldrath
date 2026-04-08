using FluentValidation;
using MediatR;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.ZoneLocationCatalog.Queries;

/// <summary>Query that retrieves zone location entries from the catalog, optionally filtered by type key.</summary>
/// <param name="TypeKey">When provided, limits results to locations with this type key.</param>
public record GetZoneLocationCatalogQuery(string? TypeKey = null) : IRequest<IReadOnlyList<ZoneLocationEntry>>;

/// <summary>Handles <see cref="GetZoneLocationCatalogQuery"/> by querying the zone location repository.</summary>
public class GetZoneLocationCatalogQueryHandler : IRequestHandler<GetZoneLocationCatalogQuery, IReadOnlyList<ZoneLocationEntry>>
{
    private readonly IZoneLocationRepository _repository;

    /// <summary>Initializes a new instance of <see cref="GetZoneLocationCatalogQueryHandler"/>.</summary>
    /// <param name="repository">Repository used to retrieve zone location entries.</param>
    public GetZoneLocationCatalogQueryHandler(IZoneLocationRepository repository) =>
        _repository = repository;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ZoneLocationEntry>> Handle(GetZoneLocationCatalogQuery request, CancellationToken cancellationToken)
    {
        if (request.TypeKey is not null)
            return await _repository.GetByTypeKeyAsync(request.TypeKey);

        return await _repository.GetAllAsync();
    }
}

/// <summary>Validates <see cref="GetZoneLocationCatalogQuery"/> inputs.</summary>
public class GetZoneLocationCatalogQueryValidator : AbstractValidator<GetZoneLocationCatalogQuery>
{
    /// <summary>Initializes a new instance of <see cref="GetZoneLocationCatalogQueryValidator"/>.</summary>
    public GetZoneLocationCatalogQueryValidator()
    {
        When(q => q.TypeKey is not null, () =>
        {
            RuleFor(q => q.TypeKey).NotEmpty().MaximumLength(100);
        });
    }
}
