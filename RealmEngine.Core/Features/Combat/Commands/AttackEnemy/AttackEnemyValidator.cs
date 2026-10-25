using FluentValidation;

namespace RealmEngine.Core.Features.Combat.Commands.AttackEnemy;

/// <summary>
/// Validates the AttackEnemy command.
/// </summary>
public class AttackEnemyValidator : AbstractValidator<AttackEnemyCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AttackEnemyValidator"/> class.
    /// </summary>
    public AttackEnemyValidator()
    {
        RuleFor(x => x.Player)
            .NotNull().WithMessage("Player cannot be null");

        RuleFor(x => x.Player.Health)
            .GreaterThan(0).WithMessage("Player must be alive to attack")
            .When(x => x.Player != null);

        RuleFor(x => x.Enemy)
            .NotNull().WithMessage("Enemy cannot be null");

        RuleFor(x => x.Enemy.Health)
            .GreaterThan(0).WithMessage("Enemy must be alive to be attacked")
            .When(x => x.Enemy != null);
    }
}
