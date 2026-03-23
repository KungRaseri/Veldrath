using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;

namespace RealmEngine.Data.Seeders;

/// <summary>Seeds baseline <see cref="Dialogue"/> rows into <see cref="ContentDbContext"/>.</summary>
public static class DialogueSeeder
{
    /// <summary>Seeds all dialogue rows (idempotent).</summary>
    public static async Task SeedAsync(ContentDbContext db)
    {
        if (await db.Dialogues.AnyAsync())
            return;

        var now = DateTimeOffset.UtcNow;

        db.Dialogues.AddRange(
            // ── Village Blacksmith ────────────────────────────────────────────
            new Dialogue
            {
                Slug         = "blacksmith-greeting",
                TypeKey      = "greetings",
                DisplayName  = "Blacksmith Greeting",
                Speaker      = "village-blacksmith",
                RarityWeight = 100,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new DialogueStats
                {
                    Tone      = 2,
                    Formality = 0,
                    Lines     = new List<string>
                    {
                        "Welcome, traveler! Need something crafted?",
                        "The forge is hot and my hammer is ready.",
                        "Finest ironwork in Thornveil, guaranteed.",
                    },
                },
                Traits = new DialogueTraits
                {
                    Hostile      = false,
                    Friendly     = true,
                    Merchant     = true,
                    QuestRelated = false,
                    Greeting     = true,
                    Farewell     = false,
                },
            },
            new Dialogue
            {
                Slug         = "blacksmith-farewell",
                TypeKey      = "farewells",
                DisplayName  = "Blacksmith Farewell",
                Speaker      = "village-blacksmith",
                RarityWeight = 100,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new DialogueStats
                {
                    Tone      = 0,
                    Formality = 0,
                    Lines     = new List<string>
                    {
                        "Come back when you need more gear.",
                        "Stay sharp out there.",
                        "May your blade never dull!",
                    },
                },
                Traits = new DialogueTraits
                {
                    Hostile      = false,
                    Friendly     = true,
                    Merchant     = false,
                    QuestRelated = false,
                    Greeting     = false,
                    Farewell     = true,
                },
            },

            // ── Tavern Keeper ─────────────────────────────────────────────────
            new Dialogue
            {
                Slug         = "tavernkeeper-greeting",
                TypeKey      = "greetings",
                DisplayName  = "Tavern Keeper Greeting",
                Speaker      = "tavern-keeper",
                RarityWeight = 100,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new DialogueStats
                {
                    Tone      = 2,
                    Formality = 0,
                    Lines     = new List<string>
                    {
                        "Hail, stranger! What'll it be?",
                        "Cold ale, warm food, and a tale or two — that's what we offer.",
                        "You look like you've had quite the journey.",
                    },
                },
                Traits = new DialogueTraits
                {
                    Hostile      = false,
                    Friendly     = true,
                    Merchant     = true,
                    QuestRelated = false,
                    Greeting     = true,
                    Farewell     = false,
                },
            },
            new Dialogue
            {
                Slug         = "tavernkeeper-quest-hook",
                TypeKey      = "quest-hooks",
                DisplayName  = "Tavern Keeper Quest Hook",
                Speaker      = "tavern-keeper",
                RarityWeight = 80,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new DialogueStats
                {
                    Tone      = 1,
                    Formality = 0,
                    Lines     = new List<string>
                    {
                        "Listen, I won't lie to you... there's been trouble on the road to Greymoor.",
                        "Travelers ain't making it through. Something's hunting them out there.",
                        "Clear those bandits and I'll make it worth your while.",
                    },
                },
                Traits = new DialogueTraits
                {
                    Hostile      = false,
                    Friendly     = true,
                    Merchant     = false,
                    QuestRelated = true,
                    Greeting     = false,
                    Farewell     = false,
                },
            });

        await db.SaveChangesAsync();
    }
}
