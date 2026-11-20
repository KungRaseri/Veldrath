using MediatR;
using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Content;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Quest;
using Character = Veldrath.Server.Data.Entities.Character;

namespace Veldrath.Server.Features.Characters.Npcs;

/// <summary>One choice a conversation offers, as shown to the client.</summary>
/// <param name="Key">Stable option key the client sends back.</param>
/// <param name="Label">The choice text the player reads.</param>
/// <param name="Enabled">Whether the character may take it right now.</param>
/// <param name="BlockedReason">Why it cannot be taken, when <paramref name="Enabled"/> is false.</param>
/// <param name="Consequences">What taking it changes, in the same words the server will apply.</param>
public record NpcDialogueOptionDto(
    string Key,
    string Label,
    bool Enabled,
    string? BlockedReason,
    IReadOnlyList<string> Consequences);

/// <summary>The conversation a character is having with one person.</summary>
/// <param name="SpeakerSlug">Archetype slug of the speaker.</param>
/// <param name="DisplayName">Name shown in the client.</param>
/// <param name="Occupation">The archetype's role category, when it has one.</param>
/// <param name="EntrySlug">The dialogue entry being shown, so a chained entry can be requested by slug.</param>
/// <param name="Prompt">The prose the speaker opens with.</param>
/// <param name="HasConversation">Whether this entry has any choices at all.</param>
/// <param name="Options">The choices, in authored order.</param>
public record NpcDialogueDto(
    string SpeakerSlug,
    string DisplayName,
    string? Occupation,
    string EntrySlug,
    string Prompt,
    bool HasConversation,
    IReadOnlyList<NpcDialogueOptionDto> Options);

/// <summary>Hub command that reads the conversation a person offers.</summary>
/// <param name="CharacterId">Character speaking.</param>
/// <param name="NpcSlug">Archetype slug of the person.</param>
/// <param name="EntrySlug">Dialogue entry to show; blank picks the speaker's entry that has choices.</param>
public record GetNpcDialogueHubCommand(Guid CharacterId, string NpcSlug, string? EntrySlug = null)
    : IRequest<NpcDialogueResult>;

/// <summary>Result returned by <see cref="GetNpcDialogueHubCommandHandler"/>.</summary>
public record NpcDialogueResult
{
    /// <summary>Gets a value indicating whether the conversation was read.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the failure reason when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the conversation.</summary>
    public NpcDialogueDto? Dialogue { get; init; }
}

/// <summary>Hub command that takes one choice in a conversation.</summary>
/// <param name="CharacterId">Character choosing.</param>
/// <param name="NpcSlug">Archetype slug of the person.</param>
/// <param name="OptionKey">Authored option key being taken.</param>
/// <param name="EntrySlug">Entry the option belongs to; blank picks the speaker's entry that has choices.</param>
public record ChooseNpcOptionHubCommand(Guid CharacterId, string NpcSlug, string OptionKey, string? EntrySlug = null)
    : IRequest<ChooseNpcOptionResult>;

/// <summary>Result returned by <see cref="ChooseNpcOptionHubCommandHandler"/>.</summary>
public record ChooseNpcOptionResult
{
    /// <summary>Gets a value indicating whether the choice was taken.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the failure reason when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the speaker's archetype slug.</summary>
    public string SpeakerSlug { get; init; } = string.Empty;

    /// <summary>Gets the name the client shows.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Gets what the speaker says in reply.</summary>
    public string Response { get; init; } = string.Empty;

    /// <summary>Gets what changed, in the same words the panel previewed.</summary>
    public IReadOnlyList<string> Applied { get; init; } = [];

    /// <summary>Gets the conversation to continue into, when the option chains to another entry.</summary>
    public NpcDialogueDto? Next { get; init; }

    /// <summary>Gets a value indicating whether the conversation ends with this choice.</summary>
    public bool Ended { get; init; }
}

/// <summary>
/// Shared reads for the dialogue commands: who is here, which entry they offer, and whether the character may take
/// an option. The rule evaluation lives here so the read path and the choose path cannot disagree about what an
/// option requires — the panel's "blocked" and the server's refusal are the same computation.
/// </summary>
internal static class NpcDialogueReader
{
    /// <summary>Finds the placement of a person at the character's location, or null when they are not there.</summary>
    /// <param name="db">Live-state context holding the placements.</param>
    /// <param name="character">Character whose location to inspect.</param>
    /// <param name="slug">Archetype slug being looked for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The placement row, or null.</returns>
    internal static Task<LocationNpc?> FindPlacementAsync(
        ApplicationDbContext db,
        Character character,
        string slug,
        CancellationToken cancellationToken) =>
        db.LocationNpcs
            .AsNoTracking()
            .Where(npc => npc.ZoneId == character.CurrentZoneId
                          && npc.LocationSlug == character.CurrentZoneLocationSlug
                          && npc.ArchetypeSlug == slug)
            .OrderBy(npc => npc.SortOrder)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>Finds the archetype behind a speaker, for the name and occupation the client shows.</summary>
    /// <param name="content">Content context.</param>
    /// <param name="slug">Archetype slug.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The archetype, or null when the content is missing.</returns>
    internal static Task<ActorArchetype?> FindArchetypeAsync(
        ContentDbContext content,
        string slug,
        CancellationToken cancellationToken) =>
        content.ActorArchetypes
            .AsNoTracking()
            .FirstOrDefaultAsync(actor => actor.Slug == slug, cancellationToken);

    /// <summary>
    /// Resolves the entry to show: the named one when it belongs to this speaker, otherwise the speaker's entry that
    /// actually has choices. An entry with no options is a spoken line, not a conversation.
    /// </summary>
    /// <param name="content">Content context.</param>
    /// <param name="speakerSlug">Archetype slug of the speaker.</param>
    /// <param name="entrySlug">Entry requested by the client, or null.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The entry, or null when this speaker has nothing to offer.</returns>
    internal static async Task<Dialogue?> PickEntryAsync(
        ContentDbContext content,
        string speakerSlug,
        string? entrySlug,
        CancellationToken cancellationToken)
    {
        var speaker = speakerSlug.ToLowerInvariant();

        var entries = await content.Dialogues
            .AsNoTracking()
            .Where(entry => entry.IsActive
                            && entry.Speaker != null
                            && entry.Speaker.ToLower() == speaker)
            .ToListAsync(cancellationToken);

        if (entries.Count == 0)
            return null;

        if (!string.IsNullOrWhiteSpace(entrySlug))
        {
            return entries.FirstOrDefault(entry =>
                string.Equals(entry.Slug, entrySlug, StringComparison.OrdinalIgnoreCase));
        }

        return entries
            .Where(entry => entry.Stats.Options.Count > 0)
            .OrderBy(entry => entry.Slug, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    /// <summary>Reads the option keys this character has already taken from a speaker.</summary>
    /// <param name="db">Live-state context.</param>
    /// <param name="characterId">Character in question.</param>
    /// <param name="speakerSlug">Archetype slug of the speaker.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The taken keys, compared case-insensitively.</returns>
    internal static async Task<HashSet<string>> ChosenKeysAsync(
        ApplicationDbContext db,
        Guid characterId,
        string speakerSlug,
        CancellationToken cancellationToken)
    {
        var keys = await db.CharacterDialogueChoices
            .AsNoTracking()
            .Where(choice => choice.CharacterId == characterId && choice.SpeakerSlug == speakerSlug)
            .Select(choice => choice.OptionKey)
            .ToListAsync(cancellationToken);

        return new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Whether an option may be taken only once, honouring the author's explicit override.</summary>
    /// <param name="option">The authored option.</param>
    /// <returns>True when it is one-shot.</returns>
    /// <remarks>
    /// Unset means "decide by grants": an option that gives the player something is one-shot, because otherwise it is
    /// a money printer. An option that grants nothing may be repeated freely.
    /// </remarks>
    internal static bool IsOneShot(DialogueOption option) =>
        option.OneShot ?? DialogueOptionGrammar.Split(option.Grants).Count > 0;

    /// <summary>Counts how many of an item the character carries.</summary>
    /// <param name="character">Character holding the bag.</param>
    /// <param name="slug">Bare item slug.</param>
    /// <returns>The carried quantity.</returns>
    internal static int CarryCount(Character character, string slug)
    {
        var bag = CharacterBag.Read(character.InventoryBlob);

        return bag
            .Where(entry => entry.TryGetValue("ItemRef", out var reference)
                            && reference.ValueKind == System.Text.Json.JsonValueKind.String
                            && string.Equals(reference.GetString(), slug, StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.TryGetValue("Quantity", out var quantity) && quantity.TryGetInt32(out var value)
                ? value
                : 0)
            .FirstOrDefault();
    }

    /// <summary>
    /// Explains why an option is unavailable, or null when it can be taken.
    /// </summary>
    /// <param name="option">The authored option.</param>
    /// <param name="character">Character being evaluated.</param>
    /// <param name="journal">The character's parsed quest journal.</param>
    /// <param name="taken">Keys already taken from this speaker.</param>
    /// <returns>A player-facing reason, or null.</returns>
    internal static string? BlockedReason(
        DialogueOption option,
        Character character,
        IReadOnlyList<CharacterQuestEntry> journal,
        IReadOnlySet<string> taken)
    {
        if (IsOneShot(option) && taken.Contains(option.Key))
            return "You have already spoken about that.";

        foreach (var entry in DialogueOptionGrammar.Split(option.Requires))
        {
            if (!DialogueOptionGrammar.TryParseRequirement(entry, out var effect, out var error))
            {
                // The validator rejects this at authoring time; refusing loudly here is the last line of defence.
                return $"This offer is broken: {error}.";
            }

            var reason = effect.Kind switch
            {
                DialogueEffectKind.Level when character.Level < effect.Amount =>
                    $"You would need level {effect.Amount}.",

                DialogueEffectKind.Item when CarryCount(character, effect.Key) < effect.Amount =>
                    $"You would need {effect.Amount} × {effect.Key}; you carry {CarryCount(character, effect.Key)}.",

                DialogueEffectKind.QuestActive when !HasQuest(journal, effect.Key, CharacterQuestEntry.StatusActive) =>
                    $"That needs {effect.Key} in progress.",

                DialogueEffectKind.QuestComplete when !HasQuest(journal, effect.Key, CharacterQuestEntry.StatusCompleted) =>
                    $"That needs {effect.Key} completed.",

                DialogueEffectKind.Objective when !HasObjective(journal, effect.Key, effect.Detail, effect.Amount) =>
                    $"That needs {effect.Key} {effect.Detail ?? string.Empty} ({effect.Amount}) first.",

                _ => null,
            };

            if (reason is not null)
                return reason;
        }

        return null;
    }

    /// <summary>Builds the conversation view for one entry against this character's live state.</summary>
    /// <param name="db">Live-state context, for the choices already taken.</param>
    /// <param name="entry">The dialogue entry to show.</param>
    /// <param name="view">The speaker's placement facts.</param>
    /// <param name="prompt">Prose the speaker opens with: the greeting, or the previous option's reply.</param>
    /// <param name="character">Character being evaluated.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The conversation, with every option already evaluated.</returns>
    internal static async Task<NpcDialogueDto> BuildAsync(
        ApplicationDbContext db,
        Dialogue entry,
        LocationNpcDto view,
        string prompt,
        Character character,
        CancellationToken cancellationToken)
    {
        var taken = await ChosenKeysAsync(db, character.Id, view.ArchetypeSlug, cancellationToken);
        var journal = QuestJournal.Parse(character.QuestBlob);

        var options = entry.Stats.Options
            .Select(option =>
            {
                var reason = BlockedReason(option, character, journal, taken);
                return new NpcDialogueOptionDto(
                    option.Key,
                    option.Label,
                    reason is null,
                    reason,
                    Consequences(option));
            })
            .ToList();

        return new NpcDialogueDto(
            view.ArchetypeSlug,
            view.DisplayName,
            view.Occupation,
            entry.Slug,
            prompt,
            options.Count > 0,
            options);
    }

    /// <summary>Describes what taking an option changes, in the words the panel shows.</summary>
    /// <param name="option">The authored option.</param>
    /// <returns>One sentence fragment per grant.</returns>
    internal static IReadOnlyList<string> Consequences(DialogueOption option) =>
        [.. DialogueOptionGrammar.Split(option.Grants).Select(entry =>
        {
            DialogueOptionGrammar.TryParseGrant(entry, out var effect, out _);
            return effect.Describe();
        })];

    private static bool HasQuest(
        IReadOnlyList<CharacterQuestEntry> journal,
        string slug,
        string status) =>
        journal.Any(entry =>
            string.Equals(entry.Slug, slug, StringComparison.OrdinalIgnoreCase)
            && string.Equals(entry.Status, status, StringComparison.OrdinalIgnoreCase));

    private static bool HasObjective(
        IReadOnlyList<CharacterQuestEntry> journal,
        string kind,
        string? key,
        int amount)
    {
        var objectiveKey = $"{kind}:{key}";

        return journal.Any(entry =>
            entry.Status == CharacterQuestEntry.StatusActive
            && entry.Objectives.TryGetValue(objectiveKey, out var _)
            && entry.Progress.GetValueOrDefault(objectiveKey) >= Math.Min(amount, entry.Objectives[objectiveKey]));
    }
}

/// <summary>
/// Handles <see cref="GetNpcDialogueHubCommand"/> by reading the conversation a placed person offers, with every
/// option already evaluated against this character.
/// </summary>
public class GetNpcDialogueHubCommandHandler(
    ICharacterRepository characterRepo,
    ApplicationDbContext db,
    ContentDbContext content,
    IDialogueRepository dialogues,
    ILogger<GetNpcDialogueHubCommandHandler> logger)
    : IRequestHandler<GetNpcDialogueHubCommand, NpcDialogueResult>
{
    /// <inheritdoc />
    public async Task<NpcDialogueResult> Handle(
        GetNpcDialogueHubCommand request,
        CancellationToken cancellationToken)
    {
        var slug = request.NpcSlug.Trim();
        if (slug.Length == 0)
            return Fail("An NPC slug is required.");

        var character = await characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (character is null)
            return Fail("No character to speak with.");

        var placement = await NpcDialogueReader.FindPlacementAsync(db, character, slug, cancellationToken);
        if (placement is null)
        {
            logger.LogDebug(
                "Character {CharacterId} tried to read '{Slug}', who is not at {Zone}/{Location}",
                request.CharacterId, slug, character.CurrentZoneId, character.CurrentZoneLocationSlug);
            return Fail($"There is nobody called {slug} here.");
        }

        var archetype = await NpcDialogueReader.FindArchetypeAsync(content, slug, cancellationToken);
        var view = NpcPlacements.ToDto(placement, archetype);
        var prompt = await NpcGreetings.PickAsync(dialogues, slug, view.DisplayName, cancellationToken);

        var entry = await NpcDialogueReader.PickEntryAsync(content, slug, request.EntrySlug, cancellationToken);
        if (entry is null)
        {
            // Somebody with lines but no authored conversation still answers, and the panel says there is nothing
            // more to say rather than showing an empty list or an error.
            return new NpcDialogueResult
            {
                Success = true,
                Dialogue = new NpcDialogueDto(
                    slug,
                    view.DisplayName,
                    view.Occupation,
                    string.Empty,
                    prompt,
                    false,
                    []),
            };
        }

        return new NpcDialogueResult
        {
            Success = true,
            Dialogue = await NpcDialogueReader.BuildAsync(db, entry, view, prompt, character, cancellationToken),
        };
    }

    private static NpcDialogueResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}

/// <summary>
/// Handles <see cref="ChooseNpcOptionHubCommand"/> by re-validating the choice against the character's live state and
/// then applying what the option grants.
/// </summary>
/// <remarks>
/// Ordering is deliberate. A one-shot option is claimed in
/// <see cref="CharacterDialogueChoice"/> <em>before</em> anything is granted, so a double click is rejected by the
/// key rather than paying twice; the claim is the same conditional-write idea the chest opening uses. Quest
/// enrolment goes through <see cref="AcceptQuestHubCommand"/> and gold through <see cref="AddGoldHubCommand"/>
/// because those own the journal and the attributes key respectively.
/// </remarks>
public class ChooseNpcOptionHubCommandHandler(
    ICharacterRepository characterRepo,
    ApplicationDbContext db,
    ContentDbContext content,
    ISender sender,
    ILogger<ChooseNpcOptionHubCommandHandler> logger)
    : IRequestHandler<ChooseNpcOptionHubCommand, ChooseNpcOptionResult>
{
    /// <inheritdoc />
    public async Task<ChooseNpcOptionResult> Handle(
        ChooseNpcOptionHubCommand request,
        CancellationToken cancellationToken)
    {
        var slug = request.NpcSlug.Trim();
        if (slug.Length == 0)
            return Fail("An NPC slug is required.");

        if (string.IsNullOrWhiteSpace(request.OptionKey))
            return Fail("An option key is required.");

        var character = await characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (character is null)
            return Fail("No character to speak with.");

        var placement = await NpcDialogueReader.FindPlacementAsync(db, character, slug, cancellationToken);
        if (placement is null)
            return Fail($"There is nobody called {slug} here.");

        var archetype = await NpcDialogueReader.FindArchetypeAsync(content, slug, cancellationToken);
        var view = NpcPlacements.ToDto(placement, archetype);

        var entry = await NpcDialogueReader.PickEntryAsync(content, slug, request.EntrySlug, cancellationToken);
        var option = entry?.Stats.Options.FirstOrDefault(candidate =>
            string.Equals(candidate.Key, request.OptionKey, StringComparison.OrdinalIgnoreCase));

        if (entry is null || option is null)
        {
            // The conversation changed under the client: say so instead of pretending the choice worked.
            return Fail("That is not something they say any more.");
        }

        var taken = await NpcDialogueReader.ChosenKeysAsync(db, character.Id, slug, cancellationToken);
        var journal = QuestJournal.Parse(character.QuestBlob);

        var blocked = NpcDialogueReader.BlockedReason(option, character, journal, taken);
        if (blocked is not null)
            return Fail(blocked);

        var oneShot = NpcDialogueReader.IsOneShot(option);
        if (oneShot && !await ClaimAsync(character.Id, slug, option.Key, cancellationToken))
            return Fail("You have already spoken about that.");

        var applied = await ApplyGrantsAsync(character, slug, option, cancellationToken);

        logger.LogInformation(
            "Character {CharacterId} chose '{OptionKey}' from {NpcId} at {Zone}/{Location}: {Applied}",
            character.Id, option.Key, placement.NpcId, placement.ZoneId, placement.LocationSlug,
            applied.Count == 0 ? "nothing" : string.Join(", ", applied));

        NpcDialogueDto? next = null;
        if (!string.IsNullOrWhiteSpace(option.NextSlug))
        {
            var chained = await NpcDialogueReader.PickEntryAsync(content, slug, option.NextSlug, cancellationToken);
            if (chained is not null)
            {
                // Reload: the grants above may have written the journal and the bag, and the chained entry must show
                // the requirements evaluated against the character as they are now, not as they were.
                var after = await characterRepo.GetByIdAsync(character.Id, cancellationToken) ?? character;
                next = await NpcDialogueReader.BuildAsync(db, chained, view, option.Response, after, cancellationToken);
            }
            else
            {
                logger.LogWarning(
                    "Dialogue option {OptionKey} on {Entry} continues into '{Next}', which is missing",
                    option.Key, entry.Slug, option.NextSlug);
            }
        }

        return new ChooseNpcOptionResult
        {
            Success = true,
            SpeakerSlug = slug,
            DisplayName = view.DisplayName,
            Response = option.Response,
            Applied = applied,
            Next = next,
            Ended = next is null,
        };
    }

    /// <summary>Records a one-shot choice, returning false when it was already taken.</summary>
    private async Task<bool> ClaimAsync(
        Guid characterId,
        string speakerSlug,
        string optionKey,
        CancellationToken cancellationToken)
    {
        db.CharacterDialogueChoices.Add(new CharacterDialogueChoice
        {
            CharacterId = characterId,
            SpeakerSlug = speakerSlug,
            OptionKey = optionKey,
            ChosenAt = DateTimeOffset.UtcNow,
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex)
        {
            // The composite key is the guard: two clicks that raced each other land here, and the second pays nothing.
            db.Entry(db.CharacterDialogueChoices.Local.First(choice =>
                choice.CharacterId == characterId
                && choice.SpeakerSlug == speakerSlug
                && choice.OptionKey == optionKey)).State = EntityState.Detached;

            logger.LogDebug(ex, "Duplicate one-shot claim for {CharacterId}/{Speaker}/{Option}",
                characterId, speakerSlug, optionKey);

            return false;
        }
    }

    /// <summary>Applies what the option grants, in the order that keeps the single writers authoritative.</summary>
    private async Task<IReadOnlyList<string>> ApplyGrantsAsync(
        Character character,
        string speakerSlug,
        DialogueOption option,
        CancellationToken cancellationToken)
    {
        var applied = new List<string>();
        var effects = new List<DialogueEffect>();

        foreach (var entry in DialogueOptionGrammar.Split(option.Grants))
        {
            if (DialogueOptionGrammar.TryParseGrant(entry, out var effect, out var error))
            {
                effects.Add(effect);
            }
            else
            {
                logger.LogWarning("Dialogue option {Option} grants an unreadable entry '{Entry}': {Error}",
                    option.Key, entry, error);
            }
        }

        var journal = QuestJournal.Parse(character.QuestBlob);
        var bag = CharacterBag.Read(character.InventoryBlob);
        var changed = false;

        // Quest enrolment first: it reloads and saves the character, so anything held in memory has to be written
        // after it rather than before.
        foreach (var effect in effects.Where(effect => effect.Kind == DialogueEffectKind.QuestStart))
        {
            if (journal.Any(entry => string.Equals(entry.Slug, effect.Key, StringComparison.OrdinalIgnoreCase)))
                continue;

            var accept = await sender.Send(new AcceptQuestHubCommand(character.Id, effect.Key), cancellationToken);
            if (!accept.Success)
            {
                logger.LogWarning(
                    "Dialogue option {Option} could not enrol character {CharacterId} in {Quest}: {Error}",
                    option.Key, character.Id, effect.Key, accept.ErrorMessage);
                continue;
            }

            applied.Add(effect.Describe());
            // AcceptQuest saved the row; reload so the objective and item grants do not overwrite its journal write.
            character = await characterRepo.GetByIdAsync(character.Id, cancellationToken) ?? character;
            journal = QuestJournal.Parse(character.QuestBlob);
            changed = false;
        }

        foreach (var effect in effects)
        {
            switch (effect.Kind)
            {
                case DialogueEffectKind.Item:
                    CharacterBag.Add(bag, effect.Key, effect.Amount);
                    applied.Add(effect.Describe());
                    changed = true;
                    break;

                case DialogueEffectKind.Objective:
                    if (QuestJournal.Advance(journal, effect.Key, effect.Detail ?? string.Empty, effect.Amount) > 0)
                    {
                        applied.Add(effect.Describe());
                        changed = true;
                    }
                    break;
            }
        }

        if (changed)
        {
            character.InventoryBlob = CharacterBag.Serialize(bag);
            character.QuestBlob = QuestJournal.Serialize(journal);
            await characterRepo.UpdateAsync(character, cancellationToken);
        }

        // Gold last and through its single writer, which owns the attributes key and the clamp.
        foreach (var effect in effects.Where(effect => effect.Kind == DialogueEffectKind.Gold))
        {
            var gold = await sender.Send(
                new AddGoldHubCommand
                {
                    CharacterId = character.Id,
                    Amount = effect.Amount,
                    Source = $"Dialogue:{speakerSlug}:{option.Key}",
                },
                cancellationToken);

            if (gold.Success)
                applied.Add(effect.Describe());
        }

        return applied;
    }

    private static ChooseNpcOptionResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}
