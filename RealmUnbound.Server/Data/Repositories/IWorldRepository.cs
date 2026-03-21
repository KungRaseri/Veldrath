using Microsoft.EntityFrameworkCore;
using RealmUnbound.Server.Data.Entities;

namespace RealmUnbound.Server.Data.Repositories;

/// <summary>Read-only repository for <see cref="World"/> catalog entries.</summary>
public interface IWorldRepository
{
    /// <summary>Returns all worlds (currently only Draveth).</summary>
    Task<List<World>> GetAllAsync();

    /// <summary>Returns the world with the given <paramref name="worldId"/>, or <see langword="null"/> if not found.</summary>
    Task<World?> GetByIdAsync(string worldId);
}

/// <summary>EF Core implementation of <see cref="IWorldRepository"/>.</summary>
public class WorldRepository(ApplicationDbContext db) : IWorldRepository
{
    /// <inheritdoc/>
    public Task<List<World>> GetAllAsync() =>
        db.Worlds.OrderBy(w => w.Name).ToListAsync();

    /// <inheritdoc/>
    public Task<World?> GetByIdAsync(string worldId) =>
        db.Worlds.FirstOrDefaultAsync(w => w.Id == worldId);
}
