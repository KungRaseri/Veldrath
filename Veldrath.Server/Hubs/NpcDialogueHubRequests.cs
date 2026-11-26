namespace Veldrath.Server.Hubs;

/// <summary>Request body for <c>GetNpcDialogue</c>.</summary>
/// <remarks>
/// A single DTO rather than two parameters, because SignalR binds hub methods positionally and a multi-parameter
/// signature silently fails to bind when a client sends an object — the rule the hub instructions already state.
/// </remarks>
/// <param name="NpcSlug">Archetype slug of the person being spoken to.</param>
/// <param name="EntrySlug">Dialogue entry to show; blank picks the speaker's entry that has choices.</param>
public record NpcDialogueHubRequest(string NpcSlug, string? EntrySlug = null);

/// <summary>Request body for <c>ChooseNpcOption</c>.</summary>
/// <param name="NpcSlug">Archetype slug of the person being spoken to.</param>
/// <param name="OptionKey">Authored option key being taken.</param>
/// <param name="EntrySlug">Entry the option belongs to; blank picks the speaker's entry that has choices.</param>
public record NpcOptionHubRequest(string NpcSlug, string OptionKey, string? EntrySlug = null);
