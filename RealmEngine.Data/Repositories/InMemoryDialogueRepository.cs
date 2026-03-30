using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>
/// In-memory stub implementation of <see cref="IDialogueRepository"/>.
/// Returns empty results — used in the InMemory (no-database) DI configuration path.
/// </summary>
public class InMemoryDialogueRepository : IDialogueRepository
{
    /// <inheritdoc />
    public Task<List<DialogueEntry>> GetAllAsync() =>
        Task.FromResult(new List<DialogueEntry>());

    /// <inheritdoc />
    public Task<DialogueEntry?> GetBySlugAsync(string slug) =>
        Task.FromResult((DialogueEntry?)null);

    /// <inheritdoc />
    public Task<List<DialogueEntry>> GetBySpeakerAsync(string speaker) =>
        Task.FromResult(new List<DialogueEntry>());
}
