using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace RealmEngine.Core.Features.Progression.Services;

/// <summary>
/// Service for loading and accessing ability definitions from the database.
/// </summary>
public class AbilityCatalogService
{
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly IAbilityRepository? _repository;
    private readonly ILogger<AbilityCatalogService> _logger;
    private readonly Dictionary<string, Ability> _abilities = new();
    private bool _initialized;

    // Primary constructor used by DI (Singleton-safe — no scoped dependency captured)
    [ActivatorUtilitiesConstructor]
    public AbilityCatalogService(IServiceScopeFactory scopeFactory, ILogger<AbilityCatalogService>? logger = null)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? NullLogger<AbilityCatalogService>.Instance;
    }

    // Secondary constructor for direct construction in tests
    public AbilityCatalogService(IAbilityRepository repository, ILogger<AbilityCatalogService>? logger = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? NullLogger<AbilityCatalogService>.Instance;
    }

    /// <summary>Initialize by loading all abilities from the repository.</summary>
    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            _logger.LogWarning("AbilityCatalogService already initialized");
            return;
        }

        try
        {
            IEnumerable<Ability> all;
            if (_scopeFactory is not null)
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IAbilityRepository>();
                all = await repo.GetAllAsync();
            }
            else
            {
                all = await _repository!.GetAllAsync();
            }

            foreach (var ability in all)
                _abilities[ability.Id] = ability;

            _initialized = true;
            _logger.LogInformation("AbilityCatalogService initialized with {Count} abilities", _abilities.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize AbilityCatalogService");
            throw;
        }
    }

    /// <summary>Get ability by ID.</summary>
    public Ability? GetAbility(string abilityId)
    {
        EnsureInitialized();
        return _abilities.GetValueOrDefault(abilityId);
    }

    /// <summary>Get all abilities.</summary>
    public IReadOnlyDictionary<string, Ability> GetAllAbilities()
    {
        EnsureInitialized();
        return _abilities;
    }

    /// <summary>Get abilities by activation type.</summary>
    public List<Ability> GetAbilitiesByType(AbilityTypeEnum type)
    {
        EnsureInitialized();
        return _abilities.Values.Where(a => a.Type == type).ToList();
    }

    /// <summary>Get abilities by tier (based on RarityWeight).</summary>
    public List<Ability> GetAbilitiesByTier(int tier)
    {
        EnsureInitialized();
        return _abilities.Values.Where(a => CalculateTier(a) == tier).ToList();
    }

    /// <summary>Get starting abilities for a class (tier 1 abilities).</summary>
    public List<Ability> GetStartingAbilities(string className)
    {
        EnsureInitialized();
        return _abilities.Values
            .Where(a => CalculateTier(a) == 1)
            .Where(a => a.AllowedClasses.Count == 0 ||
                        a.AllowedClasses.Any(c => c.Equals(className, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    /// <summary>Get abilities unlockable at a given level.</summary>
    public List<Ability> GetUnlockableAbilities(string className, int level)
    {
        EnsureInitialized();
        return _abilities.Values
            .Where(a => a.RequiredLevel <= level)
            .Where(a => a.AllowedClasses.Count == 0 ||
                        a.AllowedClasses.Any(c => c.Equals(className, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    /// <summary>Calculate ability tier from RarityWeight.</summary>
    public int CalculateTier(Ability ability)
    {
        if (ability.Id.Contains("ultimate/", StringComparison.OrdinalIgnoreCase))
            return 5;

        var weight = ability.RarityWeight;
        if (weight < 50) return 1;
        if (weight < 100) return 2;
        if (weight < 200) return 3;
        if (weight < 400) return 4;
        return 5;
    }

    /// <summary>Calculate required level to unlock an ability based on tier.</summary>
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
            throw new InvalidOperationException("AbilityCatalogService not initialized. Call InitializeAsync() first.");
    }
}

