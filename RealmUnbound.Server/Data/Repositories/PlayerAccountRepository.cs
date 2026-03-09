using Microsoft.EntityFrameworkCore;
using RealmUnbound.Server.Data.Entities;

namespace RealmUnbound.Server.Data.Repositories;

/// <summary>EF Core implementation of <see cref="IPlayerAccountRepository"/>.</summary>
public class PlayerAccountRepository : IPlayerAccountRepository
{
    private readonly ApplicationDbContext _db;

    public PlayerAccountRepository(ApplicationDbContext db) => _db = db;

    public Task<PlayerAccount?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Players.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<PlayerAccount?> FindByUsernameAsync(string username, CancellationToken ct = default) =>
        _db.Players.FirstOrDefaultAsync(p => p.Username == username, ct);

    public Task<bool> ExistsAsync(string username, CancellationToken ct = default) =>
        _db.Players.AnyAsync(p => p.Username == username, ct);

    public async Task<PlayerAccount> CreateAsync(PlayerAccount account, CancellationToken ct = default)
    {
        _db.Players.Add(account);
        await _db.SaveChangesAsync(ct);
        return account;
    }

    public async Task UpdateAsync(PlayerAccount account, CancellationToken ct = default)
    {
        _db.Players.Update(account);
        await _db.SaveChangesAsync(ct);
    }
}
