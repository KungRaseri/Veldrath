using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Generators.Modern;

/// <summary>Generates DialogueLine instances from the dialogue catalog in the database.</summary>
public class DialogueGenerator(ContentDbContext db, ILogger<DialogueGenerator> logger)
{
    private readonly Random _random = new();

    /// <summary>Generates dialogue lines of the given type and style.</summary>
    public async Task<List<DialogueLine>> GenerateDialogueAsync(string dialogueType, string style, int count = 5)
    {
        try
        {
            var all = await db.Dialogues.AsNoTracking()
                .Where(d => d.IsActive && d.TypeKey == dialogueType)
                .ToListAsync();

            var lines = all.SelectMany(d => ExtractLines(d, style)).ToList();
            if (lines.Count == 0) return [];

            var result = new List<DialogueLine>(count);
            for (int i = 0; i < count; i++)
                result.Add(lines[_random.Next(lines.Count)]);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating dialogue type={Type}", dialogueType);
            return [];
        }
    }

    /// <summary>Generates a greeting dialogue line.</summary>
    public async Task<DialogueLine?> GenerateGreetingAsync(string style = "casual")
    {
        try
        {
            var all = await db.Dialogues.AsNoTracking()
                .Where(d => d.IsActive && d.Traits.Greeting == true)
                .ToListAsync();

            var lines = all.SelectMany(d => ExtractLines(d, style)).ToList();
            return lines.Count == 0 ? null : lines[_random.Next(lines.Count)];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating greeting");
            return null;
        }
    }

    /// <summary>Generates a farewell dialogue line.</summary>
    public async Task<DialogueLine?> GenerateFarewellAsync(string style = "casual")
    {
        try
        {
            var all = await db.Dialogues.AsNoTracking()
                .Where(d => d.IsActive && d.Traits.Farewell == true)
                .ToListAsync();

            var lines = all.SelectMany(d => ExtractLines(d, style)).ToList();
            return lines.Count == 0 ? null : lines[_random.Next(lines.Count)];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating farewell");
            return null;
        }
    }

    /// <summary>Generates a response line for the given context.</summary>
    public async Task<DialogueLine?> GenerateResponseAsync(string context, string style = "neutral")
    {
        try
        {
            var all = await db.Dialogues.AsNoTracking()
                .Where(d => d.IsActive && d.TypeKey == "responses")
                .ToListAsync();

            var lines = all.SelectMany(d => ExtractLines(d, style)).ToList();
            return lines.Count == 0 ? null : lines[_random.Next(lines.Count)];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating response");
            return null;
        }
    }

    /// <summary>Generates a short conversation (greeting + responses + farewell).</summary>
    public async Task<List<DialogueLine>> GenerateConversationAsync(string style = "casual")
    {
        var result = new List<DialogueLine>();
        var greeting = await GenerateGreetingAsync(style);
        if (greeting is not null) result.Add(greeting);

        var responses = await GenerateDialogueAsync("responses", style, 2);
        result.AddRange(responses);

        var farewell = await GenerateFarewellAsync(style);
        if (farewell is not null) result.Add(farewell);

        return result;
    }

    private static IEnumerable<DialogueLine> ExtractLines(Data.Entities.Dialogue dialogue, string style)
    {
        return dialogue.Stats.Lines.Select(line => new DialogueLine
        {
            Id = Guid.NewGuid().ToString(),
            Text = line,
            Type = dialogue.TypeKey,
            Style = style
        });
    }
}
