namespace Veldrath.GameClient.Core.Payloads;

/// <summary>
/// Hub event payload received when one of the account's characters changes online state.
/// Matches the anonymous payload sent to the per-account group by <c>GameHub</c>.
/// </summary>
/// <param name="CharacterId">Identifier of the character whose status changed.</param>
/// <param name="IsOnline">Whether the character is now claimed by an active session.</param>
public sealed record CharacterStatusChangedPayload(Guid CharacterId, bool IsOnline);
