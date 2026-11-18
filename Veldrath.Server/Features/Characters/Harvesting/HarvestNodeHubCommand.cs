using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Services;
using RealmEngine.Core.Services.Harvesting;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models.Harvesting;
using Veldrath.Server.Data;

namespace Veldrath.Server.Features.Characters.Harvesting;

/// <summary>Asks for the resource nodes standing in the character's current location.</summary>
/// <param name="CharacterId">The character asking.</param>
public record GetLocationNodesHubCommand(Guid CharacterId)
    : IRequest<IReadOnlyList<LocationNodeDto>>;

/// <summary>A resource node as the client needs to see it.</summary>
/// <param name="NodeId">Stable node id, used to harvest it.</param>
/// <param name="DisplayName">Player-facing name.</param>
/// <param name="MaterialTier">"common" … "legendary".</param>
/// <param name="HealthPercent">Remaining health, 0–100.</param>
/// <param name="State">"Healthy" | "Depleted" | "Exhausted" | "Empty".</param>
/// <param name="CanHarvest">Whether a harvest would produce anything right now.</param>
public record LocationNodeDto(
    string NodeId,
    string DisplayName,
    string MaterialTier,
    int HealthPercent,
    string State,
    bool CanHarvest);

/// <summary>The client's ask: the node to harvest. The character comes from the connection, never the payload.</summary>
/// <param name="NodeId">The node standing in the character's location.</param>
public record HarvestNodeRequest(string NodeId);

/// <summary>Harvests one resource node at the character's current location.</summary>
/// <param name="CharacterId">The character harvesting, resolved from the caller's session.</param>
/// <param name="NodeId">The node to harvest.</param>
public record HarvestNodeHubCommand(Guid CharacterId, string NodeId)
    : IRequest<HarvestNodeHubResult>;

/// <summary>One stack of material that came out of a node.</summary>
/// <param name="ItemRef">Catalog reference, e.g. <c>@items/general:glowcap-mushroom</c>.</param>
/// <param name="ItemName">Display name.</param>
/// <param name="Quantity">How many were gained.</param>
public record HarvestedMaterial(string ItemRef, string ItemName, int Quantity);

/// <summary>Outcome of a harvest attempt.</summary>
public record HarvestNodeHubResult
{
    /// <summary>Gets whether the harvest produced anything.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the reason a harvest failed, worded for the player.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the node that was harvested.</summary>
    public string NodeId { get; init; } = string.Empty;

    /// <summary>Gets the node's display name.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Gets what the node yielded.</summary>
    public IReadOnlyList<HarvestedMaterial> Materials { get; init; } = [];

    /// <summary>Gets the node's remaining health after the attempt.</summary>
    public int NodeHealthRemaining { get; init; }

    /// <summary>Gets the node's remaining health as a percentage.</summary>
    public int NodeHealthPercent { get; init; }

    /// <summary>Gets the node's state after the attempt.</summary>
    public string NodeState { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the harvest was a critical one.</summary>
    public bool WasCritical { get; init; }

    /// <summary>Gets the line to show in the activity log.</summary>
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Handles <see cref="GetLocationNodesHubCommand"/> by listing the nodes in the character's location.
/// </summary>
public class GetLocationNodesHubCommandHandler(
    ApplicationDbContext db,
    INodeRepository nodes,
    ILogger<GetLocationNodesHubCommandHandler> logger)
    : IRequestHandler<GetLocationNodesHubCommand, IReadOnlyList<LocationNodeDto>>
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<LocationNodeDto>> Handle(
        GetLocationNodesHubCommand request,
        CancellationToken cancellationToken)
    {
        var character = await db.Characters
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CharacterId, cancellationToken);

        if (character?.CurrentZoneLocationSlug is not { Length: > 0 } location)
        {
            logger.LogDebug(
                "No nodes listed for character {CharacterId}: they are not standing in a location",
                request.CharacterId);
            return [];
        }

        var here = await nodes.GetNodesByLocationAsync(location);
        return here.Select(ToDto).ToList();
    }

    /// <summary>Projects a node onto what the client draws and enables.</summary>
    /// <param name="node">The stored node.</param>
    /// <returns>The node as the location panel shows it.</returns>
    public static LocationNodeDto ToDto(HarvestableNode node) => new(
        node.NodeId,
        node.DisplayName,
        node.MaterialTier,
        node.GetHealthPercent(),
        node.GetNodeState().ToString(),
        node.CanHarvest());
}

/// <summary>
/// Handles <see cref="HarvestNodeHubCommand"/> — the multiplayer harvesting path.
/// </summary>
/// <remarks>
/// The engine's own <c>HarvestNodeCommand</c> cannot be reused here: it reads a single global "current save"
/// and writes the inventory through the save-game store, which in a server holding several characters is both
/// wrong and a different store from the one the game client reads. What *is* shared is everything stateless —
/// the node row, the yield and depletion calculators, the critical roll and the harvesting loot table — so this
/// handler composes those with the server's own inventory service, exactly as combat loot does.
///
/// Two slices are deliberately not here yet, and are authored around rather than faked: tool requirements
/// (every authored node is <c>MinToolTier = 0</c>) and harvesting skill progression (nodes are common tier, so
/// rank 0 is a legitimate character, and skill XP is a separate path).
/// </remarks>
public class HarvestNodeHubCommandHandler(
    ApplicationDbContext db,
    INodeRepository nodes,
    LootTableService lootTables,
    HarvestCalculatorService calculator,
    CriticalHarvestService critical,
    ILogger<HarvestNodeHubCommandHandler> logger)
    : IRequestHandler<HarvestNodeHubCommand, HarvestNodeHubResult>
{
    private const int UntrainedSkillRank = 0;
    private const int NoToolTier = 0;

    /// <inheritdoc />
    public async Task<HarvestNodeHubResult> Handle(
        HarvestNodeHubCommand request,
        CancellationToken cancellationToken)
    {
        var character = await db.Characters
            .FirstOrDefaultAsync(c => c.Id == request.CharacterId, cancellationToken);

        if (character is null)
            return Fail("No character to harvest with.");

        var node = await nodes.GetNodeByIdAsync(request.NodeId);
        if (node is null)
            return Fail("This resource no longer exists.");

        // The node has to be where the character is: an id from anywhere else would let a client harvest the
        // world from one spot, and standing there is what makes the node reachable in the fiction.
        if (!string.Equals(node.LocationId, character.CurrentZoneLocationSlug, StringComparison.Ordinal))
            return Fail($"You are not standing where the {node.DisplayName} grows.");

        if (!node.CanHarvest())
        {
            return new HarvestNodeHubResult
            {
                Success = false,
                ErrorMessage = $"This {node.DisplayName} is depleted. It needs time to regenerate.",
                NodeId = node.NodeId,
                DisplayName = node.DisplayName,
                NodeHealthRemaining = node.CurrentHealth,
                NodeHealthPercent = node.GetHealthPercent(),
                NodeState = node.GetNodeState().ToString(),
            };
        }

        if (node.MinToolTier > NoToolTier)
            return Fail($"The {node.DisplayName} needs a proper tool.");

        var isCritical = critical.RollCritical(UntrainedSkillRank, NoToolTier, node.IsRichNode);
        var yield = calculator.CalculateYield(node, UntrainedSkillRank, NoToolTier, isCritical);
        var drops = await lootTables.RollHarvestingDrops(node.LootTableRef, yield, isCritical);

        var depletion = calculator.CalculateDepletion(node, UntrainedSkillRank, NoToolTier, hasNoTool: true);
        node.CurrentHealth = Math.Max(0, node.CurrentHealth - depletion);
        node.TimesHarvested += 1;
        node.LastHarvestedAt = DateTime.UtcNow;
        await nodes.SaveNodeAsync(node);

        // The bag is the character's own `InventoryBlob`, which is the store the client reads. The engine's
        // `IInventoryService` writes save-game records instead, so a harvest through it produced the right
        // message and an empty bag.
        var bag = ReadBag(character.InventoryBlob);
        var materials = new List<HarvestedMaterial>(drops.Count);
        foreach (var drop in drops)
        {
            var slug = drop.ItemRef[(drop.ItemRef.LastIndexOf(':') + 1)..];
            AddToBag(bag, slug, drop.Quantity);
            materials.Add(new HarvestedMaterial(drop.ItemRef, drop.ItemName, drop.Quantity));
        }

        character.InventoryBlob = JsonSerializer.Serialize(bag);
        await db.SaveChangesAsync(cancellationToken);

        if (materials.Count == 0)
        {
            logger.LogWarning(
                "Node {NodeId} ({LootTableRef}) yielded nothing for character {CharacterId}; the loot table or its entries are wrong",
                node.NodeId, node.LootTableRef, request.CharacterId);
        }

        var total = materials.Sum(m => m.Quantity);
        var unit = total == 1 ? "material" : "materials";
        var message = isCritical
            ? $"Critical harvest! {total} {unit} from the {node.DisplayName}."
            : $"Gathered {total} {unit} from the {node.DisplayName}.";

        logger.LogInformation(
            "Character {CharacterId} harvested node {NodeId}: {Count} materials, critical={Critical}, health {Health}/{MaxHealth}",
            request.CharacterId, node.NodeId, total, isCritical, node.CurrentHealth, node.MaxHealth);

        return new HarvestNodeHubResult
        {
            Success = materials.Count > 0,
            ErrorMessage = materials.Count > 0 ? null : "Nothing came away this time.",
            NodeId = node.NodeId,
            DisplayName = node.DisplayName,
            Materials = materials,
            NodeHealthRemaining = node.CurrentHealth,
            NodeHealthPercent = node.GetHealthPercent(),
            NodeState = node.GetNodeState().ToString(),
            WasCritical = isCritical,
            Message = message,
        };
    }

    private static HarvestNodeHubResult Fail(string message) => new()
    {
        Success = false,
        ErrorMessage = message,
    };

    /// <summary>Reads the character's bag blob, treating unreadable content as an empty bag.</summary>
    /// <param name="blob">The stored <c>InventoryBlob</c>.</param>
    /// <returns>The bag as mutable JSON objects, so unknown fields survive a round trip.</returns>
    private static List<Dictionary<string, JsonElement>> ReadBag(string? blob)
    {
        if (string.IsNullOrWhiteSpace(blob)) return [];

        try
        {
            return JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(blob) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>Adds a stack to the bag, stacking onto the same item when it is already there.</summary>
    /// <param name="bag">The bag to mutate.</param>
    /// <param name="itemRef">The item slug the bag is keyed by.</param>
    /// <param name="quantity">How many to add.</param>
    private static void AddToBag(List<Dictionary<string, JsonElement>> bag, string itemRef, int quantity)
    {
        var existing = bag.FirstOrDefault(entry =>
            entry.TryGetValue("ItemRef", out var reference) &&
            reference.ValueKind == JsonValueKind.String &&
            string.Equals(reference.GetString(), itemRef, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            bag.Add(new Dictionary<string, JsonElement>
            {
                ["ItemRef"] = JsonSerializer.SerializeToElement(itemRef),
                ["Quantity"] = JsonSerializer.SerializeToElement(quantity),
            });
            return;
        }

        var current = existing.TryGetValue("Quantity", out var stored) && stored.TryGetInt32(out var value) ? value : 0;
        existing["Quantity"] = JsonSerializer.SerializeToElement(current + quantity);
    }
}
