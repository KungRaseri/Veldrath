using FluentValidation;
using MediatR;
using RealmEngine.Shared.Abstractions;
using SharedSpecies = RealmEngine.Shared.Models.Species;

namespace RealmEngine.Core.Features.Species.Queries;

/// <summary>Returns all active species, optionally filtered to a single <see cref="TypeKey"/>.</summary>
/// <param name="TypeKey">When non-null, limits results to species with this type key (e.g. "humanoid", "beast").</param>
public record GetSpeciesQuery(string? TypeKey = null) : IRequest<IReadOnlyList<SharedSpecies>>;

/// <summary>Handles <see cref="GetSpeciesQuery"/>.</summary>
public class GetSpeciesQueryHandler : IRequestHandler<GetSpeciesQuery, IReadOnlyList<SharedSpecies>>
{
    private readonly ISpeciesRepository _repo;

    /// <summary>Initializes a new instance of <see cref="GetSpeciesQueryHandler"/>.</summary>
    public GetSpeciesQueryHandler(ISpeciesRepository repo) => _repo = repo;

    /// <inheritdoc />
    public async Task<IReadOnlyList<SharedSpecies>> Handle(GetSpeciesQuery request, CancellationToken cancellationToken)
    {
        if (request.TypeKey is not null)
            return await _repo.GetSpeciesByTypeAsync(request.TypeKey);

        return await _repo.GetAllSpeciesAsync();
    }
}

/// <summary>Validates <see cref="GetSpeciesQuery"/>.</summary>
public class GetSpeciesQueryValidator : AbstractValidator<GetSpeciesQuery>
{
    /// <summary>Initializes a new instance of <see cref="GetSpeciesQueryValidator"/>.</summary>
    public GetSpeciesQueryValidator()
    {
        When(q => q.TypeKey is not null, () =>
        {
            RuleFor(q => q.TypeKey)
                .NotEmpty().WithMessage("TypeKey filter must not be empty when provided.")
                .MaximumLength(100).WithMessage("TypeKey filter must not exceed 100 characters.");
        });
    }
}
