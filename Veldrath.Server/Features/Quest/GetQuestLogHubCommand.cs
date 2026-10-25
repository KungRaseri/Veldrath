using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using Veldrath.Server.Data.Repositories;

namespace Veldrath.Server.Features.Quest;

/// <summary>One objective row in the journal.</summary>
/// <param name="Key">Objective key (e.g. <c>kill:barrow-wolf</c>).</param>
/// <param name="Current">Progress so far.</param>
/// <param name="Required">Required amount.</param>
/// <param name="Complete">Whether the objective is satisfied.</param>
public record QuestObjectiveDto(string Key, int Current, int Required, bool Complete);

/// <summary>A quest the character has accepted.</summary>
/// <param name="Slug">Quest slug.</param>
/// <param name="Title">Display title.</param>
/// <param name="Description">Objective description.</param>
/// <param name="Status"><c>Active</c> or <c>Completed</c>.</param>
/// <param name="GoldReward">Gold granted on turn-in.</param>
/// <param name="XpReward">Experience granted on turn-in.</param>
/// <param name="CanTurnIn">Whether every objective is complete and the quest is still active.</param>
/// <param name="Objectives">Objective progress rows.</param>
public record QuestLogEntryDto(
    string Slug,
    string Title,
    string Description,
    string Status,
    int GoldReward,
    int XpReward,
    bool CanTurnIn,
    IReadOnlyList<QuestObjectiveDto> Objectives);

/// <summary>A catalog quest the character has not taken yet.</summary>
/// <param name="Slug">Quest slug.</param>
/// <param name="Title">Display title.</param>
/// <param name="Description">Objective description.</param>
/// <param name="GoldReward">Gold granted on turn-in.</param>
/// <param name="XpReward">Experience granted on turn-in.</param>
/// <param name="Objectives">Objective keys offered by the quest.</param>
public record AvailableQuestDto(
    string Slug,
    string Title,
    string Description,
    int GoldReward,
    int XpReward,
    IReadOnlyList<string> Objectives);

/// <summary>Hub command that retrieves the quest log for a character.</summary>
/// <param name="CharacterId">The character whose journal to load.</param>
public record GetQuestLogHubCommand(Guid CharacterId) : IRequest<GetQuestLogHubResult>;

/// <summary>Result returned by <see cref="GetQuestLogHubCommandHandler"/>.</summary>
public record GetQuestLogHubResult
{
    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the accepted quests (active and completed) with objective progress.</summary>
    public IReadOnlyList<QuestLogEntryDto> Quests { get; init; } = [];

    /// <summary>Gets catalog quests the character can still accept.</summary>
    public IReadOnlyList<AvailableQuestDto> Available { get; init; } = [];
}

/// <summary>
/// Handles <see cref="GetQuestLogHubCommand"/> by reading the character's quest journal blob and
/// pairing it with the catalog's available quests.
/// </summary>
public class GetQuestLogHubCommandHandler(
    ICharacterRepository characterRepo,
    IQuestRepository questRepository,
    ILogger<GetQuestLogHubCommandHandler> logger)
    : IRequestHandler<GetQuestLogHubCommand, GetQuestLogHubResult>
{
    /// <summary>How many untaken catalog quests to offer at once.</summary>
    internal const int MaxAvailable = 8;

    /// <inheritdoc />
    public async Task<GetQuestLogHubResult> Handle(
        GetQuestLogHubCommand request,
        CancellationToken cancellationToken)
    {
        var character = await characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (character is null)
            return new GetQuestLogHubResult
            {
                Success = false,
                ErrorMessage = $"Character {request.CharacterId} not found.",
            };

        var journal = QuestJournal.Parse(character.QuestBlob);

        var quests = journal
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

        var takenSlugs = journal.Select(entry => entry.Slug).ToHashSet(StringComparer.OrdinalIgnoreCase);

        List<AvailableQuestDto> available;
        try
        {
            var catalog = await questRepository.GetAllAsync();
            available = catalog
                .Where(quest => !string.IsNullOrWhiteSpace(quest.Slug) && !takenSlugs.Contains(quest.Slug))
                .OrderBy(quest => quest.Title)
                .Take(MaxAvailable)
                .Select(quest => new AvailableQuestDto(
                    quest.Slug,
                    string.IsNullOrWhiteSpace(quest.Title) ? quest.Slug : quest.Title,
                    quest.Description,
                    quest.GoldReward > 0 ? quest.GoldReward : quest.BaseGoldReward,
                    quest.XpReward > 0 ? quest.XpReward : quest.BaseXpReward,
                    quest.Objectives.Keys.ToList()))
                .ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to load the quest catalog; returning the journal only.");
            available = [];
        }

        logger.LogDebug(
            "Quest log for character {CharacterId}: {Count} journal entries, {Available} available",
            character.Id,
            quests.Count,
            available.Count);

        return new GetQuestLogHubResult
        {
            Success = true,
            Quests = quests,
            Available = available,
        };
    }
}
