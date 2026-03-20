using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Entities;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Persistence;

/// <summary>
/// EF Core context for game-state entities: saves, hall of fame, and inventory records.
/// Used by both standalone clients (SQLite) and <c>RealmUnbound.Server</c> (Postgres).
/// Auth/Identity/server-operational tables live in <c>ApplicationDbContext</c>.
/// </summary>
public class GameDbContext : DbContext
{
    /// <summary>Initialises a new <see cref="GameDbContext"/> with the given options.</summary>
    public GameDbContext(DbContextOptions<GameDbContext> options) : base(options) { }

    /// <summary>Save games stored as serialised JSON records.</summary>
    public DbSet<SaveGameRecord> SaveGames => Set<SaveGameRecord>();

    /// <summary>Hall of Fame entries (one row per deceased/retired character).</summary>
    public DbSet<HallOfFameEntry> HallOfFameEntries => Set<HallOfFameEntry>();

    /// <summary>Inventory slots — item ref + quantity per character per save.</summary>
    public DbSet<InventoryRecord> InventoryRecords => Set<InventoryRecord>();

    /// <summary>Spawned harvestable resource nodes in the game world, including live health and harvest state.</summary>
    public DbSet<HarvestableNodeRecord> HarvestableNodes => Set<HarvestableNodeRecord>();

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
        });

        builder.Entity<InventoryRecord>(e =>
        {
            e.HasKey(i => i.Id);
            e.HasIndex(i => new { i.SaveGameId, i.CharacterName });
            e.HasIndex(i => new { i.SaveGameId, i.CharacterName, i.ItemRef }).IsUnique();
            e.Property(i => i.SaveGameId).HasMaxLength(128).IsRequired();
            e.Property(i => i.CharacterName).HasMaxLength(128).IsRequired();
            e.Property(i => i.ItemRef).HasMaxLength(256).IsRequired();
        });

        builder.Entity<HarvestableNodeRecord>(e =>
        {
            e.HasKey(n => n.NodeId);
            e.HasIndex(n => n.LocationId);
            e.Property(n => n.NodeId).HasMaxLength(128).IsRequired();
            e.Property(n => n.LocationId).HasMaxLength(256).IsRequired();
            e.Property(n => n.LastHarvestedAt).HasColumnType("timestamp with time zone");
        });
    }
}
