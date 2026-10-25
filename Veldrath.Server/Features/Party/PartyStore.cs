using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;

namespace Veldrath.Server.Features.Party;

/// <summary>One member of a party, as the client sees them.</summary>
/// <param name="CharacterId">Character id.</param>
/// <param name="Name">Character display name.</param>
/// <param name="Level">Character level.</param>
/// <param name="CurrentHealth">Health read from the character's attribute blob.</param>
/// <param name="MaxHealth">Maximum health read from the character's attribute blob.</param>
/// <param name="IsLeader">Whether this member leads the party.</param>
public sealed record PartyMemberView(
    Guid CharacterId,
    string Name,
    int Level,
    int CurrentHealth,
    int MaxHealth,
    bool IsLeader);

/// <summary>
/// A complete view of a party. Every mutation broadcasts one of these, so clients never assemble state from deltas.
/// </summary>
/// <param name="PartyId">Party id.</param>
/// <param name="LeaderCharacterId">Character that leads the party.</param>
/// <param name="LootRule">Loot rule in force.</param>
/// <param name="Members">Members, leader first.</param>
public sealed record PartySnapshot(
    Guid PartyId,
    Guid LeaderCharacterId,
    string LootRule,
    IReadOnlyList<PartyMemberView> Members);

/// <summary>
/// Persistence helpers for multiplayer parties. Backed by the <c>Parties</c>/<c>PartyMembers</c>/<c>PartyInvites</c>
/// tables, so a party survives a restart and a rolling deploy.
/// </summary>
public sealed class PartyStore
{
    private readonly ApplicationDbContext _db;

    /// <summary>Initializes a new instance of <see cref="PartyStore"/>.</summary>
    /// <param name="db">Server database context.</param>
    public PartyStore(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>Returns the party a character belongs to, or <see langword="null"/> when they are solo.</summary>
    /// <param name="characterId">Character id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<Data.Entities.Party?> GetPartyForCharacterAsync(
        Guid characterId,
        CancellationToken cancellationToken = default) =>
        _db.Parties
            .Where(party => party.Members.Any(member => member.CharacterId == characterId))
            .Include(party => party.Members)
                .ThenInclude(member => member.Character)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>Returns a party by id with its members loaded, or <see langword="null"/>.</summary>
    /// <param name="partyId">Party id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<Data.Entities.Party?> GetPartyAsync(
        Guid partyId,
        CancellationToken cancellationToken = default) =>
        _db.Parties
            .Include(party => party.Members)
                .ThenInclude(member => member.Character)
            .FirstOrDefaultAsync(party => party.Id == partyId, cancellationToken);

    /// <summary>Creates a party led by the given character, with that character as its only member.</summary>
    /// <param name="leaderCharacterId">Character that will lead it.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created party, with members loaded.</returns>
    public async Task<Data.Entities.Party> CreateAsync(
        Guid leaderCharacterId,
        CancellationToken cancellationToken = default)
    {
        var party = new Data.Entities.Party
        {
            LeaderCharacterId = leaderCharacterId,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        party.Members.Add(new PartyMemberEntity
        {
            PartyId = party.Id,
            CharacterId = leaderCharacterId,
            JoinedAt = DateTimeOffset.UtcNow,
        });

        _db.Parties.Add(party);
        await _db.SaveChangesAsync(cancellationToken);

        _db.ChangeTracker.Clear();
        return await GetPartyAsync(party.Id, cancellationToken)
               ?? throw new InvalidOperationException($"Party {party.Id} vanished immediately after creation");
    }

    /// <summary>Removes a character from their party.</summary>
    /// <param name="characterId">Character id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when a membership row was removed.</returns>
    public async Task<bool> RemoveMemberAsync(Guid characterId, CancellationToken cancellationToken = default)
    {
        var affected = await _db.PartyMembers
            .Where(member => member.CharacterId == characterId)
            .ExecuteDeleteAsync(cancellationToken);

        _db.ChangeTracker.Clear();
        return affected == 1;
    }

    /// <summary>Deletes a party and, by cascade, its memberships and invites.</summary>
    /// <param name="partyId">Party id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task DeleteAsync(Guid partyId, CancellationToken cancellationToken = default)
    {
        await _db.Parties
            .Where(party => party.Id == partyId)
            .ExecuteDeleteAsync(cancellationToken);

        _db.ChangeTracker.Clear();
    }

    /// <summary>Promotes a character to leader.</summary>
    /// <param name="partyId">Party id.</param>
    /// <param name="characterId">The new leader.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task PromoteLeaderAsync(
        Guid partyId,
        Guid characterId,
        CancellationToken cancellationToken = default)
    {
        await _db.Parties
            .Where(party => party.Id == partyId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(party => party.LeaderCharacterId, characterId),
                cancellationToken);

        _db.ChangeTracker.Clear();
    }

    /// <summary>Projects a party into the snapshot clients receive.</summary>
    /// <param name="party">Party with members loaded.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<PartySnapshot> ToSnapshotAsync(
        Data.Entities.Party party,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var members = party.Members
            .Where(member => member.Character is not null)
            .OrderByDescending(member => member.CharacterId == party.LeaderCharacterId)
            .ThenBy(member => member.JoinedAt)
            .Select(member =>
            {
                var (currentHealth, maxHealth) = ReadVitals(member.Character!.Attributes);
                return new PartyMemberView(
                    member.CharacterId,
                    member.Character.Name,
                    member.Character.Level,
                    currentHealth,
                    maxHealth,
                    member.CharacterId == party.LeaderCharacterId);
            })
            .ToList();

        return Task.FromResult(new PartySnapshot(party.Id, party.LeaderCharacterId, party.LootRule, members));
    }

    /// <summary>
    /// Reads health from the attributes blob. A malformed blob yields zeros rather than throwing — the party panel
    /// showing "0/0" is a visible symptom, not a crash in the middle of someone's inventory.
    /// </summary>
    /// <param name="attributesJson">Raw attributes blob.</param>
    /// <returns>Current and maximum health.</returns>
    private static (int Current, int Max) ReadVitals(string? attributesJson)
    {
        if (string.IsNullOrWhiteSpace(attributesJson) || attributesJson == "{}")
        {
            return (0, 0);
        }

        try
        {
            var attributes = JsonSerializer.Deserialize<Dictionary<string, int>>(attributesJson) ?? [];
            return (
                attributes.GetValueOrDefault("CurrentHealth"),
                attributes.GetValueOrDefault("MaxHealth"));
        }
        catch (JsonException)
        {
            return (0, 0);
        }
    }
}
