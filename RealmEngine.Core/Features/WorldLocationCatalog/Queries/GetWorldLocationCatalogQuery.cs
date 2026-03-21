using FluentValidation;
using MediatR;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.WorldLocationCatalog.Queries;

/// <summary>Query that retrieves world location entries from the catalog, optionally filtered by location type.</summary>
/// <param name="LocationType">When provided, limits results to locations of this type.</param>
public record GetWorldLocationCatalogQuery(string? LocationType = null) : IRequest<IReadOnlyList<WorldLocationEntry>>;

/// <summary>Handles <see cref="GetWorldLocationCatalogQuery"/> by querying the world location repository.</summary>
public class GetWorldLocationCatalogQueryHandler : IRequestHandler<GetWorldLocationCatalogQuery, IReadOnlyList<WorldLocationEntry>>
{
    private readonly IWorldLocationRepository _repository;

    /// <summary>Initializes a new instance of <see cref="GetWorldLocationCatalogQueryHandler"/>.</summary>
    /// <param name="repository">Repository used to retrieve world location entries.</param>
    public GetWorldLocationCatalogQueryHandler(IWorldLocationRepository repository) =>
        _repository = repository;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WorldLocationEntry>> Handle(GetWorldLocationCatalogQuery request, CancellationToken cancellationToken)
    {
        if (request.LocationType is not null)
            return await _repository.GetByLocationTypeAsync(request.LocationType);

        return await _repository.GetAllAsync();
    }
}

/// <summary>Validates <see cref="GetWorldLocationCatalogQuery"/> inputs.</summary>
public class GetWorldLocationCatalogQueryValidator : AbstractValidator<GetWorldLocationCatalogQuery>
{
    /// <summary>Initializes a new instance of <see cref="GetWorldLocationCatalogQueryValidator"/>.</summary>
    public GetWorldLocationCatalogQueryValidator()
    {
        When(q => q.LocationType is not null, () =>
        {
            RuleFor(q => q.LocationType).NotEmpty().MaximumLength(100);
        });
    }
}
