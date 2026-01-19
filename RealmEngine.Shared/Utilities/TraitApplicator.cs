using RealmEngine.Shared.Models;

namespace RealmEngine.Shared.Utilities;

/// <summary>
/// Utility class for applying and querying traits on entities.
/// </summary>
public static class TraitApplicator
{
    /// <summary>
    /// Apply a dictionary of traits to an entity.
    /// </summary>
    public static void ApplyTraits(ITraitable entity, Dictionary<string, TraitValue> traits)
    {
        foreach (var trait in traits)
        {
            entity.Traits[trait.Key] = trait.Value;
        }
    }

    /// <summary>
    /// Apply a single trait to an entity.
    /// </summary>
    public static void ApplyTrait(ITraitable entity, string traitName, object value, TraitType type)
    {
        entity.Traits[traitName] = new TraitValue(value, type);
    }

    /// <summary>
    /// Get a trait value with a default fallback.
    /// </summary>
    public static T GetTrait<T>(ITraitable entity, string traitName, T defaultValue)
    {
        if (!entity.Traits.ContainsKey(traitName))
            return defaultValue;

        var trait = entity.Traits[traitName];

        if (typeof(T) == typeof(int))
            return (T)(object)trait.AsInt();
        if (typeof(T) == typeof(double))
            return (T)(object)trait.AsDouble();
        if (typeof(T) == typeof(string))
            return (T)(object)trait.AsString();
        if (typeof(T) == typeof(bool))
            return (T)(object)trait.AsBool();
        if (typeof(T) == typeof(List<string>))
            return (T)(object)trait.AsStringList();
        if (typeof(T) == typeof(List<int>))
            return (T)(object)trait.AsIntList();

        return defaultValue;
    }

    /// <summary>
    /// Check if an entity has a specific trait.
    /// </summary>
    public static bool HasTrait(ITraitable entity, string traitName)
    {
        return entity.Traits.ContainsKey(traitName);
    }

    /// <summary>
    /// Remove a trait from an entity.
    /// </summary>
    public static void RemoveTrait(ITraitable entity, string traitName)
    {
        entity.Traits.Remove(traitName);
    }

    /// <summary>
    /// Get all trait names for an entity.
    /// </summary>
    public static List<string> GetTraitNames(ITraitable entity)
    {
        return entity.Traits.Keys.ToList();
    }

    /// <summary>
    /// Merge traits from source to target (target traits take precedence).
    /// </summary>
    public static void MergeTraits(ITraitable target, Dictionary<string, TraitValue> sourceTraits)
    {
        foreach (var trait in sourceTraits)
        {
            if (!target.Traits.ContainsKey(trait.Key))
            {
                target.Traits[trait.Key] = trait.Value;
            }
        }
    }

    /// <summary>
    /// Add a numeric bonus to an existing trait (or create it if it doesn't exist).
    /// </summary>
    public static void AddNumericBonus(ITraitable entity, string traitName, double bonus)
    {
        if (entity.Traits.ContainsKey(traitName))
        {
            var currentValue = entity.Traits[traitName].AsDouble();
            entity.Traits[traitName] = new TraitValue(currentValue + bonus, TraitType.Number);
        }
        else
        {
            entity.Traits[traitName] = new TraitValue(bonus, TraitType.Number);
        }
    }

    /// <summary>
    /// Calculate total bonus for a stat from all applicable traits.
    /// Example: Get total strength from strengthBonus, might, titan, etc.
    /// </summary>
    public static int GetTotalStatBonus(ITraitable entity, params string[] traitNames)
    {
        int total = 0;
        foreach (var traitName in traitNames)
        {
            total += GetTrait(entity, traitName, 0);
        }
        return total;
    }

    /// <summary>
    /// Get resistance percentage for a damage type.
    /// </summary>
    public static int GetResistance(ITraitable entity, string resistanceType)
    {
        return GetTrait(entity, resistanceType, 0);
    }

    /// <summary>
    /// Check if entity has any resistance.
    /// </summary>
    public static bool HasResistance(ITraitable entity, string resistanceType)
    {
        return GetResistance(entity, resistanceType) > 0;
    }

    /// <summary>
    /// Get all resistances for an entity.
    /// </summary>
    public static Dictionary<string, int> GetAllResistances(ITraitable entity)
    {
        var resistances = new Dictionary<string, int>();

        var resistanceTraits = new[]
        {
            StandardTraits.ResistFire,
            StandardTraits.ResistIce,
            StandardTraits.ResistLightning,
            StandardTraits.ResistPoison,
            StandardTraits.ResistPhysical,
            StandardTraits.ResistMagic
        };

        foreach (var trait in resistanceTraits)
        {
            var value = GetResistance(entity, trait);
            if (value > 0)
            {
                resistances[trait] = value;
            }
        }

        return resistances;
    }

    /// <summary>
    /// Pretty print all traits for debugging.
    /// </summary>
    public static string DebugTraits(ITraitable entity)
    {
        if (!entity.Traits.Any())
            return "No traits";

        var lines = new List<string>();
        foreach (var trait in entity.Traits)
        {
            var valueStr = trait.Value.Type switch
            {
                TraitType.Number => trait.Value.AsDouble().ToString("F2"),
                TraitType.String => $"\"{trait.Value.AsString()}\"",
                TraitType.Boolean => trait.Value.AsBool().ToString(),
                TraitType.StringArray => $"[{string.Join(", ", trait.Value.AsStringList())}]",
                TraitType.NumberArray => $"[{string.Join(", ", trait.Value.AsIntList())}]",
                _ => trait.Value.ToString() ?? "null"
            };
            lines.Add($"  {trait.Key}: {valueStr}");
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Get traits as a dictionary for Godot UI data binding.
    /// Converts TraitValue objects to primitive types.
    /// </summary>
    /// <param name="entity">The entity</param>
    /// <returns>Dictionary of trait name to value (as object)</returns>
    public static Dictionary<string, object> GetTraitsForUI(ITraitable entity)
    {
        var result = new Dictionary<string, object>();

        foreach (var trait in entity.Traits)
        {
            result[trait.Key] = trait.Value.Type switch
            {
                TraitType.Number => trait.Value.AsDouble(),
                TraitType.String => trait.Value.AsString(),
                TraitType.Boolean => trait.Value.AsBool(),
                TraitType.StringArray => trait.Value.AsStringList(),
                TraitType.NumberArray => trait.Value.AsIntList(),
                _ => trait.Value.ToString() ?? string.Empty
            };
        }

        return result;
    }

    /// <summary>
    /// Filter traits by type.
    /// Useful for displaying only specific categories in UI.
    /// </summary>
    /// <param name="entity">The entity</param>
    /// <param name="type">The trait type to filter by</param>
    /// <returns>Dictionary of filtered traits</returns>
    public static Dictionary<string, TraitValue> FilterTraitsByType(ITraitable entity, TraitType type)
    {
        return entity.Traits
            .Where(t => t.Value.Type == type)
            .ToDictionary(t => t.Key, t => t.Value);
    }

    /// <summary>
    /// Get all numeric traits for stat calculations.
    /// </summary>
    /// <param name="entity">The entity</param>
    /// <returns>Dictionary of numeric trait names to values</returns>
    public static Dictionary<string, double> GetNumericTraits(ITraitable entity)
    {
        return entity.Traits
            .Where(t => t.Value.Type == TraitType.Number)
            .ToDictionary(t => t.Key, t => t.Value.AsDouble());
    }

    /// <summary>
    /// Get all boolean flags for conditional logic.
    /// </summary>
    /// <param name="entity">The entity</param>
    /// <returns>Dictionary of flag names to boolean values</returns>
    public static Dictionary<string, bool> GetBooleanTraits(ITraitable entity)
    {
        return entity.Traits
            .Where(t => t.Value.Type == TraitType.Boolean)
            .ToDictionary(t => t.Key, t => t.Value.AsBool());
    }

    /// <summary>
    /// Compare two entities and get trait differences.
    /// Useful for "before vs after" displays in crafting/upgrading.
    /// </summary>
    /// <param name="entity1">First entity</param>
    /// <param name="entity2">Second entity</param>
    /// <returns>Dictionary of trait changes (positive = increase, negative = decrease)</returns>
    public static Dictionary<string, double> CompareNumericTraits(ITraitable entity1, ITraitable entity2)
    {
        var differences = new Dictionary<string, double>();
        var allTraitNames = entity1.Traits.Keys.Union(entity2.Traits.Keys).ToHashSet();

        foreach (var traitName in allTraitNames)
        {
            var value1 = GetTrait<double>(entity1, traitName, 0.0);
            var value2 = GetTrait<double>(entity2, traitName, 0.0);
            var diff = value2 - value1;

            if (Math.Abs(diff) > 0.001) // Filter near-zero differences
            {
                differences[traitName] = diff;
            }
        }

        return differences;
    }

    /// <summary>
    /// Format trait differences for UI display.
    /// Example: "+5 Attack" or "-3 Defense"
    /// </summary>
    /// <param name="differences">Dictionary from CompareNumericTraits</param>
    /// <returns>List of formatted strings</returns>
    public static List<string> FormatTraitDifferences(Dictionary<string, double> differences)
    {
        return differences
            .OrderByDescending(d => Math.Abs(d.Value))
            .Select(d =>
            {
                var sign = d.Value >= 0 ? "+" : "";
                return $"{sign}{d.Value:F1} {d.Key}";
            })
            .ToList();
    }

    /// <summary>
    /// Get BBCode formatted trait differences for Godot RichTextLabel.
    /// Positive changes in green, negative in red.
    /// </summary>
    /// <param name="differences">Dictionary from CompareNumericTraits</param>
    /// <returns>BBCode formatted string</returns>
    public static string FormatTraitDifferencesBBCode(Dictionary<string, double> differences)
    {
        var lines = differences
            .OrderByDescending(d => Math.Abs(d.Value))
            .Select(d =>
            {
                var color = d.Value >= 0 ? "green" : "red";
                var sign = d.Value >= 0 ? "+" : "";
                return $"[color={color}]{sign}{d.Value:F1} {d.Key}[/color]";
            });

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Search for traits matching a pattern.
    /// Useful for finding all damage-related traits, resist traits, etc.
    /// </summary>
    /// <param name="entity">The entity</param>
    /// <param name="pattern">Search pattern (case-insensitive)</param>
    /// <returns>Dictionary of matching traits</returns>
    public static Dictionary<string, TraitValue> SearchTraits(ITraitable entity, string pattern)
    {
        return entity.Traits
            .Where(t => t.Key.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(t => t.Key, t => t.Value);
    }

    /// <summary>
    /// Get trait count by type.
    /// Useful for statistics and debugging.
    /// </summary>
    /// <param name="entity">The entity</param>
    /// <returns>Dictionary of trait type to count</returns>
    public static Dictionary<TraitType, int> GetTraitStatistics(ITraitable entity)
    {
        return entity.Traits
            .GroupBy(t => t.Value.Type)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    /// <summary>
    /// Clone all traits from source to target.
    /// Creates new TraitValue instances (deep copy).
    /// </summary>
    /// <param name="source">Source entity</param>
    /// <param name="target">Target entity</param>
    public static void CloneTraits(ITraitable source, ITraitable target)
    {
        target.Traits.Clear();
        foreach (var trait in source.Traits)
        {
            target.Traits[trait.Key] = new TraitValue(trait.Value.Value, trait.Value.Type);
        }
    }
}
