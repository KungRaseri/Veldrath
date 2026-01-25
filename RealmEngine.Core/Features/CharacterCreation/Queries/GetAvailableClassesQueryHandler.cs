using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.CharacterCreation.Queries;

/// <summary>
/// Handler for getting available character classes.
/// </summary>
public class GetAvailableClassesQueryHandler : IRequestHandler<GetAvailableClassesQuery, GetAvailableClassesResult>
{
    private readonly CharacterClassGenerator _classGenerator;
    private readonly ILogger<GetAvailableClassesQueryHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the GetAvailableClassesQueryHandler class.
    /// </summary>
    /// <param name="classGenerator">The character class generator.</param>
    /// <param name="logger">The logger instance.</param>
    public GetAvailableClassesQueryHandler(
        CharacterClassGenerator classGenerator,
        ILogger<GetAvailableClassesQueryHandler> logger)
    {
        _classGenerator = classGenerator ?? throw new ArgumentNullException(nameof(classGenerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<GetAvailableClassesResult> Handle(GetAvailableClassesQuery request, CancellationToken cancellationToken)
    {
        try
        {
            List<CharacterClass> classes;

            // Get classes by category or all classes
            if (!string.IsNullOrWhiteSpace(request.Category))
            {
                _logger.LogInformation("Getting classes from category: {Category}", request.Category);
                classes = await _classGenerator.GetClassesByCategoryAsync(request.Category, request.Hydrate);
            }
            else
            {
                _logger.LogInformation("Getting all available classes");
                classes = await _classGenerator.GetAllClassesAsync(request.Hydrate);
            }

            if (classes == null || !classes.Any())
            {
                var errorMsg = string.IsNullOrWhiteSpace(request.Category)
                    ? "No classes found"
                    : $"No classes found in category: {request.Category}";

                return new GetAvailableClassesResult
                {
                    Success = false,
                    ErrorMessage = errorMsg
                };
            }

            _logger.LogInformation("Found {Count} classes", classes.Count);

            return new GetAvailableClassesResult
            {
                Success = true,
                Classes = classes
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available classes");

            return new GetAvailableClassesResult
            {
                Success = false,
                ErrorMessage = $"Failed to get classes: {ex.Message}"
            };
        }
    }
}
