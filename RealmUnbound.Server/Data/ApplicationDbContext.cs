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

    public DbSet<Character> Characters => Set<Character>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // Engine entities — stored in server DB so character saves survive reconnect.
    public DbSet<SaveGameRecord> SaveGames => Set<SaveGameRecord>();
    public DbSet<HallOfFameEntry> HallOfFameEntries => Set<HallOfFameEntry>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); // required — scaffolds all Identity tables

        builder.Entity<Character>(e =>
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
