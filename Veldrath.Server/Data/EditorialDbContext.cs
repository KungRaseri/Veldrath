using Microsoft.EntityFrameworkCore;
using Veldrath.Server.Data.Entities.Editorial;

namespace Veldrath.Server.Data;

/// <summary>
/// EF Core context for editorial content: patch notes, lore articles, and site announcements.
/// Intentionally separate from <see cref="ApplicationDbContext"/> so editorial schema can be
/// migrated independently.
/// </summary>
public class EditorialDbContext : DbContext
{
    /// <summary>Initialises a new <see cref="EditorialDbContext"/> with the given options.</summary>
    public EditorialDbContext(DbContextOptions<EditorialDbContext> options) : base(options) { }

    /// <summary>Versioned game changelog entries.</summary>
    public DbSet<PatchNote> PatchNotes => Set<PatchNote>();

    /// <summary>World lore and story articles.</summary>
    public DbSet<LoreArticle> LoreArticles => Set<LoreArticle>();

    /// <summary>Site-wide dismissible banner announcements.</summary>
    public DbSet<EditorialAnnouncement> Announcements => Set<EditorialAnnouncement>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PatchNote>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Slug).HasMaxLength(200).IsRequired();
            e.Property(x => x.Title).HasMaxLength(300).IsRequired();
            e.Property(x => x.Summary).HasMaxLength(500).IsRequired();
            e.Property(x => x.Version).HasMaxLength(50).IsRequired();
        });

        modelBuilder.Entity<LoreArticle>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Slug).HasMaxLength(200).IsRequired();
            e.Property(x => x.Title).HasMaxLength(300).IsRequired();
            e.Property(x => x.Summary).HasMaxLength(500).IsRequired();
            e.Property(x => x.Category).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<EditorialAnnouncement>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(300).IsRequired();
            e.Property(x => x.Body).HasMaxLength(1000).IsRequired();
        });
    }
}
