namespace Veldrath.GameClient.Components.Models;

/// <summary>
/// The severity level of a step message, controlling its display color.
/// </summary>
public enum StepMessageStatus
{
    /// <summary>Informational message — displayed with neutral styling.</summary>
    Info,

    /// <summary>Success message — displayed in green.</summary>
    Success,

    /// <summary>Warning message — displayed in yellow/orange.</summary>
    Warning,

    /// <summary>Error message — displayed in red.</summary>
    Error,
}
