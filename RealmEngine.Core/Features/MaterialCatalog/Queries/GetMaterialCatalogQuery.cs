using FluentValidation;
using MediatR;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.MaterialCatalog.Queries;

/// <summary>Returns all active materials, optionally filtered by <see cref="Family"/>.</summary>
/// <param name="Family">When non-null, limits results to materials of this family (e.g. "metal", "wood", "leather").</param>
public record GetMaterialCatalogQuery(string? Family = null) : IRequest<IReadOnlyList<MaterialEntry>>;

/// <summary>Handles <see cref="GetMaterialCatalogQuery"/>.</summary>
public class GetMaterialCatalogQueryHandler : IRequestHandler<GetMaterialCatalogQuery, IReadOnlyList<MaterialEntry>>
{
    private readonly IMaterialRepository _repo;

    /// <summary>Initializes a new instance of <see cref="GetMaterialCatalogQueryHandler"/>.</summary>
    public GetMaterialCatalogQueryHandler(IMaterialRepository repo) => _repo = repo;

    /// <inheritdoc />
    public async Task<IReadOnlyList<MaterialEntry>> Handle(GetMaterialCatalogQuery request, CancellationToken cancellationToken)
    {
        if (request.Family is not null)
            return await _repo.GetByFamiliesAsync([request.Family]);

        return await _repo.GetAllAsync();
    }
}

/// <summary>Validates <see cref="GetMaterialCatalogQuery"/>.</summary>
public class GetMaterialCatalogQueryValidator : AbstractValidator<GetMaterialCatalogQuery>
{
    /// <summary>Initializes a new instance of <see cref="GetMaterialCatalogQueryValidator"/>.</summary>
    public GetMaterialCatalogQueryValidator()
    {
        When(q => q.Family is not null, () =>
        {
            RuleFor(q => q.Family)
                .NotEmpty().WithMessage("Family filter must not be empty when provided.")
                .MaximumLength(100).WithMessage("Family filter must not exceed 100 characters.");
        });
    }
}
