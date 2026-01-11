using Bogus;
using Newtonsoft.Json.Linq;
using Serilog;
using System.Text.RegularExpressions;

namespace RealmEngine.Shared.Services;

/// <summary>
/// Executes name generation patterns with support for reference tokens and component substitution.
/// Supports: {component}, @referenceType/context, literal text
/// </summary>
public class PatternExecutor
{
    private static readonly Regex ReferenceTokenRegex = new(@"@(\w+)(?:/(\w+))?", RegexOptions.Compiled);
    private static readonly Regex ComponentTokenRegex = new(@"\{(\w+)\}", RegexOptions.Compiled);

    private readonly DataReferenceResolver _referenceResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="PatternExecutor"/> class.
    /// </summary>
    public PatternExecutor()
    {
        _referenceResolver = DataReferenceResolver.Instance;
    }

    /// <summary>
    /// Execute a pattern template and generate a name.
    /// Pattern syntax:
    /// - {component}: Replace with random value from components["component"]
    /// - @materialRef/weapon: Resolve material reference with weapon context
    /// - @itemRef/swords/Longsword: Resolve specific item reference
    /// - Literal text: "+" or "-" are kept as-is
    /// </summary>
    /// <param name="pattern">Pattern template like "@materialRef/weapon + {base}"</param>
    /// <param name="components">Dictionary of component groups with their values</param>
    /// <param name="faker">Bogus Faker for weighted selection</param>
    /// <param name="itemType">Context for material resolution (weapon, armor, etc.)</param>
    /// <returns>Generated name string</returns>
    public string Execute(string pattern, Dictionary<string, List<ComponentValue>> components, Faker faker, string? itemType = null)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            Log.Warning("Empty pattern provided to PatternExecutor");
            return "Unknown";
        }

        // Split pattern by whitespace and + operators, preserving structure
        var tokens = Regex.Split(pattern, @"(\s+|\+)").Where(t => !string.IsNullOrWhiteSpace(t) && t != "+").ToArray();
        var parts = new List<string>();

        foreach (var token in tokens)
        {
            var trimmed = token.Trim();

            // Check for reference token: @materialRef/weapon
            var refMatch = ReferenceTokenRegex.Match(trimmed);
            if (refMatch.Success)
            {
                var referenceType = refMatch.Groups[1].Value; // materialRef, itemRef, etc.
                var context = refMatch.Groups[2].Success ? refMatch.Groups[2].Value : itemType; // weapon, armor, etc.

                var resolvedName = ResolveReferenceToken(referenceType, context, faker);
                if (resolvedName != null)
                {
                    parts.Add(resolvedName);
                }
                continue;
            }

            // Check for component token: {base}
            var compMatch = ComponentTokenRegex.Match(trimmed);
            if (compMatch.Success)
            {
                var componentKey = compMatch.Groups[1].Value;
                var resolvedValue = ResolveComponentToken(componentKey, components, faker);
                if (resolvedValue != null)
                {
                    parts.Add(resolvedValue);
                }
                continue;
            }

            // Literal text (skip operators like +)
            if (trimmed != "+" && trimmed != "-")
            {
                parts.Add(trimmed);
            }
        }

        return string.Join(" ", parts);
    }

    /// <summary>
    /// Resolve a reference token like @materialRef/weapon to an actual material name.
    /// </summary>
    private string? ResolveReferenceToken(string referenceType, string? context, Faker faker)
    {
        try
        {
            switch (referenceType.ToLowerInvariant())
            {
                case "materialref":
                    return ResolveMaterialReference(context, faker);

                case "itemref":
                    // Legacy pattern reference - use modern @items/category:name syntax instead
                    Log.Warning("@itemRef is deprecated - use @items/category:name reference syntax");
                    return "[Item Reference - Use @items/category:name]"; 

                case "enemyref":
                    // Legacy pattern reference - use modern @enemies/category:name syntax instead
                    Log.Warning("@enemyRef is deprecated - use @enemies/category:name reference syntax");
                    return "[Enemy Reference - Use @enemies/category:name]";

                case "npcref":
                    // Legacy pattern reference - use modern @npcs/category:name syntax instead
                    Log.Warning("@npcRef is deprecated - use @npcs/category:name reference syntax");
                    return "[NPC Reference - Use @npcs/category:name]";

                default:
                    Log.Warning("Unknown reference type: {Type}", referenceType);
                    return null;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to resolve reference token: {Type}/{Context}", referenceType, context);
            return null;
        }
    }

    /// <summary>
    /// Resolve @materialRef/weapon to a material name with weighted selection.
    /// Uses DataReferenceResolver to load materials and apply context-specific filtering.
    /// </summary>
    private string? ResolveMaterialReference(string? context, Faker faker)
    {
        if (string.IsNullOrEmpty(context))
        {
            Log.Warning("No context provided for @materialRef");
            return null;
        }

        try
        {
            // Material properties are now in materials/properties/{category}/catalog.json
            var categories = new[] { "metals", "leathers", "woods", "gemstones" };
            var materials = new List<(string name, int weight)>();

            foreach (var category in categories)
            {
                // Load catalog for this category
                var catalogPath = $"materials/properties/{category}/catalog.json";
                var catalog = _referenceResolver.GetType()
                    .GetMethod("LoadCatalog", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.Invoke(_referenceResolver, [catalogPath]) as JObject;

                if (catalog == null) continue;

                var items = catalog["items"] as JArray;
                if (items == null) continue;

                foreach (var item in items.OfType<JObject>())
                {
                    var name = item["name"]?.ToString();
                    if (string.IsNullOrEmpty(name)) continue;

                    // Check if material supports this context (weapon/armor)
                    var itemTypeTraits = item["itemTypeTraits"] as JObject;
                    if (itemTypeTraits == null || !itemTypeTraits.ContainsKey(context))
                        continue;

                    var rarityWeight = item["rarityWeight"]?.ToObject<int>() ?? 10;
                    materials.Add((name, rarityWeight));
                }
            }

            if (materials.Count == 0)
            {
                Log.Warning("No materials found for context: {Context}", context);
                return null;
            }

            // Weighted selection using Bogus
            var selectedMaterial = faker.Random.WeightedRandom(
                materials.Select(m => m.name).ToArray(),
                materials.Select(m => (float)m.weight).ToArray()
            );

            Log.Debug("Selected material: {Material} for context: {Context}", selectedMaterial, context);
            return selectedMaterial;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to resolve material reference for context: {Context}", context);
            return null;
        }
    }

    /// <summary>
    /// Resolve a component token like {base} to a random value from components["base"].
    /// Uses weighted selection based on rarityWeight.
    /// </summary>
    private string? ResolveComponentToken(string componentKey, Dictionary<string, List<ComponentValue>> components, Faker faker)
    {
        if (!components.TryGetValue(componentKey, out var values) || values.Count == 0)
        {
            Log.Warning("Component not found: {Key}", componentKey);
            return null;
        }

        // Weighted selection
        var selected = faker.Random.WeightedRandom(
            values.Select(v => v.Value).ToArray(),
            values.Select(v => (float)v.Weight).ToArray()
        );

        return selected;
    }
}

/// <summary>
/// Represents a component value with weight for pattern execution.
/// </summary>
public record ComponentValue(string Value, int Weight);
