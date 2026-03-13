using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Entities;

namespace RealmEngine.Data.Persistence;

/// <summary>
/// Shared EF Core model configuration for all game content entities.
/// Called from both <see cref="ContentDbContext"/> (standalone tools) and
/// <c>ApplicationDbContext</c> (RealmUnbound.Server) so the content schema
/// is defined exactly once.
/// </summary>
public static class ContentModelConfiguration
{
    /// <summary>
    /// Applies all content entity configurations to the given <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The model builder to configure.</param>
    public static void Configure(ModelBuilder builder)
    {
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

    /// <summary>
    /// Applies shared base columns (PK, TypeKey+Slug unique index, common string lengths)
    /// to a <see cref="ContentBase"/> entity.
    /// </summary>
    private static void ConfigureContent<T>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<T> e)
        where T : ContentBase
    {
        e.HasKey(x => x.Id);
        e.HasIndex(x => new { x.TypeKey, x.Slug }).IsUnique();
        e.HasIndex(x => x.TypeKey);
        e.Property(x => x.Slug).HasMaxLength(128).IsRequired();
        e.Property(x => x.TypeKey).HasMaxLength(64).IsRequired();
        e.Property(x => x.DisplayName).HasMaxLength(256);
    }
}
