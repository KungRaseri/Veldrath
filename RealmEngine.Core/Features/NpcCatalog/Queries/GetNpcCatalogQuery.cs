using FluentValidation;
using MediatR;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.NpcCatalog.Queries;

/// <summary>Returns all active NPCs, optionally filtered by <see cref="Category"/>.</summary>
/// <param name="Category">When non-null, limits results to NPCs of this category (e.g. "merchants", "guards").</param>
public record GetNpcCatalogQuery(string? Category = null) : IRequest<IReadOnlyList<NPC>>;

/// <summary>Handles <see cref="GetNpcCatalogQuery"/>.</summary>
public class GetNpcCatalogQueryHandler : IRequestHandler<GetNpcCatalogQuery, IReadOnlyList<NPC>>
{
    private readonly INpcRepository _repo;

    /// <summary>Initializes a new instance of <see cref="GetNpcCatalogQueryHandler"/>.</summary>
    public GetNpcCatalogQueryHandler(INpcRepository repo) => _repo = repo;

    /// <inheritdoc />
    public async Task<IReadOnlyList<NPC>> Handle(GetNpcCatalogQuery request, CancellationToken cancellationToken)
    {
        if (request.Category is not null)
            return await _repo.GetByCategoryAsync(request.Category);

        return await _repo.GetAllAsync();
    }
}

/// <summary>Validates <see cref="GetNpcCatalogQuery"/>.</summary>
public class GetNpcCatalogQueryValidator : AbstractValidator<GetNpcCatalogQuery>
{
    /// <summary>Initializes a new instance of <see cref="GetNpcCatalogQueryValidator"/>.</summary>
    public GetNpcCatalogQueryValidator()
    {
        When(q => q.Category is not null, () =>
        {
            RuleFor(q => q.Category)
                .NotEmpty().WithMessage("Category filter must not be empty when provided.")
                .MaximumLength(100).WithMessage("Category filter must not exceed 100 characters.");
        });
    }
}
