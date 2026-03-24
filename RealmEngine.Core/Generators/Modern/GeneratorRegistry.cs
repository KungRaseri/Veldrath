namespace RealmEngine.Core.Generators.Modern;

/// <summary>
/// Central registry for all game content generators.
/// Provides unified access to all generator types via dependency injection.
/// </summary>
public class GeneratorRegistry(
    PowerGenerator powers,
    CharacterClassGenerator classes,
    EnemyGenerator enemies,
    ItemGenerator items,
    NpcGenerator npcs,
    QuestGenerator quests,
    LocationGenerator locations,
    OrganizationGenerator organizations,
    DialogueGenerator dialogue,
    EnchantmentGenerator enchantments)
{
    /// <summary>Gets the power generator.</summary>
    public PowerGenerator Powers => powers;

    /// <summary>Gets the character class generator.</summary>
    public CharacterClassGenerator Classes => classes;

    /// <summary>Gets the enemy generator.</summary>
    public EnemyGenerator Enemies => enemies;

    /// <summary>Gets the item generator.</summary>
    public ItemGenerator Items => items;

    /// <summary>Gets the NPC generator.</summary>
    public NpcGenerator Npcs => npcs;

    /// <summary>Gets the quest generator.</summary>
    public QuestGenerator Quests => quests;

    /// <summary>Gets the location generator.</summary>
    public LocationGenerator Locations => locations;

    /// <summary>Gets the organization generator.</summary>
    public OrganizationGenerator Organizations => organizations;

    /// <summary>Gets the dialogue generator.</summary>
    public DialogueGenerator Dialogue => dialogue;

    /// <summary>Gets the enchantment generator.</summary>
    public EnchantmentGenerator Enchantments => enchantments;

    /// <summary>No-op — all generators are already initialized via DI.</summary>
    public void WarmUp() { }
}
