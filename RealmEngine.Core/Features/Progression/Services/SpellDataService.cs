using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace RealmEngine.Core.Features.Progression.Services;

/// <summary>
/// Service for loading and accessing spell definitions from the database.
/// Provides spell metadata organized by magical tradition (Arcane, Divine, Occult, Primal).
/// </summary>
public class SpellDataService
{
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly ISpellRepository? _repository;
    private readonly ILogger<SpellDataService> _logger;
    private readonly Dictionary<string, Spell> _spells = new();
    private readonly Dictionary<MagicalTradition, List<string>> _spellsByTradition = new();
    private bool _initialized;

    // Primary constructor used by DI (Singleton-safe — no scoped dependency captured)
    [ActivatorUtilitiesConstructor]
    public SpellDataService(IServiceScopeFactory scopeFactory, ILogger<SpellDataService>? logger = null)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? NullLogger<SpellDataService>.Instance;
    }

    // Secondary constructor for direct construction in tests
    public SpellDataService(ISpellRepository repository, ILogger<SpellDataService>? logger = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? NullLogger<SpellDataService>.Instance;
    }

    /// <summary>Initialize by loading all spells from the repository.</summary>
    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            _logger.LogWarning("SpellCatalogService already initialized");
            return;
        }

        try
        {
            IEnumerable<Spell> all;
            if (_scopeFactory is not null)
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<ISpellRepository>();
                all = await repo.GetAllAsync();
            }
            else
            {
                all = await _repository!.GetAllAsync();
            }

            foreach (var spell in all)
            {
                _spells[spell.SpellId] = spell;
                if (!_spellsByTradition.ContainsKey(spell.Tradition))
                    _spellsByTradition[spell.Tradition] = [];
                _spellsByTradition[spell.Tradition].Add(spell.SpellId);
            }

            _initialized = true;
            _logger.LogInformation("SpellCatalogService initialized with {Count} spells", _spells.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize SpellCatalogService");
            throw;
        }
    }

    /// <summary>
    /// Get spell by ID.
    /// </summary>
    public Spell? GetSpell(string spellId)
    {
        EnsureInitialized();
        return _spells.GetValueOrDefault(spellId);
    }

    /// <summary>
    /// Get all spells.
    /// </summary>
    public IReadOnlyDictionary<string, Spell> GetAllSpells()
    {
        EnsureInitialized();
        return _spells;
    }

    /// <summary>
    /// Get spells by magical tradition.
    /// </summary>
    public List<Spell> GetSpellsByTradition(MagicalTradition tradition)
    {
        EnsureInitialized();
        
        if (!_spellsByTradition.TryGetValue(tradition, out var spellIds))
        {
            return new List<Spell>();
        }

        return spellIds.Select(id => _spells[id]).ToList();
    }

    /// <summary>
    /// Get spells by rank (0-10).
    /// </summary>
    public List<Spell> GetSpellsByRank(int rank)
    {
        EnsureInitialized();
        return _spells.Values.Where(s => s.Rank == rank).ToList();
    }

    /// <summary>
    /// Get learnable spells for a character based on their magic skills.
    /// </summary>
    public List<Spell> GetLearnableSpells(Character character)
    {
        EnsureInitialized();
        
        var learnable = new List<Spell>();

        foreach (var spell in _spells.Values)
        {
            // Check if character has the tradition skill
            var traditionSkillId = GetTraditionSkillId(spell.Tradition);
            if (!character.Skills.TryGetValue(traditionSkillId, out var skill))
            {
                continue; // Don't have this tradition
            }

            // Can learn spells up to character's skill rank (with some tolerance)
            if (spell.MinimumSkillRank <= skill.CurrentRank + 10)
            {
                learnable.Add(spell);
            }
        }

        return learnable;
    }

    /// <summary>
    /// Get tradition skill ID from tradition enum.
    /// </summary>
    public string GetTraditionSkillId(MagicalTradition tradition)
    {
        return tradition switch
        {
            MagicalTradition.Arcane => "arcane",
            MagicalTradition.Divine => "divine",
            MagicalTradition.Occult => "occult",
            MagicalTradition.Primal => "primal",
            _ => "arcane"
        };
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
            throw new InvalidOperationException("SpellCatalogService not initialized. Call InitializeAsync() first.");
    }
}
