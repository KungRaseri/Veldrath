using Veldrath.Server.Data.Entities;

namespace Veldrath.Server.Data.Repositories;

/// <summary>Persistence contract for <see cref="PlayerAccount"/>.</summary>
public interface IPlayerAccountRepository
{
    /// <summary>Returns the account with the given ID, or <see langword="null"/> if not found.</summary>
    Task<PlayerAccount?> FindByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Returns the account with the given username, or <see langword="null"/> if not found.</summary>
    Task<PlayerAccount?> FindByUsernameAsync(string username, CancellationToken ct = default);

    /// <summary>Returns <see langword="true"/> if an account with <paramref name="username"/> already exists.</summary>
    Task<bool> ExistsAsync(string username, CancellationToken ct = default);

    /// <summary>Persists a new account and returns the saved entity.</summary>
    Task<PlayerAccount> CreateAsync(PlayerAccount account, CancellationToken ct = default);

    /// <summary>Persists changes to an existing account.</summary>
    Task UpdateAsync(PlayerAccount account, CancellationToken ct = default);
}
