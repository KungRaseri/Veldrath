using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using Veldrath.Server.Data.Repositories;

namespace Veldrath.Server.Features.Quest;

/// <summary>Hub command that accepts a catalog quest into the character's journal.</summary>
/// <param name="CharacterId">Character accepting the quest.</param>
/// <param name="Slug">Quest slug.</param>
public record AcceptQuestHubCommand(Guid CharacterId, string Slug) : IRequest<AcceptQuestHubResult>;

/// <summary>Result returned by <see cref="AcceptQuestHubCommandHandler"/>.</summary>
public record AcceptQuestHubResult
{
    /// <summary>Gets a value indicating whether the quest was accepted.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the journal after the change.</summary>
    public IReadOnlyList<QuestLogEntryDto> Quests { get; init; } = [];
}

/// <summary>Hub command that abandons an active quest.</summary>
/// <param name="CharacterId">Character abandoning the quest.</param>
/// <param name="Slug">Quest slug.</param>
public record AbandonQuestHubCommand(Guid CharacterId, string Slug) : IRequest<AbandonQuestHubResult>;

/// <summary>Result returned by <see cref="AbandonQuestHubCommandHandler"/>.</summary>
public record AbandonQuestHubResult
{
    /// <summary>Gets a value indicating whether the quest was abandoned.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the journal after the change.</summary>
    public IReadOnlyList<QuestLogEntryDto> Quests { get; init; } = [];
}

/// <summary>Hub command that turns in a completed quest.</summary>
/// <param name="CharacterId">Character turning the quest in.</param>
/// <param name="Slug">Quest slug.</param>
public record TurnInQuestHubCommand(Guid CharacterId, string Slug) : IRequest<TurnInQuestHubResult>;

/// <summary>Result returned by <see cref="TurnInQuestHubCommandHandler"/>.</summary>
public record TurnInQuestHubResult
{
    /// <summary>Gets a value indicating whether the quest was turned in.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the quest slug that was turned in.</summary>
    public string Slug { get; init; } = string.Empty;

    /// <summary>Gets the gold reward to grant.</summary>
    public int GoldReward { get; init; }

    /// <summary>Gets the experience reward to grant.</summary>
    public int XpReward { get; init; }

    /// <summary>Gets the journal after the change.</summary>
    public IReadOnlyList<QuestLogEntryDto> Quests { get; init; } = [];
}

/// <summary>Hub command that advances objectives from a game event.</summary>
/// <param name="CharacterId">Character whose journal to update.</param>
/// <param name="Kind">Objective kind (<c>kill</c>, <c>collect</c>, <c>visit</c>).</param>
/// <param name="Key">Enemy, item or location identifier.</param>
/// <param name="Amount">Progress to add (default 1).</param>
public record AdvanceQuestObjectivesHubCommand(Guid CharacterId, string Kind, string Key, int Amount = 1)
    : IRequest<AdvanceQuestObjectivesHubResult>;

/// <summary>Result returned by <see cref="AdvanceQuestObjectivesHubCommandHandler"/>.</summary>
public record AdvanceQuestObjectivesHubResult
{
    /// <summary>Gets a value indicating whether the journal was updated.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets how many quests changed.</summary>
    public int QuestsChanged { get; init; }

    /// <summary>Gets the journal after the change.</summary>
    public IReadOnlyList<QuestLogEntryDto> Quests { get; init; } = [];
}

/// <summary>
/// Projects journal entries into the client DTO used by every quest command result.
/// </summary>
internal static class QuestProjection
{
    internal static IReadOnlyList<QuestLogEntryDto> ToDtos(IEnumerable<CharacterQuestEntry> entries) =>
        entries
            .Select(entry => new QuestLogEntryDto(
                entry.Slug,
                entry.Title,
                entry.Description,
                entry.Status,
                entry.GoldReward,
                entry.XpReward,
                entry.Status == CharacterQuestEntry.StatusActive && entry.IsComplete,
                entry.Objectives
                    .Select(objective => new QuestObjectiveDto(
                        objective.Key,
                        Math.Min(objective.Value, entry.Progress.GetValueOrDefault(objective.Key)),
                        objective.Value,
                        entry.Progress.GetValueOrDefault(objective.Key) >= objective.Value))
                    .ToList()))
            .ToList();
}

/// <summary>
/// Handles <see cref="AcceptQuestHubCommand"/> by copying a catalog quest into the journal with empty
/// objective progress.
/// </summary>
public class AcceptQuestHubCommandHandler(
    ICharacterRepository characterRepo,
    IQuestRepository questRepository,
    ILogger<AcceptQuestHubCommandHandler> logger)
    : IRequestHandler<AcceptQuestHubCommand, AcceptQuestHubResult>
{
    /// <inheritdoc />
    public async Task<AcceptQuestHubResult> Handle(
        AcceptQuestHubCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Slug))
            return new AcceptQuestHubResult { Success = false, ErrorMessage = "A quest slug is required." };

        var character = await characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (character is null)
            return new AcceptQuestHubResult
            {
                Success = false,
                ErrorMessage = $"Character {request.CharacterId} not found.",
            };

        var journal = QuestJournal.Parse(character.QuestBlob);
        if (journal.Any(entry => string.Equals(entry.Slug, request.Slug, StringComparison.OrdinalIgnoreCase)))
            return new AcceptQuestHubResult
            {
                Success = false,
                ErrorMessage = "That quest is already in your journal.",
            };

        RealmEngine.Shared.Models.Quest? quest;
        try
        {
            quest = await questRepository.GetBySlugAsync(request.Slug);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to load quest '{Slug}' from the catalog.", request.Slug);
            quest = null;
        }

        if (quest is null)
            return new AcceptQuestHubResult
            {
                Success = false,
                ErrorMessage = $"Quest '{request.Slug}' does not exist.",
            };

        journal.Add(new CharacterQuestEntry(
            quest.Slug,
            string.IsNullOrWhiteSpace(quest.Title) ? quest.Slug : quest.Title,
            quest.Description,
            CharacterQuestEntry.StatusActive,
            quest.GoldReward > 0 ? quest.GoldReward : quest.BaseGoldReward,
            quest.XpReward > 0 ? quest.XpReward : quest.BaseXpReward,
            new Dictionary<string, int>(quest.Objectives, StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            DateTimeOffset.UtcNow));

        character.QuestBlob = QuestJournal.Serialize(journal);
        await characterRepo.UpdateAsync(character, cancellationToken);

        logger.LogInformation(
            "Character {CharacterId} accepted quest '{Slug}'",
            character.Id,
            request.Slug);

        return new AcceptQuestHubResult { Success = true, Quests = QuestProjection.ToDtos(journal) };
    }
}

/// <summary>
/// Handles <see cref="AbandonQuestHubCommand"/> by removing an active quest from the journal.
/// </summary>
public class AbandonQuestHubCommandHandler(
    ICharacterRepository characterRepo,
    ILogger<AbandonQuestHubCommandHandler> logger)
    : IRequestHandler<AbandonQuestHubCommand, AbandonQuestHubResult>
{
    /// <inheritdoc />
    public async Task<AbandonQuestHubResult> Handle(
        AbandonQuestHubCommand request,
        CancellationToken cancellationToken)
    {
        var character = await characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (character is null)
            return new AbandonQuestHubResult
            {
                Success = false,
                ErrorMessage = $"Character {request.CharacterId} not found.",
            };

        var journal = QuestJournal.Parse(character.QuestBlob);
        var removed = journal.RemoveAll(entry =>
            string.Equals(entry.Slug, request.Slug, StringComparison.OrdinalIgnoreCase)
            && entry.Status == CharacterQuestEntry.StatusActive);

        if (removed == 0)
            return new AbandonQuestHubResult
            {
                Success = false,
                ErrorMessage = "That quest is not active in your journal.",
            };

        character.QuestBlob = QuestJournal.Serialize(journal);
        await characterRepo.UpdateAsync(character, cancellationToken);

        logger.LogInformation(
            "Character {CharacterId} abandoned quest '{Slug}'",
            character.Id,
            request.Slug);

        return new AbandonQuestHubResult { Success = true, Quests = QuestProjection.ToDtos(journal) };
    }
}

/// <summary>
/// Handles <see cref="TurnInQuestHubCommand"/> by marking a completed quest as turned in and returning
/// the rewards for the hub to grant through the existing gold/experience commands.
/// </summary>
public class TurnInQuestHubCommandHandler(
    ICharacterRepository characterRepo,
    ILogger<TurnInQuestHubCommandHandler> logger)
    : IRequestHandler<TurnInQuestHubCommand, TurnInQuestHubResult>
{
    /// <inheritdoc />
    public async Task<TurnInQuestHubResult> Handle(
        TurnInQuestHubCommand request,
        CancellationToken cancellationToken)
    {
        var character = await characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (character is null)
            return new TurnInQuestHubResult
            {
                Success = false,
                ErrorMessage = $"Character {request.CharacterId} not found.",
            };

        var journal = QuestJournal.Parse(character.QuestBlob);
        var index = journal.FindIndex(entry =>
            string.Equals(entry.Slug, request.Slug, StringComparison.OrdinalIgnoreCase));

        if (index < 0)
            return new TurnInQuestHubResult
            {
                Success = false,
                ErrorMessage = "That quest is not in your journal.",
            };

        var entry = journal[index];
        if (entry.Status != CharacterQuestEntry.StatusActive)
            return new TurnInQuestHubResult
            {
                Success = false,
                ErrorMessage = "That quest has already been turned in.",
            };

        if (!entry.IsComplete)
            return new TurnInQuestHubResult
            {
                Success = false,
                ErrorMessage = "Not every objective is complete yet.",
            };

        journal[index] = entry with { Status = CharacterQuestEntry.StatusCompleted };
        character.QuestBlob = QuestJournal.Serialize(journal);
        await characterRepo.UpdateAsync(character, cancellationToken);

        logger.LogInformation(
            "Character {CharacterId} turned in quest '{Slug}' (+{Gold} gold, +{Xp} xp)",
            character.Id,
            entry.Slug,
            entry.GoldReward,
            entry.XpReward);

        return new TurnInQuestHubResult
        {
            Success = true,
            Slug = entry.Slug,
            GoldReward = entry.GoldReward,
            XpReward = entry.XpReward,
            Quests = QuestProjection.ToDtos(journal),
        };
    }
}

/// <summary>
/// Handles <see cref="AdvanceQuestObjectivesHubCommand"/> by matching an event against the objective keys
/// of every active quest.
/// </summary>
public class AdvanceQuestObjectivesHubCommandHandler(
    ICharacterRepository characterRepo,
    ILogger<AdvanceQuestObjectivesHubCommandHandler> logger)
    : IRequestHandler<AdvanceQuestObjectivesHubCommand, AdvanceQuestObjectivesHubResult>
{
    /// <inheritdoc />
    public async Task<AdvanceQuestObjectivesHubResult> Handle(
        AdvanceQuestObjectivesHubCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Kind) || string.IsNullOrWhiteSpace(request.Key))
            return new AdvanceQuestObjectivesHubResult { Success = false, ErrorMessage = "Kind and key are required." };

        var character = await characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (character is null)
            return new AdvanceQuestObjectivesHubResult
            {
                Success = false,
                ErrorMessage = $"Character {request.CharacterId} not found.",
            };

        var journal = QuestJournal.Parse(character.QuestBlob);
        var changed = QuestJournal.Advance(journal, request.Kind, request.Key, request.Amount);
        if (changed == 0)
            return new AdvanceQuestObjectivesHubResult { Success = true, Quests = QuestProjection.ToDtos(journal) };

        character.QuestBlob = QuestJournal.Serialize(journal);
        await characterRepo.UpdateAsync(character, cancellationToken);

        logger.LogInformation(
            "Character {CharacterId} advanced {Count} quest(s) via {Kind}:{Key}",
            character.Id,
            changed,
            request.Kind,
            request.Key);

        return new AdvanceQuestObjectivesHubResult
        {
            Success = true,
            QuestsChanged = changed,
            Quests = QuestProjection.ToDtos(journal),
        };
    }
}
