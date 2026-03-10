namespace RealmUnbound.Server.Data.Entities;

/// <summary>
/// Live record of a character currently online inside a zone.
/// Created when <c>EnterZone</c> is called; deleted on disconnect or explicit <c>LeaveZone</c>.
/// </summary>
public class ZoneSession
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CharacterId { get; set; }

    public string CharacterName { get; set; } = string.Empty;

    /// <summary>SignalR <c>Context.ConnectionId</c> — used to find and clean up on disconnect.</summary>
    public string ConnectionId { get; set; } = string.Empty;

    public string ZoneId { get; set; } = string.Empty;

    public DateTimeOffset EnteredAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public Character Character { get; set; } = null!;
    public Zone Zone { get; set; } = null!;
}
