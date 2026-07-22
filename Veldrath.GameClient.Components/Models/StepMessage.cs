namespace Veldrath.GameClient.Components.Models;

/// <summary>
/// A single step-level feedback message with a status for visual conditioning.
/// </summary>
/// <param name="Status">The severity level controlling display color.</param>
/// <param name="Message">The human-readable message text to display.</param>
public sealed record StepMessage(StepMessageStatus Status, string Message);
