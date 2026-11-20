using MediatR;
using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Repositories;

namespace Veldrath.Server.Features.Characters.Npcs;

/// <summary>A person standing at the character's location, as shown to the client.</summary>
/// <param name="NpcId">Placement identifier.</param>
/// <param name="ArchetypeSlug">The archetype slug, which is also the speaker key <c>TalkToNpc</c> advances.</param>
/// <param name="DisplayName">Name shown in the client.</param>
/// <param name="Occupation">The archetype's role category, such as <c>townspeople</c>.</param>
/// <param name="IsShopkeeper">Whether this person trades.</param>
/// <param name="IsQuestGiver">Whether this person can offer quests.</param>
/// <param name="HasDialogue">Whether this person has lines to say.</param>
public record LocationNpcDto(
    Guid NpcId,
    string ArchetypeSlug,
    string DisplayName,
    string? Occupation,
    bool IsShopkeeper,
    bool IsQuestGiver,
    bool HasDialogue);

/// <summary>Hub command that lists the people standing where the character is.</summary>
/// <param name="CharacterId">Character whose current location to inspect.</param>
public record GetLocationNpcsHubCommand(Guid CharacterId) : IRequest<IReadOnlyList<LocationNpcDto>>;

/// <summary>Hub command that talks to one person.</summary>
/// <param name="CharacterId">Character speaking.</param>
/// <param name="NpcSlug">Archetype slug of the person being spoken to.</param>
public record TalkToNpcHubCommand(Guid CharacterId, string NpcSlug) : IRequest<TalkToNpcHubResult>;

/// <summary>Result returned by <see cref="TalkToNpcHubCommandHandler"/>.</summary>
public record TalkToNpcHubResult
{
    /// <summary>Gets a value indicating whether the person was there and answered.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the failure reason when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the archetype slug that was spoken to.</summary>
    public string NpcSlug { get; init; } = string.Empty;

    /// <summary>Gets the name the client shows.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Gets the line the person said.</summary>
    public string Line { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether this person trades.</summary>
    public bool IsShopkeeper { get; init; }

    /// <summary>Gets a value indicating whether this person can offer quests.</summary>
    public bool IsQuestGiver { get; init; }
}

/// <summary>Shared lookups for the NPC commands: the placement rows, and the archetype facts behind them.</summary>
/// <remarks>
/// The role flags live on the content archetype (<c>ActorArchetype.Traits</c>), not on the shared <c>NPC</c>
/// projection, so they are read from <see cref="ContentDbContext"/> directly rather than widening a contract the
/// engine does not use.
/// </remarks>
internal static class NpcPlacements
{
    /// <summary>Projects a placement plus its archetype onto the client view.</summary>
    /// <param name="npc">Placement row.</param>
    /// <param name="archetype">Archetype row, or <see langword="null"/> when the content is missing.</param>
    /// <returns>The client view.</returns>
    internal static LocationNpcDto ToDto(Data.Entities.LocationNpc npc, ActorArchetype? archetype) =>
        new(
            npc.Id,
            npc.ArchetypeSlug,
            npc.DisplayName ?? archetype?.DisplayName ?? npc.ArchetypeSlug,
            archetype?.TypeKey,
            archetype?.Traits.Shopkeeper ?? false,
            archetype?.Traits.QuestGiver ?? false,
            archetype?.Traits.HasDialogue ?? false);
}

/// <summary>
/// Picks a line a placed person can actually say when someone walks up to them.
/// </summary>
/// <remarks>
/// Shared by <c>TalkToNpc</c> and the dialogue panel, so the line a player hears on approach is the line the panel
/// opens with. Two pickers would drift the moment one of them was edited, and the drift would look like a bug in the
/// conversation rather than in the greeting.
/// </remarks>
internal static class NpcGreetings
{
    private static readonly Random Picker = new();

    /// <summary>
    /// Chooses one greeting line, falling back to a neutral one so a speaker whose dialogue has not been authored yet
    /// still answers.
    /// </summary>
    /// <param name="dialogues">Dialogue catalog.</param>
    /// <param name="slug">Speaker key.</param>
    /// <param name="displayName">Name to use in the fallback line.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>One line of dialogue.</returns>
    internal static async Task<string> PickAsync(
        IDialogueRepository dialogues,
        string slug,
        string displayName,
        CancellationToken cancellationToken)
    {
        try
        {
            var entries = await dialogues.GetBySpeakerAsync(slug);
            cancellationToken.ThrowIfCancellationRequested();

            var lines = entries
                .Where(entry => entry.TypeKey == "greetings"
                                || entry.DisplayName.Contains("Greeting", StringComparison.OrdinalIgnoreCase))
                .SelectMany(entry => entry.Lines)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();

            if (lines.Count > 0)
                return lines[Picker.Next(lines.Count)];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A missing dialogue catalog must not stop the conversation; the neutral line is still true.
        }

        return $"{displayName} looks up as you approach.";
    }
}

/// <summary>
/// Handles <see cref="GetLocationNpcsHubCommand"/> by listing the people placed at the character's location.
/// </summary>
public class GetLocationNpcsHubCommandHandler(
    ICharacterRepository characterRepo,
    ApplicationDbContext db,
    ContentDbContext content,
    ILogger<GetLocationNpcsHubCommandHandler> logger)
    : IRequestHandler<GetLocationNpcsHubCommand, IReadOnlyList<LocationNpcDto>>
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<LocationNpcDto>> Handle(
        GetLocationNpcsHubCommand request,
        CancellationToken cancellationToken)
    {
        var character = await characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (character is null || string.IsNullOrWhiteSpace(character.CurrentZoneLocationSlug))
            return [];

        var placements = await db.LocationNpcs
            .AsNoTracking()
            .Where(npc => npc.ZoneId == character.CurrentZoneId
                          && npc.LocationSlug == character.CurrentZoneLocationSlug)
            .OrderBy(npc => npc.SortOrder)
            .ThenBy(npc => npc.ArchetypeSlug)
            .ToListAsync(cancellationToken);

        if (placements.Count == 0)
            return [];

        var slugs = placements.Select(npc => npc.ArchetypeSlug).Distinct(StringComparer.Ordinal).ToList();
        var archetypes = await content.ActorArchetypes
            .AsNoTracking()
            .Where(actor => slugs.Contains(actor.Slug))
            .ToDictionaryAsync(actor => actor.Slug, StringComparer.Ordinal, cancellationToken);

        logger.LogDebug(
            "People at {Zone}/{Location}: {Count}",
            character.CurrentZoneId,
            character.CurrentZoneLocationSlug,
            placements.Count);

        return placements.Select(npc => NpcPlacements.ToDto(
            npc,
            archetypes.GetValueOrDefault(npc.ArchetypeSlug))).ToList();
    }
}

/// <summary>
/// Handles <see cref="TalkToNpcHubCommand"/>: checks the person is actually standing with the character, then
/// answers with one of their own lines.
/// </summary>
public class TalkToNpcHubCommandHandler(
    ICharacterRepository characterRepo,
    ApplicationDbContext db,
    ContentDbContext content,
    IDialogueRepository dialogues,
    ILogger<TalkToNpcHubCommandHandler> logger)
    : IRequestHandler<TalkToNpcHubCommand, TalkToNpcHubResult>
{
    /// <inheritdoc />
    public async Task<TalkToNpcHubResult> Handle(
        TalkToNpcHubCommand request,
        CancellationToken cancellationToken)
    {
        var slug = request.NpcSlug.Trim();
        if (slug.Length == 0)
            return Fail("An NPC slug is required.");

        var character = await characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (character is null)
            return Fail("No character to speak with.");

        var placement = await db.LocationNpcs
            .AsNoTracking()
            .Where(npc => npc.ZoneId == character.CurrentZoneId
                          && npc.LocationSlug == character.CurrentZoneLocationSlug
                          && npc.ArchetypeSlug == slug)
            .OrderBy(npc => npc.SortOrder)
            .FirstOrDefaultAsync(cancellationToken);

        if (placement is null)
        {
            logger.LogDebug(
                "Character {CharacterId} tried to talk to '{Slug}', who is not at {Zone}/{Location}",
                request.CharacterId, slug, character.CurrentZoneId, character.CurrentZoneLocationSlug);
            return Fail($"There is nobody called {slug} here.");
        }

        var archetype = await content.ActorArchetypes
            .AsNoTracking()
            .FirstOrDefaultAsync(actor => actor.Slug == slug, cancellationToken);

        var view = NpcPlacements.ToDto(placement, archetype);
        var line = await NpcGreetings.PickAsync(dialogues, slug, view.DisplayName, cancellationToken);

        logger.LogInformation(
            "Character {CharacterId} spoke with {NpcId} at {Zone}/{Location}",
            request.CharacterId, placement.NpcId, placement.ZoneId, placement.LocationSlug);

        return new TalkToNpcHubResult
        {
            Success = true,
            NpcSlug = slug,
            DisplayName = view.DisplayName,
            Line = line,
            IsShopkeeper = view.IsShopkeeper,
            IsQuestGiver = view.IsQuestGiver,
        };
    }

    private static TalkToNpcHubResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}
