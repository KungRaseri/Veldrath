using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;

namespace Veldrath.Server.Tests.Infrastructure;

/// <summary>
/// Presence-only <see cref="IPlayerSessionRepository"/> for tests that need to know whether a character is online but
/// do not care about zone bookkeeping. Every unrelated member throws, so an accidental dependency shows up loudly.
/// </summary>
internal sealed class FakePlayerSessionRepository : IPlayerSessionRepository
{
    private readonly HashSet<Guid> _online = new();

    /// <summary>Marks a character as online.</summary>
    /// <param name="characterId">Character id.</param>
    public void MarkOnline(Guid characterId) => _online.Add(characterId);

    /// <summary>Marks a character as offline.</summary>
    /// <param name="characterId">Character id.</param>
    public void MarkOffline(Guid characterId) => _online.Remove(characterId);

    /// <inheritdoc />
    public Task<PlayerSession?> GetByCharacterIdAsync(Guid characterId) =>
        Task.FromResult(_online.Contains(characterId)
            ? new PlayerSession { CharacterId = characterId, ConnectionId = $"conn-{characterId:N}" }
            : null);

    /// <inheritdoc />
    public Task<PlayerSession?> GetByCharacterNameAsync(string characterName) => Task.FromResult<PlayerSession?>(null);

    /// <inheritdoc />
    public Task<PlayerSession?> GetByConnectionIdAsync(string connectionId) => Task.FromResult<PlayerSession?>(null);

    /// <inheritdoc />
    public Task<List<PlayerSession>> GetByZoneIdAsync(string zoneId) => Task.FromResult(new List<PlayerSession>());

    /// <inheritdoc />
    public Task<List<PlayerSession>> GetByRegionIdAsync(string regionId) => Task.FromResult(new List<PlayerSession>());

    /// <inheritdoc />
    public Task<List<PlayerSession>> GetOnRegionMapAsync(string regionId) => Task.FromResult(new List<PlayerSession>());

    /// <inheritdoc />
    public Task AddAsync(PlayerSession session) =>
        throw new NotSupportedException("Presence fake used in a test that writes sessions");

    /// <inheritdoc />
    public Task RemoveAsync(PlayerSession session) =>
        throw new NotSupportedException("Presence fake used in a test that writes sessions");

    /// <inheritdoc />
    public Task RemoveByConnectionIdAsync(string connectionId) =>
        throw new NotSupportedException("Presence fake used in a test that writes sessions");

    /// <inheritdoc />
    public Task UpdateMapPositionAsync(
        Guid characterId,
        string mapId,
        string nodeId,
        string? zoneId,
        string? regionId) =>
        throw new NotSupportedException("Presence fake used in a test that writes sessions");
}
