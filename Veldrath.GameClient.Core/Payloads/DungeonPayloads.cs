namespace Veldrath.GameClient.Core.Payloads;

/// <summary>
/// Hub event payload received when the character enters a dungeon.
/// </summary>
/// <param name="DungeonId">The dungeon's unique identifier.</param>
/// <param name="DungeonSlug">The dungeon's URL-safe slug identifier.</param>
public sealed record DungeonEnteredPayload(string DungeonId, string DungeonSlug);
