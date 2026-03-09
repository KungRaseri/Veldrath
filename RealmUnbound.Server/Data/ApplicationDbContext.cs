using Microsoft.EntityFrameworkCore;
using RealmUnbound.Server.Data.Entities;

namespace RealmUnbound.Server.Data;

/// <summary>Primary EF Core context for player and session state.</summary>
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<PlayerAccount> Players => Set<PlayerAccount>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<PlayerAccount>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.HasIndex(p => p.Username).IsUnique();
            entity.Property(p => p.Username).HasMaxLength(32).IsRequired();
        });
    }
}
