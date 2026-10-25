using MediatR;

namespace RealmEngine.Core.Features.Victory.Commands;

/// <summary>
/// Command to start New Game Plus mode.
/// </summary>
public record StartNewGamePlusCommand : IRequest<StartNewGamePlusResult>;

/// <summary>
/// Result of start New Game Plus operation.
/// </summary>
/// <param name="Success">Whether the operation was successful.</param>
/// <param name="Message">Result message.</param>
public record StartNewGamePlusResult(bool Success, string Message);

/// <summary>
/// Handles the StartNewGamePlus command.
/// </summary>
public class StartNewGamePlusHandler : IRequestHandler<StartNewGamePlusCommand, StartNewGamePlusResult>
{
    private readonly Services.NewGamePlusService _ngPlusService;

    /// <summary>
    /// Initializes a new instance of the <see cref="StartNewGamePlusHandler"/> class.
    /// </summary>
    /// <param name="ngPlusService">The New Game Plus service.</param>
    public StartNewGamePlusHandler(Services.NewGamePlusService ngPlusService)
    {
        _ngPlusService = ngPlusService;
    }

    /// <summary>
    /// Handles the StartNewGamePlusCommand and returns the result.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result.</returns>
    public async Task<StartNewGamePlusResult> Handle(StartNewGamePlusCommand request, CancellationToken cancellationToken)
    {
        var result = await _ngPlusService.StartNewGamePlusAsync();

        if (result.Success)
        {
            return new StartNewGamePlusResult(true, "New Game+ started!");
        }

        return new StartNewGamePlusResult(false, "Failed to start New Game+");
    }
}
