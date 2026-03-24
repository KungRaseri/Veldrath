using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.ItemGeneration.Commands;

/// <summary>
/// Handler for generating abilities via AbilityGenerator.
/// </summary>
public class GenerateAbilityCommandHandler : IRequestHandler<GenerateAbilityCommand, GenerateAbilityResult>
{
    private readonly AbilityGenerator _abilityGenerator;
    private readonly ILogger<GenerateAbilityCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the GenerateAbilityCommandHandler class.
    /// </summary>
    /// <param name="abilityGenerator">The ability generator.</param>
    /// <param name="logger">The logger instance.</param>
    public GenerateAbilityCommandHandler(
        AbilityGenerator abilityGenerator,
        ILogger<GenerateAbilityCommandHandler> logger)
    {
        _abilityGenerator = abilityGenerator ?? throw new ArgumentNullException(nameof(abilityGenerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<GenerateAbilityResult> Handle(GenerateAbilityCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(request.Category))
            {
                return new GenerateAbilityResult
                {
                    Success = false,
                    ErrorMessage = "Category is required"
                };
            }

            if (string.IsNullOrWhiteSpace(request.Subcategory))
            {
                return new GenerateAbilityResult
                {
                    Success = false,
                    ErrorMessage = "Subcategory is required"
                };
            }

            // Generate specific ability by name
            if (!string.IsNullOrWhiteSpace(request.AbilityName))
            {
                _logger.LogInformation(
                    "Generating specific ability: {Category}/{Subcategory}/{AbilityName}",
                    request.Category, request.Subcategory, request.AbilityName
                );

                var ability = await _abilityGenerator.GenerateAbilityByNameAsync(
                    request.Category,
                    request.Subcategory,
                    request.AbilityName,
                    request.Hydrate
                );

                if (ability == null)
                {
                    return new GenerateAbilityResult
                    {
                        Success = false,
                        ErrorMessage = $"Ability not found: {request.Category}/{request.Subcategory}/{request.AbilityName}"
                    };
                }

                return new GenerateAbilityResult
                {
                    Success = true,
                    Ability = ability,
                    Abilities = new List<Power> { ability }
                };
            }

            // Generate random abilities
            _logger.LogInformation(
                "Generating {Count} random abilities from {Category}/{Subcategory}",
                request.Count, request.Category, request.Subcategory
            );

            var abilities = await _abilityGenerator.GenerateAbilitiesAsync(
                request.Category,
                request.Subcategory,
                request.Count,
                request.Hydrate
            );

            if (abilities == null || !abilities.Any())
            {
                return new GenerateAbilityResult
                {
                    Success = false,
                    ErrorMessage = $"No abilities found in {request.Category}/{request.Subcategory}"
                };
            }

            return new GenerateAbilityResult
            {
                Success = true,
                Ability = abilities.FirstOrDefault(),
                Abilities = abilities
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating abilities: {Category}/{Subcategory}",
                request.Category, request.Subcategory);

            return new GenerateAbilityResult
            {
                Success = false,
                ErrorMessage = $"Generation failed: {ex.Message}"
            };
        }
    }
}
