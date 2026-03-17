using System.Text.Json;
using System.Text.Json.Serialization;
using RealmEngine.Data.Services;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Services;

/// <summary>
/// Service for generating descriptive text using procedural word lists.
/// </summary>
public class DescriptiveTextService
{
    private readonly GameConfigService _configService;
    private readonly Random _random = new();
    private Dictionary<string, List<string>>? _adjectives;
    private Dictionary<string, List<string>>? _colors;
    private List<string>? _smells;
    private List<string>? _sounds;
    private List<string>? _textures;
    private List<string>? _verbs;
    private Dictionary<string, List<string>>? _weather;
    private Dictionary<string, List<string>>? _timeOfDay;

    public DescriptiveTextService(GameConfigService configService)
    {
        _configService = configService;
    }

    /// <summary>
    /// Get a random adjective from a specific category.
    /// </summary>
    /// <param name="category">Category: positive, negative, size, appearance, condition</param>
    public string GetAdjective(string category = "positive")
    {
        EnsureAdjectivesLoaded();
        
        if (_adjectives != null && _adjectives.TryGetValue(category, out var list) && list.Count > 0)
        {
            return list[_random.Next(list.Count)];
        }
        
        return "strange";
    }

    /// <summary>
    /// Get a random color with optional modifier.
    /// </summary>
    /// <param name="includeModifier">Whether to include modifier like "dark" or "bright"</param>
    public string GetColor(bool includeModifier = false)
    {
        EnsureColorsLoaded();
        
        if (_colors == null || !_colors.TryGetValue("base_color", out var baseColors))
        {
            return "grey";
        }

        var color = baseColors[_random.Next(baseColors.Count)];
        
        if (includeModifier && _colors.TryGetValue("modifier", out var modifiers))
        {
            var modifier = modifiers[_random.Next(modifiers.Count)];
            return $"{modifier} {color}";
        }
        
        return color;
    }

    /// <summary>
    /// Get a random smell description.
    /// </summary>
    public string GetSmell()
    {
        EnsureSmellsLoaded();
        
        if (_smells != null && _smells.Count > 0)
        {
            return _smells[_random.Next(_smells.Count)];
        }
        
        return "musty air";
    }

    /// <summary>
    /// Get a random sound description.
    /// </summary>
    public string GetSound()
    {
        EnsureSoundsLoaded();
        
        if (_sounds != null && _sounds.Count > 0)
        {
            return _sounds[_random.Next(_sounds.Count)];
        }
        
        return "distant echoes";
    }

    /// <summary>
    /// Get a random texture description.
    /// </summary>
    public string GetTexture()
    {
        EnsureTexturesLoaded();
        
        if (_textures != null && _textures.Count > 0)
        {
            return _textures[_random.Next(_textures.Count)];
        }
        
        return "smooth";
    }

    /// <summary>
    /// Get a random action verb.
    /// </summary>
    public string GetVerb()
    {
        EnsureVerbsLoaded();
        
        if (_verbs != null && _verbs.Count > 0)
        {
            return _verbs[_random.Next(_verbs.Count)];
        }
        
        return "strikes";
    }

    /// <summary>
    /// Get weather description for a category.
    /// </summary>
    /// <param name="category">Category: clear, rain, storm, snow, fog, wind</param>
    public string GetWeather(string category = "clear")
    {
        EnsureWeatherLoaded();
        
        if (_weather != null && _weather.TryGetValue(category, out var list) && list.Count > 0)
        {
            return list[_random.Next(list.Count)];
        }
        
        return "pleasant weather";
    }

    /// <summary>
    /// Get time of day description.
    /// </summary>
    /// <param name="period">Period: dawn, morning, noon, afternoon, dusk, evening, night, midnight</param>
    public string GetTimeOfDay(string period = "noon")
    {
        EnsureTimeOfDayLoaded();
        
        if (_timeOfDay != null && _timeOfDay.TryGetValue(period, out var list) && list.Count > 0)
        {
            return list[_random.Next(list.Count)];
        }
        
        return "midday sun";
    }

    /// <summary>
    /// Generate atmospheric description combining multiple elements.
    /// </summary>
    public string GenerateAtmosphere(bool includeWeather = true, bool includeTime = true, bool includeSound = true, bool includeSmell = true)
    {
        var parts = new List<string>();
        
        if (includeTime)
        {
            var periods = new[] { "dawn", "morning", "noon", "afternoon", "dusk", "evening", "night" };
            var period = periods[_random.Next(periods.Length)];
            parts.Add(GetTimeOfDay(period));
        }
        
        if (includeWeather)
        {
            var categories = new[] { "clear", "rain", "fog", "wind" };
            var category = categories[_random.Next(categories.Length)];
            parts.Add(GetWeather(category));
        }
        
        if (includeSound)
        {
            parts.Add($"You hear {GetSound()}");
        }
        
        if (includeSmell)
        {
            parts.Add($"The air smells of {GetSmell()}");
        }
        
        return string.Join(". ", parts) + ".";
    }

    /// <summary>
    /// Generate item description with adjectives and colors.
    /// </summary>
    public string GenerateItemDescription(string itemName, bool includeCondition = true, bool includeTexture = true)
    {
        var parts = new List<string>();
        
        if (includeCondition)
        {
            parts.Add(GetAdjective("condition"));
        }
        
        parts.Add(GetColor());
        parts.Add(itemName);
        
        if (includeTexture)
        {
            return $"A {string.Join(" ", parts)} with a {GetTexture()} finish";
        }
        
        return $"A {string.Join(" ", parts)}";
    }

    private DescriptiveTextData? _data;

    private DescriptiveTextData LoadData()
    {
        if (_data != null) return _data;
        var raw = _configService.GetData("descriptive-text");
        _data = raw != null
            ? JsonSerializer.Deserialize<DescriptiveTextData>(raw, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            : new DescriptiveTextData(null, null, null, null, null, null, null, null);
        return _data!;
    }

    private void EnsureAdjectivesLoaded()
    {
        if (_adjectives != null) return;
        _adjectives = LoadData().Components ?? [];
    }

    private void EnsureColorsLoaded()
    {
        if (_colors != null) return;
        var data = LoadData();
        _colors = data.Colors ?? data.Components ?? [];
    }

    private void EnsureSmellsLoaded()
    {
        if (_smells != null) return;
        _smells = LoadData().Smells ?? [];
    }

    private void EnsureSoundsLoaded()
    {
        if (_sounds != null) return;
        _sounds = LoadData().Sounds ?? [];
    }

    private void EnsureTexturesLoaded()
    {
        if (_textures != null) return;
        _textures = LoadData().Textures ?? [];
    }

    private void EnsureVerbsLoaded()
    {
        if (_verbs != null) return;
        _verbs = LoadData().Verbs ?? [];
    }

    private void EnsureWeatherLoaded()
    {
        if (_weather != null) return;
        _weather = LoadData().WeatherConditions ?? [];
    }

    private void EnsureTimeOfDayLoaded()
    {
        if (_timeOfDay != null) return;
        _timeOfDay = LoadData().TimePeriods ?? [];
    }

    private sealed record DescriptiveTextData(
        [property: JsonPropertyName("components")] Dictionary<string, List<string>>? Components,
        [property: JsonPropertyName("colors")] Dictionary<string, List<string>>? Colors,
        [property: JsonPropertyName("smells")] List<string>? Smells,
        [property: JsonPropertyName("sounds")] List<string>? Sounds,
        [property: JsonPropertyName("textures")] List<string>? Textures,
        [property: JsonPropertyName("verbs")] List<string>? Verbs,
        [property: JsonPropertyName("weather_conditions")] Dictionary<string, List<string>>? WeatherConditions,
        [property: JsonPropertyName("time_periods")] Dictionary<string, List<string>>? TimePeriods);
}
