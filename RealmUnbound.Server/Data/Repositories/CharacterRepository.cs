using Microsoft.EntityFrameworkCore;
using RealmUnbound.Server.Data.Entities;

namespace RealmUnbound.Server.Data.Repositories;

/// <summary>EF Core implementation of <see cref="ICharacterRepository"/>.</summary>
public class CharacterRepository : ICharacterRepository
{
    private readonly ApplicationDbContext _db;

    public CharacterRepository(ApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<Character>> GetByAccountIdAsync(Guid accountId, CancellationToken ct = default) =>
        await _db.Characters
            .Where(c => c.AccountId == accountId && c.DeletedAt == null)
            .OrderByDescending(c => c.LastPlayedAt)
            .ToListAsync(ct);

    public Task<Character?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Characters.FirstOrDefaultAsync(c => c.Id == id && c.DeletedAt == null, ct);

    public Task<Character?> GetLastPlayedAsync(Guid accountId, CancellationToken ct = default) =>
        _db.Characters
            .Where(c => c.AccountId == accountId && c.DeletedAt == null)
            .OrderByDescending(c => c.LastPlayedAt)
            .FirstOrDefaultAsync(ct);

    public Task<bool> NameExistsAsync(string name, CancellationToken ct = default) =>
        _db.Characters.AnyAsync(c => c.Name == name && c.DeletedAt == null, ct);

    public Task<int> GetActiveCountAsync(Guid accountId, CancellationToken ct = default) =>
        _db.Characters.CountAsync(c => c.AccountId == accountId && c.DeletedAt == null, ct);

    public async Task<Character> CreateAsync(Character character, CancellationToken ct = default)
    {
        _db.Characters.Add(character);
        await _db.SaveChangesAsync(ct);
        return character;
    }

    public async Task UpdateAsync(Character character, CancellationToken ct = default)
    {
        _db.Characters.Update(character);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SoftDeleteAsync(Guid id, CancellationToken ct = default)
    {
        var character = await _db.Characters.FindAsync([id], ct);
        if (character is null) return;
        character.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
