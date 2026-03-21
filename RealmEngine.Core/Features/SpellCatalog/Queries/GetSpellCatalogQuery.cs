using FluentValidation;
using MediatR;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.SpellCatalog.Queries;

/// <summary>Returns all active spells, optionally filtered by <see cref="School"/>.</summary>
/// <param name="School">When non-null, limits results to spells of this school (e.g. "fire", "arcane").</param>
public record GetSpellCatalogQuery(string? School = null) : IRequest<IReadOnlyList<Spell>>;

/// <summary>Handles <see cref="GetSpellCatalogQuery"/>.</summary>
public class GetSpellCatalogQueryHandler : IRequestHandler<GetSpellCatalogQuery, IReadOnlyList<Spell>>
{
    private readonly ISpellRepository _repo;

    /// <summary>Initializes a new instance of <see cref="GetSpellCatalogQueryHandler"/>.</summary>
    public GetSpellCatalogQueryHandler(ISpellRepository repo) => _repo = repo;

    /// <inheritdoc />
    public async Task<IReadOnlyList<Spell>> Handle(GetSpellCatalogQuery request, CancellationToken cancellationToken)
    {
        if (request.School is not null)
            return await _repo.GetBySchoolAsync(request.School);

        return await _repo.GetAllAsync();
    }
}

/// <summary>Validates <see cref="GetSpellCatalogQuery"/>.</summary>
public class GetSpellCatalogQueryValidator : AbstractValidator<GetSpellCatalogQuery>
{
    /// <summary>Initializes a new instance of <see cref="GetSpellCatalogQueryValidator"/>.</summary>
    public GetSpellCatalogQueryValidator()
    {
        When(q => q.School is not null, () =>
        {
            RuleFor(q => q.School)
                .NotEmpty().WithMessage("School filter must not be empty when provided.")
                .MaximumLength(100).WithMessage("School filter must not exceed 100 characters.");
        });
    }
}
