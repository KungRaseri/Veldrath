using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>EF Core-backed repository for dialogue catalog data.</summary>
public class EfCoreDialogueRepository(ContentDbContext db, ILogger<EfCoreDialogueRepository> logger)
    : IDialogueRepository
{
    /// <inheritdoc />
    public async Task<List<DialogueEntry>> GetAllAsync()
    {
        var entities = await db.Dialogues.AsNoTracking()
            .Where(d => d.IsActive)
            .ToListAsync();

        logger.LogDebug("Loaded {Count} dialogues from database", entities.Count);
        return entities.Select(MapToModel).ToList();
    }

    /// <inheritdoc />
    public async Task<DialogueEntry?> GetBySlugAsync(string slug)
    {
        var entity = await db.Dialogues.AsNoTracking()
            .Where(d => d.IsActive && d.Slug == slug)
            .FirstOrDefaultAsync();

        return entity is null ? null : MapToModel(entity);
    }

    /// <inheritdoc />
    public async Task<List<DialogueEntry>> GetBySpeakerAsync(string speaker)
    {
        var lower = speaker.ToLowerInvariant();
        var entities = await db.Dialogues.AsNoTracking()
            .Where(d => d.IsActive && d.Speaker != null && d.Speaker.ToLower() == lower)
            .ToListAsync();

        return entities.Select(MapToModel).ToList();
    }

    private static DialogueEntry MapToModel(Entities.Dialogue d) =>
        new(d.Slug, d.DisplayName ?? d.Slug, d.TypeKey, d.Speaker, d.RarityWeight, d.Stats.Lines);
}
