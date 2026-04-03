using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.CharacterCreation.Queries;

/// <summary>
/// Returns a preview <see cref="Character"/> assembled from the current session state without persisting it.
/// </summary>
public record GetCreationPreviewQuery(Guid SessionId) : IRequest<GetCreationPreviewResult>;

/// <summary>
/// Result of <see cref="GetCreationPreviewQuery"/>.
/// </summary>
public record GetCreationPreviewResult
{
    /// <summary>Gets a value indicating whether the preview was built successfully.</summary>
    public bool Success { get; init; }

    /// <summary>Gets a message describing the result.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Gets the assembled (not persisted) character preview, or <see langword="null"/> on failure.</summary>
    public Character? Character { get; init; }

    /// <summary>Gets the display name of the selected species, or <see langword="null"/> when none is selected.</summary>
    public string? SpeciesDisplayName { get; init; }

    /// <summary>Gets the display name of the selected background, or <see langword="null"/> when none is selected.</summary>
    public string? BackgroundDisplayName { get; init; }
}

/// <summary>
/// Handles <see cref="GetCreationPreviewQuery"/>.
/// </summary>
public class GetCreationPreviewHandler(
    ICharacterCreationSessionStore sessionStore,
    ILogger<GetCreationPreviewHandler> logger)
    : IRequestHandler<GetCreationPreviewQuery, GetCreationPreviewResult>
{
    private static readonly PointBuyConfig PointBuy = new();

    /// <inheritdoc />
    public async Task<GetCreationPreviewResult> Handle(GetCreationPreviewQuery request, CancellationToken cancellationToken)
    {
        var session = await sessionStore.GetSessionAsync(request.SessionId);
        if (session is null)
            return new GetCreationPreviewResult { Success = false, Message = $"Session {request.SessionId} not found." };

        if (session.SelectedClass is null)
            return new GetCreationPreviewResult { Success = false, Message = "No class selected yet." };

        var cls = session.SelectedClass;
        var allocations = session.AttributeAllocations;

        var character = new Character
        {
            Name      = session.CharacterName ?? string.Empty,
            ClassName = cls.Name,
            Level     = 1,

            Strength     = (allocations?.GetValueOrDefault("Strength",     10) ?? 10) + cls.BonusStrength,
            Dexterity    = (allocations?.GetValueOrDefault("Dexterity",    10) ?? 10) + cls.BonusDexterity,
            Constitution = (allocations?.GetValueOrDefault("Constitution", 10) ?? 10) + cls.BonusConstitution,
            Intelligence = (allocations?.GetValueOrDefault("Intelligence", 10) ?? 10) + cls.BonusIntelligence,
            Wisdom       = (allocations?.GetValueOrDefault("Wisdom",       10) ?? 10) + cls.BonusWisdom,
            Charisma     = (allocations?.GetValueOrDefault("Charisma",     10) ?? 10) + cls.BonusCharisma,

            Health    = cls.StartingHealth,
            MaxHealth = cls.StartingHealth,
            Mana      = cls.StartingMana,
            MaxMana   = cls.StartingMana,

            BackgroundId = session.SelectedBackground?.GetBackgroundId(),
            SpeciesSlug  = session.SelectedSpecies?.Slug,
            CurrentLocationId = session.SelectedLocationId,
        };

        // Apply background bonuses
        session.SelectedBackground?.ApplyBonuses(character);

        // Apply species bonuses
        session.SelectedSpecies?.ApplyBonuses(character);

        logger.LogDebug("Session {SessionId}: preview built for candidate '{Name}'", request.SessionId, character.Name);
        return new GetCreationPreviewResult
        {
            Success              = true,
            Character            = character,
            SpeciesDisplayName   = session.SelectedSpecies?.DisplayName,
            BackgroundDisplayName = session.SelectedBackground?.Name,
        };
    }
}
