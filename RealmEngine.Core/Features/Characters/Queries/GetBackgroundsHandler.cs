using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Repositories;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Characters.Queries;

/// <summary>
/// Handler for retrieving all character backgrounds with optional attribute filtering
/// </summary>
public class GetBackgroundsHandler : IRequestHandler<GetBackgroundsQuery, List<Background>>
{
    private readonly IBackgroundRepository _repository;
    private readonly ILogger<GetBackgroundsHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the GetBackgroundsHandler
    /// </summary>
    public GetBackgroundsHandler(IBackgroundRepository repository, ILogger<GetBackgroundsHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<Background>> Handle(GetBackgroundsQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving backgrounds (Filter: {Filter})", request.FilterByAttribute ?? "None");

        if (!string.IsNullOrWhiteSpace(request.FilterByAttribute))
        {
            return await _repository.GetBackgroundsByAttributeAsync(request.FilterByAttribute);
        }

        return await _repository.GetAllBackgroundsAsync();
    }
}
