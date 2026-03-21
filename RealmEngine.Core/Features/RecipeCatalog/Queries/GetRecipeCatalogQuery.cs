using FluentValidation;
using MediatR;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.RecipeCatalog.Queries;

/// <summary>Returns all active recipes, optionally filtered by <see cref="CraftingSkill"/>.</summary>
/// <param name="CraftingSkill">When non-null, limits results to recipes requiring this crafting skill (e.g. "blacksmithing").</param>
public record GetRecipeCatalogQuery(string? CraftingSkill = null) : IRequest<IReadOnlyList<Recipe>>;

/// <summary>Handles <see cref="GetRecipeCatalogQuery"/>.</summary>
public class GetRecipeCatalogQueryHandler : IRequestHandler<GetRecipeCatalogQuery, IReadOnlyList<Recipe>>
{
    private readonly IRecipeRepository _repo;

    /// <summary>Initializes a new instance of <see cref="GetRecipeCatalogQueryHandler"/>.</summary>
    public GetRecipeCatalogQueryHandler(IRecipeRepository repo) => _repo = repo;

    /// <inheritdoc />
    public async Task<IReadOnlyList<Recipe>> Handle(GetRecipeCatalogQuery request, CancellationToken cancellationToken)
    {
        if (request.CraftingSkill is not null)
            return await _repo.GetByCraftingSkillAsync(request.CraftingSkill);

        return await _repo.GetAllAsync();
    }
}

/// <summary>Validates <see cref="GetRecipeCatalogQuery"/>.</summary>
public class GetRecipeCatalogQueryValidator : AbstractValidator<GetRecipeCatalogQuery>
{
    /// <summary>Initializes a new instance of <see cref="GetRecipeCatalogQueryValidator"/>.</summary>
    public GetRecipeCatalogQueryValidator()
    {
        When(q => q.CraftingSkill is not null, () =>
        {
            RuleFor(q => q.CraftingSkill)
                .NotEmpty().WithMessage("CraftingSkill filter must not be empty when provided.")
                .MaximumLength(100).WithMessage("CraftingSkill filter must not exceed 100 characters.");
        });
    }
}
