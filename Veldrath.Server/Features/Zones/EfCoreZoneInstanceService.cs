using Microsoft.EntityFrameworkCore;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;

namespace Veldrath.Server.Features.Zones;

/// <summary>
/// EF Core implementation of <see cref="IZoneInstanceService"/>.
/// </summary>
/// <remarks>
/// <para>
/// Creation is guarded by two partial unique indexes on <c>ZoneInstances</c> — one for a party and one for a
/// solo character. When two members of the same party enter at the same instant, one insert wins and the
/// other gets a <see cref="DbUpdateException"/>; the loser clears its change tracker and adopts the winner's
/// run. This is the same race the enemy spawn path already resolves and logs, and it is the reason the
/// indexes exist rather than a "check then insert".
/// </para>
/// <para>
/// Membership is a pointer (<see cref="PlayerSession.ZoneInstanceId"/>); participation rows exist so credit
/// survives a disconnect or an early exit.
/// </para>
/// </remarks>
public class EfCoreZoneInstanceService : IZoneInstanceService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<EfCoreZoneInstanceService> _logger;

    /// <summary>Initializes a new instance of <see cref="EfCoreZoneInstanceService"/>.</summary>
    /// <param name="db">Server database context.</param>
    /// <param name="logger">Logger for the entry race and for sweeps.</param>
    public EfCoreZoneInstanceService(ApplicationDbContext db, ILogger<EfCoreZoneInstanceService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ZoneInstance> FindOrCreateAsync(
        string zoneId,
        Guid characterId,
        string? difficultyMode = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(zoneId))
            throw new ArgumentException("A run needs a zone id.", nameof(zoneId));
        if (characterId == Guid.Empty)
            throw new ArgumentException("A run needs a character.", nameof(characterId));

        var partyId = await _db.PartyMembers
            .Where(member => member.CharacterId == characterId)
            .Select(member => (Guid?)member.PartyId)
            .FirstOrDefaultAsync(cancellationToken);

        var existing = await FindOpenAsync(zoneId, characterId, partyId, cancellationToken);
        if (existing is not null)
        {
            await TouchAndJoinAsync(existing, characterId, cancellationToken);
            return existing;
        }

        var run = new ZoneInstance
        {
            ZoneId = zoneId,
            OwnerPartyId = partyId,
            CreatedByCharacterId = characterId,
        };
        run.ZoneGroup = ZoneGroupName.ForInstance(zoneId, difficultyMode, run.Id).Value;

        _db.ZoneInstances.Add(run);
        _db.ZoneInstanceParticipants.Add(new ZoneInstanceParticipant
        {
            ZoneInstanceId = run.Id,
            CharacterId = characterId,
        });

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Another member of this party opened the run first. Adopt theirs instead of fighting it.
            _db.ChangeTracker.Clear();
            _logger.LogInformation(
                ex,
                "Lost the instance entry race for {ZoneId} ({PartyId}); adopting the run already open",
                zoneId,
                partyId);

            var winner = await FindOpenAsync(zoneId, characterId, partyId, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"An instance for zone '{zoneId}' was rejected as a duplicate but none is open.");

            await TouchAndJoinAsync(winner, characterId, cancellationToken);
            return winner;
        }

        _logger.LogInformation(
            "Opened instance {InstanceId} for zone {ZoneId} (party: {PartyId})",
            run.Id,
            zoneId,
            partyId);

        return run;
    }

    /// <inheritdoc />
    public Task<ZoneInstance?> GetAsync(Guid instanceId, CancellationToken cancellationToken = default) =>
        _db.ZoneInstances
            .AsNoTracking()
            .FirstOrDefaultAsync(instance => instance.Id == instanceId, cancellationToken);

    /// <inheritdoc />
    public async Task<bool> JoinAsync(
        Guid instanceId,
        Guid characterId,
        CancellationToken cancellationToken = default)
    {
        var run = await _db.ZoneInstances
            .FirstOrDefaultAsync(instance => instance.Id == instanceId, cancellationToken);

        if (run is null || run.State == ZoneInstanceState.Purging)
            return false;

        await TouchAndJoinAsync(run, characterId, cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task LeaveAsync(
        Guid instanceId,
        Guid characterId,
        CancellationToken cancellationToken = default)
    {
        var participant = await _db.ZoneInstanceParticipants
            .FirstOrDefaultAsync(
                row => row.ZoneInstanceId == instanceId && row.CharacterId == characterId,
                cancellationToken);

        if (participant?.LeftAt is null)
        {
            if (participant is not null)
                participant.LeftAt = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<int> SweepAsync(TimeSpan grace, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow - grace;

        var occupied = await _db.PlayerSessions
            .Where(session => session.ZoneInstanceId != null)
            .Select(session => session.ZoneInstanceId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var stale = await _db.ZoneInstances
            .Where(instance =>
                instance.State == ZoneInstanceState.Open && instance.LastOccupiedAt < cutoff)
            .ToListAsync(cancellationToken);

        foreach (var run in stale.Where(run => !occupied.Contains(run.Id)))
            run.State = ZoneInstanceState.Empty;

        var purgeable = await _db.ZoneInstances
            .Where(instance =>
                instance.State == ZoneInstanceState.Empty && instance.LastOccupiedAt < cutoff)
            .ToListAsync(cancellationToken);

        _db.ZoneInstances.RemoveRange(purgeable);

        if (stale.Count > 0 || purgeable.Count > 0)
            await _db.SaveChangesAsync(cancellationToken);

        if (purgeable.Count > 0)
            _logger.LogInformation("Purged {Count} empty zone instances", purgeable.Count);

        return purgeable.Count;
    }

    private async Task<ZoneInstance?> FindOpenAsync(
        string zoneId,
        Guid characterId,
        Guid? partyId,
        CancellationToken cancellationToken) =>
        await _db.ZoneInstances.FirstOrDefaultAsync(
            instance =>
                instance.ZoneId == zoneId &&
                instance.State == ZoneInstanceState.Open &&
                (partyId != null
                    ? instance.OwnerPartyId == partyId
                    : instance.OwnerPartyId == null && instance.CreatedByCharacterId == characterId),
            cancellationToken);

    private async Task TouchAndJoinAsync(
        ZoneInstance run,
        Guid characterId,
        CancellationToken cancellationToken)
    {
        run.LastOccupiedAt = DateTimeOffset.UtcNow;

        var participant = await _db.ZoneInstanceParticipants.FirstOrDefaultAsync(
            row => row.ZoneInstanceId == run.Id && row.CharacterId == characterId,
            cancellationToken);

        if (participant is null)
        {
            _db.ZoneInstanceParticipants.Add(new ZoneInstanceParticipant
            {
                ZoneInstanceId = run.Id,
                CharacterId = characterId,
            });
        }
        else
        {
            participant.LeftAt = null;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
