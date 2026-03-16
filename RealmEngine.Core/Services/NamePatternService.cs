using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Abstractions;
using RealmEngine.Data.Entities;

namespace RealmEngine.Core.Services;

/// <summary>
/// Singleton service that loads all <see cref="NamePatternSet"/> records at startup and serves them by entity path.
/// Call <see cref="InitializeAsync"/> after the DI container is built.
/// </summary>
public class NamePatternService
{
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly INamePatternRepository? _repository;
    private readonly ILogger<NamePatternService> _logger;
    private readonly Dictionary<string, NamePatternSet> _sets = new(StringComparer.OrdinalIgnoreCase);
    private bool _initialized;

    // Primary constructor used by DI (singleton-safe — no scoped dependency captured)
    [ActivatorUtilitiesConstructor]
    public NamePatternService(IServiceScopeFactory scopeFactory, ILogger<NamePatternService>? logger = null)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? NullLogger<NamePatternService>.Instance;
    }

    // Secondary constructor for direct construction in tests
    public NamePatternService(INamePatternRepository repository, ILogger<NamePatternService>? logger = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? NullLogger<NamePatternService>.Instance;
    }

    /// <summary>Initialize by loading all pattern sets from the repository.</summary>
    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            _logger.LogWarning("NamePatternService already initialized");
            return;
        }

        try
        {
            IEnumerable<NamePatternSet> all;
            if (_scopeFactory is not null)
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<INamePatternRepository>();
                all = await repo.GetAllAsync();
            }
            else
            {
                all = await _repository!.GetAllAsync();
            }

            foreach (var set in all)
                _sets[set.EntityPath] = set;

            _initialized = true;
            _logger.LogInformation("NamePatternService initialized with {Count} pattern sets", _sets.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize NamePatternService");
            throw;
        }
    }

    /// <summary>Returns the pattern set for the given entity path, or <c>null</c> if not found or not yet initialized.</summary>
    public NamePatternSet? GetPatternSet(string entityPath)
    {
        if (!_initialized)
            return null;
        return _sets.GetValueOrDefault(entityPath);
    }

    /// <summary>Returns whether a pattern set exists for the given entity path.</summary>
    public bool HasPatternSet(string entityPath)
    {
        if (!_initialized) return false;
        return _sets.ContainsKey(entityPath);
    }
}
