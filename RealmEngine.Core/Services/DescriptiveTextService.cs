using Newtonsoft.Json.Linq;
using RealmEngine.Data.Services;
using Serilog;

namespace RealmEngine.Core.Services;

/// <summary>
/// Service for generating descriptive text using procedural word lists.
/// Provides atmospheric descriptions for items, locations, NPCs, and combat.
/// </summary>
public class DescriptiveTextService
{
    private readonly GameDataCache _dataCache;
    private readonly Random _random = new();
    private Dictionary<string, List<string>>? _adjectives;
    private Dictionary<string, List<string>>? _colors;
    private List<string>? _smells;
    private List<string>? _sounds;
    private List<string>? _textures;
    private List<string>? _verbs;
    private Dictionary<string, List<string>>? _weather;
    private Dictionary<string, List<string>>? _timeOfDay;

    /// <summary>
    /// Initializes a new instance of the DescriptiveTextService class.
    /// </summary>
    /// <param name="dataCache">The game data cache service.</param>
    public DescriptiveTextService(GameDataCache dataCache)
    {
        _dataCache = dataCache;
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

    private void EnsureAdjectivesLoaded()
    {
        if (_adjectives != null) return;
        
        var file = _dataCache.GetFile("general/adjectives.json");
        if (file == null)
        {
            Log.Warning("Adjectives file not found");
            _adjectives = new Dictionary<string, List<string>>();
            return;
        }

        _adjectives = new Dictionary<string, List<string>>();
        var components = file.JsonData["components"] as JObject;
        
        if (components != null)
        {
            foreach (var prop in components.Properties())
            {
                var list = prop.Value.ToObject<List<string>>();
                if (list != null)
                {
                    _adjectives[prop.Name] = list;
                }
            }
        }
    }

    private void EnsureColorsLoaded()
    {
        if (_colors != null) return;
        
        var file = _dataCache.GetFile("general/colors.json");
        if (file == null)
        {
            Log.Warning("Colors file not found");
            _colors = new Dictionary<string, List<string>>();
            return;
        }

        _colors = new Dictionary<string, List<string>>();
        var components = file.JsonData["components"] as JObject;
        
        if (components != null)
        {
            foreach (var prop in components.Properties())
            {
                var list = prop.Value.ToObject<List<string>>();
                if (list != null)
                {
                    _colors[prop.Name] = list;
                }
            }
        }
    }

    private void EnsureSmellsLoaded()
    {
        if (_smells != null) return;
        
        var file = _dataCache.GetFile("general/smells.json");
        if (file == null)
        {
            _smells = new List<string>();
            return;
        }

        _smells = file.JsonData["smells"]?.ToObject<List<string>>() ?? new List<string>();
    }

    private void EnsureSoundsLoaded()
    {
        if (_sounds != null) return;
        
        var file = _dataCache.GetFile("general/sounds.json");
        if (file == null)
        {
            _sounds = new List<string>();
            return;
        }

        _sounds = file.JsonData["sounds"]?.ToObject<List<string>>() ?? new List<string>();
    }

    private void EnsureTexturesLoaded()
    {
        if (_textures != null) return;
        
        var file = _dataCache.GetFile("general/textures.json");
        if (file == null)
        {
            _textures = new List<string>();
            return;
        }

        _textures = file.JsonData["textures"]?.ToObject<List<string>>() ?? new List<string>();
    }

    private void EnsureVerbsLoaded()
    {
        if (_verbs != null) return;
        
        var file = _dataCache.GetFile("general/verbs.json");
        if (file == null)
        {
            _verbs = new List<string>();
            return;
        }

        _verbs = file.JsonData["verbs"]?.ToObject<List<string>>() ?? new List<string>();
    }

    private void EnsureWeatherLoaded()
    {
        if (_weather != null) return;
        
        var file = _dataCache.GetFile("general/weather.json");
        if (file == null)
        {
            _weather = new Dictionary<string, List<string>>();
            return;
        }

        _weather = new Dictionary<string, List<string>>();
        var conditions = file.JsonData["weather_conditions"] as JObject;
        
        if (conditions != null)
        {
            foreach (var prop in conditions.Properties())
            {
                var list = prop.Value.ToObject<List<string>>();
                if (list != null)
                {
                    _weather[prop.Name] = list;
                }
            }
        }
    }

    private void EnsureTimeOfDayLoaded()
    {
        if (_timeOfDay != null) return;
        
        var file = _dataCache.GetFile("general/time_of_day.json");
        if (file == null)
        {
            _timeOfDay = new Dictionary<string, List<string>>();
            return;
        }

        _timeOfDay = new Dictionary<string, List<string>>();
        var periods = file.JsonData["time_periods"] as JObject;
        
        if (periods != null)
        {
            foreach (var prop in periods.Properties())
            {
                var list = prop.Value.ToObject<List<string>>();
                if (list != null)
                {
                    _timeOfDay[prop.Name] = list;
                }
            }
        }
    }
}
