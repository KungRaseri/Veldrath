using FluentValidation;
using MediatR;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.SkillCatalog.Queries;

/// <summary>Returns all active skills, optionally filtered by <see cref="Category"/>.</summary>
/// <param name="Category">When non-null, limits results to skills of this category (e.g. "combat", "magic", "stealth").</param>
public record GetSkillCatalogQuery(string? Category = null) : IRequest<IReadOnlyList<SkillDefinition>>;

/// <summary>Handles <see cref="GetSkillCatalogQuery"/>.</summary>
public class GetSkillCatalogQueryHandler : IRequestHandler<GetSkillCatalogQuery, IReadOnlyList<SkillDefinition>>
{
    private readonly ISkillRepository _repo;

    /// <summary>Initializes a new instance of <see cref="GetSkillCatalogQueryHandler"/>.</summary>
    public GetSkillCatalogQueryHandler(ISkillRepository repo) => _repo = repo;

    /// <inheritdoc />
    public async Task<IReadOnlyList<SkillDefinition>> Handle(GetSkillCatalogQuery request, CancellationToken cancellationToken)
    {
        if (request.Category is not null)
            return await _repo.GetByCategoryAsync(request.Category);

        return await _repo.GetAllAsync();
    }
}

/// <summary>Validates <see cref="GetSkillCatalogQuery"/>.</summary>
public class GetSkillCatalogQueryValidator : AbstractValidator<GetSkillCatalogQuery>
{
    /// <summary>Initializes a new instance of <see cref="GetSkillCatalogQueryValidator"/>.</summary>
    public GetSkillCatalogQueryValidator()
    {
        When(q => q.Category is not null, () =>
        {
            RuleFor(q => q.Category)
                .NotEmpty().WithMessage("Category filter must not be empty when provided.")
                .MaximumLength(100).WithMessage("Category filter must not exceed 100 characters.");
        });
    }
}
