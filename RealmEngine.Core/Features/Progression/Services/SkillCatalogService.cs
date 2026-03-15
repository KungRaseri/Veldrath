using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace RealmEngine.Core.Features.Progression.Services;

/// <summary>
/// Service for loading and accessing skill definitions from the database.
/// </summary>
public class SkillCatalogService
{
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly ISkillRepository? _repository;
    private readonly ILogger<SkillCatalogService> _logger;
    private readonly Dictionary<string, SkillDefinition> _skillDefinitions = new();
    private bool _initialized;

    // Primary constructor used by DI (Singleton-safe — no scoped dependency captured)
    [ActivatorUtilitiesConstructor]
    public SkillCatalogService(IServiceScopeFactory scopeFactory, ILogger<SkillCatalogService>? logger = null)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? NullLogger<SkillCatalogService>.Instance;
    }

    // Secondary constructor for direct construction in tests
    public SkillCatalogService(ISkillRepository repository, ILogger<SkillCatalogService>? logger = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? NullLogger<SkillCatalogService>.Instance;
    }

    /// <summary>Initialize by loading all skills from the repository.</summary>
    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            _logger.LogWarning("SkillCatalogService already initialized");
            return;
        }

        try
        {
            IEnumerable<SkillDefinition> all;
            if (_scopeFactory is not null)
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<ISkillRepository>();
                all = await repo.GetAllAsync();
            }
            else
            {
                all = await _repository!.GetAllAsync();
            }

            foreach (var skill in all)
                _skillDefinitions[skill.SkillId] = skill;

            _initialized = true;
            _logger.LogInformation("SkillCatalogService initialized with {Count} skills", _skillDefinitions.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize SkillCatalogService");
            throw;
        }
    }

    /// <summary>Get skill definition by ID.</summary>
    public virtual SkillDefinition? GetSkillDefinition(string skillId)
    {
        EnsureInitialized();
        return _skillDefinitions.GetValueOrDefault(skillId);
    }

    /// <summary>Get all skill definitions.</summary>
    public IReadOnlyDictionary<string, SkillDefinition> GetAllSkills()
    {
        EnsureInitialized();
        return _skillDefinitions;
    }

    /// <summary>Get skills by category.</summary>
    public List<SkillDefinition> GetSkillsByCategory(string category)
    {
        EnsureInitialized();
        return [.. _skillDefinitions.Values
            .Where(s => s.Category.Equals(category, StringComparison.OrdinalIgnoreCase))];
    }

    /// <summary>
    /// Calculate XP required to reach next rank.
    /// Formula: baseXPCost + (currentRank * baseXPCost * costMultiplier)
    /// </summary>
    public int CalculateXPToNextRank(string skillId, int currentRank)
    {
        var skillDef = GetSkillDefinition(skillId);
        if (skillDef == null)
        {
            _logger.LogWarning("Unknown skill ID: {SkillId}", skillId);
            return 100;
        }

        if (currentRank >= skillDef.MaxRank)
            return int.MaxValue;

        return skillDef.BaseXPCost + (int)(currentRank * skillDef.BaseXPCost * skillDef.CostMultiplier);
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
            throw new InvalidOperationException("SkillCatalogService not initialized. Call InitializeAsync() first.");
    }
}
