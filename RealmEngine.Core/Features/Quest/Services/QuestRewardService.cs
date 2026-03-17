using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Shared.Models;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.Quests.Services;

/// <summary>
/// Service for distributing quest rewards to the player.
/// </summary>
public class QuestRewardService
{
    private readonly ISaveGameService _saveGameService;
    private readonly ItemGenerator? _itemGenerator;
    private readonly ILogger<QuestRewardService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuestRewardService"/> class.
    /// </summary>
    /// <param name="saveGameService">The save game service.</param>
    /// <param name="itemGenerator">The item generator for resolving item rewards. Optional — when null, item rewards are logged as warnings and skipped.</param>
    /// <param name="logger">The logger.</param>
    public QuestRewardService(ISaveGameService saveGameService, ItemGenerator? itemGenerator, ILogger<QuestRewardService> logger)
    {
        _saveGameService = saveGameService;
        _itemGenerator = itemGenerator;
        _logger = logger;
    }

    /// <summary>
    /// Distributes quest rewards to the player's character and save game.
    /// </summary>
    public virtual async Task DistributeRewardsAsync(Quest quest, Character character, SaveGame saveGame)
    {
        // Award experience
        if (quest.XpReward > 0)
        {
            character.Experience += quest.XpReward;
            _logger.LogInformation("Quest reward: {XP} experience awarded", quest.XpReward);
        }

        // Award gold
        if (quest.GoldReward > 0)
        {
            character.Gold += quest.GoldReward;
            _logger.LogInformation("Quest reward: {Gold} gold awarded", quest.GoldReward);
        }

        // Award apocalypse bonus time
        if (quest.ApocalypseBonusMinutes > 0 && saveGame.ApocalypseMode)
        {
            saveGame.ApocalypseBonusMinutes += quest.ApocalypseBonusMinutes;
            _logger.LogInformation("Quest reward: {Minutes} minutes added to Apocalypse timer", quest.ApocalypseBonusMinutes);
        }

        // Award items — resolve v4.1 item references to actual generated items
        if (quest.ItemRewardIds != null && quest.ItemRewardIds.Any())
        {
            foreach (var itemRef in quest.ItemRewardIds)
            {
                var item = await TryGenerateItemAsync(itemRef);
                if (item != null)
                {
                    character.Inventory.Add(item);
                    _logger.LogInformation("Quest reward: '{ItemName}' added to inventory (from {ItemRef})", item.Name, itemRef);
                }
                else
                {
                    _logger.LogWarning("Quest reward: Could not generate item for reference '{ItemRef}'", itemRef);
                }
            }
        }

        _saveGameService.SaveGame(saveGame);
    }

    /// <summary>
    /// Attempts to generate an item from a v4.1 reference string (e.g. "@items/weapons/swords:iron-sword").
    /// Falls back to a random item from the same category if the named item is not found.
    /// Returns null if no item generator is available or generation fails.
    /// </summary>
    private async Task<Item?> TryGenerateItemAsync(string itemRef)
    {
        if (_itemGenerator == null) return null;
        // Parse "@items/{category}:{name}" — find the first slash after "@items/" and the last colon
        var slashIdx = itemRef.IndexOf('/');
        var colonIdx = itemRef.LastIndexOf(':');
        if (slashIdx < 0 || colonIdx <= slashIdx)
            return null;

        var category = itemRef[(slashIdx + 1)..colonIdx];
        var itemName = itemRef[(colonIdx + 1)..];

        try
        {
            var named = await _itemGenerator.GenerateItemByNameAsync(category, itemName, hydrate: true);
            if (named != null) return named;

            // Named item not found — fall back to a random item from the category
            var fallback = await _itemGenerator.GenerateItemsAsync(category, 1);
            return fallback.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate item for reference '{ItemRef}'", itemRef);
            return null;
        }
    }
}
