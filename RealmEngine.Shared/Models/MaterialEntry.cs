namespace RealmEngine.Shared.Models;

/// <summary>Lightweight material catalog projection used by the repository layer.</summary>
public record MaterialEntry(
    string Slug,
    string DisplayName,
    string MaterialFamily,
    float RarityWeight,
    bool IsActive,
    float? Hardness,
    float? Conductivity,
    bool? Magical,
    bool? Enchantable);
