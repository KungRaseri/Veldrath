namespace Veldrath.Server.Data.Entities;

/// <summary>
/// A player-submitted report about another player's behaviour.
/// Created by the <c>/report</c> in-game chat command.
/// Admins and moderators review and resolve reports via the Foundry admin panel.
/// </summary>
public class PlayerReport
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Character ID of the player who submitted the report, or <c>null</c> for web-portal reports.</summary>
    public Guid? ReporterCharacterId { get; set; }

    /// <summary>Character name of the reporter at submission time (denormalised).</summary>
    public string ReporterName { get; set; } = string.Empty;

    /// <summary>Character ID of the reported player, or <c>null</c> for web-portal reports.</summary>
    public Guid? TargetCharacterId { get; set; }

    /// <summary>Character name of the reported player at submission time (denormalised).</summary>
    public string TargetName { get; set; } = string.Empty;

    /// <summary>The reason provided by the reporter.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the report was submitted.</summary>
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Whether a staff member has reviewed and closed this report.</summary>
    public bool IsResolved { get; set; } = false;

    /// <summary>UTC timestamp when the report was resolved, or <c>null</c> if still open.</summary>
    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>Account ID of the staff member who resolved the report, or <c>null</c> if still open.</summary>
    public Guid? ResolvedByAccountId { get; set; }
}
