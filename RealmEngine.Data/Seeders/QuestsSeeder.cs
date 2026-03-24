using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;

namespace RealmEngine.Data.Seeders;

/// <summary>Seeds baseline <see cref="Quest"/> rows into <see cref="ContentDbContext"/>.</summary>
public static class QuestsSeeder
{
    /// <summary>Seeds all quest rows (idempotent).</summary>
    public static async Task SeedAsync(ContentDbContext db)
    {
        if (await db.Quests.AnyAsync())
            return;

        var now = DateTimeOffset.UtcNow;

        db.Quests.AddRange(
            // Combat quest
            new Quest
            {
                Slug         = "clear-the-forest",
                TypeKey      = "combat",
                DisplayName  = "Clear the Forest",
                RarityWeight = 80,
                MinLevel     = 1,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new QuestStats
                {
                    XpReward         = 150,
                    GoldReward       = 30,
                    ReputationReward = 10,
                },
                Traits = new QuestTraits
                {
                    Repeatable            = true,
                    MainStory             = false,
                    Timed                 = false,
                    GroupQuest            = false,
                    HiddenUntilDiscovered = false,
                },
                Objectives = new QuestObjectives
                {
                    Items =
                    [
                        new QuestObjective
                        {
                            Type        = "kill",
                            Target      = "goblin-scout",
                            Quantity    = 5,
                            Description = "Defeat 5 Goblin Scouts in the Darkwood Forest.",
                        },
                    ],
                },
                Rewards = new QuestRewards
                {
                    Items =
                    [
                        new QuestReward { Type = "xp",   Amount = 150 },
                        new QuestReward { Type = "gold", Amount = 30  },
                    ],
                },
            },

            // Gathering quest
            new Quest
            {
                Slug         = "gather-mushrooms",
                TypeKey      = "gathering",
                DisplayName  = "Gather Mushrooms",
                RarityWeight = 70,
                MinLevel     = 1,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new QuestStats
                {
                    XpReward         = 75,
                    GoldReward       = 15,
                    ReputationReward = 5,
                },
                Traits = new QuestTraits
                {
                    Repeatable            = true,
                    MainStory             = false,
                    Timed                 = false,
                    GroupQuest            = false,
                    HiddenUntilDiscovered = false,
                },
                Objectives = new QuestObjectives
                {
                    Items =
                    [
                        new QuestObjective
                        {
                            Type        = "collect",
                            Target      = "glowcap-mushroom",
                            Quantity    = 10,
                            Description = "Collect 10 Glowcap Mushrooms from the forest floor.",
                        },
                    ],
                },
                Rewards = new QuestRewards
                {
                    Items =
                    [
                        new QuestReward { Type = "xp",   Amount = 75  },
                        new QuestReward { Type = "gold", Amount = 15  },
                        new QuestReward { Type = "item", ItemDomain = "items/general", ItemSlug = "health-potion", Amount = 3 },
                    ],
                },
            },

            // Story quest
            new Quest
            {
                Slug         = "the-lost-shipment",
                TypeKey      = "story",
                DisplayName  = "The Lost Shipment",
                RarityWeight = 40,
                MinLevel     = 3,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new QuestStats
                {
                    XpReward         = 400,
                    GoldReward       = 80,
                    ReputationReward = 25,
                },
                Traits = new QuestTraits
                {
                    Repeatable            = false,
                    MainStory             = true,
                    Timed                 = false,
                    GroupQuest            = false,
                    HiddenUntilDiscovered = false,
                },
                Objectives = new QuestObjectives
                {
                    Items =
                    [
                        new QuestObjective
                        {
                            Type        = "kill",
                            Target      = "bandit-ruffian",
                            Quantity    = 3,
                            Description = "Defeat the bandits who ambushed the merchant convoy.",
                        },
                        new QuestObjective
                        {
                            Type        = "collect",
                            Target      = "lost-cargo",
                            Quantity    = 1,
                            Description = "Recover the stolen cargo crate.",
                        },
                        new QuestObjective
                        {
                            Type        = "interact",
                            Target      = "village-blacksmith",
                            Quantity    = 1,
                            Description = "Return the cargo to the Village Blacksmith.",
                        },
                    ],
                },
                Rewards = new QuestRewards
                {
                    Items =
                    [
                        new QuestReward { Type = "xp",   Amount = 400 },
                        new QuestReward { Type = "gold", Amount = 80  },
                        new QuestReward { Type = "reputation", Amount = 25 },
                    ],
                },
            }
        );

        await db.SaveChangesAsync();
    }
}
