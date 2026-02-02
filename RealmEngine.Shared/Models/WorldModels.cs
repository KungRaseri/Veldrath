namespace RealmEngine.Shared.Models;

/// <summary>
/// Represents a world location or environment.
/// </summary>
public class Location
{
    /// <summary>Gets or sets the unique identifier.</summary>
    public required string Id { get; set; }
    
    /// <summary>Gets or sets the name.</summary>
    public required string Name { get; set; }
    
    /// <summary>Gets or sets the description.</summary>
    public required string Description { get; set; }
    
    /// <summary>Gets or sets the type (town, dungeon, wilderness, environment, region).</summary>
    public required string Type { get; set; }
    
    /// <summary>Gets or sets the parent region.</summary>
    public string? ParentRegion { get; set; }
    
    /// <summary>Gets or sets the level.</summary>
    public int Level { get; set; }
    
    /// <summary>Gets or sets the danger rating.</summary>
    public int DangerRating { get; set; }
    
    /// <summary>Gets or sets whether this location has a shop.</summary>
    public bool HasShop { get; set; }
    
    /// <summary>Gets or sets whether this location has an inn for resting.</summary>
    public bool HasInn { get; set; }
    
    /// <summary>Gets or sets whether this location is a safe zone (no random combat).</summary>
    public bool IsSafeZone { get; set; }
    
    /// <summary>Gets or sets whether this location can be selected as a starting zone for new characters.</summary>
    public bool IsStartingZone { get; set; }
    
    /// <summary>Gets or sets the location type (settlement, wilderness, dungeon).</summary>
    public string? LocationType { get; set; }
    
    /// <summary>Gets or sets the danger level (Low, Medium, High, Extreme).</summary>
    public string? Danger { get; set; }
    
    /// <summary>Gets or sets the difficulty level (Easy, Medium, Hard, Deadly).</summary>
    public string? Difficulty { get; set; }
    
    /// <summary>Gets or sets the features.</summary>
    public List<string> Features { get; set; } = new();
    /// <summary>Gets or sets the NPC IDs present at this location.</summary>
    public List<string> Npcs { get; set; } = new();
    /// <summary>Gets or sets the enemy IDs that can spawn at this location.</summary>
    public List<string> Enemies { get; set; } = new();
    /// <summary>Gets or sets the loot item IDs available at this location.</summary>
    public List<string> Loot { get; set; } = new();
    /// <summary>Gets or sets additional metadata for the location.</summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Fully resolved NPC objects present at this location.
    /// Populated by LocationGenerator.GenerateAsync() when hydrating templates.
    /// Not serialized to JSON (template IDs stored in Npcs instead).
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<NPC> NpcObjects { get; set; } = new();

    /// <summary>
    /// Fully resolved Enemy objects that can spawn at this location.
    /// Populated by LocationGenerator.GenerateAsync() when hydrating templates.
    /// Not serialized to JSON (template IDs stored in Enemies instead).
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<Enemy> EnemyObjects { get; set; } = new();

    /// <summary>
    /// Fully resolved Item objects available as loot at this location.
    /// Populated by LocationGenerator.GenerateAsync() when hydrating templates.
    /// Not serialized to JSON (template IDs stored in Loot instead).
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<Item> LootObjects { get; set; } = new();
}

/// <summary>
/// Represents an organization (guild, faction, shop, business).
/// </summary>
public class Organization
{
    /// <summary>Gets or sets the unique identifier.</summary>
    public required string Id { get; set; }
    /// <summary>Gets or sets the organization name.</summary>
    public required string Name { get; set; }
    /// <summary>Gets or sets the description.</summary>
    public required string Description { get; set; }
    /// <summary>Gets or sets the organization type (guild, faction, shop, business).</summary>
    public required string Type { get; set; }
    /// <summary>Gets or sets the leader ID.</summary>
    public string? Leader { get; set; }
    /// <summary>Gets or sets the member IDs.</summary>
    public List<string> Members { get; set; } = new();
    /// <summary>Gets or sets the reputation value.</summary>
    public int Reputation { get; set; }
    /// <summary>Gets or sets the wealth amount.</summary>
    public int Wealth { get; set; }
    /// <summary>Gets or sets the available services.</summary>
    public List<string> Services { get; set; } = new();
    /// <summary>Gets or sets the inventory item IDs.</summary>
    public List<string> Inventory { get; set; } = new();
    /// <summary>Gets or sets the item prices (item ID -> price).</summary>
    public Dictionary<string, int> Prices { get; set; } = new();
    /// <summary>Gets or sets additional metadata.</summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Fully resolved NPC objects who are members of this organization.
    /// Populated by OrganizationGenerator.GenerateAsync() when hydrating templates.
    /// Not serialized to JSON (template IDs stored in Members instead).
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<NPC> MemberObjects { get; set; } = new();

    /// <summary>
    /// Fully resolved Item objects available in this organization's inventory.
    /// Populated by OrganizationGenerator.GenerateAsync() when hydrating templates.
    /// Not serialized to JSON (template IDs stored in Inventory instead).
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<Item> InventoryObjects { get; set; } = new();
}

/// <summary>
/// Represents a dialogue line or response.
/// </summary>
public class DialogueLine
{
    /// <summary>Gets or sets the unique identifier.</summary>
    public required string Id { get; set; }
    /// <summary>Gets or sets the dialogue text.</summary>
    public required string Text { get; set; }
    /// <summary>Gets or sets the dialogue type (greeting, farewell, response).</summary>
    public required string Type { get; set; }
    /// <summary>Gets or sets the speaking style (formal, casual, aggressive, etc.).</summary>
    public required string Style { get; set; }
    /// <summary>Gets or sets the dialogue context.</summary>
    public string? Context { get; set; }
    /// <summary>Gets or sets the tags for categorizing the dialogue.</summary>
    public List<string> Tags { get; set; } = new();
    /// <summary>Gets or sets additional metadata.</summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Result of location-based loot generation.
/// Provides gold, XP, and item drop information based on location danger.
/// </summary>
public class LocationLootResult
{
    /// <summary>Gets or sets the amount of gold to reward.</summary>
    public int GoldAmount { get; set; }

    /// <summary>Gets or sets the amount of experience to reward.</summary>
    public int ExperienceAmount { get; set; }

    /// <summary>Gets or sets whether an item should drop.</summary>
    public bool ShouldDropItem { get; set; }

    /// <summary>Gets or sets the suggested rarity for the dropped item.</summary>
    public ItemRarity? SuggestedItemRarity { get; set; }

    /// <summary>Gets or sets the suggested item category (weapons, armor, materials, consumables).</summary>
    public string? ItemCategory { get; set; }
}
