using FluentValidation;
using MediatR;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.AbilityCatalog.Queries;

/// <summary>Returns all active abilities, optionally filtered by <see cref="AbilityType"/>.</summary>
/// <param name="AbilityType">When non-null, limits results to abilities of this type (e.g. "active", "passive").</param>
public record GetAbilityCatalogQuery(string? AbilityType = null) : IRequest<IReadOnlyList<Ability>>;

/// <summary>Handles <see cref="GetAbilityCatalogQuery"/>.</summary>
public class GetAbilityCatalogQueryHandler : IRequestHandler<GetAbilityCatalogQuery, IReadOnlyList<Ability>>
{
    private readonly IAbilityRepository _repo;

    /// <summary>Initializes a new instance of <see cref="GetAbilityCatalogQueryHandler"/>.</summary>
    public GetAbilityCatalogQueryHandler(IAbilityRepository repo) => _repo = repo;

    /// <inheritdoc />
    public async Task<IReadOnlyList<Ability>> Handle(GetAbilityCatalogQuery request, CancellationToken cancellationToken)
    {
        if (request.AbilityType is not null)
            return await _repo.GetByTypeAsync(request.AbilityType);

        return await _repo.GetAllAsync();
    }
}

/// <summary>Validates <see cref="GetAbilityCatalogQuery"/>.</summary>
public class GetAbilityCatalogQueryValidator : AbstractValidator<GetAbilityCatalogQuery>
{
    /// <summary>Initializes a new instance of <see cref="GetAbilityCatalogQueryValidator"/>.</summary>
    public GetAbilityCatalogQueryValidator()
    {
        When(q => q.AbilityType is not null, () =>
        {
            RuleFor(q => q.AbilityType)
                .NotEmpty().WithMessage("AbilityType filter must not be empty when provided.")
                .MaximumLength(100).WithMessage("AbilityType filter must not exceed 100 characters.");
        });
    }
}
