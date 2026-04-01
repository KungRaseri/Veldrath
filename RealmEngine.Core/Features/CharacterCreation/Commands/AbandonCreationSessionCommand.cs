using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.CharacterCreation.Commands;

/// <summary>
/// Abandons a character creation session, removing it from the store.
/// </summary>
public record AbandonCreationSessionCommand(Guid SessionId) : IRequest<AbandonCreationSessionResult>;

/// <summary>
/// Result of <see cref="AbandonCreationSessionCommand"/>.
/// </summary>
public record AbandonCreationSessionResult
{
    /// <summary>Gets a value indicating whether the session was found and removed.</summary>
    public bool Success { get; init; }

    /// <summary>Gets a message describing the result.</summary>
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Handles <see cref="AbandonCreationSessionCommand"/>.
/// </summary>
public class AbandonCreationSessionHandler(
    ICharacterCreationSessionStore sessionStore,
    ILogger<AbandonCreationSessionHandler> logger)
    : IRequestHandler<AbandonCreationSessionCommand, AbandonCreationSessionResult>
{
    /// <inheritdoc />
    public async Task<AbandonCreationSessionResult> Handle(AbandonCreationSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await sessionStore.GetSessionAsync(request.SessionId);
        if (session is null)
            return new AbandonCreationSessionResult { Success = false, Message = $"Session {request.SessionId} not found." };

        session.Status = CreationSessionStatus.Abandoned;
        await sessionStore.RemoveSessionAsync(request.SessionId);
        logger.LogInformation("Session {SessionId} abandoned", request.SessionId);
        return new AbandonCreationSessionResult { Success = true, Message = "Session abandoned." };
    }
}
