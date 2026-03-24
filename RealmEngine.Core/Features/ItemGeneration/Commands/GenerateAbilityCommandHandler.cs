using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.ItemGeneration.Commands;

/// <summary>
/// Handler for generating powers via PowerGenerator.
/// </summary>
public class GeneratePowerCommandHandler : IRequestHandler<GeneratePowerCommand, GeneratePowerResult>
{
    private readonly PowerGenerator _powerGenerator;
    private readonly ILogger<GeneratePowerCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the GeneratePowerCommandHandler class.
    /// </summary>
    /// <param name="powerGenerator">The power generator.</param>
    /// <param name="logger">The logger instance.</param>
    public GeneratePowerCommandHandler(
        PowerGenerator powerGenerator,
        ILogger<GeneratePowerCommandHandler> logger)
    {
        _powerGenerator = powerGenerator ?? throw new ArgumentNullException(nameof(powerGenerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<GeneratePowerResult> Handle(GeneratePowerCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(request.Category))
            {
                return new GeneratePowerResult
                {
                    Success = false,
                    ErrorMessage = "Category is required"
                };
            }

            if (string.IsNullOrWhiteSpace(request.Subcategory))
            {
                return new GeneratePowerResult
                {
                    Success = false,
                    ErrorMessage = "Subcategory is required"
                };
            }

            // Generate specific power by name
            if (!string.IsNullOrWhiteSpace(request.PowerName))
            {
                _logger.LogInformation(
                    "Generating specific power: {Category}/{Subcategory}/{PowerName}",
                    request.Category, request.Subcategory, request.PowerName
                );

                var power = await _powerGenerator.GenerateAbilityByNameAsync(
                    request.Category,
                    request.Subcategory,
                    request.PowerName,
                    request.Hydrate
                );

                if (power == null)
                {
                    return new GeneratePowerResult
                    {
                        Success = false,
                        ErrorMessage = $"Power not found: {request.Category}/{request.Subcategory}/{request.PowerName}"
                    };
                }

                return new GeneratePowerResult
                {
                    Success = true,
                    Power = power,
                    Powers = new List<Power> { power }
                };
            }

            // Generate random powers
            _logger.LogInformation(
                "Generating {Count} random powers from {Category}/{Subcategory}",
                request.Count, request.Category, request.Subcategory
            );

            var powers = await _powerGenerator.GenerateAbilitiesAsync(
                request.Category,
                request.Subcategory,
                request.Count,
                request.Hydrate
            );

            if (powers == null || !powers.Any())
            {
                return new GeneratePowerResult
                {
                    Success = false,
                    ErrorMessage = $"No powers found in {request.Category}/{request.Subcategory}"
                };
            }

            return new GeneratePowerResult
            {
                Success = true,
                Power = powers.FirstOrDefault(),
                Powers = powers
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating powers: {Category}/{Subcategory}",
                request.Category, request.Subcategory);

            return new GeneratePowerResult
            {
                Success = false,
                ErrorMessage = $"Generation failed: {ex.Message}"
            };
        }
    }
}
