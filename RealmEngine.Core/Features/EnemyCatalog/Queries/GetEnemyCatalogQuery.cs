using FluentValidation;
using MediatR;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.EnemyCatalog.Queries;

/// <summary>Returns all active enemies, optionally filtered by <see cref="Family"/>.</summary>
/// <param name="Family">When non-null, limits results to enemies of this family (e.g. "wolves", "humanoids/bandits").</param>
public record GetEnemyCatalogQuery(string? Family = null) : IRequest<IReadOnlyList<Enemy>>;

/// <summary>Handles <see cref="GetEnemyCatalogQuery"/>.</summary>
public class GetEnemyCatalogQueryHandler : IRequestHandler<GetEnemyCatalogQuery, IReadOnlyList<Enemy>>
{
    private readonly IEnemyRepository _repo;

    /// <summary>Initializes a new instance of <see cref="GetEnemyCatalogQueryHandler"/>.</summary>
    public GetEnemyCatalogQueryHandler(IEnemyRepository repo) => _repo = repo;

    /// <inheritdoc />
    public async Task<IReadOnlyList<Enemy>> Handle(GetEnemyCatalogQuery request, CancellationToken cancellationToken)
    {
        if (request.Family is not null)
            return await _repo.GetByFamilyAsync(request.Family);

        return await _repo.GetAllAsync();
    }
}

/// <summary>Validates <see cref="GetEnemyCatalogQuery"/>.</summary>
public class GetEnemyCatalogQueryValidator : AbstractValidator<GetEnemyCatalogQuery>
{
    /// <summary>Initializes a new instance of <see cref="GetEnemyCatalogQueryValidator"/>.</summary>
    public GetEnemyCatalogQueryValidator()
    {
        When(q => q.Family is not null, () =>
        {
            RuleFor(q => q.Family)
                .NotEmpty().WithMessage("Family filter must not be empty when provided.")
                .MaximumLength(100).WithMessage("Family filter must not exceed 100 characters.");
        });
    }
}
