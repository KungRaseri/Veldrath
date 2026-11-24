using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RealmEngine.Core.Services;
using RealmEngine.Shared.Abstractions;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Settings;

namespace Veldrath.Server.Features.Characters.Chests;

/// <summary>A chest standing at the character's location, as shown to the client.</summary>
/// <param name="Id">Chest identifier used to open it.</param>
/// <param name="DisplayName">Name shown in the client.</param>
/// <param name="LocationSlug">Location the chest stands at.</param>
/// <param name="Kind">Whether the chest is seeded content or placed by the sweeper.</param>
public record LocationChestDto(Guid Id, string DisplayName, string LocationSlug, string Kind);

/// <summary>Hub command that lists the unopened chests where the character is standing.</summary>
/// <param name="CharacterId">Character whose current location to inspect.</param>
public record GetLocationChestsHubCommand(Guid CharacterId) : IRequest<IReadOnlyList<LocationChestDto>>;

/// <summary>The client's request shape for opening a chest.</summary>
/// <param name="ChestId">Chest identifier from the last <c>LocationChests</c> payload.</param>
public record OpenChestRequest(Guid ChestId);

/// <summary>Hub command that opens one chest for the character.</summary>
/// <param name="CharacterId">Character opening the chest.</param>
/// <param name="ChestId">Chest identifier.</param>
public record OpenChestHubCommand(Guid CharacterId, Guid ChestId) : IRequest<OpenChestHubResult>;

/// <summary>One item taken out of a chest.</summary>
/// <param name="ItemRef">Item-reference slug.</param>
/// <param name="Name">Display name resolved by the loot roll.</param>
/// <param name="Quantity">How many were taken.</param>
public record ChestLootItem(string ItemRef, string Name, int Quantity);

/// <summary>Result returned by <see cref="OpenChestHubCommandHandler"/>.</summary>
public record OpenChestHubResult
{
    /// <summary>Gets a value indicating whether the chest was opened by this call.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the failure reason when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the chest identifier.</summary>
    public Guid ChestId { get; init; }

    /// <summary>Gets the name of the chest that was opened.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Gets what came out of it.</summary>
    public IReadOnlyList<ChestLootItem> Items { get; init; } = [];

    /// <summary>Gets the gold that came out of it, if any.</summary>
    public int Gold { get; init; }

    /// <summary>Gets the character's gold total after the award, or <see langword="null"/> when no gold was paid.</summary>
    /// <remarks>
    /// This travels in-process so the hub can announce <c>GoldChanged</c> from the server's own total. The
    /// zone-wide <c>ChestOpened</c> broadcast deliberately omits it: the loot is announced, but the opener's
    /// purse is not the party's business.
    /// </remarks>
    public int? NewGoldTotal { get; init; }

    /// <summary>Gets the server-worded outcome line the client logs.</summary>
    public string? Message { get; init; }

    /// <summary>Gets the chests still unopened at the location after this one.</summary>
    public IReadOnlyList<LocationChestDto> RemainingChests { get; init; } = [];
}

/// <summary>
/// Handles <see cref="GetLocationChestsHubCommand"/> by listing unopened chests at the character's location.
/// </summary>
/// <remarks>
/// The read is scoped by the session's instance run as well as by zone and location, so a party inside a delve
/// never sees another run's chests, exactly as it never sees its floor loot.
/// </remarks>
public class GetLocationChestsHubCommandHandler(
    ICharacterRepository characterRepo,
    IPlayerSessionRepository sessionRepo,
    ApplicationDbContext db,
    ILogger<GetLocationChestsHubCommandHandler> logger)
    : IRequestHandler<GetLocationChestsHubCommand, IReadOnlyList<LocationChestDto>>
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<LocationChestDto>> Handle(
        GetLocationChestsHubCommand request,
        CancellationToken cancellationToken)
    {
        var character = await characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (character is null || string.IsNullOrWhiteSpace(character.CurrentZoneLocationSlug))
            return [];

        var session = await sessionRepo.GetByCharacterIdAsync(request.CharacterId);

        var chests = await db.LootChests
            .AsNoTracking()
            .Where(chest => chest.ZoneId == character.CurrentZoneId
                            && chest.LocationSlug == character.CurrentZoneLocationSlug
                            && chest.ZoneInstanceId == session!.ZoneInstanceId
                            && !chest.IsOpened)
            .OrderBy(chest => chest.Kind)
            .ThenBy(chest => chest.DisplayName)
            .ToListAsync(cancellationToken);

        logger.LogDebug(
            "Chests at {Zone}/{Location}: {Count}",
            character.CurrentZoneId,
            character.CurrentZoneLocationSlug,
            chests.Count);

        return chests.Select(ToDto).ToList();
    }

    /// <summary>Projects a chest row into the client view.</summary>
    /// <param name="chest">Chest row.</param>
    /// <returns>The client view.</returns>
    internal static LocationChestDto ToDto(LootChest chest) =>
        new(chest.Id, chest.DisplayName, chest.LocationSlug, chest.Kind.ToString());
}

/// <summary>
/// Handles <see cref="OpenChestHubCommand"/>: rolls the chest's loot tables, claims the chest so exactly one
/// caller can win it, and puts the result in the character's bag.
/// </summary>
/// <remarks>
/// The claim is a conditional update — <c>IsOpened = true</c> only where the row is still unopened — rather than
/// a read-then-write, because a shared chest opened by two players at once must not pay out twice. The loser of
/// that race is told so; the winner takes the loot, and the row's reopen timer is set from
/// <see cref="ChestOptions.ReopenAfterMinutes"/> so the sweeper can bring it back.
/// </remarks>
public class OpenChestHubCommandHandler(
    ICharacterRepository characterRepo,
    IPlayerSessionRepository sessionRepo,
    ApplicationDbContext db,
    LootTableService lootTables,
    ISender sender,
    IOptions<ChestOptions> options,
    ILogger<OpenChestHubCommandHandler> logger)
    : IRequestHandler<OpenChestHubCommand, OpenChestHubResult>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <inheritdoc />
    public async Task<OpenChestHubResult> Handle(
        OpenChestHubCommand request,
        CancellationToken cancellationToken)
    {
        var character = await characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (character is null)
            return Fail("No character to open a chest with.");

        var chest = await db.LootChests
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == request.ChestId, cancellationToken);

        if (chest is null)
            return Fail("That chest is no longer there.");

        if (chest.IsOpened)
            return Fail($"The {chest.DisplayName} has already been emptied.");

        // Standing in the room is what makes the chest reachable: an id from anywhere else would let a client
        // open the world from one spot.
        if (!string.Equals(chest.ZoneId, character.CurrentZoneId, StringComparison.Ordinal) ||
            !string.Equals(chest.LocationSlug, character.CurrentZoneLocationSlug, StringComparison.Ordinal))
        {
            return Fail($"You are not standing where the {chest.DisplayName} is.");
        }

        var session = await sessionRepo.GetByCharacterIdAsync(request.CharacterId);
        if (chest.ZoneInstanceId != session?.ZoneInstanceId)
            return Fail("That chest belongs to another run.");

        var refs = ParseRefs(chest.LootTableRefs);
        if (refs.Count == 0)
        {
            logger.LogWarning("Chest {ChestId} has no usable loot table refs ({Refs})", chest.ChestId, chest.LootTableRefs);
            return Fail($"The {chest.DisplayName} is empty.");
        }

        var roll = await lootTables.RollChestDrops(refs, chest.MergeStrategy);

        var now = DateTimeOffset.UtcNow;
        var claim = await db.LootChests
            .Where(row => row.Id == chest.Id && !row.IsOpened)
            .ExecuteUpdateAsync(
                update => update
                    .SetProperty(row => row.IsOpened, true)
                    .SetProperty(row => row.OpenedAt, now)
                    .SetProperty(row => row.ReopenAt, now.AddMinutes(options.Value.ReopenAfterMinutes))
                    .SetProperty(row => row.OpenedByCharacterId, request.CharacterId),
                cancellationToken);

        if (claim == 0)
            return Fail($"Someone else got to the {chest.DisplayName} first.");

        // The bag is the character's own `InventoryBlob`, the store the client reads — the same reason harvest
        // and combat loot write it directly instead of going through the engine's save-game inventory.
        var bag = CharacterBag.Read(character.InventoryBlob);
        var items = new List<ChestLootItem>(roll.Drops.Count);

        foreach (var drop in roll.Drops)
        {
            var slug = drop.ItemRef[(drop.ItemRef.LastIndexOf(':') + 1)..];
            CharacterBag.Add(bag, slug, drop.Quantity);
            items.Add(new ChestLootItem(drop.ItemRef, drop.ItemName, drop.Quantity));
        }

        character.InventoryBlob = CharacterBag.Serialize(bag);
        await db.SaveChangesAsync(cancellationToken);

        int? newGoldTotal = null;
        if (roll.Gold > 0)
        {
            // Gold lives in the attributes blob and has exactly one writer, so go through it rather than
            // re-implementing the key and the clamp here.
            var award = await sender.Send(
                new AddGoldHubCommand
                {
                    CharacterId = request.CharacterId,
                    Amount = roll.Gold,
                    Source = $"Chest:{chest.ChestId}",
                },
                cancellationToken);

            if (award.Success)
                newGoldTotal = award.NewGoldTotal;
            else
                logger.LogWarning(
                    "Chest {ChestId} rolled {Gold} gold for character {CharacterId}, but the award failed: {Error}",
                    chest.ChestId, roll.Gold, request.CharacterId, award.ErrorMessage);
        }

        var summary = items.Count == 0
            ? string.Empty
            : string.Join(", ", items.Select(item => item.Quantity > 1 ? $"{item.Name} x{item.Quantity}" : item.Name));

        var message = items.Count == 0 && roll.Gold == 0
            ? $"The {chest.DisplayName} was empty."
            : string.Join(" ", new[]
            {
                $"Opened the {chest.DisplayName}:",
                summary,
                roll.Gold > 0 ? $"and {roll.Gold} gold." : string.Empty,
            }.Where(part => !string.IsNullOrWhiteSpace(part)));

        logger.LogInformation(
            "Character {CharacterId} opened chest {ChestId} ({Kind}) at {Zone}/{Location}: {Drops} item stack(s), {Gold} gold",
            request.CharacterId, chest.ChestId, chest.Kind, chest.ZoneId, chest.LocationSlug, items.Count, roll.Gold);

        var remaining = await db.LootChests
            .AsNoTracking()
            .Where(row => row.ZoneId == chest.ZoneId
                          && row.LocationSlug == chest.LocationSlug
                          && row.ZoneInstanceId == chest.ZoneInstanceId
                          && !row.IsOpened)
            .OrderBy(row => row.Kind)
            .ThenBy(row => row.DisplayName)
            .ToListAsync(cancellationToken);

        return new OpenChestHubResult
        {
            Success = true,
            ChestId = chest.Id,
            DisplayName = chest.DisplayName,
            Items = items,
            Gold = roll.Gold,
            NewGoldTotal = newGoldTotal,
            Message = message,
            RemainingChests = remaining.Select(GetLocationChestsHubCommandHandler.ToDto).ToList(),
        };
    }

    private static List<string> ParseRefs(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static OpenChestHubResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}
