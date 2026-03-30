using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>
/// In-memory stub implementation of <see cref="IEnchantmentRepository"/>.
/// Returns empty results — used in the InMemory (no-database) DI configuration path.
/// </summary>
public class InMemoryEnchantmentRepository : IEnchantmentRepository
{
    /// <inheritdoc />
    public Task<List<Enchantment>> GetAllAsync() =>
        Task.FromResult(new List<Enchantment>());

    /// <inheritdoc />
    public Task<Enchantment?> GetBySlugAsync(string slug) =>
        Task.FromResult((Enchantment?)null);

    /// <inheritdoc />
    public Task<List<Enchantment>> GetByTargetSlotAsync(string targetSlot) =>
        Task.FromResult(new List<Enchantment>());
}
