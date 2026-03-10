using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Models;
using RealmUnbound.Server.Data.Entities;

namespace RealmUnbound.Server.Data;

/// <summary>
/// Primary EF Core context for all server-side persistence.
/// Inherits ASP.NET Core Identity tables (AspNetUsers, AspNetRoles, etc.) via
/// <see cref="IdentityDbContext{TUser,TRole,TKey}"/>.
/// Provider is selected at startup from <c>Database:Provider</c> in configuration:
///   "sqlite"   — local dev (<c>Data Source=game-dev.db</c>)
///   "postgres" — Docker / production (Npgsql)
/// </summary>
public class ApplicationDbContext : IdentityDbContext<PlayerAccount, IdentityRole<Guid>, Guid>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Entities.Character> Characters => Set<Entities.Character>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Zone> Zones => Set<Zone>();
    public DbSet<ZoneSession> ZoneSessions => Set<ZoneSession>();

    // Engine entities — stored in server DB so character saves survive reconnect.
    public DbSet<SaveGameRecord> SaveGames => Set<SaveGameRecord>();
    public DbSet<HallOfFameEntry> HallOfFameEntries => Set<HallOfFameEntry>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); // required — scaffolds all Identity tables

        builder.Entity<Entities.Character>(e =>
        {
            e.HasKey(c => c.Id);
            // Character names are globally unique across the server.
            e.HasIndex(c => c.Name).IsUnique();
            // A single account cannot have two characters in the same slot.
            e.HasIndex(c => new { c.AccountId, c.SlotIndex }).IsUnique();
            e.Property(c => c.Attributes).HasColumnType("text");
            e.HasOne(c => c.Account)
             .WithMany()
             .HasForeignKey(c => c.AccountId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RefreshToken>(e =>
        {
            e.HasKey(rt => rt.Id);
            e.HasIndex(rt => rt.TokenHash).IsUnique();
            e.HasOne(rt => rt.Account)
             .WithMany()
             .HasForeignKey(rt => rt.AccountId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Zone>(e =>
        {
            e.HasKey(z => z.Id);
            e.Property(z => z.Id).HasMaxLength(64);
            e.HasMany(z => z.Sessions)
             .WithOne(s => s.Zone)
             .HasForeignKey(s => s.ZoneId)
             .OnDelete(DeleteBehavior.Cascade);

            // Static zone seed data
            e.HasData(
                new Zone { Id = "starting-zone",    Name = "Ashenveil Crossroads", Description = "A small crossroads at the edge of the Ashenveil Forest, where new adventurers gather.", Type = ZoneType.Tutorial,   MinLevel = 0, MaxPlayers = 0, IsStarter = true  },
                new Zone { Id = "town-millhaven",   Name = "Millhaven",            Description = "A prosperous market town built along the Silver River, hub of trade and gossip.",      Type = ZoneType.Town,       MinLevel = 0, MaxPlayers = 0, IsStarter = false },
                new Zone { Id = "town-ironhold",    Name = "Ironhold",             Description = "A fortified dwarven outpost in the foothills, renowned for its smiths and ales.",      Type = ZoneType.Town,       MinLevel = 5, MaxPlayers = 0, IsStarter = false },
                new Zone { Id = "dungeon-grotto",   Name = "Mossglow Grotto",      Description = "A shallow cave network overrun with kobolds and giant insects — ideal for beginners.", Type = ZoneType.Dungeon,    MinLevel = 1, MaxPlayers = 0, IsStarter = false },
                new Zone { Id = "wild-ashenveil",   Name = "Ashenveil Forest",     Description = "Dense woodland alive with wolves, bandits, and rumours of something darker within.", Type = ZoneType.Wilderness, MinLevel = 3, MaxPlayers = 0, IsStarter = false }
            );
        });

        builder.Entity<ZoneSession>(e =>
        {
            e.HasKey(s => s.Id);
            // Each connection can only be in one zone at a time
            e.HasIndex(s => s.ConnectionId).IsUnique();
            // A character can only be in one zone session at any given time
            e.HasIndex(s => s.CharacterId).IsUnique();
            e.HasOne(s => s.Character)
             .WithMany()
             .HasForeignKey(s => s.CharacterId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SaveGameRecord>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => s.PlayerName);
            e.Property(s => s.DataJson).HasColumnType("text");
        });

        builder.Entity<HallOfFameEntry>(e =>
        {
            e.HasKey(h => h.Id);
            e.HasIndex(h => h.FameScore);
        });
    }
}
