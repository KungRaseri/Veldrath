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

        // ── Support tables ────────────────────────────────────────────────────

        builder.Entity<ContentRegistry>(e =>
        {
            e.HasKey(cr => new { cr.Domain, cr.TypeKey, cr.Slug });
            e.HasIndex(cr => cr.EntityId);
            e.Property(cr => cr.Domain).HasMaxLength(64).IsRequired();
            e.Property(cr => cr.TypeKey).HasMaxLength(64).IsRequired();
            e.Property(cr => cr.Slug).HasMaxLength(128).IsRequired();
            e.Property(cr => cr.TableName).HasMaxLength(64).IsRequired();
        });

        builder.Entity<TraitDefinition>(e =>
        {
            e.HasKey(td => td.Key);
            e.Property(td => td.Key).HasMaxLength(64);
            e.Property(td => td.ValueType).HasMaxLength(16).IsRequired();
            e.Property(td => td.Description).HasMaxLength(256);
            e.Property(td => td.AppliesTo).HasMaxLength(256);
        });

        // ── Content entity helper — configures shared base columns ────────────

        static void ConfigureContent<T>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<T> e)
            where T : ContentBase
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TypeKey, x.Slug }).IsUnique();
            e.HasIndex(x => x.TypeKey);
            e.Property(x => x.Slug).HasMaxLength(128).IsRequired();
            e.Property(x => x.TypeKey).HasMaxLength(64).IsRequired();
            e.Property(x => x.DisplayName).HasMaxLength(256);
        }

        // ── Abilities ─────────────────────────────────────────────────────────

        builder.Entity<Ability>(e =>
        {
            ConfigureContent(e);
            e.Property(x => x.AbilityType).HasMaxLength(32).IsRequired();
            e.OwnsOne(x => x.Stats, o => o.ToJson());
            e.OwnsOne(x => x.Effects, o => o.ToJson());
            e.OwnsOne(x => x.Traits, o => o.ToJson());
        });

        // ── Enemies ───────────────────────────────────────────────────────────

        builder.Entity<Enemy>(e =>
        {
            ConfigureContent(e);
            e.HasOne(x => x.LootTable)
             .WithMany()
             .HasForeignKey(x => x.LootTableId)
             .OnDelete(DeleteBehavior.SetNull);
            e.OwnsOne(x => x.Stats, o => o.ToJson());
            e.OwnsOne(x => x.Traits, o => o.ToJson());
            e.OwnsOne(x => x.Properties, o => o.ToJson());
        });

        builder.Entity<EnemyAbilityPool>(e =>
        {
            e.HasKey(x => new { x.EnemyId, x.AbilityId });
            e.HasOne(x => x.Enemy).WithMany(en => en.AbilityPool)
             .HasForeignKey(x => x.EnemyId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Ability).WithMany(a => a.EnemyPool)
             .HasForeignKey(x => x.AbilityId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── Weapons ───────────────────────────────────────────────────────────

        builder.Entity<Weapon>(e =>
        {
            ConfigureContent(e);
            e.Property(x => x.WeaponType).HasMaxLength(32).IsRequired();
            e.Property(x => x.DamageType).HasMaxLength(32).IsRequired();
            e.OwnsOne(x => x.Stats, o => o.ToJson());
            e.OwnsOne(x => x.Traits, o => o.ToJson());
        });

        // ── Armor ─────────────────────────────────────────────────────────────

        builder.Entity<Armor>(e =>
        {
            ConfigureContent(e);
            e.Property(x => x.ArmorType).HasMaxLength(32).IsRequired();
            e.Property(x => x.EquipSlot).HasMaxLength(32).IsRequired();
            e.OwnsOne(x => x.Stats, o => o.ToJson());
            e.OwnsOne(x => x.Traits, o => o.ToJson());
        });

        // ── General items ─────────────────────────────────────────────────────

        builder.Entity<Item>(e =>
        {
            ConfigureContent(e);
            e.Property(x => x.ItemType).HasMaxLength(32).IsRequired();
            e.OwnsOne(x => x.Stats, o => o.ToJson());
            e.OwnsOne(x => x.Traits, o => o.ToJson());
        });

        // ── Materials ─────────────────────────────────────────────────────────

        builder.Entity<Material>(e =>
        {
            ConfigureContent(e);
            e.Property(x => x.MaterialFamily).HasMaxLength(32).IsRequired();
            e.OwnsOne(x => x.Stats, o => o.ToJson());
            e.OwnsOne(x => x.Traits, o => o.ToJson());
        });

        // ── Enchantments ──────────────────────────────────────────────────────

        builder.Entity<Enchantment>(e =>
        {
            ConfigureContent(e);
            e.Property(x => x.TargetSlot).HasMaxLength(32);
            e.OwnsOne(x => x.Stats, o => o.ToJson());
            e.OwnsOne(x => x.Traits, o => o.ToJson());
        });

        // ── Skills ────────────────────────────────────────────────────────────

        builder.Entity<Skill>(e =>
        {
            ConfigureContent(e);
            e.OwnsOne(x => x.Stats, o => o.ToJson());
            e.OwnsOne(x => x.Traits, o => o.ToJson());
        });

        // ── Spells ────────────────────────────────────────────────────────────

        builder.Entity<Spell>(e =>
        {
            ConfigureContent(e);
            e.Property(x => x.School).HasMaxLength(32).IsRequired();
            e.OwnsOne(x => x.Stats, o => o.ToJson());
            e.OwnsOne(x => x.Traits, o => o.ToJson());
        });

        // ── Character Classes ─────────────────────────────────────────────────

        builder.Entity<CharacterClass>(e =>
        {
            ConfigureContent(e);
            e.Property(x => x.PrimaryStat).HasMaxLength(32).IsRequired();
            e.OwnsOne(x => x.Stats, o => o.ToJson());
            e.OwnsOne(x => x.Traits, o => o.ToJson());
        });

        builder.Entity<ClassAbilityUnlock>(e =>
        {
            e.HasKey(x => new { x.ClassId, x.AbilityId });
            e.HasOne(x => x.Class).WithMany(c => c.AbilityUnlocks)
             .HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Ability).WithMany(a => a.ClassUnlocks)
             .HasForeignKey(x => x.AbilityId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── Backgrounds ───────────────────────────────────────────────────────

        builder.Entity<Background>(e =>
        {
            ConfigureContent(e);
            e.OwnsOne(x => x.Stats, o => o.ToJson());
            e.OwnsOne(x => x.Traits, o => o.ToJson());
        });

        // ── NPCs ──────────────────────────────────────────────────────────────

        builder.Entity<Npc>(e =>
        {
            ConfigureContent(e);
            e.Property(x => x.Faction).HasMaxLength(64);
            e.OwnsOne(x => x.Stats, o => o.ToJson());
            e.OwnsOne(x => x.Traits, o => o.ToJson());
            e.OwnsOne(x => x.Schedule, o => o.ToJson());
        });

        builder.Entity<NpcAbility>(e =>
        {
            e.HasKey(x => new { x.NpcId, x.AbilityId });
            e.HasOne(x => x.Npc).WithMany(n => n.Abilities)
             .HasForeignKey(x => x.NpcId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Ability).WithMany(a => a.NpcAssignments)
             .HasForeignKey(x => x.AbilityId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── Quests ────────────────────────────────────────────────────────────

        builder.Entity<Quest>(e =>
        {
            ConfigureContent(e);
            e.OwnsOne(x => x.Stats, o => o.ToJson());
            e.OwnsOne(x => x.Traits, o => o.ToJson());
            e.OwnsOne(x => x.Objectives, o =>
            {
                o.ToJson();
                o.OwnsMany(q => q.Items);
            });
            e.OwnsOne(x => x.Rewards, o =>
            {
                o.ToJson();
                o.OwnsMany(q => q.Items);
            });
        });

        // ── Recipes ───────────────────────────────────────────────────────────

        builder.Entity<Recipe>(e =>
        {
            ConfigureContent(e);
            e.Property(x => x.OutputItemDomain).HasMaxLength(64).IsRequired();
            e.Property(x => x.OutputItemSlug).HasMaxLength(128).IsRequired();
            e.Property(x => x.CraftingSkill).HasMaxLength(64).IsRequired();
            e.OwnsOne(x => x.Traits, o => o.ToJson());
        });

        builder.Entity<RecipeIngredient>(e =>
        {
            e.HasKey(x => new { x.RecipeId, x.ItemDomain, x.ItemSlug });
            e.Property(x => x.ItemDomain).HasMaxLength(64).IsRequired();
            e.Property(x => x.ItemSlug).HasMaxLength(128).IsRequired();
            e.HasOne(x => x.Recipe).WithMany(r => r.Ingredients)
             .HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── Loot tables ───────────────────────────────────────────────────────

        builder.Entity<LootTable>(e =>
        {
            ConfigureContent(e);
            e.OwnsOne(x => x.Traits, o => o.ToJson());
        });

        builder.Entity<LootTableEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.LootTableId);
            e.Property(x => x.ItemDomain).HasMaxLength(64).IsRequired();
            e.Property(x => x.ItemSlug).HasMaxLength(128).IsRequired();
            e.HasOne(x => x.LootTable).WithMany(lt => lt.Entries)
             .HasForeignKey(x => x.LootTableId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── Organizations ─────────────────────────────────────────────────────

        builder.Entity<Organization>(e =>
        {
            ConfigureContent(e);
            e.Property(x => x.OrgType).HasMaxLength(32).IsRequired();
            e.OwnsOne(x => x.Stats, o => o.ToJson());
            e.OwnsOne(x => x.Traits, o => o.ToJson());
        });

        // ── Material properties ───────────────────────────────────────────────

        builder.Entity<MaterialProperty>(e =>
        {
            ConfigureContent(e);
            e.Property(x => x.MaterialFamily).HasMaxLength(32).IsRequired();
            e.OwnsOne(x => x.Stats, o => o.ToJson());
            e.OwnsOne(x => x.Traits, o => o.ToJson());
        });

        // ── World locations ───────────────────────────────────────────────────

        builder.Entity<WorldLocation>(e =>
        {
            ConfigureContent(e);
            e.Property(x => x.LocationType).HasMaxLength(32).IsRequired();
            e.OwnsOne(x => x.Stats, o => o.ToJson());
            e.OwnsOne(x => x.Traits, o => o.ToJson());
        });

        // ── Dialogue ──────────────────────────────────────────────────────────

        builder.Entity<Dialogue>(e =>
        {
            ConfigureContent(e);
            e.Property(x => x.Speaker).HasMaxLength(64);
            e.OwnsOne(x => x.Stats, o =>
            {
                o.ToJson();
                o.PrimitiveCollection(d => d.Lines);
            });
            e.OwnsOne(x => x.Traits, o => o.ToJson());
        });

        // ── Name patterns ─────────────────────────────────────────────────────

        builder.Entity<NamePatternSet>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.EntityPath).IsUnique();
            e.Property(x => x.EntityPath).HasMaxLength(128).IsRequired();
            e.Property(x => x.DisplayName).HasMaxLength(256);
        });

        builder.Entity<NamePattern>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Template).HasMaxLength(256).IsRequired();
            e.HasOne(x => x.Set).WithMany(s => s.Patterns)
             .HasForeignKey(x => x.SetId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<NameComponent>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.SetId, x.ComponentKey, x.Value }).IsUnique();
            e.Property(x => x.ComponentKey).HasMaxLength(64).IsRequired();
            e.Property(x => x.Value).HasMaxLength(128).IsRequired();
            e.HasOne(x => x.Set).WithMany(s => s.Components)
             .HasForeignKey(x => x.SetId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── System configuration ──────────────────────────────────────────────

        builder.Entity<GameConfig>(e =>
        {
            e.HasKey(x => x.ConfigKey);
            e.Property(x => x.ConfigKey).HasMaxLength(64);
            e.Property(x => x.Data).HasColumnType("jsonb").IsRequired();
        });
    }
}
