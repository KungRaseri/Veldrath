using ReactiveUI;
using System.Reactive;
using Veldrath.Contracts.Chat;

namespace Veldrath.Client.ViewModels;

/// <summary>Wraps a <see cref="ChatCommandInfoDto"/> for display in the command suggestion popup.</summary>
public sealed class ChatCommandInfoViewModel
{
    private readonly ChatCommandInfoDto _dto;

    /// <summary>Initializes a new instance of <see cref="ChatCommandInfoViewModel"/>.</summary>
    /// <param name="dto">The contract DTO received from the server.</param>
    /// <param name="onSelect">Callback invoked with the command's usage string when the player selects this suggestion.</param>
    public ChatCommandInfoViewModel(ChatCommandInfoDto dto, Action<string> onSelect)
    {
        _dto = dto;
        SelectCommand = ReactiveCommand.Create(() => onSelect(dto.Usage));
    }

    /// <summary>Gets the command keyword without the leading slash, e.g. <c>"roll"</c>.</summary>
    public string Command => _dto.Command;

    /// <summary>Gets the full usage syntax including the leading slash, e.g. <c>"/roll [max]"</c>.</summary>
    public string Usage => _dto.Usage;

    /// <summary>Gets the short description of what the command does.</summary>
    public string Description => _dto.Description;

    /// <summary>
    /// Gets the formatted text shown in the suggestion row: usage padded to 32 characters followed by the description.
    /// </summary>
    public string DisplayText => $"{_dto.Usage,-32}{_dto.Description}";

    /// <summary>Gets the required permission string, or <see langword="null"/> for player-level commands.</summary>
    public string? RequiredPermission => _dto.RequiredPermission;

    /// <summary>Fills the chat input with this command's usage template and closes the suggestion popup.</summary>
    public ReactiveCommand<Unit, Unit> SelectCommand { get; }
}
