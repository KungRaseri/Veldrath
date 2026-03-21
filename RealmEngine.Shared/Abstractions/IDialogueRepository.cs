using RealmEngine.Shared.Models;

namespace RealmEngine.Shared.Abstractions;

/// <summary>Repository interface for reading dialogue catalog data.</summary>
public interface IDialogueRepository
{
    /// <summary>Returns all active dialogues.</summary>
    Task<List<DialogueEntry>> GetAllAsync();

    /// <summary>Returns a single dialogue by slug, or <see langword="null"/> if not found.</summary>
    Task<DialogueEntry?> GetBySlugAsync(string slug);

    /// <summary>Returns all active dialogues with the given speaker (e.g. "merchant", "guard"). Rows with a null speaker are excluded.</summary>
    Task<List<DialogueEntry>> GetBySpeakerAsync(string speaker);
}
