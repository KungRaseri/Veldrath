using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.CharacterCreation.Commands;

/// <summary>
/// Finalizes a character creation session, creating the character from all accumulated choices.
/// </summary>
public record FinalizeCreationSessionCommand : IRequest<CreateCharacterResult>
{
    /// <summary>Gets the session to finalize.</summary>
    public required Guid SessionId { get; init; }

    /// <summary>Gets the character's name. When <see langword="null"/>, falls back to the name stored on the session via <see cref="SetCreationNameCommand"/>.</summary>
    public string? CharacterName { get; init; }

    /// <summary>Gets the difficulty level (defaults to Normal).</summary>
    public string DifficultyLevel { get; init; } = "Normal";
}

/// <summary>
/// Handles <see cref="FinalizeCreationSessionCommand"/>.
/// </summary>
public class FinalizeCreationSessionHandler(
    ICharacterCreationSessionStore sessionStore,
    IMediator mediator,
    ILogger<FinalizeCreationSessionHandler> logger)
    : IRequestHandler<FinalizeCreationSessionCommand, CreateCharacterResult>
{
    /// <inheritdoc />
    public async Task<CreateCharacterResult> Handle(FinalizeCreationSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await sessionStore.GetSessionAsync(request.SessionId);
        if (session is null)
            return Fail($"Session {request.SessionId} not found.");
        if (session.Status == CreationSessionStatus.Abandoned)
            return Fail("Cannot finalize an abandoned session.");

        if (session.SelectedClass is null)
            return Fail("A character class must be selected before finalizing.");
        if (session.SelectedSpecies is null)
            return Fail("A species must be selected before finalizing.");
        if (session.SelectedBackground is null)
            return Fail("A background must be selected before finalizing.");

        var resolvedName = request.CharacterName ?? session.CharacterName;
        if (string.IsNullOrWhiteSpace(resolvedName))
            return Fail("Character name is required. Provide it at finalization or call the name step first.");

        var command = new CreateCharacterCommand
        {
            CharacterName        = resolvedName,
            CharacterClass       = session.SelectedClass,
            BackgroundId         = session.SelectedBackground?.GetBackgroundId(),
            SpeciesSlug          = session.SelectedSpecies?.Slug,
            AttributeAllocations = session.AttributeAllocations,
            DifficultyLevel      = request.DifficultyLevel,
            StartingLocationId   = session.SelectedLocationId,
            PreferredArmorType   = session.PreferredArmorType,
            PreferredWeaponType  = session.PreferredWeaponType,
            IncludeShield        = session.IncludeShield,
        };

        var result = await mediator.Send(command, cancellationToken);

        if (result.Success)
        {
            session.Status        = CreationSessionStatus.Finalized;
            session.CharacterName = resolvedName;
            await sessionStore.UpdateSessionAsync(session);
            logger.LogInformation("Session {SessionId} finalized — character '{Name}' created", request.SessionId, resolvedName);
        }

        return result;
    }

    private static CreateCharacterResult Fail(string message) =>
        new() { Success = false, Message = message };
}
