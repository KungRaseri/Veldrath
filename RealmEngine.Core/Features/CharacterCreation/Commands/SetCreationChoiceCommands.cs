using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Features.CharacterCreation.Queries;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
namespace RealmEngine.Core.Features.CharacterCreation.Commands;

/// <summary>
/// Sets the character class choice on an in-progress creation session.
/// </summary>
public record SetCreationClassCommand(Guid SessionId, string ClassName) : IRequest<SetCreationChoiceResult>;

/// <summary>
/// Sets the character name on an in-progress creation session.
/// </summary>
public record SetCreationNameCommand(Guid SessionId, string CharacterName) : IRequest<SetCreationChoiceResult>;

/// <summary>
/// Sets the species choice on an in-progress creation session.
/// </summary>
public record SetCreationSpeciesCommand(Guid SessionId, string SpeciesSlug) : IRequest<SetCreationChoiceResult>;

/// <summary>
/// Sets the background choice on an in-progress creation session.
/// </summary>
public record SetCreationBackgroundCommand(Guid SessionId, string BackgroundId) : IRequest<SetCreationChoiceResult>;

/// <summary>
/// Sets the equipment preferences on an in-progress creation session.
/// </summary>
public record SetCreationEquipmentPreferencesCommand(
    Guid SessionId,
    string? PreferredArmorType,
    string? PreferredWeaponType,
    bool IncludeShield) : IRequest<SetCreationChoiceResult>;

/// <summary>
/// Sets the starting location choice on an in-progress creation session.
/// </summary>
public record SetCreationLocationCommand(Guid SessionId, string LocationId) : IRequest<SetCreationChoiceResult>;

/// <summary>
/// Shared result type for all single-step session mutation commands.
/// </summary>
public record SetCreationChoiceResult
{
    /// <summary>Gets a value indicating whether the update was applied.</summary>
    public bool Success { get; init; }

    /// <summary>Gets a message describing the result.</summary>
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Handles <see cref="SetCreationClassCommand"/>.
/// </summary>
public class SetCreationClassHandler(
    ICharacterCreationSessionStore sessionStore,
    IMediator mediator,
    ILogger<SetCreationClassHandler> logger)
    : IRequestHandler<SetCreationClassCommand, SetCreationChoiceResult>
{
    /// <inheritdoc />
    public async Task<SetCreationChoiceResult> Handle(SetCreationClassCommand request, CancellationToken cancellationToken)
    {
        var session = await sessionStore.GetSessionAsync(request.SessionId);
        if (session is null)
            return new SetCreationChoiceResult { Success = false, Message = $"Session {request.SessionId} not found." };
        if (session.Status != CreationSessionStatus.Draft)
            return new SetCreationChoiceResult { Success = false, Message = $"Session is already {session.Status} and cannot be modified." };

        var classResult = await mediator.Send(new GetCharacterClassQuery { ClassName = request.ClassName }, cancellationToken);
        if (!classResult.Found || classResult.CharacterClass is null)
            return new SetCreationChoiceResult { Success = false, Message = $"Class '{request.ClassName}' not found." };

        session.SelectedClass = classResult.CharacterClass;
        await sessionStore.UpdateSessionAsync(session);
        logger.LogDebug("Session {SessionId}: class set to {ClassName}", request.SessionId, request.ClassName);
        return new SetCreationChoiceResult { Success = true, Message = $"Class set to '{classResult.CharacterClass.Name}'." };
    }
}

/// <summary>
/// Handles <see cref="SetCreationNameCommand"/>.
/// </summary>
public class SetCreationNameHandler(
    ICharacterCreationSessionStore sessionStore,
    ILogger<SetCreationNameHandler> logger)
    : IRequestHandler<SetCreationNameCommand, SetCreationChoiceResult>
{
    /// <inheritdoc />
    public async Task<SetCreationChoiceResult> Handle(SetCreationNameCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CharacterName))
            return new SetCreationChoiceResult { Success = false, Message = "Character name cannot be empty." };

        var trimmed = request.CharacterName.Trim();
        if (trimmed.Length < 2)
            return new SetCreationChoiceResult { Success = false, Message = "Character name must be at least 2 characters." };
        if (trimmed.Length > 30)
            return new SetCreationChoiceResult { Success = false, Message = "Character name cannot exceed 30 characters." };
        if (!trimmed.All(char.IsLetter))
            return new SetCreationChoiceResult { Success = false, Message = "Character name may only contain letters." };

        var session = await sessionStore.GetSessionAsync(request.SessionId);
        if (session is null)
            return new SetCreationChoiceResult { Success = false, Message = $"Session {request.SessionId} not found." };
        if (session.Status != CreationSessionStatus.Draft)
            return new SetCreationChoiceResult { Success = false, Message = $"Session is already {session.Status} and cannot be modified." };

        session.CharacterName = trimmed;
        await sessionStore.UpdateSessionAsync(session);
        logger.LogDebug("Session {SessionId}: name set to '{Name}'", request.SessionId, trimmed);
        return new SetCreationChoiceResult { Success = true, Message = $"Character name set to '{trimmed}'." };
    }
}

/// <summary>
/// Handles <see cref="SetCreationSpeciesCommand"/>.
/// </summary>
public class SetCreationSpeciesHandler(
    ICharacterCreationSessionStore sessionStore,
    ISpeciesRepository speciesRepository,
    ILogger<SetCreationSpeciesHandler> logger)
    : IRequestHandler<SetCreationSpeciesCommand, SetCreationChoiceResult>
{
    /// <inheritdoc />
    public async Task<SetCreationChoiceResult> Handle(SetCreationSpeciesCommand request, CancellationToken cancellationToken)
    {
        var session = await sessionStore.GetSessionAsync(request.SessionId);
        if (session is null)
            return new SetCreationChoiceResult { Success = false, Message = $"Session {request.SessionId} not found." };
        if (session.Status != CreationSessionStatus.Draft)
            return new SetCreationChoiceResult { Success = false, Message = $"Session is already {session.Status} and cannot be modified." };

        var species = await speciesRepository.GetSpeciesBySlugAsync(request.SpeciesSlug);
        if (species is null)
            return new SetCreationChoiceResult { Success = false, Message = $"Species '{request.SpeciesSlug}' not found." };

        session.SelectedSpecies = species;
        await sessionStore.UpdateSessionAsync(session);
        logger.LogDebug("Session {SessionId}: species set to {Slug}", request.SessionId, request.SpeciesSlug);
        return new SetCreationChoiceResult { Success = true, Message = $"Species set to '{species.DisplayName}'." };
    }
}

/// <summary>
/// Handles <see cref="SetCreationBackgroundCommand"/>.
/// </summary>
public class SetCreationBackgroundHandler(
    ICharacterCreationSessionStore sessionStore,
    IBackgroundRepository backgroundRepository,
    ILogger<SetCreationBackgroundHandler> logger)
    : IRequestHandler<SetCreationBackgroundCommand, SetCreationChoiceResult>
{
    /// <inheritdoc />
    public async Task<SetCreationChoiceResult> Handle(SetCreationBackgroundCommand request, CancellationToken cancellationToken)
    {
        var session = await sessionStore.GetSessionAsync(request.SessionId);
        if (session is null)
            return new SetCreationChoiceResult { Success = false, Message = $"Session {request.SessionId} not found." };
        if (session.Status != CreationSessionStatus.Draft)
            return new SetCreationChoiceResult { Success = false, Message = $"Session is already {session.Status} and cannot be modified." };

        var background = await backgroundRepository.GetBackgroundByIdAsync(request.BackgroundId);
        if (background is null)
            return new SetCreationChoiceResult { Success = false, Message = $"Background '{request.BackgroundId}' not found." };

        session.SelectedBackground = background;
        await sessionStore.UpdateSessionAsync(session);
        logger.LogDebug("Session {SessionId}: background set to {BackgroundId}", request.SessionId, request.BackgroundId);
        return new SetCreationChoiceResult { Success = true, Message = $"Background set to '{background.Name}'." };
    }
}

/// <summary>
/// Handles <see cref="SetCreationEquipmentPreferencesCommand"/>.
/// </summary>
public class SetCreationEquipmentPreferencesHandler(
    ICharacterCreationSessionStore sessionStore)
    : IRequestHandler<SetCreationEquipmentPreferencesCommand, SetCreationChoiceResult>
{
    /// <inheritdoc />
    public async Task<SetCreationChoiceResult> Handle(SetCreationEquipmentPreferencesCommand request, CancellationToken cancellationToken)
    {
        if (request.PreferredArmorType is not null && string.IsNullOrWhiteSpace(request.PreferredArmorType))
            return new SetCreationChoiceResult { Success = false, Message = "PreferredArmorType cannot be an empty or whitespace string. Omit the field or provide a valid type." };
        if (request.PreferredWeaponType is not null && string.IsNullOrWhiteSpace(request.PreferredWeaponType))
            return new SetCreationChoiceResult { Success = false, Message = "PreferredWeaponType cannot be an empty or whitespace string. Omit the field or provide a valid type." };

        var session = await sessionStore.GetSessionAsync(request.SessionId);
        if (session is null)
            return new SetCreationChoiceResult { Success = false, Message = $"Session {request.SessionId} not found." };
        if (session.Status != CreationSessionStatus.Draft)
            return new SetCreationChoiceResult { Success = false, Message = $"Session is already {session.Status} and cannot be modified." };

        session.PreferredArmorType  = request.PreferredArmorType;
        session.PreferredWeaponType = request.PreferredWeaponType;
        session.IncludeShield       = request.IncludeShield;
        await sessionStore.UpdateSessionAsync(session);
        return new SetCreationChoiceResult { Success = true, Message = "Equipment preferences updated." };
    }
}

/// <summary>
/// Handles <see cref="SetCreationLocationCommand"/>.
/// </summary>
public class SetCreationLocationHandler(
    ICharacterCreationSessionStore sessionStore,
    IZoneLocationRepository zoneLocationRepository)
    : IRequestHandler<SetCreationLocationCommand, SetCreationChoiceResult>
{
    /// <inheritdoc />
    public async Task<SetCreationChoiceResult> Handle(SetCreationLocationCommand request, CancellationToken cancellationToken)
    {
        var session = await sessionStore.GetSessionAsync(request.SessionId);
        if (session is null)
            return new SetCreationChoiceResult { Success = false, Message = $"Session {request.SessionId} not found." };
        if (session.Status != CreationSessionStatus.Draft)
            return new SetCreationChoiceResult { Success = false, Message = $"Session is already {session.Status} and cannot be modified." };

        var location = await zoneLocationRepository.GetBySlugAsync(request.LocationId);
        if (location is null)
            return new SetCreationChoiceResult { Success = false, Message = $"Location '{request.LocationId}' not found." };

        session.SelectedLocationId = request.LocationId;
        await sessionStore.UpdateSessionAsync(session);
        return new SetCreationChoiceResult { Success = true, Message = $"Starting location set to '{location.DisplayName}'." };
    }
}
