using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RealmEngine.Shared.Abstractions;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Settings;

namespace Veldrath.Server.Features.Quest;

/// <summary>
/// Hub command that performs the tutorial handoff: place a character who has not finished the tutorial on the
/// authored tutorial start location and put the tutorial quest in their journal.
/// </summary>
/// <param name="CharacterId">Character logging in.</param>
public record PrepareTutorialHubCommand(Guid CharacterId) : IRequest<PrepareTutorialHubResult>;

/// <summary>Result returned by <see cref="PrepareTutorialHubCommandHandler"/>.</summary>
public record PrepareTutorialHubResult
{
    /// <summary>Gets a value indicating whether the handoff ran without a hard failure.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets whether there was nothing to do — the tutorial is finished or already started.</summary>
    public bool Skipped { get; init; }

    /// <summary>Gets whether the character was moved and the quest accepted.</summary>
    public bool Placed { get; init; }

    /// <summary>Gets the zone the character was placed in.</summary>
    public string? ZoneId { get; init; }

    /// <summary>Gets the location slug the character was placed at.</summary>
    public string? LocationSlug { get; init; }

    /// <summary>Gets the display name of that location.</summary>
    public string? LocationDisplayName { get; init; }

    /// <summary>Gets the tutorial quest slug involved, when known.</summary>
    public string? QuestSlug { get; init; }

    /// <summary>Gets the client-facing explanation when the handoff degraded.</summary>
    public string? Warning { get; init; }
}

/// <summary>
/// Handles <see cref="PrepareTutorialHubCommand"/>. The tutorial counts as finished once its quest is turned in, so
/// completion is read from the journal rather than duplicated into the attribute blob — one source of truth, and no
/// way for the two to disagree.
/// </summary>
public class PrepareTutorialHubCommandHandler(
    ICharacterRepository characterRepo,
    IZoneLocationRepository zoneLocations,
    ISender mediator,
    IOptions<TutorialOptions> options,
    ILogger<PrepareTutorialHubCommandHandler> logger)
    : IRequestHandler<PrepareTutorialHubCommand, PrepareTutorialHubResult>
{
    /// <summary>Attributes-blob key marking that the character has already been handed to the tutorial.</summary>
    public const string StartedAttributeKey = "TutorialStarted";

    /// <inheritdoc />
    public async Task<PrepareTutorialHubResult> Handle(
        PrepareTutorialHubCommand request,
        CancellationToken cancellationToken)
    {
        var slug = options.Value.TutorialQuestSlug;

        var character = await characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (character is null)
            return new PrepareTutorialHubResult
            {
                Success = false,
                ErrorMessage = $"Character {request.CharacterId} not found.",
            };

        // A hardcore character that is already gone has nothing to onboard.
        if (character.DeletedAt.HasValue)
            return new PrepareTutorialHubResult { Success = true, Skipped = true, QuestSlug = slug };

        var journal = QuestJournal.Parse(character.QuestBlob);
        var entry = journal.FirstOrDefault(candidate =>
            string.Equals(candidate.Slug, slug, StringComparison.OrdinalIgnoreCase));

        if (entry is not null && string.Equals(entry.Status, CharacterQuestEntry.StatusCompleted, StringComparison.OrdinalIgnoreCase))
            return new PrepareTutorialHubResult { Success = true, Skipped = true, QuestSlug = slug };

        var attrs = ParseAttrs(character.Attributes);
        if (attrs.ContainsKey(StartedAttributeKey))
            return new PrepareTutorialHubResult { Success = true, Skipped = true, QuestSlug = slug };

        var start = (await zoneLocations.GetAllAsync()).FirstOrDefault(location => location.IsTutorialStart);
        if (start is null)
        {
            logger.LogError(
                "No zone location is flagged IsTutorialStart; character {CharacterIdPrefix} starts at the normal entry point",
                character.Id.ToString()[..8]);
            return new PrepareTutorialHubResult
            {
                Success = true,
                QuestSlug = slug,
                Warning = "The tutorial has not been set up yet, so you begin at the usual place.",
            };
        }

        // Accept first: if the quest content is missing there is no point moving the player somewhere for it.
        var accept = await mediator.Send(new AcceptQuestHubCommand(character.Id, slug), cancellationToken);
        if (!accept.Success)
        {
            logger.LogError(
                "Tutorial quest '{Slug}' could not be accepted for character {CharacterIdPrefix}: {Reason}",
                slug, character.Id.ToString()[..8], accept.ErrorMessage);
            return new PrepareTutorialHubResult
            {
                Success = true,
                QuestSlug = slug,
                Warning = "The tutorial quest is unavailable right now, so you begin at the usual place.",
            };
        }

        character.CurrentZoneId = start.ZoneId;
        character.CurrentZoneLocationSlug = start.Slug;
        attrs[StartedAttributeKey] = 1;
        character.Attributes = JsonSerializer.Serialize(attrs);
        await characterRepo.UpdateAsync(character, cancellationToken);

        logger.LogInformation(
            "Character {CharacterIdPrefix} started the tutorial at {LocationSlug} with quest '{Slug}'",
            character.Id.ToString()[..8], start.Slug, slug);

        return new PrepareTutorialHubResult
        {
            Success = true,
            Placed = true,
            ZoneId = start.ZoneId,
            LocationSlug = start.Slug,
            LocationDisplayName = start.DisplayName,
            QuestSlug = slug,
        };
    }

    /// <summary>Reads the attribute blob, treating a malformed blob as empty rather than failing the login.</summary>
    /// <param name="blob">Raw attributes blob.</param>
    /// <returns>The parsed attributes.</returns>
    private static Dictionary<string, int> ParseAttrs(string? blob)
    {
        var attrs = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(blob) || blob == "{}") return attrs;

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, int>>(blob) ?? attrs;
        }
        catch (JsonException)
        {
            return attrs;
        }
    }
}
