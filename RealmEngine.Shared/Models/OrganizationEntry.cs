namespace RealmEngine.Shared.Models;

/// <summary>Lightweight organization catalog projection used by the repository layer.</summary>
public record OrganizationEntry(
    string Slug,
    string DisplayName,
    string TypeKey,
    string OrgType,
    int RarityWeight);
