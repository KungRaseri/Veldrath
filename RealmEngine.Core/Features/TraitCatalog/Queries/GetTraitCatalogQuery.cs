using FluentValidation;
using MediatR;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.TraitCatalog.Queries;

/// <summary>Query that retrieves trait definition entries from the catalog, optionally filtered by the entity types they apply to.</summary>
/// <param name="AppliesTo">When provided, limits results to traits that apply to this entity type (or to all entities).</param>
public record GetTraitCatalogQuery(string? AppliesTo = null) : IRequest<IReadOnlyList<TraitDefinitionEntry>>;

/// <summary>Handles <see cref="GetTraitCatalogQuery"/> by querying the trait definition repository.</summary>
public class GetTraitCatalogQueryHandler : IRequestHandler<GetTraitCatalogQuery, IReadOnlyList<TraitDefinitionEntry>>
{
    private readonly ITraitDefinitionRepository _repository;

    /// <summary>Initializes a new instance of <see cref="GetTraitCatalogQueryHandler"/>.</summary>
    /// <param name="repository">Repository used to retrieve trait definition entries.</param>
    public GetTraitCatalogQueryHandler(ITraitDefinitionRepository repository) =>
        _repository = repository;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TraitDefinitionEntry>> Handle(GetTraitCatalogQuery request, CancellationToken cancellationToken)
    {
        if (request.AppliesTo is not null)
            return await _repository.GetByAppliesToAsync(request.AppliesTo);

        return await _repository.GetAllAsync();
    }
}

/// <summary>Validates <see cref="GetTraitCatalogQuery"/> inputs.</summary>
public class GetTraitCatalogQueryValidator : AbstractValidator<GetTraitCatalogQuery>
{
    /// <summary>Initializes a new instance of <see cref="GetTraitCatalogQueryValidator"/>.</summary>
    public GetTraitCatalogQueryValidator()
    {
        When(q => q.AppliesTo is not null, () =>
        {
            RuleFor(q => q.AppliesTo).NotEmpty().MaximumLength(100);
        });
    }
}
