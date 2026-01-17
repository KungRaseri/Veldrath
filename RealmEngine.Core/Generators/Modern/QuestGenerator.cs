using RealmEngine.Data.Services;
using RealmEngine.Shared.Models;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Generators.Modern;

/// <summary>
/// Generates Quest instances from quests catalog JSON files.
/// Supports various quest types (kill, collect, escort, explore) with objectives, rewards, and prerequisites.
/// </summary>
public class QuestGenerator
{
    private readonly GameDataCache _dataCache;
    private readonly ReferenceResolverService _referenceResolver;
    private readonly Random _random;
    private readonly ILogger<QuestGenerator> _logger;

    /// <summary>
    /// Initializes a new instance of the QuestGenerator class.
    /// </summary>
    /// <param name="dataCache">The game data cache for accessing quest catalog files.</param>
    /// <param name="referenceResolver">The reference resolver for resolving JSON references.</param>
    /// <param name="logger">Logger for this generator.</param>
    public QuestGenerator(GameDataCache dataCache, ReferenceResolverService referenceResolver, ILogger<QuestGenerator> logger)
    {
        _dataCache = dataCache ?? throw new ArgumentNullException(nameof(dataCache));
        _referenceResolver = referenceResolver ?? throw new ArgumentNullException(nameof(referenceResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _random = new Random();
    }

    /// <summary>
    /// Generates a list of random quests of a specific type.
    /// </summary>
    /// <param name="questType">The quest type (e.g., "kill", "collect", "escort", "explore").</param>
    /// <param name="count">The number of quests to generate (default: 3).</param>
    /// <param name="hydrate">If true, populates resolved ItemRewards, AbilityRewards, ObjectiveLocations, and ObjectiveNpcs properties (default: true).</param>
    /// <returns>A list of generated Quest instances.</returns>
    public async Task<List<Quest>> GenerateQuestsAsync(string questType, int count = 3, bool hydrate = true)
    {
        try
        {
            var catalogFile = _dataCache.GetFile($"quests/catalog.json");
            if (catalogFile?.JsonData == null)
            {
                return new List<Quest>();
            }

            var catalog = catalogFile.JsonData;
            var items = GetItemsFromCatalog(catalog, questType);
            
            if (items == null || !items.Any())
            {
                return new List<Quest>();
            }

            var result = new List<Quest>();

            for (int i = 0; i < count; i++)
            {
                var randomQuest = GetRandomWeightedItem(items);
                if (randomQuest != null)
                {
                    var quest = await ConvertToQuestAsync(randomQuest, questType);
                    if (quest != null)
                    {
                        if (hydrate)
                        {
                            await HydrateQuestAsync(quest);
                        }
                        result.Add(quest);
                    }
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating quests for type {QuestType}", questType);
            return new List<Quest>();
        }
    }

    /// <summary>
    /// Generates a specific quest by name from a quest type category.
    /// </summary>
    /// <param name="questType">The quest type category to search in.</param>
    /// <param name="questName">The name or display name of the quest to generate.</param>
    /// <param name="hydrate">If true, populates resolved ItemRewards, AbilityRewards, ObjectiveLocations, and ObjectiveNpcs properties (default: true).</param>
    /// <returns>The generated Quest instance, or null if not found.</returns>
    public async Task<Quest?> GenerateQuestByNameAsync(string questType, string questName, bool hydrate = true)
    {
        try
        {
            var catalogFile = _dataCache.GetFile($"quests/catalog.json");
            if (catalogFile?.JsonData == null)
            {
                return null;
            }

            var catalog = catalogFile.JsonData;
            var items = GetItemsFromCatalog(catalog, questType);
            
            var catalogQuest = items?.FirstOrDefault(q => 
                string.Equals(GetStringProperty(q, "name"), questName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(GetStringProperty(q, "displayName"), questName, StringComparison.OrdinalIgnoreCase));

            if (catalogQuest != null)
            {
                var quest = await ConvertToQuestAsync(catalogQuest, questType);
                if (quest != null && hydrate)
                {
                    await HydrateQuestAsync(quest);
                }
                return quest;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating quest {QuestName} from type {QuestType}", questName, questType);
            return null;
        }
    }

    private static IEnumerable<JToken>? GetItemsFromCatalog(JToken catalog, string questType)
    {
        // Navigate to quest_types -> specific type -> items
        var questTypes = catalog["quest_types"];
        if (questTypes == null) return null;

        var typeData = questTypes[questType];
        if (typeData == null) return null;

        var items = typeData["items"];
        return items?.Children();
    }

    private async Task<Quest?> ConvertToQuestAsync(JToken catalogQuest, string questType)
    {
        try
        {
            var quest = new Quest
            {
                Id = $"{questType}:{GetStringProperty(catalogQuest, "name")}",
                Slug = GetStringProperty(catalogQuest, "slug") ?? string.Empty,
                Title = GetStringProperty(catalogQuest, "displayName") ?? GetStringProperty(catalogQuest, "name") ?? "Unknown Quest",
                DisplayName = GetStringProperty(catalogQuest, "displayName") ?? GetStringProperty(catalogQuest, "name") ?? "Unknown Quest",
                Description = GetStringProperty(catalogQuest, "description") ?? "A mysterious task awaits",
                QuestType = GetStringProperty(catalogQuest, "questType") ?? questType,
                Difficulty = GetStringProperty(catalogQuest, "difficulty") ?? "medium",
                Type = GetBoolProperty(catalogQuest, "legendary", false) ? "legendary" : "side",
                RarityWeight = GetIntProperty(catalogQuest, "rarityWeight", 50),
                QuestGiverTypes = GetStringArrayProperty(catalogQuest, "questGiverTypes")?.ToList() ?? new List<string>(),
                CombatOptional = GetBoolProperty(catalogQuest, "combatOptional", false),
                MinClues = GetIntProperty(catalogQuest, "minClues", 0),
                MaxClues = GetIntProperty(catalogQuest, "maxClues", 0),
                
                // Set rewards
                GoldReward = GetIntProperty(catalogQuest, "baseGoldReward", 50),
                XpReward = GetIntProperty(catalogQuest, "baseXpReward", 100),
                BaseGoldReward = GetIntProperty(catalogQuest, "baseGoldReward", 50),
                BaseXpReward = GetIntProperty(catalogQuest, "baseXpReward", 100),
                
                // Set target info for kill/fetch quests
                TargetName = GetStringProperty(catalogQuest, "targetType") ?? "",
                Quantity = GetIntProperty(catalogQuest, "minQuantity", 1),
                Location = GetStringProperty(catalogQuest, "location") ?? "Unknown",
                
                // Initialize as not active/completed
                IsActive = false,
                IsCompleted = false
            };

            // Resolve item reward references
            if (catalogQuest["itemRewards"] is JArray itemRewards)
            {
                quest.ItemRewardIds = await ResolveReferencesAsync(itemRewards);
            }

            // Resolve ability reward references
            if (catalogQuest["abilityRewards"] is JArray abilityRewards)
            {
                quest.AbilityRewardIds = await ResolveReferencesAsync(abilityRewards);
            }

            // Resolve objective location references
            if (catalogQuest["locations"] is JArray locations)
            {
                quest.ObjectiveLocationIds = await ResolveReferencesAsync(locations);
            }

            // Resolve objective NPC references
            if (catalogQuest["npcs"] is JArray npcs)
            {
                quest.ObjectiveNpcIds = await ResolveReferencesAsync(npcs);
            }

            // Resolve objective enemy references
            if (catalogQuest["enemies"] is JArray enemies)
            {
                quest.ObjectiveEnemyIds = await ResolveReferencesAsync(enemies);
            }

            // Handle objectives - create simple objectives from quest data
            var objectives = new Dictionary<string, int>();
            
            if (quest.QuestType == "kill" && !string.IsNullOrEmpty(quest.TargetName))
            {
                objectives[$"Defeat {quest.Quantity} {quest.TargetName}"] = quest.Quantity;
            }
            else if (quest.QuestType == "fetch")
            {
                var itemType = GetStringProperty(catalogQuest, "itemType");
                if (!string.IsNullOrEmpty(itemType))
                {
                    objectives[$"Collect {quest.Quantity} {itemType}"] = quest.Quantity;
                }
            }
            else if (quest.QuestType == "escort")
            {
                var npcType = GetStringProperty(catalogQuest, "npcType");
                objectives[$"Escort {npcType ?? "NPC"} safely"] = 1;
            }
            else if (quest.QuestType == "investigate")
            {
                var minClues = GetIntProperty(catalogQuest, "minClues", 3);
                objectives[$"Find {minClues} clues"] = minClues;
            }
            else if (quest.QuestType == "delivery")
            {
                objectives[$"Deliver package to {quest.Location}"] = 1;
            }
            
            quest.Objectives = objectives;
            quest.ObjectiveProgress = objectives.Keys.ToDictionary(k => k, v => 0);

            return quest;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting catalog quest to Quest");
            return null;
        }
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
            return value != null ? value.Value<int>() : defaultValue;
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
    /// Hydrates a quest by resolving reference IDs to full objects.
    /// Populates ItemRewards, AbilityRewards, ObjectiveLocations, and ObjectiveNpcs properties.
    /// </summary>
    /// <param name="quest">The quest to hydrate.</param>
    private async Task HydrateQuestAsync(Quest quest)
    {
        // Resolve item rewards
        if (quest.ItemRewardIds != null && quest.ItemRewardIds.Any())
        {
            var itemRewards = new List<Item>();
            foreach (var refId in quest.ItemRewardIds)
            {
                try
                {
                    var itemJson = await _referenceResolver.ResolveToObjectAsync(refId);
                    if (itemJson != null)
                    {
                        var item = itemJson.ToObject<Item>();
                        if (item != null)
                        {
                            itemRewards.Add(item);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to resolve item reward '{RefId}'", refId);
                }
            }
            quest.ItemRewards = itemRewards;
        }

        // Resolve ability rewards
        if (quest.AbilityRewardIds != null && quest.AbilityRewardIds.Any())
        {
            var abilityRewards = new List<Ability>();
            foreach (var refId in quest.AbilityRewardIds)
            {
                try
                {
                    var abilityJson = await _referenceResolver.ResolveToObjectAsync(refId);
                    if (abilityJson != null)
                    {
                        var ability = abilityJson.ToObject<Ability>();
                        if (ability != null)
                        {
                            abilityRewards.Add(ability);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to resolve ability reward '{RefId}'", refId);
                }
            }
            quest.AbilityRewards = abilityRewards;
        }

        // Resolve objective locations
        if (quest.ObjectiveLocationIds != null && quest.ObjectiveLocationIds.Any())
        {
            var locations = new List<Location>();
            foreach (var refId in quest.ObjectiveLocationIds)
            {
                try
                {
                    var locationJson = await _referenceResolver.ResolveToObjectAsync(refId);
                    if (locationJson != null)
                    {
                        var location = locationJson.ToObject<Location>();
                        if (location != null)
                        {
                            locations.Add(location);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to resolve objective location '{RefId}'", refId);
                }
            }
            quest.ObjectiveLocations = locations;
        }

        // Resolve objective NPCs
        if (quest.ObjectiveNpcIds != null && quest.ObjectiveNpcIds.Any())
        {
            var npcs = new List<NPC>();
            foreach (var refId in quest.ObjectiveNpcIds)
            {
                try
                {
                    var npcJson = await _referenceResolver.ResolveToObjectAsync(refId);
                    if (npcJson != null)
                    {
                        var npc = npcJson.ToObject<NPC>();
                        if (npc != null)
                        {
                            npcs.Add(npc);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to resolve objective NPC '{RefId}'", refId);
                }
            }
            quest.ObjectiveNpcs = npcs;
        }
    }

    /// <summary>
    /// Safely extracts a dictionary property from a JToken with type conversion support.
    /// </summary>
    /// <typeparam name="T">The value type (int, string, or double).</typeparam>
    /// <param name="obj">The JToken to extract from.</param>
    /// <param name="propertyName">The name of the property.</param>
    /// <returns>A dictionary with the extracted values, or an empty dictionary if the property doesn't exist or conversion fails.</returns>
    private static Dictionary<string, T> GetDictionaryProperty<T>(JToken obj, string propertyName)
    {
        try
        {
            var dict = new Dictionary<string, T>();
            var value = obj[propertyName];
            if (value is JObject jObj)
            {
                foreach (var prop in jObj.Properties())
                {
                    if (typeof(T) == typeof(int))
                    {
                        dict[prop.Name] = (T)(object)prop.Value.Value<int>();
                    }
                    else if (typeof(T) == typeof(string))
                    {
                        dict[prop.Name] = (T)(object)(prop.Value.Value<string>() ?? string.Empty);
                    }
                    else if (typeof(T) == typeof(double))
                    {
                        dict[prop.Name] = (T)(object)prop.Value.Value<double>();
                    }
                }
            }
            return dict;
        }
        catch
        {
            return new Dictionary<string, T>();
        }
    }

    /// <summary>
    /// Safely extracts a string array from a JToken property, supporting both JArray and space-separated string formats.
    /// </summary>
    /// <param name="obj">The JToken to extract from.</param>
    /// <param name="propertyName">The name of the property.</param>
    /// <returns>A string array with the extracted values, or null if the property doesn't exist or conversion fails.</returns>
    private static string[]? GetStringArrayProperty(JToken obj, string propertyName)
    {
        try
        {
            var value = obj[propertyName];
            if (value == null) return null;

            if (value is JArray array)
            {
                return array.Select(x => x?.Value<string>()).Where(x => x != null).ToArray()!;
            }

            if (value.Type == JTokenType.String)
            {
                // Handle space-separated string format
                var stringValue = value.Value<string>();
                return stringValue?.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }}