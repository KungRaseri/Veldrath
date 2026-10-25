using Microsoft.EntityFrameworkCore;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;

namespace Veldrath.Server.Features.Characters.Combat;

/// <summary>
/// EF Core implementation of <see cref="ICombatSessionStore"/>, backed by the <c>CombatSessions</c> table.
/// </summary>
/// <remarks>
/// The character id is the primary key, so "one fight per character" is enforced by the database. Sessions carry
/// <c>DisconnectedAt</c> for the reconnect grace window added in D4 (#35); this class only reads and writes it.
/// </remarks>
public class EfCoreCombatSessionStore : ICombatSessionStore
{
    private readonly ApplicationDbContext _db;

    /// <summary>Initializes a new instance of <see cref="EfCoreCombatSessionStore"/>.</summary>
    /// <param name="db">Server database context.</param>
    public EfCoreCombatSessionStore(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<ActiveCombatSession?> TryGetAsync(Guid characterId, CancellationToken cancellationToken = default)
    {
        var row = await _db.CombatSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.CharacterId == characterId, cancellationToken);

        return row is null ? null : ToSession(row);
    }

    /// <inheritdoc />
    public Task<bool> IsInCombatAsync(Guid characterId, CancellationToken cancellationToken = default) =>
        _db.CombatSessions.AnyAsync(s => s.CharacterId == characterId, cancellationToken);

    /// <inheritdoc />
    public async Task SetAsync(
        Guid characterId,
        ActiveCombatSession session,
        CancellationToken cancellationToken = default)
    {
        var row = await _db.CombatSessions
            .FirstOrDefaultAsync(s => s.CharacterId == characterId, cancellationToken);

        if (row is null)
        {
            _db.CombatSessions.Add(new CombatSession
            {
                CharacterId = characterId,
                EnemyInstanceId = session.EnemyId,
                ZoneGroup = session.ZoneGroup,
                LocationSlug = session.LocationSlug,
                TurnCount = session.TurnCount,
                PlayerDefending = session.IsPlayerDefending,
                StartedAt = session.StartedAt,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
        }
        else
        {
            row.EnemyInstanceId = session.EnemyId;
            row.ZoneGroup = session.ZoneGroup;
            row.LocationSlug = session.LocationSlug;
            row.TurnCount = session.TurnCount;
            row.PlayerDefending = session.IsPlayerDefending;
            row.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task RemoveAsync(Guid characterId, CancellationToken cancellationToken = default) =>
        _db.CombatSessions
            .Where(s => s.CharacterId == characterId)
            .ExecuteDeleteAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> RemoveByEnemyIdAsync(
        Guid enemyId,
        CancellationToken cancellationToken = default)
    {
        var released = await _db.CombatSessions
            .Where(s => s.EnemyInstanceId == enemyId)
            .Select(s => s.CharacterId)
            .ToListAsync(cancellationToken);

        if (released.Count > 0)
        {
            await _db.CombatSessions
                .Where(s => s.EnemyInstanceId == enemyId)
                .ExecuteDeleteAsync(cancellationToken);
        }

        return released;
    }

    private static ActiveCombatSession ToSession(CombatSession row) =>
        new(
            ZoneGroup: row.ZoneGroup,
            LocationSlug: row.LocationSlug,
            EnemyId: row.EnemyInstanceId,
            IsPlayerDefending: row.PlayerDefending,
            TurnCount: row.TurnCount,
            StartedAt: row.StartedAt);
}
