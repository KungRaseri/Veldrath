using FluentValidation;
using MediatR;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.EnchantmentCatalog.Queries;

/// <summary>Returns all active enchantments, optionally filtered by <see cref="TargetSlot"/>.</summary>
/// <param name="TargetSlot">When non-null, limits results to enchantments with this target slot (e.g. "weapon", "armor").</param>
public record GetEnchantmentCatalogQuery(string? TargetSlot = null) : IRequest<IReadOnlyList<Enchantment>>;

/// <summary>Handles <see cref="GetEnchantmentCatalogQuery"/>.</summary>
public class GetEnchantmentCatalogQueryHandler : IRequestHandler<GetEnchantmentCatalogQuery, IReadOnlyList<Enchantment>>
{
    private readonly IEnchantmentRepository _repo;

    /// <summary>Initializes a new instance of <see cref="GetEnchantmentCatalogQueryHandler"/>.</summary>
    public GetEnchantmentCatalogQueryHandler(IEnchantmentRepository repo) => _repo = repo;

    /// <inheritdoc />
    public async Task<IReadOnlyList<Enchantment>> Handle(GetEnchantmentCatalogQuery request, CancellationToken cancellationToken)
    {
        if (request.TargetSlot is not null)
            return await _repo.GetByTargetSlotAsync(request.TargetSlot);

        return await _repo.GetAllAsync();
    }
}

/// <summary>Validates <see cref="GetEnchantmentCatalogQuery"/>.</summary>
public class GetEnchantmentCatalogQueryValidator : AbstractValidator<GetEnchantmentCatalogQuery>
{
    /// <summary>Initializes a new instance of <see cref="GetEnchantmentCatalogQueryValidator"/>.</summary>
    public GetEnchantmentCatalogQueryValidator()
    {
        When(q => q.TargetSlot is not null, () =>
        {
            RuleFor(q => q.TargetSlot)
                .NotEmpty().WithMessage("TargetSlot filter must not be empty when provided.")
                .MaximumLength(100).WithMessage("TargetSlot filter must not exceed 100 characters.");
        });
    }
}
