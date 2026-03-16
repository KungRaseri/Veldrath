using Microsoft.Extensions.Logging;
using RealmEngine.Data.Entities;

namespace RealmEngine.Core.Generators;

/// <summary>
/// Composes entity names from <see cref="NamePatternSet"/> data loaded from the database.
/// </summary>
public class NameComposer
{
    private readonly ILogger<NameComposer> _logger;
    private readonly Random _random;

    public NameComposer(ILogger<NameComposer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _random = new Random();
    }

    /// <summary>
    /// Picks a weighted-random <see cref="NamePattern"/> from the set and composes a name from it.
    /// Returns <see cref="string.Empty"/> when the set has no patterns.
    /// <paramref name="componentValues"/> is populated with each resolved token → value pair.
    /// </summary>
    public string ComposeName(NamePatternSet patternSet, out Dictionary<string, string> componentValues)
    {
        componentValues = new Dictionary<string, string>();

        var pattern = SelectPattern(patternSet.Patterns);
        if (pattern is null)
        {
            _logger.LogWarning("No patterns in NamePatternSet '{Path}'", patternSet.EntityPath);
            return string.Empty;
        }

        return ComposeFromTemplate(pattern.Template, patternSet.Components, out componentValues);
    }

    /// <summary>
    /// Composes a name from an explicit <paramref name="template"/> (e.g. <c>"{prefix} {base}"</c>)
    /// and the given <paramref name="components"/> collection.
    /// </summary>
    public string ComposeFromTemplate(
        string template,
        IEnumerable<NameComponent> components,
        out Dictionary<string, string> componentValues)
    {
        componentValues = new Dictionary<string, string>();

        if (string.IsNullOrWhiteSpace(template))
            return string.Empty;

        // Group components by their ComponentKey for fast lookup
        var grouped = components
            .GroupBy(c => c.ComponentKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var result = template;
        var tokens = System.Text.RegularExpressions.Regex.Matches(template, @"\{([^}]+)\}");

        foreach (System.Text.RegularExpressions.Match match in tokens)
        {
            var token = match.Groups[1].Value;

            if (grouped.TryGetValue(token, out var pool) && pool.Count > 0)
            {
                var selected = SelectWeightedComponent(pool);
                if (selected is not null)
                {
                    componentValues[token] = selected.Value;
                    result = result.Replace($"{{{token}}}", selected.Value);
                }
            }
            else
            {
                // No components for this token — remove placeholder
                result = result.Replace($"{{{token}}}", string.Empty);
            }
        }

        // Handle bare "base" pattern (no braces)
        if (template == "base" && grouped.TryGetValue("base", out var basePart) && basePart.Count > 0)
        {
            var selected = SelectWeightedComponent(basePart);
            if (selected is not null)
            {
                componentValues["base"] = selected.Value;
                return selected.Value;
            }
        }

        return System.Text.RegularExpressions.Regex.Replace(result.Trim(), @"\s+", " ");
    }

    /// <summary>Selects a weighted-random <see cref="NamePattern"/> from the collection.</summary>
    public NamePattern? SelectPattern(IEnumerable<NamePattern> patterns)
    {
        var list = patterns.ToList();
        if (list.Count == 0) return null;

        var total = list.Sum(p => p.RarityWeight > 0 ? p.RarityWeight : 1);
        var roll = _random.Next(total);
        var cumulative = 0;

        foreach (var p in list)
        {
            cumulative += p.RarityWeight > 0 ? p.RarityWeight : 1;
            if (roll < cumulative) return p;
        }

        return list[^1];
    }

    /// <summary>Selects a weighted-random <see cref="NameComponent"/> from a pool sharing the same key.</summary>
    public NameComponent? SelectWeightedComponent(IEnumerable<NameComponent> pool)
    {
        var list = pool.ToList();
        if (list.Count == 0) return null;

        var total = list.Sum(c => c.RarityWeight > 0 ? c.RarityWeight : 1);
        var roll = _random.Next(total);
        var cumulative = 0;

        foreach (var c in list)
        {
            cumulative += c.RarityWeight > 0 ? c.RarityWeight : 1;
            if (roll < cumulative) return c;
        }

        return list[^1];
    }

    /// <summary>
    /// Composes a name and separates the resolved tokens into prefixes, base name, and suffixes
    /// based on their position relative to the <c>{base}</c> token in the pattern.
    /// </summary>
    public (List<string> prefixes, string baseName, List<string> suffixes) ComposeNameStructured(
        NamePatternSet patternSet,
        out Dictionary<string, string> componentValues)
    {
        componentValues = new Dictionary<string, string>();

        var pattern = SelectPattern(patternSet.Patterns);
        if (pattern is null)
            return ([], string.Empty, []);

        var template = pattern.Template;
        var grouped = patternSet.Components
            .GroupBy(c => c.ComponentKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var tokens = System.Text.RegularExpressions.Regex.Matches(template, @"\{([^}]+)\}");
        var prefixes = new List<string>();
        var suffixes = new List<string>();
        string baseName = string.Empty;
        bool foundBase = false;

        foreach (System.Text.RegularExpressions.Match match in tokens)
        {
            var token = match.Groups[1].Value;

            if (token == "base")
            {
                foundBase = true;
                if (grouped.TryGetValue("base", out var basePool))
                {
                    var selected = SelectWeightedComponent(basePool);
                    if (selected is not null)
                    {
                        baseName = selected.Value;
                        componentValues["base"] = baseName;
                    }
                }
                continue;
            }

            if (!grouped.TryGetValue(token, out var pool) || pool.Count == 0) continue;

            var comp = SelectWeightedComponent(pool);
            if (comp is null) continue;

            componentValues[token] = comp.Value;
            if (!foundBase)
                prefixes.Add(comp.Value);
            else
                suffixes.Add(comp.Value);
        }

        return (prefixes, baseName, suffixes);
    }
}
