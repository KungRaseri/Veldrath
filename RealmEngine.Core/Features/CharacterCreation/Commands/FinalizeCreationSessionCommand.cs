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

    /// <summary>Gets the character's name, entered at finalization.</summary>
    public required string CharacterName { get; init; }

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

        if (session.SelectedClass is null)
            return Fail("A character class must be selected before finalizing.");

        if (string.IsNullOrWhiteSpace(request.CharacterName))
            return Fail("Character name is required.");

        var command = new CreateCharacterCommand
        {
            CharacterName        = request.CharacterName,
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
            session.CharacterName = request.CharacterName;
            await sessionStore.UpdateSessionAsync(session);
            logger.LogInformation("Session {SessionId} finalized — character '{Name}' created", request.SessionId, request.CharacterName);
        }

        return result;
    }

    private static CreateCharacterResult Fail(string message) =>
        new() { Success = false, Message = message };
}
