using Newtonsoft.Json.Linq;
using Serilog;

namespace RealmEngine.Data.Services;

/// <summary>
/// Service for loading experience and leveling configuration from configuration/experience.json
/// Provides XP requirements, activity bonuses, and prestige system configuration
/// </summary>
public class ExperienceConfigService
{
    private readonly GameDataCache _dataCache;
    private ExperienceConfig? _config;

    /// <summary>
    /// Initializes a new instance of ExperienceConfigService
    /// </summary>
    /// <param name="dataCache">Game data cache instance</param>
    public ExperienceConfigService(GameDataCache dataCache)
    {
        _dataCache = dataCache ?? throw new ArgumentNullException(nameof(dataCache));
    }

    /// <summary>
    /// Loads the experience configuration
    /// </summary>
    public ExperienceConfig LoadConfig()
    {
        if (_config != null)
            return _config;

        try
        {
            var file = _dataCache.GetFile("configuration/experience.json");
            if (file == null)
            {
                Log.Warning("experience.json not found, using defaults");
                return GetDefaultConfig();
            }

            var json = file.JsonData;

            _config = new ExperienceConfig
            {
                Version = json["metadata"]?["version"]?.Value<string>() ?? "4.0",
                LevelCurve = ParseLevelCurve(json["levelCurve"] as JObject),
                XPSources = ParseXPSources(json["xpSources"] as JObject),
                Bonuses = ParseBonuses(json["bonuses"] as JObject),
                Prestige = ParsePrestige(json["prestige"] as JObject),
                Penalties = ParsePenalties(json["penalties"] as JObject)
            };

            Log.Information("✅ Loaded experience config (version {Version})", _config.Version);
            return _config;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load experience.json, using defaults");
            return GetDefaultConfig();
        }
    }

    /// <summary>
    /// Calculates XP required for a specific level
    /// </summary>
    public int CalculateXPRequirement(int level)
    {
        var config = LoadConfig();
        var curve = config.LevelCurve;

        if (level > curve.MaxLevel)
            level = curve.MaxLevel;

        return (int)(curve.BaseXP * Math.Pow(level, curve.Exponent));
    }

    /// <summary>
    /// Calculates combat XP with level difference and enemy type modifiers
    /// </summary>
    public int CalculateCombatXP(int playerLevel, int enemyLevel, string enemyType)
    {
        var config = LoadConfig();
        var combat = config.XPSources.Combat;

        var baseXP = combat.BaseXP;
        
        // Apply level difference modifier
        var levelDiff = enemyLevel - playerLevel;
        var diffModifier = GetLevelDifferenceModifier(combat.LevelDifferenceModifiers, levelDiff);
        
        // Apply enemy type multiplier
        var typeMultiplier = combat.EnemyTypeMultipliers.TryGetValue(enemyType.ToLowerInvariant(), out var mult) ? mult : 1.0;

        return (int)(baseXP * diffModifier * typeMultiplier);
    }

    /// <summary>
    /// Calculates quest completion XP
    /// </summary>
    public int CalculateQuestXP(string difficulty)
    {
        var config = LoadConfig();
        var quest = config.XPSources.QuestCompletion;

        var diffMultiplier = quest.DifficultyMultipliers.TryGetValue(difficulty.ToLowerInvariant(), out var mult) ? mult : 1.0;
        return (int)(quest.BaseXP * diffMultiplier);
    }

    /// <summary>
    /// Calculates crafting XP with rarity multiplier
    /// </summary>
    public int CalculateCraftingXP(string rarity, bool isFirstTime)
    {
        var config = LoadConfig();
        var crafting = config.XPSources.Crafting;

        var rarityMultiplier = crafting.RarityMultipliers.TryGetValue(rarity.ToLowerInvariant(), out var mult) ? mult : 1.0;
        var baseXP = crafting.BaseXP * rarityMultiplier;
        
        if (isFirstTime)
            baseXP *= crafting.FirstTimeBonus;

        return (int)baseXP;
    }

    /// <summary>
    /// Gets total XP multiplier from active bonuses
    /// </summary>
    public double GetTotalXPMultiplier(bool isRested, int partySize, bool inGuild, bool isEvent)
    {
        var config = LoadConfig();
        var multiplier = 1.0;

        if (isRested && config.Bonuses.RestBonus.Enabled)
            multiplier *= config.Bonuses.RestBonus.BonusMultiplier;

        if (partySize > 1 && config.Bonuses.GroupBonus.Enabled)
        {
            var groupBonus = (partySize - 1) * config.Bonuses.GroupBonus.BonusPerMember;
            groupBonus = Math.Min(groupBonus, config.Bonuses.GroupBonus.MaxBonus);
            multiplier *= (1.0 + groupBonus);
        }

        if (inGuild && config.Bonuses.GuildBonus.Enabled)
            multiplier *= config.Bonuses.GuildBonus.BonusMultiplier;

        if (isEvent && config.Bonuses.EventBonus.Enabled)
            multiplier *= config.Bonuses.EventBonus.BonusMultiplier;

        return multiplier;
    }

    /// <summary>
    /// Clears the cached configuration, forcing reload on next access
    /// </summary>
    public void ClearCache()
    {
        _config = null;
        Log.Debug("Experience config cache cleared");
    }

    #region Private Parsing Methods

    private LevelCurve ParseLevelCurve(JObject? json)
    {
        if (json == null)
            return new LevelCurve { Formula = "exponential", BaseXP = 100, Exponent = 1.5, MaxLevel = 100 };

        return new LevelCurve
        {
            Formula = json["formula"]?.Value<string>() ?? "exponential",
            BaseXP = json["baseXP"]?.Value<int>() ?? 100,
            Exponent = json["exponent"]?.Value<double>() ?? 1.5,
            MaxLevel = json["maxLevel"]?.Value<int>() ?? 100,
            Description = json["description"]?.Value<string>() ?? ""
        };
    }

    private XPSources ParseXPSources(JObject? json)
    {
        if (json == null)
            return GetDefaultXPSources();

        return new XPSources
        {
            Combat = ParseCombatXP(json["combat"] as JObject),
            QuestCompletion = ParseQuestXP(json["questCompletion"] as JObject),
            Exploration = ParseExplorationXP(json["exploration"] as JObject),
            Crafting = ParseCraftingXP(json["crafting"] as JObject),
            Gathering = ParseGatheringXP(json["gathering"] as JObject)
        };
    }

    private CombatXP ParseCombatXP(JObject? json)
    {
        if (json == null)
            return new CombatXP { BaseXP = 50, LevelDifferenceModifiers = new Dictionary<string, double>(), EnemyTypeMultipliers = new Dictionary<string, double>() };

        var levelDiffMods = new Dictionary<string, double>();
        var levelDiffJson = json["levelDifferenceModifiers"] as JObject;
        if (levelDiffJson != null)
        {
            foreach (var prop in levelDiffJson.Properties())
            {
                levelDiffMods[prop.Name] = prop.Value.Value<double>();
            }
        }

        var enemyTypeMults = new Dictionary<string, double>();
        var enemyTypeJson = json["enemyTypeMultipliers"] as JObject;
        if (enemyTypeJson != null)
        {
            foreach (var prop in enemyTypeJson.Properties())
            {
                enemyTypeMults[prop.Name] = prop.Value.Value<double>();
            }
        }

        return new CombatXP
        {
            BaseXP = json["baseXP"]?.Value<int>() ?? 50,
            Description = json["description"]?.Value<string>() ?? "",
            LevelDifferenceModifiers = levelDiffMods,
            EnemyTypeMultipliers = enemyTypeMults
        };
    }

    private QuestXP ParseQuestXP(JObject? json)
    {
        if (json == null)
            return new QuestXP { BaseXP = 500, DifficultyMultipliers = new Dictionary<string, double>() };

        var diffMults = new Dictionary<string, double>();
        var diffJson = json["difficultyMultipliers"] as JObject;
        if (diffJson != null)
        {
            foreach (var prop in diffJson.Properties())
            {
                diffMults[prop.Name] = prop.Value.Value<double>();
            }
        }

        return new QuestXP
        {
            BaseXP = json["baseXP"]?.Value<int>() ?? 500,
            Description = json["description"]?.Value<string>() ?? "",
            DifficultyMultipliers = diffMults
        };
    }

    private ExplorationXP ParseExplorationXP(JObject? json)
    {
        if (json == null)
            return new ExplorationXP { NewLocationXP = 100, DiscoveryXP = 50, LandmarkXP = 200 };

        return new ExplorationXP
        {
            NewLocationXP = json["newLocationXP"]?.Value<int>() ?? 100,
            DiscoveryXP = json["discoveryXP"]?.Value<int>() ?? 50,
            LandmarkXP = json["landmarkXP"]?.Value<int>() ?? 200,
            Description = json["description"]?.Value<string>() ?? ""
        };
    }

    private CraftingXP ParseCraftingXP(JObject? json)
    {
        if (json == null)
            return new CraftingXP { BaseXP = 25, RarityMultipliers = new Dictionary<string, double>(), FirstTimeBonus = 2.0 };

        var rarityMults = new Dictionary<string, double>();
        var rarityJson = json["rarityMultipliers"] as JObject;
        if (rarityJson != null)
        {
            foreach (var prop in rarityJson.Properties())
            {
                rarityMults[prop.Name] = prop.Value.Value<double>();
            }
        }

        return new CraftingXP
        {
            BaseXP = json["baseXP"]?.Value<int>() ?? 25,
            Description = json["description"]?.Value<string>() ?? "",
            RarityMultipliers = rarityMults,
            FirstTimeBonus = json["firstTimeBonus"]?.Value<double>() ?? 2.0
        };
    }

    private GatheringXP ParseGatheringXP(JObject? json)
    {
        if (json == null)
            return new GatheringXP { BaseXP = 10, RarityMultipliers = new Dictionary<string, double>() };

        var rarityMults = new Dictionary<string, double>();
        var rarityJson = json["rarityMultipliers"] as JObject;
        if (rarityJson != null)
        {
            foreach (var prop in rarityJson.Properties())
            {
                rarityMults[prop.Name] = prop.Value.Value<double>();
            }
        }

        return new GatheringXP
        {
            BaseXP = json["baseXP"]?.Value<int>() ?? 10,
            Description = json["description"]?.Value<string>() ?? "",
            RarityMultipliers = rarityMults
        };
    }

    private Bonuses ParseBonuses(JObject? json)
    {
        if (json == null)
            return GetDefaultBonuses();

        return new Bonuses
        {
            RestBonus = ParseRestBonus(json["restBonus"] as JObject),
            GroupBonus = ParseGroupBonus(json["groupBonus"] as JObject),
            GuildBonus = ParseGuildBonus(json["guildBonus"] as JObject),
            EventBonus = ParseEventBonus(json["eventBonus"] as JObject)
        };
    }

    private RestBonus ParseRestBonus(JObject? json)
    {
        if (json == null)
            return new RestBonus { Enabled = true, MaxBonusXP = 150, BonusMultiplier = 2.0 };

        return new RestBonus
        {
            Enabled = json["enabled"]?.Value<bool>() ?? true,
            MaxBonusXP = json["maxBonusXP"]?.Value<int>() ?? 150,
            BonusMultiplier = json["bonusMultiplier"]?.Value<double>() ?? 2.0,
            Description = json["description"]?.Value<string>() ?? ""
        };
    }

    private GroupBonus ParseGroupBonus(JObject? json)
    {
        if (json == null)
            return new GroupBonus { Enabled = true, BonusPerMember = 0.1, MaxBonus = 0.5 };

        return new GroupBonus
        {
            Enabled = json["enabled"]?.Value<bool>() ?? true,
            BonusPerMember = json["bonusPerMember"]?.Value<double>() ?? 0.1,
            MaxBonus = json["maxBonus"]?.Value<double>() ?? 0.5,
            Description = json["description"]?.Value<string>() ?? ""
        };
    }

    private GuildBonus ParseGuildBonus(JObject? json)
    {
        if (json == null)
            return new GuildBonus { Enabled = true, BonusMultiplier = 1.15 };

        return new GuildBonus
        {
            Enabled = json["enabled"]?.Value<bool>() ?? true,
            BonusMultiplier = json["bonusMultiplier"]?.Value<double>() ?? 1.15,
            Description = json["description"]?.Value<string>() ?? ""
        };
    }

    private EventBonus ParseEventBonus(JObject? json)
    {
        if (json == null)
            return new EventBonus { Enabled = true, BonusMultiplier = 2.0 };

        return new EventBonus
        {
            Enabled = json["enabled"]?.Value<bool>() ?? true,
            BonusMultiplier = json["bonusMultiplier"]?.Value<double>() ?? 2.0,
            Description = json["description"]?.Value<string>() ?? ""
        };
    }

    private Prestige ParsePrestige(JObject? json)
    {
        if (json == null)
            return new Prestige { Enabled = true, MinimumLevel = 100, ResetLevel = 1, PermanentBonusPerPrestige = 0.05, MaxPrestige = 10, PrestigeRewards = new Dictionary<int, PrestigeReward>() };

        var rewards = new Dictionary<int, PrestigeReward>();
        var rewardsJson = json["prestigeRewards"] as JObject;
        if (rewardsJson != null)
        {
            foreach (var prop in rewardsJson.Properties())
            {
                if (int.TryParse(prop.Name, out var level))
                {
                    var rewardJson = prop.Value as JObject;
                    if (rewardJson != null)
                    {
                        rewards[level] = new PrestigeReward
                        {
                            Title = rewardJson["title"]?.Value<string>() ?? "",
                            StatBonus = rewardJson["statBonus"]?.Value<int>() ?? 0,
                            Description = rewardJson["description"]?.Value<string>() ?? ""
                        };
                    }
                }
            }
        }

        return new Prestige
        {
            Enabled = json["enabled"]?.Value<bool>() ?? true,
            MinimumLevel = json["minimumLevel"]?.Value<int>() ?? 100,
            ResetLevel = json["resetLevel"]?.Value<int>() ?? 1,
            PermanentBonusPerPrestige = json["permanentBonusPerPrestige"]?.Value<double>() ?? 0.05,
            MaxPrestige = json["maxPrestige"]?.Value<int>() ?? 10,
            PrestigeRewards = rewards
        };
    }

    private Penalties ParsePenalties(JObject? json)
    {
        if (json == null)
            return new Penalties { Death = new DeathPenalty { XPLoss = 0.05, CanLoseLevel = false } };

        var deathJson = json["death"] as JObject;
        return new Penalties
        {
            Death = new DeathPenalty
            {
                XPLoss = deathJson?["xpLoss"]?.Value<double>() ?? 0.05,
                Description = deathJson?["description"]?.Value<string>() ?? "",
                CanLoseLevel = deathJson?["canLoseLevel"]?.Value<bool>() ?? false
            }
        };
    }

    private double GetLevelDifferenceModifier(Dictionary<string, double> modifiers, int levelDiff)
    {
        // Try exact match
        var key = levelDiff.ToString();
        if (modifiers.TryGetValue(key, out var modifier))
            return modifier;

        // Check for range keys like "6+"
        if (levelDiff >= 6 && modifiers.TryGetValue("6+", out var rangeModifier))
            return rangeModifier;

        // Default to 1.0 if no match
        return 1.0;
    }

    #endregion

    #region Default Configuration

    private ExperienceConfig GetDefaultConfig()
    {
        return new ExperienceConfig
        {
            Version = "4.0",
            LevelCurve = new LevelCurve { Formula = "exponential", BaseXP = 100, Exponent = 1.5, MaxLevel = 100 },
            XPSources = GetDefaultXPSources(),
            Bonuses = GetDefaultBonuses(),
            Prestige = new Prestige { Enabled = true, MinimumLevel = 100, ResetLevel = 1, PermanentBonusPerPrestige = 0.05, MaxPrestige = 10, PrestigeRewards = new Dictionary<int, PrestigeReward>() },
            Penalties = new Penalties { Death = new DeathPenalty { XPLoss = 0.05, CanLoseLevel = false } }
        };
    }

    private XPSources GetDefaultXPSources()
    {
        return new XPSources
        {
            Combat = new CombatXP { BaseXP = 50, LevelDifferenceModifiers = new Dictionary<string, double> { ["0"] = 1.0 }, EnemyTypeMultipliers = new Dictionary<string, double> { ["normal"] = 1.0 } },
            QuestCompletion = new QuestXP { BaseXP = 500, DifficultyMultipliers = new Dictionary<string, double> { ["normal"] = 1.0 } },
            Exploration = new ExplorationXP { NewLocationXP = 100, DiscoveryXP = 50, LandmarkXP = 200 },
            Crafting = new CraftingXP { BaseXP = 25, RarityMultipliers = new Dictionary<string, double> { ["common"] = 1.0 }, FirstTimeBonus = 2.0 },
            Gathering = new GatheringXP { BaseXP = 10, RarityMultipliers = new Dictionary<string, double> { ["common"] = 1.0 } }
        };
    }

    private Bonuses GetDefaultBonuses()
    {
        return new Bonuses
        {
            RestBonus = new RestBonus { Enabled = true, MaxBonusXP = 150, BonusMultiplier = 2.0 },
            GroupBonus = new GroupBonus { Enabled = true, BonusPerMember = 0.1, MaxBonus = 0.5 },
            GuildBonus = new GuildBonus { Enabled = true, BonusMultiplier = 1.15 },
            EventBonus = new EventBonus { Enabled = true, BonusMultiplier = 2.0 }
        };
    }

    #endregion
}

#region Configuration Model Classes

/// <summary>
/// Complete experience configuration
/// </summary>
public class ExperienceConfig
{
    /// <summary>Configuration version</summary>
    public required string Version { get; set; }
    
    /// <summary>Level curve configuration</summary>
    public required LevelCurve LevelCurve { get; set; }
    
    /// <summary>XP sources configuration</summary>
    public required XPSources XPSources { get; set; }
    
    /// <summary>XP bonuses configuration</summary>
    public required Bonuses Bonuses { get; set; }
    
    /// <summary>Prestige system configuration</summary>
    public required Prestige Prestige { get; set; }
    
    /// <summary>Penalties configuration</summary>
    public required Penalties Penalties { get; set; }
}

/// <summary>Level curve configuration</summary>
public class LevelCurve
{
    /// <summary>Formula type</summary>
    public required string Formula { get; set; }
    
    /// <summary>Base XP amount</summary>
    public required int BaseXP { get; set; }
    
    /// <summary>Exponent for curve calculation</summary>
    public required double Exponent { get; set; }
    
    /// <summary>Maximum level</summary>
    public required int MaxLevel { get; set; }
    
    /// <summary>Description</summary>
    public string? Description { get; set; }
}

/// <summary>XP sources configuration</summary>
public class XPSources
{
    /// <summary>Combat XP configuration</summary>
    public required CombatXP Combat { get; set; }
    
    /// <summary>Quest XP configuration</summary>
    public required QuestXP QuestCompletion { get; set; }
    
    /// <summary>Exploration XP configuration</summary>
    public required ExplorationXP Exploration { get; set; }
    
    /// <summary>Crafting XP configuration</summary>
    public required CraftingXP Crafting { get; set; }
    
    /// <summary>Gathering XP configuration</summary>
    public required GatheringXP Gathering { get; set; }
}

/// <summary>Combat XP configuration</summary>
public class CombatXP
{
    /// <summary>Base XP amount</summary>
    public required int BaseXP { get; set; }
    
    /// <summary>Description</summary>
    public string? Description { get; set; }
    
    /// <summary>Level difference modifiers</summary>
    public required Dictionary<string, double> LevelDifferenceModifiers { get; set; }
    
    /// <summary>Enemy type multipliers</summary>
    public required Dictionary<string, double> EnemyTypeMultipliers { get; set; }
}

/// <summary>Quest XP configuration</summary>
public class QuestXP
{
    /// <summary>Base XP amount</summary>
    public required int BaseXP { get; set; }
    
    /// <summary>Description</summary>
    public string? Description { get; set; }
    
    /// <summary>Difficulty multipliers</summary>
    public required Dictionary<string, double> DifficultyMultipliers { get; set; }
}

/// <summary>Exploration XP configuration</summary>
public class ExplorationXP
{
    /// <summary>XP for new location</summary>
    public required int NewLocationXP { get; set; }
    
    /// <summary>XP for discovery</summary>
    public required int DiscoveryXP { get; set; }
    
    /// <summary>XP for landmark</summary>
    public required int LandmarkXP { get; set; }
    
    /// <summary>Description</summary>
    public string? Description { get; set; }
}

/// <summary>Crafting XP configuration</summary>
public class CraftingXP
{
    /// <summary>Base XP amount</summary>
    public required int BaseXP { get; set; }
    
    /// <summary>Description</summary>
    public string? Description { get; set; }
    
    /// <summary>Rarity multipliers</summary>
    public required Dictionary<string, double> RarityMultipliers { get; set; }
    
    /// <summary>First time crafting bonus</summary>
    public required double FirstTimeBonus { get; set; }
}

/// <summary>Gathering XP configuration</summary>
public class GatheringXP
{
    /// <summary>Base XP amount</summary>
    public required int BaseXP { get; set; }
    
    /// <summary>Description</summary>
    public string? Description { get; set; }
    
    /// <summary>Rarity multipliers</summary>
    public required Dictionary<string, double> RarityMultipliers { get; set; }
}

/// <summary>XP bonuses configuration</summary>
public class Bonuses
{
    /// <summary>Rest bonus configuration</summary>
    public required RestBonus RestBonus { get; set; }
    
    /// <summary>Group bonus configuration</summary>
    public required GroupBonus GroupBonus { get; set; }
    
    /// <summary>Guild bonus configuration</summary>
    public required GuildBonus GuildBonus { get; set; }
    
    /// <summary>Event bonus configuration</summary>
    public required EventBonus EventBonus { get; set; }
}

/// <summary>Rest bonus configuration</summary>
public class RestBonus
{
    /// <summary>Whether enabled</summary>
    public required bool Enabled { get; set; }
    
    /// <summary>Maximum bonus XP</summary>
    public required int MaxBonusXP { get; set; }
    
    /// <summary>Bonus multiplier</summary>
    public required double BonusMultiplier { get; set; }
    
    /// <summary>Description</summary>
    public string? Description { get; set; }
}

/// <summary>Group bonus configuration</summary>
public class GroupBonus
{
    /// <summary>Whether enabled</summary>
    public required bool Enabled { get; set; }
    
    /// <summary>Bonus per party member</summary>
    public required double BonusPerMember { get; set; }
    
    /// <summary>Maximum bonus</summary>
    public required double MaxBonus { get; set; }
    
    /// <summary>Description</summary>
    public string? Description { get; set; }
}

/// <summary>Guild bonus configuration</summary>
public class GuildBonus
{
    /// <summary>Whether enabled</summary>
    public required bool Enabled { get; set; }
    
    /// <summary>Bonus multiplier</summary>
    public required double BonusMultiplier { get; set; }
    
    /// <summary>Description</summary>
    public string? Description { get; set; }
}

/// <summary>Event bonus configuration</summary>
public class EventBonus
{
    /// <summary>Whether enabled</summary>
    public required bool Enabled { get; set; }
    
    /// <summary>Bonus multiplier</summary>
    public required double BonusMultiplier { get; set; }
    
    /// <summary>Description</summary>
    public string? Description { get; set; }
}

/// <summary>Prestige system configuration</summary>
public class Prestige
{
    /// <summary>Whether enabled</summary>
    public required bool Enabled { get; set; }
    
    /// <summary>Minimum level for prestige</summary>
    public required int MinimumLevel { get; set; }
    
    /// <summary>Level reset value</summary>
    public required int ResetLevel { get; set; }
    
    /// <summary>Permanent bonus per prestige level</summary>
    public required double PermanentBonusPerPrestige { get; set; }
    
    /// <summary>Maximum prestige level</summary>
    public required int MaxPrestige { get; set; }
    
    /// <summary>Prestige rewards by level</summary>
    public required Dictionary<int, PrestigeReward> PrestigeRewards { get; set; }
}

/// <summary>Prestige reward</summary>
public class PrestigeReward
{
    /// <summary>Title granted</summary>
    public required string Title { get; set; }
    
    /// <summary>Stat bonus percentage</summary>
    public required int StatBonus { get; set; }
    
    /// <summary>Description</summary>
    public required string Description { get; set; }
}

/// <summary>Penalties configuration</summary>
public class Penalties
{
    /// <summary>Death penalty configuration</summary>
    public required DeathPenalty Death { get; set; }
}

/// <summary>Death penalty configuration</summary>
public class DeathPenalty
{
    /// <summary>XP loss percentage</summary>
    public required double XPLoss { get; set; }
    
    /// <summary>Whether can lose level</summary>
    public required bool CanLoseLevel { get; set; }
    
    /// <summary>Description</summary>
    public string? Description { get; set; }
}

#endregion
