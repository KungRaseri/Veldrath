using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace RealmEngine.Core.Features.Progression.Services;

/// <summary>
/// Service for loading and accessing the unified power catalog (abilities, spells, talents, etc.).
/// Merges the former <c>AbilityDataService</c> and <c>SpellDataService</c> into a single singleton.
/// </summary>
public class PowerDataService
{
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly IPowerRepository? _repository;
    private readonly ILogger<PowerDataService> _logger;
    private readonly Dictionary<string, Power> _powers = new();
    private readonly Dictionary<MagicalTradition, List<string>> _powersByTradition = new();
    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    // Primary constructor used by DI (Singleton-safe — no scoped dependency captured)
    [ActivatorUtilitiesConstructor]
    /// <summary>Initializes a new instance of <see cref="PowerDataService"/> with a scope factory (DI use).</summary>
    public PowerDataService(IServiceScopeFactory scopeFactory, ILogger<PowerDataService>? logger = null)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? NullLogger<PowerDataService>.Instance;
    }

    // Secondary constructor for direct construction in tests
    /// <summary>Initializes a new instance of <see cref="PowerDataService"/> with a direct repository (test use).</summary>
    public PowerDataService(IPowerRepository repository, ILogger<PowerDataService>? logger = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? NullLogger<PowerDataService>.Instance;
    }

    /// <summary>Initialize by loading all powers from the repository.</summary>
    public async Task InitializeAsync()
    {
        await _initLock.WaitAsync();
        try
        {
            if (_initialized)
            {
                _logger.LogWarning("PowerDataService already initialized");
                return;
            }

            IEnumerable<Power> all;
            if (_scopeFactory is not null)
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IPowerRepository>();
                all = await repo.GetAllAsync();
            }
            else
            {
                all = await _repository!.GetAllAsync();
            }

            foreach (var power in all)
            {
                _powers[power.Id] = power;
                if (power.Tradition.HasValue)
                {
                    if (!_powersByTradition.ContainsKey(power.Tradition.Value))
                        _powersByTradition[power.Tradition.Value] = [];
                    _powersByTradition[power.Tradition.Value].Add(power.Id);
                }
            }

            _initialized = true;
            _logger.LogInformation("PowerDataService initialized with {Count} powers", _powers.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize PowerDataService");
            throw;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>Get a power by ID.</summary>
    public Power? GetPower(string powerId)
    {
        EnsureInitialized();
        return _powers.GetValueOrDefault(powerId);
    }

    /// <summary>Get all powers.</summary>
    public IReadOnlyDictionary<string, Power> GetAllPowers()
    {
        EnsureInitialized();
        return _powers;
    }

    /// <summary>Get powers filtered by acquisition type.</summary>
    public List<Power> GetPowersByType(PowerType type)
    {
        EnsureInitialized();
        return _powers.Values.Where(p => p.Type == type).ToList();
    }

    /// <summary>Get powers filtered by effect type (used by combat AI).</summary>
    public List<Power> GetPowersByEffectType(PowerEffectType effectType)
    {
        EnsureInitialized();
        return _powers.Values.Where(p => p.EffectType == effectType).ToList();
    }

    /// <summary>Get powers filtered by magical tradition.</summary>
    public List<Power> GetPowersByTradition(MagicalTradition tradition)
    {
        EnsureInitialized();
        if (!_powersByTradition.TryGetValue(tradition, out var ids))
            return [];
        return ids.Select(id => _powers[id]).ToList();
    }

    /// <summary>Get powers filtered by rank (0–10, spell rank).</summary>
    public List<Power> GetPowersByRank(int rank)
    {
        EnsureInitialized();
        return _powers.Values.Where(p => p.Rank == rank).ToList();
    }

    /// <summary>Get powers by tier (derived from RarityWeight).</summary>
    public List<Power> GetPowersByTier(int tier)
    {
        EnsureInitialized();
        return _powers.Values.Where(p => CalculateTier(p) == tier).ToList();
    }

    /// <summary>Get starting powers for a class (tier 1 non-spell powers).</summary>
    public List<Power> GetStartingPowers(string className)
    {
        EnsureInitialized();
        return _powers.Values
            .Where(p => p.Type != PowerType.Spell && p.Type != PowerType.Cantrip)
            .Where(p => CalculateTier(p) == 1)
            .Where(p => p.AllowedClasses.Count == 0 ||
                        p.AllowedClasses.Any(c => c.Equals(className, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    /// <summary>Get powers unlockable at a given level.</summary>
    public List<Power> GetUnlockablePowers(string className, int level)
    {
        EnsureInitialized();
        return _powers.Values
            .Where(p => p.RequiredLevel <= level)
            .Where(p => p.AllowedClasses.Count == 0 ||
                        p.AllowedClasses.Any(c => c.Equals(className, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    /// <summary>
    /// Get learnable spells for a character based on their magic tradition skills.
    /// </summary>
    public List<Power> GetLearnablePowers(Character character)
    {
        EnsureInitialized();

        var learnable = new List<Power>();

        foreach (var power in _powers.Values)
        {
            if (!power.Tradition.HasValue) continue;

            var traditionSkillId = GetTraditionSkillId(power.Tradition.Value);
            if (!character.Skills.TryGetValue(traditionSkillId, out var skill))
                continue;

            // Can learn powers up to character's skill rank (with some tolerance)
            if (power.MinimumSkillRank <= skill.CurrentRank + 10)
                learnable.Add(power);
        }

        return learnable;
    }

    /// <summary>Get the skill identifier for a magical tradition.</summary>
    public string GetTraditionSkillId(MagicalTradition tradition) =>
        tradition switch
        {
            MagicalTradition.Arcane => "arcane",
            MagicalTradition.Divine => "divine",
            MagicalTradition.Occult => "occult",
            MagicalTradition.Primal => "primal",
            _ => "arcane"
        };

    /// <summary>Calculate power tier from RarityWeight.</summary>
    public int CalculateTier(Power power)
    {
        if (power.Id.Contains("ultimate/", StringComparison.OrdinalIgnoreCase))
            return 5;

        var weight = power.RarityWeight;
        if (weight < 50) return 1;
        if (weight < 100) return 2;
        if (weight < 200) return 3;
        if (weight < 400) return 4;
        return 5;
    }

    /// <summary>Calculate required level to unlock a power based on tier.</summary>
    public int GetRequiredLevelForTier(int tier) => tier switch
    {
        1 => 1,
        2 => 5,
        3 => 10,
        4 => 15,
        5 => 20,
        _ => 1
    };

    private void EnsureInitialized()
    {
        if (!_initialized)
            throw new InvalidOperationException("PowerDataService not initialized. Call InitializeAsync() first.");
    }
}
