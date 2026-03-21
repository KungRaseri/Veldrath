namespace RealmEngine.Shared.Models;

/// <summary>Lightweight dialogue catalog projection used by the repository layer.</summary>
public record DialogueEntry(
    string Slug,
    string DisplayName,
    string TypeKey,
    string? Speaker,
    int RarityWeight,
    List<string> Lines);
