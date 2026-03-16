using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Abstractions;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;

namespace RealmEngine.Core.Repositories;

/// <summary>EF Core-backed repository for name-generation pattern sets.</summary>
public class EfCoreNamePatternRepository(ContentDbContext db, ILogger<EfCoreNamePatternRepository> logger)
    : INamePatternRepository
{
    /// <inheritdoc />
    public async Task<IEnumerable<NamePatternSet>> GetAllAsync()
    {
        var sets = await db.NamePatternSets.AsNoTracking()
            .Include(s => s.Patterns)
            .Include(s => s.Components)
            .ToListAsync();

        logger.LogDebug("Loaded {Count} name pattern sets from database", sets.Count);
        return sets;
    }

    /// <inheritdoc />
    public async Task<NamePatternSet?> GetByEntityPathAsync(string entityPath)
    {
        return await db.NamePatternSets.AsNoTracking()
            .Include(s => s.Patterns)
            .Include(s => s.Components)
            .FirstOrDefaultAsync(s => s.EntityPath == entityPath);
    }
}
