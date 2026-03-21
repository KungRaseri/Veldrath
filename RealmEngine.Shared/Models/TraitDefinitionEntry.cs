namespace RealmEngine.Shared.Models;

/// <summary>Lightweight trait definition projection used by the repository layer.</summary>
public record TraitDefinitionEntry(
    string Key,
    string ValueType,
    string? Description,
    string? AppliesTo);
