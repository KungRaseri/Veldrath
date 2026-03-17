using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Generators.Modern;

namespace RealmEngine.Core.Features.ItemGeneration.Commands;

/// <summary>
/// Handler for GenerateEnemyCommand.
/// Delegates to EnemyGenerator to create enemies.
/// </summary>
public class GenerateEnemyCommandHandler : IRequestHandler<GenerateEnemyCommand, GenerateEnemyResult>
{
    private readonly EnemyGenerator _enemyGenerator;
    private readonly ILogger<GenerateEnemyCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateEnemyCommandHandler"/> class.
    /// </summary>
    /// <param name="enemyGenerator">The enemy generator.</param>
    /// <param name="logger">The logger.</param>
    public GenerateEnemyCommandHandler(EnemyGenerator enemyGenerator, ILogger<GenerateEnemyCommandHandler> logger)
    {
        _enemyGenerator = enemyGenerator;
        _logger = logger;
    }

    /// <summary>
    /// Handles the generate enemy command.
    /// </summary>
    /// <param name="request">The generate enemy command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated enemy result.</returns>
    public async Task<GenerateEnemyResult> Handle(GenerateEnemyCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Category))
            {
                return new GenerateEnemyResult
                {
                    Success = false,
                    ErrorMessage = "Category cannot be empty"
                };
            }

            var enemies = await _enemyGenerator.GenerateEnemiesAsync(request.Category, 1, request.Hydrate);
            
            if (enemies == null || enemies.Count == 0)
            {
                return new GenerateEnemyResult
                {
                    Success = false,
                    ErrorMessage = $"No enemies found in category: {request.Category}"
                };
            }

            var enemy = enemies[0];

            // Apply level if specified
            if (request.Level.HasValue && request.Level.Value > 0)
            {
                enemy.Level = request.Level.Value;
                // Scale health based on level (simple formula)
                enemy.MaxHealth = enemy.MaxHealth * request.Level.Value;
                enemy.Health = enemy.MaxHealth;
            }

            _logger.LogDebug("Generated enemy: {EnemyName} (Level {Level}) from category {Category}", 
                enemy.Name, enemy.Level, request.Category);

            return new GenerateEnemyResult
            {
                Success = true,
                Enemy = enemy
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating enemy from category {Category}", request.Category);
            return new GenerateEnemyResult
            {
                Success = false,
                ErrorMessage = $"Failed to generate enemy: {ex.Message}"
            };
        }
    }
}
