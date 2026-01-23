namespace RealmEngine.Core.Abstractions;

/// <summary>
/// Result of adding bonus time to the apocalypse timer.
/// </summary>
public class BonusTimeResult
{
    /// <summary>Gets or sets the minutes added.</summary>
    public int MinutesAdded { get; set; }
    
    /// <summary>Gets or sets the reason for the bonus.</summary>
    public string Reason { get; set; } = string.Empty;
    
    /// <summary>Gets or sets the total remaining minutes after bonus.</summary>
    public int TotalRemainingMinutes { get; set; }
}
