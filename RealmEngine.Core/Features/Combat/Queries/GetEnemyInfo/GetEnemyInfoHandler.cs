using MediatR;

namespace RealmEngine.Core.Features.Combat.Queries.GetEnemyInfo;

/// <summary>
/// Handles the GetEnemyInfo query.
/// </summary>
public class GetEnemyInfoHandler : IRequestHandler<GetEnemyInfoQuery, EnemyInfoDto>
{
    /// <summary>
    /// Handles the get enemy info query and returns detailed enemy information.
    /// </summary>
    /// <param name="request">The get enemy info query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, containing the enemy information.</returns>
    public Task<EnemyInfoDto> Handle(GetEnemyInfoQuery request, CancellationToken cancellationToken)
    {
        var enemy = request.Enemy;

        var info = new EnemyInfoDto
        {
            Name = enemy.Name,
            Level = enemy.Level,
            Health = enemy.Health,
            MaxHealth = enemy.MaxHealth,
            Attack = enemy.BasePhysicalDamage,
            Defense = enemy.GetPhysicalDefense(),
            Difficulty = enemy.Difficulty,
            Description = $"A level {enemy.Level} {enemy.Difficulty.ToString().ToLower()} enemy"
        };

        return Task.FromResult(info);
    }
}