using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Features.CharacterCreation.Queries;
using RealmEngine.Core.Features.Exploration.Queries;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using SpeciesModel = RealmEngine.Shared.Models.Species;

namespace RealmEngine.Core.Features.CharacterCreation.Commands;

/// <summary>
/// Begins a new character creation wizard session and returns all data needed to populate the UI.
/// </summary>
public record BeginCreationSessionCommand : IRequest<BeginCreationSessionResult>;

/// <summary>
/// Result of beginning a character creation session.
/// </summary>
public record BeginCreationSessionResult
{
    /// <summary>Gets the session identifier to use in subsequent session commands.</summary>
    public Guid SessionId { get; init; }

    /// <summary>Gets a value indicating whether the session was created successfully.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the point-buy configuration used for attribute allocation.</summary>
    public PointBuyConfig PointBuyConfig { get; init; } = new();

    /// <summary>Gets the list of available character classes.</summary>
    public List<CharacterClass> AvailableClasses { get; init; } = [];

    /// <summary>Gets the list of available playable species.</summary>
    public List<SpeciesModel> AvailableSpecies { get; init; } = [];

    /// <summary>Gets the list of available backgrounds.</summary>
    public List<Background> AvailableBackgrounds { get; init; } = [];

    /// <summary>Gets the list of available starting locations.</summary>
    public List<Location> AvailableLocations { get; init; } = [];
}

/// <summary>
/// Handles <see cref="BeginCreationSessionCommand"/>.
/// </summary>
public class BeginCreationSessionHandler(
    ICharacterCreationSessionStore sessionStore,
    ISpeciesRepository speciesRepository,
    IBackgroundRepository backgroundRepository,
    IMediator mediator,
    ILogger<BeginCreationSessionHandler> logger)
    : IRequestHandler<BeginCreationSessionCommand, BeginCreationSessionResult>
{
    /// <inheritdoc />
    public async Task<BeginCreationSessionResult> Handle(BeginCreationSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await sessionStore.CreateSessionAsync();
        logger.LogInformation("Started character creation session {SessionId}", session.SessionId);

        // All four repositories ultimately share the same scoped ContentDbContext for this MediatR pipeline
        // invocation (EfCoreCharacterClassRepository, EfCoreSpeciesRepository, EfCoreBackgroundRepository).
        // GetStartingLocationsHandler uses IDbContextFactory so it is independent, but awaiting sequentially
        // keeps the code simple and avoids any future regressions if dependencies change.
        GetAvailableClassesResult classesResult;
        List<Location> locationsResult;
        List<SpeciesModel> species;
        List<Background> backgrounds;
        try
        {
            classesResult   = await mediator.Send(new GetAvailableClassesQuery(), cancellationToken);
            locationsResult = await mediator.Send(new GetStartingLocationsQuery(BackgroundId: null, FilterByRecommended: false), cancellationToken);
            species         = await speciesRepository.GetAllSpeciesAsync();
            backgrounds     = await backgroundRepository.GetAllBackgroundsAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load session content for {SessionId}", session.SessionId);
            return new BeginCreationSessionResult { SessionId = session.SessionId, Success = false };
        }

        if (!classesResult.Success)
        {
            logger.LogError("Failed to load available classes for {SessionId}: {Error}", session.SessionId, classesResult.ErrorMessage);
            return new BeginCreationSessionResult { SessionId = session.SessionId, Success = false };
        }

        return new BeginCreationSessionResult
        {
            SessionId            = session.SessionId,
            Success              = true,
            PointBuyConfig       = new PointBuyConfig(),
            AvailableClasses     = classesResult.Classes,
            AvailableSpecies     = species,
            AvailableBackgrounds = backgrounds,
            AvailableLocations   = locationsResult,
        };
    }
}
