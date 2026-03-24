using FluentValidation;
using MediatR;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.PowerCatalog.Queries;

/// <summary>
/// Returns all active powers, optionally filtered by <see cref="PowerType"/> or <see cref="School"/>.
/// </summary>
/// <param name="PowerType">When non-null, limits results to powers of this acquisition type (e.g. "spell", "passive").</param>
/// <param name="School">When non-null, limits results to powers of this magical school (e.g. "fire", "arcane").</param>
public record GetPowerCatalogQuery(string? PowerType = null, string? School = null) : IRequest<IReadOnlyList<Power>>;

/// <summary>Handles <see cref="GetPowerCatalogQuery"/>.</summary>
public class GetPowerCatalogQueryHandler : IRequestHandler<GetPowerCatalogQuery, IReadOnlyList<Power>>
{
    private readonly IPowerRepository _repo;

    /// <summary>Initializes a new instance of <see cref="GetPowerCatalogQueryHandler"/>.</summary>
    public GetPowerCatalogQueryHandler(IPowerRepository repo) => _repo = repo;

    /// <inheritdoc />
    public async Task<IReadOnlyList<Power>> Handle(GetPowerCatalogQuery request, CancellationToken cancellationToken)
    {
        if (request.PowerType is not null)
            return await _repo.GetByTypeAsync(request.PowerType);

        if (request.School is not null)
            return await _repo.GetBySchoolAsync(request.School);

        return await _repo.GetAllAsync();
    }
}

/// <summary>Validates <see cref="GetPowerCatalogQuery"/>.</summary>
public class GetPowerCatalogQueryValidator : AbstractValidator<GetPowerCatalogQuery>
{
    /// <summary>Initializes a new instance of <see cref="GetPowerCatalogQueryValidator"/>.</summary>
    public GetPowerCatalogQueryValidator()
    {
        When(q => q.PowerType is not null, () =>
        {
            RuleFor(q => q.PowerType)
                .NotEmpty().WithMessage("PowerType filter must not be empty when provided.")
                .MaximumLength(64).WithMessage("PowerType filter must not exceed 64 characters.");
        });

        When(q => q.School is not null, () =>
        {
            RuleFor(q => q.School)
                .NotEmpty().WithMessage("School filter must not be empty when provided.")
                .MaximumLength(64).WithMessage("School filter must not exceed 64 characters.");
        });
    }
}
