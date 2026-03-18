using MediatR;
using RealmEngine.Core.Features.Salvaging.Services;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Salvaging.Queries;

/// <summary>
/// Query to preview the scrap materials and yield rate that salvaging an item would produce,
/// without consuming the item or making any state changes.
/// </summary>
public record GetSalvagePreviewQuery : IRequest<SalvagePreviewResult>
{
    /// <summary>Gets the character whose crafting skill governs the yield rate.</summary>
    public required Character Character { get; init; }

    /// <summary>Gets the item to preview salvaging.</summary>
    public required Item Item { get; init; }
}

/// <summary>Read-only preview of what salvaging an item would yield.</summary>
public record SalvagePreviewResult
{
    /// <summary>Gets a value indicating whether the item is eligible for salvaging.</summary>
    public bool CanSalvage { get; init; }

    /// <summary>
    /// Gets the reason salvaging is blocked, or <see langword="null"/> when
    /// <see cref="CanSalvage"/> is <see langword="true"/>.
    /// </summary>
    public string? BlockedReason { get; init; }

    /// <summary>Gets the crafting skill that governs yield for this item type.</summary>
    public string SkillName { get; init; } = string.Empty;

    /// <summary>Gets the character's current rank in the governing skill.</summary>
    public int SkillLevel { get; init; }

    /// <summary>Gets the expected yield rate as a percentage (40–100).</summary>
    public double YieldRate { get; init; }

    /// <summary>Gets the scrap material names mapped to their expected quantities.</summary>
    public Dictionary<string, int> ExpectedScrap { get; init; } = [];
}

/// <summary>Handles the <see cref="GetSalvagePreviewQuery"/>.</summary>
public class GetSalvagePreviewHandler : IRequestHandler<GetSalvagePreviewQuery, SalvagePreviewResult>
{
    /// <summary>Initializes a new instance of <see cref="GetSalvagePreviewHandler"/>.</summary>
    public GetSalvagePreviewHandler() { }

    /// <summary>Returns a read-only preview of the expected salvage yield without consuming the item.</summary>
    public Task<SalvagePreviewResult> Handle(GetSalvagePreviewQuery request, CancellationToken cancellationToken)
    {
        var item      = request.Item;
        var character = request.Character;

        if (!SalvageService.CanBeSalvaged(item.Type))
        {
            return Task.FromResult(new SalvagePreviewResult
            {
                CanSalvage    = false,
                BlockedReason = $"{item.Type} items cannot be salvaged."
            });
        }

        var skillName  = SalvageService.GetSkillName(item.Type);
        var skillLevel = character.Skills.TryGetValue(skillName, out var skill) ? skill.CurrentRank : 0;
        var yieldRate  = SalvageService.CalculateYieldRate(character, item.Type);
        var scrap      = SalvageService.GetExpectedScrap(item, yieldRate);

        return Task.FromResult(new SalvagePreviewResult
        {
            CanSalvage    = true,
            SkillName     = skillName,
            SkillLevel    = skillLevel,
            YieldRate     = yieldRate,
            ExpectedScrap = scrap
        });
    }
}
