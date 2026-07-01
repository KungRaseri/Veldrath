using Veldrath.GameClient.Core.Abstractions;

namespace Veldrath.GameClient.Core.Payloads;

/// <summary>
/// Hub event payload received when the character's quest log is sent from the server.
/// Contains the full list of active and completed quests.
/// </summary>
/// <param name="Active">The currently active quests.</param>
/// <param name="Completed">The completed quests.</param>
public sealed record QuestLogReceivedPayload(List<QuestLogEntry> Active, List<QuestLogEntry> Completed);
