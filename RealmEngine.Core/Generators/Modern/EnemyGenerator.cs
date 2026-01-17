using RealmEngine.Data.Services;
using RealmEngine.Shared.Models;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace RealmEngine.Core.Generators.Modern;

/// <summary>
/// Generates Enemy instances from enemies catalog JSON files.
/// Supports hierarchical categorization (e.g., beasts/wolves, undead/skeletons) and weighted random selection.
/// </summary>
public class EnemyGenerator
{
    private readonly GameDataCache _dataCache;
    private readonly ReferenceResolverService _referenceResolver;
    private readonly Random _random;
    private readonly ILogger<EnemyGenerator> _logger;
    private readonly NameComposer _nameComposer;

    /// <summary>
    /// Initializes a new instance of the EnemyGenerator class.
    /// </summary>
    /// <param name="dataCache">The game data cache for accessing enemy catalog files.</param>
    /// <param name="referenceResolver">The reference resolver for resolving JSON references.</param>
    /// <param name="logger">Logger for this generator.</param>
    public EnemyGenerator(GameDataCache dataCache, ReferenceResolverService referenceResolver, ILogger<EnemyGenerator> logger)
    {
        _dataCache = dataCache ?? throw new ArgumentNullException(nameof(dataCache));
        _referenceResolver = referenceResolver ?? throw new ArgumentNullException(nameof(referenceResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _random = new Random();
        _nameComposer = new NameComposer(NullLogger<NameComposer>.Instance);
    }

    /// <summary>
    /// Generates a list of random enemies from a specific category.
    /// </summary>
    /// <param name="category">The enemy category (e.g., "beasts", "undead", "dragons").</param>
    /// <param name="count">The number of enemies to generate (default: 5).</param>
    /// <param name="hydrate">If true, populates resolved Abilities and LootTable properties (default: true).</param>
    /// <returns>A list of generated Enemy instances.</returns>
    public async Task<List<Enemy>> GenerateEnemiesAsync(string category, int count = 5, bool hydrate = true)
    {
        try
        {
            var catalogFile = _dataCache.GetFile($"enemies/{category}/catalog.json");
            if (catalogFile?.JsonData == null)
            {
                return new List<Enemy>();
            }

            var catalog = catalogFile.JsonData;
            var items = GetItemsFromCatalog(catalog);
            
            if (items == null || !items.Any())
            {
                return new List<Enemy>();
            }

            var result = new List<Enemy>();

            for (int i = 0; i < count; i++)
            {
                var (randomEnemy, categoryProperties) = GetRandomWeightedItemWithProperties(catalog);
                if (randomEnemy != null)
                {
                    var enemy = await ConvertToEnemyAsync(randomEnemy, category, categoryProperties);
                    if (enemy != null)
                    {
                        if (hydrate)
                        {
                            await HydrateEnemyAsync(enemy);
                        }
                        result.Add(enemy);
                    }
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating enemies for category {Category}", category);
            return new List<Enemy>();
        }
    }

    /// <summary>
    /// Generates a specific enemy by name from a category.
    /// </summary>
    /// <param name="category">The enemy category to search in.</param>
    /// <param name="enemyName">The name of the enemy to generate.</param>
    /// <param name="hydrate">If true, populates resolved Abilities and LootTable properties (default: true).</param>
    /// <returns>The generated Enemy instance, or null if not found.</returns>
    public async Task<Enemy?> GenerateEnemyByNameAsync(string category, string enemyName, bool hydrate = true)
    {
        try
        {
            var catalogFile = _dataCache.GetFile($"enemies/{category}/catalog.json");
            if (catalogFile?.JsonData == null)
            {
                return null;
            }

            var catalog = catalogFile.JsonData;
            var (catalogEnemy, categoryProperties) = FindEnemyInCatalog(catalog, enemyName);

            if (catalogEnemy != null)
            {
                var enemy = await ConvertToEnemyAsync(catalogEnemy, category, categoryProperties);
                if (enemy != null && hydrate)
                {
                    await HydrateEnemyAsync(enemy);
                }
                return enemy;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating enemy {EnemyName} from category {Category}", enemyName, category);
            return null;
        }
    }

    private static IEnumerable<JToken>? GetItemsFromCatalog(JToken catalog)
    {
        var allItems = new List<JToken>();
        
        // Handle hierarchical structure: beast_types -> wolves/bears/etc -> items
        foreach (var property in catalog.Children<JProperty>())
        {
            if (property.Name == "metadata") continue;
            
            // This is a type category (beast_types, undead_types, etc)
            var typeCategory = property.Value;
            if (typeCategory is JObject typeCategoryObj)
            {
                foreach (var subType in typeCategoryObj.Children<JProperty>())
                {
                    if (subType.Name == "metadata") continue;
                    
                    // This is a specific type (wolves, bears, etc)
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

    private async Task<Enemy?> ConvertToEnemyAsync(JToken catalogEnemy, string category, JObject? categoryProperties = null)
    {
        try
        {
            var level = GetIntProperty(catalogEnemy, "level", 1);
            
            // v5.1: Read attributes from attributes object, fallback to v4.0 top-level
            var attributes = catalogEnemy["attributes"] as JObject;
            int str = attributes?["strength"]?.Value<int>() ?? GetIntProperty(catalogEnemy, "strength", 10);
            int dex = attributes?["dexterity"]?.Value<int>() ?? GetIntProperty(catalogEnemy, "dexterity", 10);
            int con = attributes?["constitution"]?.Value<int>() ?? GetIntProperty(catalogEnemy, "constitution", 10);
            int intel = attributes?["intelligence"]?.Value<int>() ?? GetIntProperty(catalogEnemy, "intelligence", 10);
            int wis = attributes?["wisdom"]?.Value<int>() ?? GetIntProperty(catalogEnemy, "wisdom", 10);
            int cha = attributes?["charisma"]?.Value<int>() ?? GetIntProperty(catalogEnemy, "charisma", 10);
            
            // Determine if this is a boss enemy from category properties
            bool isBoss = categoryProperties?["isBoss"]?.Value<bool>() ?? false;
            var rarity = GetIntProperty(catalogEnemy, "rarity", 10);
            
            // v5.1: Read stats from stats object (formulas), fallback to v4.0 direct values
            var statsObj = catalogEnemy["stats"] as JObject;
            int health = GetStatValue(statsObj, "health", catalogEnemy, "health", 50, level, str, dex, con, intel, wis, cha);
            int attack = GetStatValue(statsObj, "attack", catalogEnemy, "attack", 5, level, str, dex, con, intel, wis, cha);
            int defense = GetStatValue(statsObj, "defense", catalogEnemy, "defense", 10, level, str, dex, con, intel, wis, cha);
            int magicAttack = GetStatValue(statsObj, "magicAttack", catalogEnemy, "magicAttack", 0, level, str, dex, con, intel, wis, cha);
            
            var enemy = new Enemy
            {
                Id = $"{category}:{GetStringProperty(catalogEnemy, "name")}",
                Slug = GetStringProperty(catalogEnemy, "slug") ?? string.Empty,
                Name = GetStringProperty(catalogEnemy, "name") ?? "Unknown Enemy",
                Description = GetStringProperty(catalogEnemy, "description") ?? "A mysterious creature",
                Health = health,
                MaxHealth = health,
                Level = level,
                
                // Map attributes (both individual and dictionary)
                Strength = str,
                Dexterity = dex,
                Constitution = con,
                Intelligence = intel,
                Wisdom = wis,
                Charisma = cha,
                Attributes = GetDictionaryProperty<int>(catalogEnemy, "attributes"),
                Stats = GetDictionaryProperty<string>(catalogEnemy, "stats"),
                
                // Map damage properties
                BasePhysicalDamage = attack,
                BaseMagicDamage = magicAttack,
                
                // Map rewards (bosses get 2.5x multiplier)
                XP = GetIntProperty(catalogEnemy, "xp", 25) * (isBoss ? 2 : 1),
                GoldReward = GetIntProperty(catalogEnemy, "gold", 10) * (isBoss ? 3 : 1),
                
                // Set Type and Difficulty based on boss status and rarity
                Type = DetermineEnemyType(category, isBoss),
                Difficulty = DetermineDifficulty(rarity, isBoss)
            };

            // v5.1: Resolve ability references from combat.abilities, fallback to v4.0 top-level abilities
            var combatObj = catalogEnemy["combat"] as JObject;
            var abilities = combatObj?["abilities"]?.ToObject<string[]>() ?? GetStringArrayProperty(catalogEnemy, "abilities");
            if (abilities?.Any() == true)
            {
                var resolvedAbilities = new List<string>();
                foreach (var ability in abilities)
                {
                    // Keep the reference ID (don't resolve yet - hydration will handle that)
                    resolvedAbilities.Add(ability);
                }
                enemy.AbilityIds = resolvedAbilities;
            }

            // Resolve loot table references
            var loot = GetStringArrayProperty(catalogEnemy, "loot");
            if (loot?.Any() == true)
            {
                var resolvedLoot = new List<string>();
                foreach (var lootRef in loot)
                {
                    if (lootRef.StartsWith("@"))
                    {
                        var resolved = await _referenceResolver.ResolveAsync(lootRef);
                        if (resolved is string resolvedId)
                        {
                            resolvedLoot.Add(resolvedId);
                        }
                    }
                    else
                    {
                        resolvedLoot.Add(lootRef);
                    }
                }
                enemy.LootTableIds = resolvedLoot;
            }

            // Apply pattern-based naming from names.json
            ApplyNameFromPattern(enemy, category);

            return enemy;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting catalog enemy to Enemy");
            return null;
        }
    }

    /// <summary>
    /// Apply pattern-based naming from names.json to populate Prefixes/Suffixes and enhanced Name.
    /// </summary>
    private void ApplyNameFromPattern(Enemy enemy, string category)
    {
        try
        {
            var namesPath = $"enemies/{category}/names.json";
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
            enemy.Prefixes.AddRange(prefixes);
            enemy.Suffixes.AddRange(suffixes);

            // Update the name if we got a better one from the pattern
            if (!string.IsNullOrEmpty(name) && name != enemy.BaseName)
            {
                enemy.Name = name;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error applying name pattern for {Category}", category);
            // Fallback: keep catalog name
        }
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

    /// <summary>
    /// Gets a stat value from v5.1 stats object (formula) or v4.0 direct property (number).
    /// </summary>
    /// <param name="statsObj">v5.1 stats object with formula strings</param>
    /// <param name="statName">Name of the stat in stats object</param>
    /// <param name="catalogEnemy">v4.0 catalog enemy with direct properties</param>
    /// <param name="fallbackProperty">v4.0 property name for fallback</param>
    /// <param name="defaultValue">Default value if neither v5.1 nor v4.0 found</param>
    /// <param name="level">Character level for formula evaluation</param>
    /// <param name="str">Strength attribute</param>
    /// <param name="dex">Dexterity attribute</param>
    /// <param name="con">Constitution attribute</param>
    /// <param name="intel">Intelligence attribute</param>
    /// <param name="wis">Wisdom attribute</param>
    /// <param name="cha">Charisma attribute</param>
    /// <returns>Evaluated stat value</returns>
    private int GetStatValue(JObject? statsObj, string statName, JToken catalogEnemy, string fallbackProperty, 
        int defaultValue, int level, int str, int dex, int con, int intel, int wis, int cha)
    {
        // Try v5.1 stats object first (formula strings)
        if (statsObj?[statName] != null)
        {
            var formula = statsObj[statName]?.Value<string>();
            if (!string.IsNullOrEmpty(formula))
            {
                try
                {
                    return EvaluateStatFormula(formula, level, str, dex, con, intel, wis, cha);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to evaluate formula '{Formula}' for stat '{StatName}'", formula, statName);
                    // Fall through to v4.0 fallback
                }
            }
        }
        
        // Fallback to v4.0 direct property (numeric value)
        return GetIntProperty(catalogEnemy, fallbackProperty, defaultValue);
    }

    /// <summary>
    /// Evaluates a stat formula string like "constitution_mod * 2 + level * 5 + 30".
    /// </summary>
    private int EvaluateStatFormula(string formula, int level, int str, int dex, int con, int intel, int wis, int cha)
    {
        // Calculate ability modifiers
        int str_mod = (str - 10) / 2;
        int dex_mod = (dex - 10) / 2;
        int con_mod = (con - 10) / 2;
        int int_mod = (intel - 10) / 2;
        int wis_mod = (wis - 10) / 2;
        int cha_mod = (cha - 10) / 2;
        
        // Replace variables with actual values
        var evalFormula = formula
            .Replace("strength_mod", str_mod.ToString())
            .Replace("dexterity_mod", dex_mod.ToString())
            .Replace("constitution_mod", con_mod.ToString())
            .Replace("intelligence_mod", int_mod.ToString())
            .Replace("wisdom_mod", wis_mod.ToString())
            .Replace("charisma_mod", cha_mod.ToString())
            .Replace("level", level.ToString())
            .Replace(" ", ""); // Remove spaces for parsing
        
        // Simple expression evaluator (handles +, -, *, /)
        return EvaluateExpression(evalFormula);
    }

    /// <summary>
    /// Evaluates a simple mathematical expression (supports +, -, *, /).
    /// </summary>
    private int EvaluateExpression(string expression)
    {
        try
        {
            // Use DataTable.Compute as a simple expression evaluator
            var result = new System.Data.DataTable().Compute(expression, null);
            return Convert.ToInt32(result);
        }
        catch
        {
            _logger.LogWarning("Failed to evaluate expression '{Expression}'", expression);
            return 10; // Default fallback value
        }
    }


    /// <summary>
    /// Hydrates an enemy by resolving reference IDs to full objects.
    /// Populates Abilities and LootTable properties.
    /// </summary>
    /// <param name="enemy">The enemy to hydrate.</param>
    private async Task HydrateEnemyAsync(Enemy enemy)
    {
        // Resolve abilities
        if (enemy.AbilityIds != null && enemy.AbilityIds.Any())
        {
            var abilities = new List<Ability>();
            foreach (var refId in enemy.AbilityIds)
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
            enemy.Abilities = abilities;
        }

        // Resolve loot table
        if (enemy.LootTableIds != null && enemy.LootTableIds.Any())
        {
            var lootTable = new List<Item>();
            foreach (var refId in enemy.LootTableIds)
            {
                try
                {
                    var itemJson = await _referenceResolver.ResolveToObjectAsync(refId);
                    if (itemJson != null)
                    {
                        var item = itemJson.ToObject<Item>();
                        if (item != null)
                        {
                            lootTable.Add(item);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to resolve loot item '{RefId}'", refId);
                }
            }
            enemy.LootTable = lootTable;
        }
    }
    
    /// <summary>
    /// Get a random weighted item along with its category properties from the catalog.
    /// </summary>
    private (JToken?, JObject?) GetRandomWeightedItemWithProperties(JToken catalog)
    {
        var itemsWithProperties = new List<(JToken item, JObject properties)>();
        
        // Handle hierarchical structure: beast_types -> wolves/bears/etc -> items
        foreach (var property in catalog.Children<JProperty>())
        {
            if (property.Name == "metadata") continue;
            
            // This is a type category (beast_types, undead_types, etc)
            var typeCategory = property.Value;
            if (typeCategory is JObject typeCategoryObj)
            {
                foreach (var subType in typeCategoryObj.Children<JProperty>())
                {
                    if (subType.Name == "metadata") continue;
                    
                    // Get properties object
                    var props = subType.Value["properties"] as JObject;
                    
                    // Get items array
                    var items = subType.Value["items"];
                    if (items != null && items.HasValues)
                    {
                        foreach (var item in items.Children())
                        {
                            itemsWithProperties.Add((item, props ?? new JObject()));
                        }
                    }
                }
            }
        }
        
        if (!itemsWithProperties.Any()) return (null, null);
        
        // Calculate total weight
        var totalWeight = itemsWithProperties.Sum(pair => GetIntProperty(pair.item, "rarityWeight", 1));
        var randomValue = _random.Next(1, totalWeight + 1);
        
        int currentWeight = 0;
        foreach (var (item, props) in itemsWithProperties)
        {
            currentWeight += GetIntProperty(item, "rarityWeight", 1);
            if (randomValue <= currentWeight)
            {
                return (item, props);
            }
        }
        
        return itemsWithProperties.First();
    }
    
    /// <summary>
    /// Find a specific enemy by name in the catalog along with its category properties.
    /// </summary>
    private (JToken?, JObject?) FindEnemyInCatalog(JToken catalog, string enemyName)
    {
        // Handle hierarchical structure
        foreach (var property in catalog.Children<JProperty>())
        {
            if (property.Name == "metadata") continue;
            
            var typeCategory = property.Value;
            if (typeCategory is JObject typeCategoryObj)
            {
                foreach (var subType in typeCategoryObj.Children<JProperty>())
                {
                    if (subType.Name == "metadata") continue;
                    
                    // Get properties object
                    var props = subType.Value["properties"] as JObject;
                    
                    // Search in items
                    var items = subType.Value["items"];
                    if (items != null && items.HasValues)
                    {
                        var enemy = items.Children().FirstOrDefault(e =>
                            string.Equals(GetStringProperty(e, "name"), enemyName, StringComparison.OrdinalIgnoreCase));
                        
                        if (enemy != null)
                        {
                            return (enemy, props ?? new JObject());
                        }
                    }
                }
            }
        }
        
        return (null, null);
    }
    
    /// <summary>
    /// Determines the enemy type based on category name and boss status.
    /// </summary>
    private static EnemyType DetermineEnemyType(string category, bool isBoss)
    {
        if (isBoss) return EnemyType.Boss;
        
        return category.ToLowerInvariant() switch
        {
            string c when c.Contains("beast") || c.Contains("wolf") || c.Contains("wolves") => EnemyType.Beast,
            string c when c.Contains("undead") || c.Contains("skeleton") || c.Contains("zombie") || c.Contains("vampire") => EnemyType.Undead,
            string c when c.Contains("demon") => EnemyType.Demon,
            string c when c.Contains("elemental") => EnemyType.Elemental,
            string c when c.Contains("humanoid") || c.Contains("orc") || c.Contains("goblin") || c.Contains("bandit") => EnemyType.Humanoid,
            string c when c.Contains("dragon") => EnemyType.Dragon,
            _ => EnemyType.Common
        };
    }
    
    /// <summary>
    /// Determines the difficulty based on rarity and boss status.
    /// </summary>
    private static EnemyDifficulty DetermineDifficulty(int rarity, bool isBoss)
    {
        if (isBoss) return EnemyDifficulty.Boss;
        
        return rarity switch
        {
            >= 90 => EnemyDifficulty.Elite,
            >= 60 => EnemyDifficulty.Hard,
            >= 30 => EnemyDifficulty.Normal,
            _ => EnemyDifficulty.Easy
        };
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
}