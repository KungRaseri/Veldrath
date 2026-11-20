using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;

namespace RealmEngine.Data.Tests.Persistence;

/// <summary>
/// Proves that a dialogue option survives a save and a load through the owned JSON column.
/// </summary>
/// <remarks>
/// The InMemory provider keeps object references, so it cannot show whether a value actually reaches the database —
/// which is the failure that matters here: an option that is written by Foundry and silently lost by the mapping
/// would show up as a conversation with no choices and nothing in the log. SQLite is the provider in this suite that
/// stores a <c>ToJson()</c> owned graph as real JSON.
/// </remarks>
public class DialogueOptionPersistenceTests
{
    [Fact]
    public async Task Options_survive_a_round_trip_through_the_json_column()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ContentDbContext>().UseSqlite(connection).Options;

        await using (var write = new ContentDbContext(options))
        {
            await write.Database.EnsureCreatedAsync();
            write.Dialogues.Add(new Dialogue
            {
                Slug = "elder-greeting",
                TypeKey = "greetings",
                DisplayName = "The Elder Greets You",
                Speaker = "wayfarer-elder",
                Stats = new DialogueStats
                {
                    Lines = ["Well met, traveller."],
                    Options =
                    [
                        new DialogueOption
                        {
                            Key = "ask-about-woods",
                            Label = "What happened in the wood?",
                            Response = "Something older than the road walks there now.",
                            Requires = "level:3\nitem:glowcap-mushroom:1",
                            Grants = "gold:25",
                            OneShot = true,
                            NextSlug = "elder-follow-up",
                        },
                    ],
                },
            });

            await write.SaveChangesAsync();
        }

        await using (var read = new ContentDbContext(options))
        {
            var stored = await read.Dialogues.SingleAsync(dialogue => dialogue.Slug == "elder-greeting");

            stored.Stats.Lines.Should().Equal("Well met, traveller.");
            stored.Stats.Options.Should().ContainSingle();

            var option = stored.Stats.Options[0];
            option.Key.Should().Be("ask-about-woods");
            option.Label.Should().Be("What happened in the wood?");
            option.Response.Should().Be("Something older than the road walks there now.");
            option.Requires.Should().Be("level:3\nitem:glowcap-mushroom:1");
            option.Grants.Should().Be("gold:25");
            option.OneShot.Should().BeTrue();
            option.NextSlug.Should().Be("elder-follow-up");
        }
    }

    [Fact]
    public async Task An_entry_without_options_keeps_an_empty_list()
    {
        // The shape every dialogue authored before options existed is in; loading it must not throw or invent one.
        using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ContentDbContext>().UseSqlite(connection).Options;

        await using (var write = new ContentDbContext(options))
        {
            await write.Database.EnsureCreatedAsync();
            write.Dialogues.Add(new Dialogue
            {
                Slug = "tavern-greeting",
                TypeKey = "greetings",
                DisplayName = "The Keeper Looks Up",
                Speaker = "tavern-keeper",
                Stats = new DialogueStats { Lines = ["Ale?"] },
            });
            await write.SaveChangesAsync();
        }

        await using (var read = new ContentDbContext(options))
        {
            var stored = await read.Dialogues.SingleAsync(dialogue => dialogue.Slug == "tavern-greeting");

            stored.Stats.Options.Should().BeEmpty();
            stored.Stats.Lines.Should().Equal("Ale?");
        }
    }
}
