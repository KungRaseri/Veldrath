using FluentValidation;
using MediatR;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.ItemCatalog.Queries;

/// <summary>Returns all active catalog items, optionally filtered by <see cref="ItemType"/>.</summary>
/// <param name="ItemType">When non-null, limits results to items with this type (e.g. "consumable", "gem", "rune").</param>
public record GetItemCatalogQuery(string? ItemType = null) : IRequest<IReadOnlyList<Item>>;

/// <summary>Handles <see cref="GetItemCatalogQuery"/>.</summary>
public class GetItemCatalogQueryHandler : IRequestHandler<GetItemCatalogQuery, IReadOnlyList<Item>>
{
    private readonly IItemRepository _repo;

    /// <summary>Initializes a new instance of <see cref="GetItemCatalogQueryHandler"/>.</summary>
    public GetItemCatalogQueryHandler(IItemRepository repo) => _repo = repo;

    /// <inheritdoc />
    public async Task<IReadOnlyList<Item>> Handle(GetItemCatalogQuery request, CancellationToken cancellationToken)
    {
        if (request.ItemType is not null)
            return await _repo.GetByTypeAsync(request.ItemType);

        return await _repo.GetAllAsync();
    }
}

/// <summary>Validates <see cref="GetItemCatalogQuery"/>.</summary>
public class GetItemCatalogQueryValidator : AbstractValidator<GetItemCatalogQuery>
{
    /// <summary>Initializes a new instance of <see cref="GetItemCatalogQueryValidator"/>.</summary>
    public GetItemCatalogQueryValidator()
    {
        When(q => q.ItemType is not null, () =>
        {
            RuleFor(q => q.ItemType)
                .NotEmpty().WithMessage("ItemType filter must not be empty when provided.")
                .MaximumLength(100).WithMessage("ItemType filter must not exceed 100 characters.");
        });
    }
}
