using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Characters.Queries;

/// <summary>
/// Handler for retrieving a specific character background by ID or slug
/// </summary>
public class GetBackgroundHandler : IRequestHandler<GetBackgroundQuery, Background?>
{
    private readonly IBackgroundRepository _repository;
    private readonly ILogger<GetBackgroundHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the GetBackgroundHandler
    /// </summary>
    public GetBackgroundHandler(IBackgroundRepository repository, ILogger<GetBackgroundHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Background?> Handle(GetBackgroundQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving background: {BackgroundId}", request.BackgroundId);
        return await _repository.GetBackgroundByIdAsync(request.BackgroundId);
    }
}
