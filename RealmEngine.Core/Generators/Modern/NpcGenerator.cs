using RealmEngine.Data.Services;
using RealmEngine.Shared.Models;
using Newtonsoft.Json.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace RealmEngine.Core.Generators.Modern;

/// <summary>
/// Generates NPC (Non-Player Character) instances from npcs catalog JSON files.
/// Supports different NPC roles (merchants, quest givers, companions) and procedural name generation.
/// </summary>
public class NpcGenerator
{
    private readonly GameDataCache _dataCache;
    private readonly ReferenceResolverService _referenceResolver;
    private readonly Random _random;
    private readonly ILogger<NpcGenerator> _logger;
    private readonly NameComposer _nameComposer;

    /// <summary>
    /// Initializes a new instance of the NpcGenerator class.
    /// </summary>
    /// <param name="dataCache">The game data cache for accessing NPC catalog files.</param>
    /// <param name="referenceResolver">The reference resolver for resolving JSON references.</param>
    /// <param name="logger">Logger for this generator.</param>
    public NpcGenerator(GameDataCache dataCache, ReferenceResolverService referenceResolver, ILogger<NpcGenerator> logger)
    {
        _dataCache = dataCache ?? throw new ArgumentNullException(nameof(dataCache));
        _referenceResolver = referenceResolver ?? throw new ArgumentNullException(nameof(referenceResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _random = new Random();
        _nameComposer = new NameComposer(NullLogger<NameComposer>.Instance);
    }

    /// <summary>
    /// Generates a list of random NPCs from a specific category.
    /// </summary>
    /// <param name="category">The NPC category (e.g., "merchants", "guards", "quest-givers").</param>
    /// <param name="count">The number of NPCs to generate (default: 5).</param>
    /// <param name="hydrate">If true, populates resolved Dialogues, Abilities, and Inventory properties (default: true).</param>
    /// <returns>A list of generated NPC instances.</returns>
    public async Task<List<NPC>> GenerateNpcsAsync(string category, int count = 5, bool hydrate = true)
    {
        try
        {
            var catalogFile = _dataCache.GetFile($"npcs/{category}/catalog.json");
            if (catalogFile?.JsonData == null)
            {
                return new List<NPC>();
            }

            var catalog = catalogFile.JsonData;
            var items = GetItemsFromCatalog(catalog);

            if (items == null || !items.Any())
            {
                return new List<NPC>();
            }

            var result = new List<NPC>();

            for (int i = 0; i < count; i++)
            {
                var randomNpc = GetRandomWeightedItem(items);
                if (randomNpc != null)
                {
                    var npc = await ConvertToNpcAsync(randomNpc, category);
                    if (npc != null)
                    {
                        if (hydrate)
                        {
                            await HydrateNpcAsync(npc);
                        }
                        result.Add(npc);
                    }
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating NPCs for category {Category}", category);
            return new List<NPC>();
        }
    }

    /// <summary>
    /// Generates a specific NPC by name from a category.
    /// </summary>
    /// <param name="category">The NPC category to search in.</param>
    /// <param name="npcName">The name or display name of the NPC to generate.</param>
    /// <param name="hydrate">If true, populates resolved Dialogues, Abilities, and Inventory properties (default: true).</param>
    /// <returns>The generated NPC instance, or null if not found.</returns>
    public async Task<NPC?> GenerateNpcByNameAsync(string category, string npcName, bool hydrate = true)
    {
        try
        {
            var catalogFile = _dataCache.GetFile($"npcs/{category}/catalog.json");
            if (catalogFile?.JsonData == null)
            {
                return null;
            }

            var catalog = catalogFile.JsonData;
            var items = GetItemsFromCatalog(catalog);

            var catalogNpc = items?.FirstOrDefault(n =>
                string.Equals(GetStringProperty(n, "name"), npcName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(GetStringProperty(n, "displayName"), npcName, StringComparison.OrdinalIgnoreCase));

            if (catalogNpc != null)
            {
                var npc = await ConvertToNpcAsync(catalogNpc, category);
                if (npc != null && hydrate)
                {
                    await HydrateNpcAsync(npc);
                }
                return npc;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating NPC {NpcName} from category {Category}", npcName, category);
            return null;
        }
    }

    private static IEnumerable<JToken>? GetItemsFromCatalog(JToken catalog)
    {
        var allItems = new List<JToken>();

        // Handle hierarchical structure: occupation_types/background_types -> general_traders/etc -> items
        foreach (var property in catalog.Children<JProperty>())
        {
            if (property.Name == "metadata") continue;

            // This is a type category (occupation_types, background_types, etc)
            var typeCategory = property.Value;
            if (typeCategory is JObject typeCategoryObj)
            {
                foreach (var subType in typeCategoryObj.Children<JProperty>())
                {
                    if (subType.Name == "metadata") continue;

                    // This is a specific type (general_traders, reformed_criminals, etc)
                    var items = subType.Value["items"];
                    if (items != null && items.HasValues)
                    {
                        allItems.AddRange(items.Children());
                    }
                }
            }
        }

        return allItems.Any() ? allItems : null;
    }

    private async Task<NPC?> ConvertToNpcAsync(JToken catalogNpc, string category)
    {
        try
        {
            var npc = new NPC
            {
                Id = $"{category}:{GetStringProperty(catalogNpc, "name") ?? GetStringProperty(catalogNpc, "displayName")}",
                Slug = GetStringProperty(catalogNpc, "slug") ?? string.Empty,
                Name = GetStringProperty(catalogNpc, "displayName") ?? GetStringProperty(catalogNpc, "name") ?? "Unknown NPC",
                Age = GetIntProperty(catalogNpc, "age", _random.Next(20, 70)),
                Occupation = GetStringProperty(catalogNpc, "occupation") ?? GetStringProperty(catalogNpc, "socialClass") ?? "Citizen",
                Gold = GetIntProperty(catalogNpc, "baseGold", _random.Next(10, 100)),
                Dialogue = GetStringProperty(catalogNpc, "greeting") ?? GetStringProperty(catalogNpc, "description") ?? "Hello there!",
                IsFriendly = GetBoolProperty(catalogNpc, "isFriendly", !GetBoolProperty(catalogNpc, "isHostile", false))
            };

            // Resolve dialogue references
            if (catalogNpc["dialogues"] is JArray dialogues)
            {
                npc.DialogueIds = await ResolveReferencesAsync(dialogues);
            }

            // Resolve ability references
            if (catalogNpc["abilities"] is JArray abilities)
            {
                npc.AbilityIds = await ResolveReferencesAsync(abilities);
            }

            // Resolve inventory references
            if (catalogNpc["inventory"] is JArray inventory)
            {
                npc.InventoryIds = await ResolveReferencesAsync(inventory);
            }

            // Apply pattern-based naming from names.json
            ApplyNameFromPattern(npc);

            return npc;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting catalog NPC to NPC");
            return null;
        }
    }

    /// <summary>
    /// Apply pattern-based naming from names.json to populate Prefixes/Suffixes and enhanced Name.
    /// Note: NPCs use a single names.json file (not category-specific).
    /// </summary>
    private void ApplyNameFromPattern(NPC npc)
    {
        try
        {
            var namesPath = "npcs/names.json";
            if (!_dataCache.FileExists(namesPath)) return;

            var namesFile = _dataCache.GetFile(namesPath);
            if (namesFile?.JsonData == null) return;

            var patterns = namesFile.JsonData["patterns"];
            if (patterns == null) return;

            // Select random pattern
            var pattern = GetRandomWeightedPattern(patterns);
            if (pattern == null) return;

            var patternString = GetStringProperty(pattern, "pattern");
            if (string.IsNullOrEmpty(patternString)) return;

            // Get components
            var components = namesFile.JsonData["components"];
            if (components == null) return;

            // Use NameComposer to resolve pattern
            var (name, baseName, prefixes, suffixes) = _nameComposer.ComposeNameWithComponents(patternString, components);

            // Populate component lists
            npc.Prefixes.AddRange(prefixes);
            npc.Suffixes.AddRange(suffixes);

            // Update the name if we got a better one from the pattern
            if (!string.IsNullOrEmpty(name))
            {
                npc.Name = name;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error applying name pattern for NPC");
            // Fallback: keep catalog name
        }
    }

    private JToken? GetRandomWeightedPattern(JToken patterns)
    {
        if (patterns == null || !patterns.Any()) return null;

        var patternList = patterns.Children().ToList();
        if (!patternList.Any()) return null;

        var totalWeight = patternList.Sum(p => GetIntProperty(p, "rarityWeight", 1));
        var randomValue = _random.Next(1, totalWeight + 1);

        int currentWeight = 0;
        foreach (var pattern in patternList)
        {
            currentWeight += GetIntProperty(pattern, "rarityWeight", 1);
            if (randomValue <= currentWeight)
            {
                return pattern;
            }
        }

        return patternList.FirstOrDefault();
    }

    private async Task<List<string>> ResolveReferencesAsync(JArray? referenceArray)
    {
        var resolvedIds = new List<string>();
        if (referenceArray == null) return resolvedIds;

        foreach (var item in referenceArray)
        {
            var reference = item.ToString();
            
            if (reference.StartsWith("@"))
            {
                var resolvedId = await _referenceResolver.ResolveAsync(reference);
                if (resolvedId != null)
                {
                    resolvedIds.Add(resolvedId.ToString() ?? string.Empty);
                }
            }
        }

        return resolvedIds;
    }

    private JToken? GetRandomWeightedItem(IEnumerable<JToken> items)
    {
        var itemList = items.ToList();
        if (!itemList.Any()) return null;

        var totalWeight = itemList.Sum(item => GetIntProperty(item, "rarityWeight", 1));
        var randomValue = _random.Next(1, totalWeight + 1);

        int currentWeight = 0;
        foreach (var item in itemList)
        {
            currentWeight += GetIntProperty(item, "rarityWeight", 1);
            if (randomValue <= currentWeight)
            {
                return item;
            }
        }

        return itemList.First();
    }

    private static int GetIntProperty(JToken obj, string propertyName, int defaultValue)
    {
        try
        {
            var value = obj[propertyName];
            if (value != null)
            {
                // Handle dice notation like "5d10"
                var stringValue = value.Value<string>();
                if (stringValue?.Contains('d') == true)
                {
                    var parts = stringValue.Split('d');
                    if (parts.Length == 2 && int.TryParse(parts[0], out var rolls) && int.TryParse(parts[1], out var sides))
                    {
                        var random = new Random();
                        var total = 0;
                        for (int i = 0; i < rolls; i++)
                        {
                            total += random.Next(1, sides + 1);
                        }
                        return total;
                    }
                }
                return value.Value<int>();
            }
            return defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    private static string? GetStringProperty(JToken obj, string propertyName)
    {
        try
        {
            var value = obj[propertyName];
            return value?.Value<string>();
        }
        catch
        {
            return null;
        }
    }

    private static bool GetBoolProperty(JToken obj, string propertyName, bool defaultValue)
    {
        try
        {
            var value = obj[propertyName];
            return value != null ? value.Value<bool>() : defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// Hydrates an NPC by resolving reference IDs to full objects.
    /// Populates Dialogues, Abilities, and Inventory properties.
    /// </summary>
    /// <param name="npc">The NPC to hydrate.</param>
    private async Task HydrateNpcAsync(NPC npc)
    {
        // Resolve dialogues
        if (npc.DialogueIds != null && npc.DialogueIds.Any())
        {
            var dialogues = new List<DialogueLine>();
            foreach (var refId in npc.DialogueIds)
            {
                try
                {
                    var dialogueJson = await _referenceResolver.ResolveToObjectAsync(refId);
                    if (dialogueJson != null)
                    {
                        var dialogue = dialogueJson.ToObject<DialogueLine>();
                        if (dialogue != null)
                        {
                            dialogues.Add(dialogue);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to resolve dialogue '{RefId}'", refId);
                }
            }
            npc.Dialogues = dialogues;
        }

        // Resolve abilities
        if (npc.AbilityIds != null && npc.AbilityIds.Any())
        {
            var abilities = new List<Ability>();
            foreach (var refId in npc.AbilityIds)
            {
                try
                {
                    var abilityJson = await _referenceResolver.ResolveToObjectAsync(refId);
                    if (abilityJson != null)
                    {
                        var ability = abilityJson.ToObject<Ability>();
                        if (ability != null)
                        {
                            abilities.Add(ability);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to resolve ability '{RefId}'", refId);
                }
            }
            npc.Abilities = abilities;
        }

        // Resolve inventory
        if (npc.InventoryIds != null && npc.InventoryIds.Any())
        {
            var inventory = new List<Item>();
            foreach (var refId in npc.InventoryIds)
            {
                try
                {
                    var itemJson = await _referenceResolver.ResolveToObjectAsync(refId);
                    if (itemJson != null)
                    {
                        var item = itemJson.ToObject<Item>();
                        if (item != null)
                        {
                            inventory.Add(item);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to resolve inventory item '{RefId}'", refId);
                }
            }
            npc.Inventory = inventory;
        }
    }
}
