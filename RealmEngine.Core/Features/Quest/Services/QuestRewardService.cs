using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Shared.Models;
using Serilog;

namespace RealmEngine.Core.Features.Quests.Services;

/// <summary>
/// Service for distributing quest rewards to the player.
/// </summary>
public class QuestRewardService
{
    private readonly SaveGameService _saveGameService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuestRewardService"/> class.
    /// </summary>
    /// <param name="saveGameService">The save game service.</param>
    public QuestRewardService(SaveGameService saveGameService)
    {
        _saveGameService = saveGameService;
    }

    /// <summary>
    /// Distributes quest rewards to the player's character and save game.
    /// </summary>
    public virtual void DistributeRewards(Quest quest, Character character, SaveGame saveGame)
    {
        // Award experience
        if (quest.XpReward > 0)
        {
            character.Experience += quest.XpReward;
            Log.Information("Quest reward: {XP} experience awarded", quest.XpReward);
        }

        // Award gold
        if (quest.GoldReward > 0)
        {
            character.Gold += quest.GoldReward;
            Log.Information("Quest reward: {Gold} gold awarded", quest.GoldReward);
        }

        // Award apocalypse bonus time
        if (quest.ApocalypseBonusMinutes > 0 && saveGame.ApocalypseMode)
        {
            saveGame.ApocalypseBonusMinutes += quest.ApocalypseBonusMinutes;
            Log.Information("Quest reward: {Minutes} minutes added to Apocalypse timer", quest.ApocalypseBonusMinutes);
        }

        // Award items (note: item references are in v4.1 format like "@items/weapons/swords:iron-longsword")
        // Grant item rewards from quest
        if (quest.ItemRewardIds != null && quest.ItemRewardIds.Any())
        {
            foreach (var itemRef in quest.ItemRewardIds)
            {
                Log.Information("Quest reward: Item reference '{ItemRef}' granted", itemRef);
                // Note: Actual item generation from references requires ItemGenerator integration
                // For now, add a placeholder item to indicate quest reward was granted
                character.Inventory.Add(new Item
                {
                    Name = $"Quest Reward: {itemRef}",
                    Description = $"Reward from quest '{quest.Name}' (Reference: {itemRef})",
                    Type = ItemType.QuestItem,
                    Rarity = ItemRarity.Uncommon
                });
            }
        }

        _saveGameService.SaveGame(saveGame);
    }
}
