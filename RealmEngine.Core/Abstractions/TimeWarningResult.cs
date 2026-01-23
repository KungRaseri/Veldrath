namespace RealmEngine.Core.Abstractions;

/// <summary>
/// Result of checking for apocalypse time warnings.
/// </summary>
public class TimeWarningResult
{
    /// <summary>Gets or sets the warning title.</summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>Gets or sets the warning message.</summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>Gets or sets the remaining minutes.</summary>
    public int RemainingMinutes { get; set; }
    
    /// <summary>Gets or sets the formatted time remaining string.</summary>
    public string TimeFormatted { get; set; } = string.Empty;
}
