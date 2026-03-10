using Microsoft.EntityFrameworkCore;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Persistence;

/// <summary>
/// EF Core context for standalone / Godot local persistence (SQLite).
/// Not used by RealmUnbound.Server — the server uses its own <c>ApplicationDbContext</c>.
/// </summary>
public class GameDbContext : DbContext
{
    /// <summary>Initialises a new <see cref="GameDbContext"/> with the given options.</summary>
    public GameDbContext(DbContextOptions<GameDbContext> options) : base(options) { }

    /// <summary>Save games stored as serialised JSON records.</summary>
    public DbSet<SaveGameRecord> SaveGames => Set<SaveGameRecord>();

    /// <summary>Hall of Fame entries (one row per deceased/retired character).</summary>
    public DbSet<HallOfFameEntry> HallOfFameEntries => Set<HallOfFameEntry>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<SaveGameRecord>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => s.PlayerName);
            e.HasIndex(s => s.SlotIndex);
            e.Property(s => s.DataJson).HasColumnType("text");
        });

        builder.Entity<HallOfFameEntry>(e =>
        {
            e.HasKey(h => h.Id);
            e.HasIndex(h => h.FameScore);
            // All other properties are primitive — no further config needed.
        });
    }
}
