namespace Veldrath.Contracts.Chat;

/// <summary>
/// Describes a single chat slash-command available to the player.
/// Used by <c>GetChatCommands</c> to give clients a role-filtered list of commands
/// they are permitted to execute.
/// </summary>
/// <param name="Command">
/// The slash-command keyword without the leading <c>/</c>, e.g. <c>"roll"</c>, <c>"kick"</c>.
/// </param>
/// <param name="Usage">
/// Full usage syntax shown in the suggestion UI, e.g. <c>"/roll [max]"</c>.
/// </param>
/// <param name="Description">Short human-readable description of what the command does.</param>
/// <param name="RequiredPermission">
/// The permission string required to run this command, or <see langword="null"/> when the command
/// is available to all authenticated players.
/// </param>
public record ChatCommandInfoDto(
    string Command,
    string Usage,
    string Description,
    string? RequiredPermission);
