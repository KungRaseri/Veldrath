using FluentValidation;
using MediatR;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.MaterialPropertyCatalog.Queries;

/// <summary>Query that retrieves material property entries from the catalog, optionally filtered by material family.</summary>
/// <param name="Family">When provided, limits results to material properties of this family.</param>
public record GetMaterialPropertyCatalogQuery(string? Family = null) : IRequest<IReadOnlyList<MaterialPropertyEntry>>;

/// <summary>Handles <see cref="GetMaterialPropertyCatalogQuery"/> by querying the material property repository.</summary>
public class GetMaterialPropertyCatalogQueryHandler : IRequestHandler<GetMaterialPropertyCatalogQuery, IReadOnlyList<MaterialPropertyEntry>>
{
    private readonly IMaterialPropertyRepository _repository;

    /// <summary>Initializes a new instance of <see cref="GetMaterialPropertyCatalogQueryHandler"/>.</summary>
    /// <param name="repository">Repository used to retrieve material property entries.</param>
    public GetMaterialPropertyCatalogQueryHandler(IMaterialPropertyRepository repository) =>
        _repository = repository;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MaterialPropertyEntry>> Handle(GetMaterialPropertyCatalogQuery request, CancellationToken cancellationToken)
    {
        if (request.Family is not null)
            return await _repository.GetByFamilyAsync(request.Family);

        return await _repository.GetAllAsync();
    }
}

/// <summary>Validates <see cref="GetMaterialPropertyCatalogQuery"/> inputs.</summary>
public class GetMaterialPropertyCatalogQueryValidator : AbstractValidator<GetMaterialPropertyCatalogQuery>
{
    /// <summary>Initializes a new instance of <see cref="GetMaterialPropertyCatalogQueryValidator"/>.</summary>
    public GetMaterialPropertyCatalogQueryValidator()
    {
        When(q => q.Family is not null, () =>
        {
            RuleFor(q => q.Family).NotEmpty().MaximumLength(100);
        });
    }
}
