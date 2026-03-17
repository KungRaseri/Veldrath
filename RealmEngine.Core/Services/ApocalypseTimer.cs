using RealmEngine.Core.Abstractions;

using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Services;

/// <summary>
/// Manages the countdown timer for Apocalypse mode.
/// This is a shared service, not a feature, as it's infrastructure.
/// Pure domain logic - UI handled by Godot.
/// </summary>
public class ApocalypseTimer : IApocalypseTimer
{
    private DateTime _startTime;
    private int _totalMinutes = 240; // 4 hours = 240 minutes
    private int _bonusMinutes = 0;
    private bool _isPaused = false;
    private TimeSpan _pausedDuration = TimeSpan.Zero;
    private DateTime? _pauseStartTime = null;
    private bool _hasShownOneHourWarning = false;
    private bool _hasShownThirtyMinWarning = false;
    private bool _hasShownTenMinWarning = false;

private readonly ILogger<ApocalypseTimer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApocalypseTimer"/> class.
    /// </summary>
    public ApocalypseTimer(ILogger<ApocalypseTimer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Start the apocalypse timer.
    /// </summary>
    public void Start()
    {
        _startTime = DateTime.Now;
        _isPaused = false;
        _hasShownOneHourWarning = false;
        _hasShownThirtyMinWarning = false;
        _hasShownTenMinWarning = false;

        _logger.LogInformation("Apocalypse timer started. {TotalMinutes} minutes until world end.", _totalMinutes);
    }

    /// <summary>
    /// Start timer from a saved state (for loading saves).
    /// </summary>
    /// <param name="startTime">The saved start time.</param>
    /// <param name="bonusMinutes">The bonus minutes earned.</param>
    public void StartFromSave(DateTime startTime, int bonusMinutes)
    {
        _startTime = startTime;
        _bonusMinutes = bonusMinutes;
        _isPaused = false;

        _logger.LogInformation("Apocalypse timer restored from save. Started at: {StartTime}, Bonus: {BonusMinutes}",
            startTime, bonusMinutes);
    }

    /// <summary>
    /// Pause the timer (during menus, saves, etc.).
    /// </summary>
    public void Pause()
    {
        if (!_isPaused)
        {
            _isPaused = true;
            _pauseStartTime = DateTime.Now;
            _logger.LogDebug("Apocalypse timer paused");
        }
    }

    /// <summary>
    /// Resume the timer.
    /// </summary>
    public void Resume()
    {
        if (_isPaused && _pauseStartTime.HasValue)
        {
            _pausedDuration += DateTime.Now - _pauseStartTime.Value;
            _isPaused = false;
            _pauseStartTime = null;
            _logger.LogDebug("Apocalypse timer resumed. Total paused time: {PausedMinutes} minutes",
                _pausedDuration.TotalMinutes);
        }
    }

    /// <summary>
    /// Get remaining minutes on the timer.
    /// </summary>
    /// <returns>The remaining minutes.</returns>
    public int GetRemainingMinutes()
    {
        if (_isPaused && _pauseStartTime.HasValue)
        {
            // Calculate as if we're still paused
            var elapsed = (_pauseStartTime.Value - _startTime) - _pausedDuration;
            return Math.Max(0, (int)(_totalMinutes + _bonusMinutes - elapsed.TotalMinutes));
        }

        var totalElapsed = (DateTime.Now - _startTime) - _pausedDuration;
        return Math.Max(0, (int)(_totalMinutes + _bonusMinutes - totalElapsed.TotalMinutes));
    }

    /// <summary>
    /// Check if timer has expired.
    /// </summary>
    /// <returns>True if timer has expired.</returns>
    public bool IsExpired()
    {
        return GetRemainingMinutes() <= 0;
    }

    /// <summary>
    /// Add bonus minutes to the timer.
    /// Returns result info for Godot UI to display.
    /// </summary>
    /// <param name="minutes">The minutes to add.</param>
    /// <param name="reason">The reason for the bonus time.</param>
    /// <returns>Information about the bonus time awarded.</returns>
    public BonusTimeResult AddBonusTime(int minutes, string reason = "Quest completed")
    {
        _bonusMinutes += minutes;
        var remaining = GetRemainingMinutes();

        _logger.LogInformation("Bonus time awarded: {Minutes} minutes. Reason: {Reason}. Remaining: {Remaining}",
            minutes, reason, remaining);

        return new BonusTimeResult
        {
            MinutesAdded = minutes,
            Reason = reason,
            TotalRemainingMinutes = remaining
        };
    }

    /// <summary>
    /// Get formatted time remaining string.
    /// </summary>
    /// <returns>The formatted time string.</returns>
    public string GetFormattedTimeRemaining()
    {
        var remaining = GetRemainingMinutes();
        var hours = remaining / 60;
        var mins = remaining % 60;

        return $"{hours}h {mins}m";
    }

    /// <summary>
    /// Get colored time display for UI.
    /// </summary>
    /// <returns>The colored time display string.</returns>
    public string GetColoredTimeDisplay()
    {
        var remaining = GetRemainingMinutes();
        var formatted = GetFormattedTimeRemaining();

        var color = remaining switch
        {
            < 10 => "red",
            < 30 => "yellow",
            < 60 => "orange",
            _ => "green"
        };

        return $"[{color}]⏱ {formatted}[/]";
    }

    /// <summary>
    /// Check if time warnings should be triggered and return warning info.
    /// Godot handles displaying warnings.
    /// </summary>
    /// <returns>Time warning info if a warning should be shown, null otherwise.</returns>
    public TimeWarningResult? CheckTimeWarnings()
    {
        var remaining = GetRemainingMinutes();

        if (remaining <= 60 && !_hasShownOneHourWarning)
        {
            _hasShownOneHourWarning = true;
            _logger.LogWarning("Apocalypse timer warning: 1 HOUR REMAINING!");
            return new TimeWarningResult
            {
                Title = "1 HOUR REMAINING!",
                Message = "The apocalypse draws near...",
                RemainingMinutes = remaining,
                TimeFormatted = GetFormattedTimeRemaining()
            };
        }
        else if (remaining <= 30 && !_hasShownThirtyMinWarning)
        {
            _hasShownThirtyMinWarning = true;
            _logger.LogWarning("Apocalypse timer warning: 30 MINUTES REMAINING!");
            return new TimeWarningResult
            {
                Title = "30 MINUTES REMAINING!",
                Message = "Time is running out!",
                RemainingMinutes = remaining,
                TimeFormatted = GetFormattedTimeRemaining()
            };
        }
        else if (remaining <= 10 && !_hasShownTenMinWarning)
        {
            _hasShownTenMinWarning = true;
            _logger.LogWarning("Apocalypse timer warning: 10 MINUTES REMAINING!");
            return new TimeWarningResult
            {
                Title = "10 MINUTES REMAINING!",
                Message = "The end is imminent!",
                RemainingMinutes = remaining,
                TimeFormatted = GetFormattedTimeRemaining()
            };
        }

        return null;
    }

    /// <summary>
    /// Get total time limit with bonuses.
    /// </summary>
    /// <returns>The total time limit in minutes.</returns>
    public int GetTotalTimeLimit()
    {
        return _totalMinutes + _bonusMinutes;
    }

    /// <summary>
    /// Get time elapsed.
    /// </summary>
    /// <returns>The elapsed time in minutes.</returns>
    public int GetElapsedMinutes()
    {
        var totalElapsed = (DateTime.Now - _startTime) - _pausedDuration;
        return (int)totalElapsed.TotalMinutes;
    }

    /// <summary>
    /// Get bonus minutes awarded so far (for save persistence).
    /// </summary>
    /// <returns>The bonus minutes.</returns>
    public int GetBonusMinutes()
    {
        return _bonusMinutes;
    }
}
