using RealmUnbound.Server.Data.Entities;

namespace RealmUnbound.Server.Data.Repositories;

/// <summary>Persistence contract for <see cref="PlayerAccount"/>.</summary>
public interface IPlayerAccountRepository
{
    Task<PlayerAccount?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<PlayerAccount?> FindByUsernameAsync(string username, CancellationToken ct = default);
    Task<bool> ExistsAsync(string username, CancellationToken ct = default);
    Task<PlayerAccount> CreateAsync(PlayerAccount account, CancellationToken ct = default);
    Task UpdateAsync(PlayerAccount account, CancellationToken ct = default);
}
