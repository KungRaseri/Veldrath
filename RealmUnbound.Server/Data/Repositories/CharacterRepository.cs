using Microsoft.EntityFrameworkCore;
using RealmUnbound.Server.Data.Entities;

namespace RealmUnbound.Server.Data.Repositories;

/// <summary>EF Core implementation of <see cref="ICharacterRepository"/>.</summary>
public class CharacterRepository : ICharacterRepository
{
    private readonly ApplicationDbContext _db;

    public CharacterRepository(ApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<Character>> GetByAccountIdAsync(Guid accountId, CancellationToken ct = default)
    {
        var list = await _db.Characters
            .Where(c => c.AccountId == accountId && c.DeletedAt == null)
            .ToListAsync(ct);
        // Sort client-side: DateTimeOffset ORDER BY is not supported by the SQLite provider.
        return list.OrderByDescending(c => c.LastPlayedAt).ToList();
    }

    public Task<Character?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Characters.FirstOrDefaultAsync(c => c.Id == id && c.DeletedAt == null, ct);

    public async Task<Character?> GetLastPlayedAsync(Guid accountId, CancellationToken ct = default)
    {
        var list = await _db.Characters
            .Where(c => c.AccountId == accountId && c.DeletedAt == null)
            .ToListAsync(ct);
        return list.OrderByDescending(c => c.LastPlayedAt).FirstOrDefault();
    }

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

        // Rename the deleted character so the unique IX_Characters_Name index no longer
        // blocks a new character from taking the same name.
        // Only rename on first deletion to keep idempotency clean.
        if (character.DeletedAt is null)
        {
            var shortId = id.ToString("N")[..8];
            character.Name = $"{character.Name}_deleted_{shortId}";

            // Use a large negative slot to satisfy IX_Characters_AccountId_SlotIndex while
            // still freeing the real slot for a new character.
            // Using hash-derived value ensures uniqueness across multiple deletions on one account.
            character.SlotIndex = -(Math.Abs(id.GetHashCode()) % 1_000_000 + 1);
        }

        character.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateCurrentZoneAsync(Guid id, string zoneId, CancellationToken ct = default)
    {
        var character = await _db.Characters.FindAsync([id], ct);
        if (character is null) return;
        character.CurrentZoneId  = zoneId;
        character.LastPlayedAt   = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
