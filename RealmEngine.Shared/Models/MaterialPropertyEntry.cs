namespace RealmEngine.Shared.Models;

/// <summary>Lightweight material property catalog projection used by the repository layer.</summary>
public record MaterialPropertyEntry(
    string Slug,
    string DisplayName,
    string TypeKey,
    string MaterialFamily,
    float CostScale,
    int RarityWeight);
