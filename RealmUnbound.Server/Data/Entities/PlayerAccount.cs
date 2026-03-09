namespace RealmUnbound.Server.Data.Entities;

/// <summary>Represents a registered player account in the persistent store.</summary>
public class PlayerAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Unique display name chosen at registration.</summary>
    public string Username { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastSeenAt { get; set; }
}
