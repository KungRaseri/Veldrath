using FluentValidation;
using MediatR;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.LootTableCatalog.Queries;

/// <summary>Returns all active loot tables, optionally filtered by <see cref="Context"/>.</summary>
/// <param name="Context">When non-null, limits results to loot tables for this context (e.g. "enemies", "chests", "harvesting").</param>
public record GetLootTableCatalogQuery(string? Context = null) : IRequest<IReadOnlyList<LootTableData>>;

/// <summary>Handles <see cref="GetLootTableCatalogQuery"/>.</summary>
public class GetLootTableCatalogQueryHandler : IRequestHandler<GetLootTableCatalogQuery, IReadOnlyList<LootTableData>>
{
    private readonly ILootTableRepository _repo;

    /// <summary>Initializes a new instance of <see cref="GetLootTableCatalogQueryHandler"/>.</summary>
    public GetLootTableCatalogQueryHandler(ILootTableRepository repo) => _repo = repo;

    /// <inheritdoc />
    public async Task<IReadOnlyList<LootTableData>> Handle(GetLootTableCatalogQuery request, CancellationToken cancellationToken)
    {
        if (request.Context is not null)
            return await _repo.GetByContextAsync(request.Context);

        return await _repo.GetAllAsync();
    }
}

/// <summary>Validates <see cref="GetLootTableCatalogQuery"/>.</summary>
public class GetLootTableCatalogQueryValidator : AbstractValidator<GetLootTableCatalogQuery>
{
    /// <summary>Initializes a new instance of <see cref="GetLootTableCatalogQueryValidator"/>.</summary>
    public GetLootTableCatalogQueryValidator()
    {
        When(q => q.Context is not null, () =>
        {
            RuleFor(q => q.Context)
                .NotEmpty().WithMessage("Context filter must not be empty when provided.")
                .MaximumLength(100).WithMessage("Context filter must not exceed 100 characters.");
        });
    }
}
