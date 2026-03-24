using FluentValidation;
using MediatR;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.ZoneLocationCatalog.Queries;

/// <summary>Query that retrieves zone location entries from the catalog, optionally filtered by location type.</summary>
/// <param name="LocationType">When provided, limits results to locations of this type.</param>
public record GetZoneLocationCatalogQuery(string? LocationType = null) : IRequest<IReadOnlyList<ZoneLocationEntry>>;

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
        if (request.LocationType is not null)
            return await _repository.GetByLocationTypeAsync(request.LocationType);

        return await _repository.GetAllAsync();
    }
}

/// <summary>Validates <see cref="GetZoneLocationCatalogQuery"/> inputs.</summary>
public class GetZoneLocationCatalogQueryValidator : AbstractValidator<GetZoneLocationCatalogQuery>
{
    /// <summary>Initializes a new instance of <see cref="GetZoneLocationCatalogQueryValidator"/>.</summary>
    public GetZoneLocationCatalogQueryValidator()
    {
        When(q => q.LocationType is not null, () =>
        {
            RuleFor(q => q.LocationType).NotEmpty().MaximumLength(100);
        });
    }
}
