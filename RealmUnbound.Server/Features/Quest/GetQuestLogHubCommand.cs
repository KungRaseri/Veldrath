using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmUnbound.Server.Data.Repositories;

namespace RealmUnbound.Server.Features.Quest;

/// <summary>A slim quest entry returned to the client in the journal panel.</summary>
/// <param name="Slug">The quest's unique identifier slug.</param>
/// <param name="Title">The human-readable quest title.</param>
/// <param name="Status">The quest status: <c>"Active"</c>, <c>"Completed"</c>, or <c>"Failed"</c>.</param>
public record QuestLogEntryDto(string Slug, string Title, string Status);

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

    /// <summary>Gets all quest log entries for the character.</summary>
    public IReadOnlyList<QuestLogEntryDto> Quests { get; init; } = [];
}

/// <summary>
/// Handles <see cref="GetQuestLogHubCommand"/> by looking up the character's save game via
/// <see cref="ISaveGameRepository"/> and projecting its quests into <see cref="QuestLogEntryDto"/> entries.
/// Active, completed, and failed quests are all included.
/// </summary>
public class GetQuestLogHubCommandHandler : IRequestHandler<GetQuestLogHubCommand, GetQuestLogHubResult>
{
    private readonly ICharacterRepository _characterRepo;
    private readonly ISaveGameRepository _saveGameRepo;
    private readonly ILogger<GetQuestLogHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="GetQuestLogHubCommandHandler"/>.</summary>
    /// <param name="characterRepo">Repository used to resolve the character's name.</param>
    /// <param name="saveGameRepo">Repository used to load the character's save game.</param>
    /// <param name="logger">Logger instance.</param>
    public GetQuestLogHubCommandHandler(
        ICharacterRepository characterRepo,
        ISaveGameRepository saveGameRepo,
        ILogger<GetQuestLogHubCommandHandler> logger)
    {
        _characterRepo = characterRepo;
        _saveGameRepo  = saveGameRepo;
        _logger        = logger;
    }

    /// <summary>Handles the command and returns the quest log.</summary>
    /// <param name="request">The command containing the character ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="GetQuestLogHubResult"/> containing the quest entries.</returns>
    public async Task<GetQuestLogHubResult> Handle(GetQuestLogHubCommand request, CancellationToken cancellationToken)
    {
        var character = await _characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (character is null)
            return new GetQuestLogHubResult { Success = false, ErrorMessage = $"Character {request.CharacterId} not found." };

        var saves   = _saveGameRepo.GetByPlayerName(character.Name);
        var saveGame = saves.FirstOrDefault();

        if (saveGame is null)
        {
            _logger.LogInformation("No save game found for character '{Name}' — returning empty quest log.", character.Name);
            return new GetQuestLogHubResult { Success = true, Quests = [] };
        }

        var quests = saveGame.ActiveQuests.Select(q => new QuestLogEntryDto(q.Slug, q.Title, "Active"))
            .Concat(saveGame.CompletedQuests.Select(q => new QuestLogEntryDto(q.Slug, q.Title, "Completed")))
            .Concat(saveGame.FailedQuests.Select(q => new QuestLogEntryDto(q.Slug, q.Title, "Failed")))
            .ToList();

        _logger.LogInformation(
            "Quest log for character '{Name}': {Active} active, {Completed} completed, {Failed} failed.",
            character.Name,
            saveGame.ActiveQuests.Count,
            saveGame.CompletedQuests.Count,
            saveGame.FailedQuests.Count);

        return new GetQuestLogHubResult { Success = true, Quests = quests };
    }
}
