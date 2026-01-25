using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.CharacterCreation.Queries;

/// <summary>
/// Handler for getting specific character class details.
/// </summary>
public class GetClassDetailsQueryHandler : IRequestHandler<GetClassDetailsQuery, GetClassDetailsResult>
{
    private readonly CharacterClassGenerator _classGenerator;
    private readonly ILogger<GetClassDetailsQueryHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the GetClassDetailsQueryHandler class.
    /// </summary>
    /// <param name="classGenerator">The character class generator.</param>
    /// <param name="logger">The logger instance.</param>
    public GetClassDetailsQueryHandler(
        CharacterClassGenerator classGenerator,
        ILogger<GetClassDetailsQueryHandler> logger)
    {
        _classGenerator = classGenerator ?? throw new ArgumentNullException(nameof(classGenerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<GetClassDetailsResult> Handle(GetClassDetailsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.ClassName))
            {
                return new GetClassDetailsResult
                {
                    Success = false,
                    ErrorMessage = "Class name is required"
                };
            }

            _logger.LogInformation("Getting details for class: {ClassName}", request.ClassName);

            // Get class by name (searches all categories)
            var characterClass = await _classGenerator.GetClassByNameAsync(
                request.ClassName,
                request.Hydrate
            );

            if (characterClass == null)
            {
                return new GetClassDetailsResult
                {
                    Success = false,
                    ErrorMessage = $"Class not found: {request.ClassName}"
                };
            }

            _logger.LogInformation("Found class: {ClassName} ({Slug})", characterClass.Name, characterClass.Slug);

            return new GetClassDetailsResult
            {
                Success = true,
                Class = characterClass
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting class details for: {ClassName}", request.ClassName);

            return new GetClassDetailsResult
            {
                Success = false,
                ErrorMessage = $"Failed to get class details: {ex.Message}"
            };
        }
    }
}
