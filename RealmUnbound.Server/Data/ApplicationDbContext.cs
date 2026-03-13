using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmUnbound.Server.Data.Entities;
using HallOfFameEntry = RealmEngine.Shared.Models.HallOfFameEntry;

namespace RealmUnbound.Server.Data;

/// <summary>
/// Primary EF Core context for all server-side persistence.
/// Inherits ASP.NET Core Identity tables (AspNetUsers, AspNetRoles, etc.) via
/// <see cref="IdentityDbContext{TUser,TRole,TKey}"/>.
/// Always backed by PostgreSQL; use <see cref="ContentDbContext"/> for standalone
/// content-browsing tools (e.g. RealmForge) that don't need Identity.
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

    // ── Content registry & vocabulary ────────────────────────────────────────
    public DbSet<ContentRegistry> ContentRegistry => Set<ContentRegistry>();
    public DbSet<TraitDefinition> TraitDefinitions => Set<TraitDefinition>();

    // ── Catalog content ───────────────────────────────────────────────────────
    public DbSet<Ability> Abilities => Set<Ability>();
    public DbSet<Enemy> Enemies => Set<Enemy>();
    public DbSet<Weapon> Weapons => Set<Weapon>();
    public DbSet<Armor> Armors => Set<Armor>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Material> Materials => Set<Material>();
    public DbSet<Enchantment> Enchantments => Set<Enchantment>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<Spell> Spells => Set<Spell>();
    public DbSet<CharacterClass> CharacterClasses => Set<CharacterClass>();
    public DbSet<Background> Backgrounds => Set<Background>();
    public DbSet<Npc> Npcs => Set<Npc>();
    public DbSet<Quest> Quests => Set<Quest>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<LootTable> LootTables => Set<LootTable>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<MaterialProperty> MaterialProperties => Set<MaterialProperty>();
    public DbSet<WorldLocation> WorldLocations => Set<WorldLocation>();
    public DbSet<Dialogue> Dialogues => Set<Dialogue>();

    // ── Junction tables ───────────────────────────────────────────────────────
    public DbSet<EnemyAbilityPool> EnemyAbilityPools => Set<EnemyAbilityPool>();
    public DbSet<ClassAbilityUnlock> ClassAbilityUnlocks => Set<ClassAbilityUnlock>();
    public DbSet<NpcAbility> NpcAbilities => Set<NpcAbility>();
    public DbSet<LootTableEntry> LootTableEntries => Set<LootTableEntry>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();

    // ── Name patterns ─────────────────────────────────────────────────────────
    public DbSet<NamePatternSet> NamePatternSets => Set<NamePatternSet>();
    public DbSet<NamePattern> NamePatterns => Set<NamePattern>();
    public DbSet<NameComponent> NameComponents => Set<NameComponent>();

    // ── System configuration ──────────────────────────────────────────────────
    public DbSet<GameConfig> GameConfigs => Set<GameConfig>();

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

        // All 31 content entity configurations are defined once in ContentModelConfiguration.
        ContentModelConfiguration.Configure(builder);
    }
}
