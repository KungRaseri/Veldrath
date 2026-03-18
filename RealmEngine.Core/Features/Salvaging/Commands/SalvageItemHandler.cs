using MediatR;
using RealmEngine.Core.Features.Salvaging.Services;
using RealmEngine.Core.Services;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Salvaging.Commands;

/// <summary>
/// Handler for salvaging items into scrap materials.
/// Implements type-based material mapping and skill-based yield rates.
/// </summary>
public class SalvageItemHandler : IRequestHandler<SalvageItemCommand, SalvageItemResult>
{
    private readonly RecipeDataService? _recipeCatalogLoader;

    /// <summary>
    /// Initializes a new instance of <see cref="SalvageItemHandler"/>.
    /// </summary>
    /// <param name="recipeCatalogLoader">Optional recipe catalog loader for recipe-based salvaging.</param>
    public SalvageItemHandler(RecipeDataService? recipeCatalogLoader = null)
    {
        _recipeCatalogLoader = recipeCatalogLoader;
    }

    /// <summary>Handles the salvage item request.</summary>
    public Task<SalvageItemResult> Handle(SalvageItemCommand request, CancellationToken cancellationToken)
    {
        var character = request.Character;
        var item      = request.Item;

        if (!SalvageService.CanBeSalvaged(item.Type))
        {
            return Task.FromResult(new SalvageItemResult
            {
                Success       = false,
                Message       = $"Cannot salvage {item.Type} items.",
                YieldRate     = 0,
                ItemDestroyed = false
            });
        }

        var skillName      = SalvageService.GetSkillName(item.Type);
        var yieldRate      = SalvageService.CalculateYieldRate(character, item.Type);
        var scrapMaterials = SalvageService.GetExpectedScrap(item, yieldRate);

        return Task.FromResult(new SalvageItemResult
        {
            Success       = true,
            Message       = $"Salvaged {item.Name} at {yieldRate:F1}% yield rate. Recovered {string.Join(", ", scrapMaterials.Select(kvp => $"{kvp.Value}x {kvp.Key}"))}.",
            ScrapMaterials = scrapMaterials,
            SkillUsed     = skillName,
            YieldRate     = yieldRate,
            ItemDestroyed = true
        });
    }
}
