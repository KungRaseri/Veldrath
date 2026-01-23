namespace RealmEngine.Core.Abstractions;

/// <summary>
/// Interface for managing the countdown timer for Apocalypse mode.
/// This interface enables testing and mocking of the ApocalypseTimer service.
/// </summary>
public interface IApocalypseTimer
{
    /// <summary>
    /// Start the apocalypse timer.
    /// </summary>
    void Start();

    /// <summary>
    /// Start timer from a saved state (for loading saves).
    /// </summary>
    void StartFromSave(DateTime startTime, int bonusMinutes);

    /// <summary>
    /// Pause the timer (during menus, saves, etc.).
    /// </summary>
    void Pause();

    /// <summary>
    /// Resume the timer.
    /// </summary>
    void Resume();

    /// <summary>
    /// Get remaining minutes on the timer.
    /// </summary>
    int GetRemainingMinutes();

    /// <summary>
    /// Check if timer has expired.
    /// </summary>
    bool IsExpired();

    /// <summary>
    /// Add bonus minutes to the timer.
    /// Returns result info for Godot UI to display.
    /// </summary>
    /// <param name="minutes">Minutes to add.</param>
    /// <param name="reason">Reason for bonus time.</param>
    /// <returns>Information about the bonus time awarded.</returns>
    BonusTimeResult AddBonusTime(int minutes, string reason = "Quest completed");

    /// <summary>
    /// Get formatted time remaining string.
    /// </summary>
    string GetFormattedTimeRemaining();

    /// <summary>
    /// Get colored time display for UI.
    /// </summary>
    string GetColoredTimeDisplay();

    /// <summary>
    /// Check and return time warnings if they should be triggered.
    /// Returns warning info for Godot UI to display.
    /// </summary>
    /// <returns>Warning info if a warning should be shown, null otherwise.</returns>
    TimeWarningResult? CheckTimeWarnings();

    /// <summary>
    /// Get total time limit with bonuses.
    /// </summary>
    int GetTotalTimeLimit();

    /// <summary>
    /// Get time elapsed.
    /// </summary>
    int GetElapsedMinutes();

    /// <summary>
    /// Get bonus minutes awarded so far (for save persistence).
    /// </summary>
    int GetBonusMinutes();
}
