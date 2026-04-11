namespace Veldrath.Client.Services;

/// <summary>
/// Carries a single pending alert message across a navigation boundary so that a
/// view model that triggers navigation can pass a message to the destination view model.
/// Registered as a singleton; the consumer clears the value after reading it.
/// </summary>
public interface ISessionAlertService
{
    /// <summary>
    /// A one-shot alert message to be displayed at the next main-menu visit.
    /// Set before navigating; read and cleared by <see cref="ViewModels.MainMenuViewModel"/>.
    /// </summary>
    string? PendingAlert { get; set; }
}

/// <summary>Initializes a new instance of <see cref="SessionAlertService"/>.</summary>
public class SessionAlertService : ISessionAlertService
{
    /// <inheritdoc />
    public string? PendingAlert { get; set; }
}
