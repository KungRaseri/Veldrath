using FluentValidation;

namespace RealmEngine.Core.Features.Combat.Commands.FleeFromCombat;

/// <summary>
/// Validates the FleeFromCombat command.
/// </summary>
public class FleeFromCombatValidator : AbstractValidator<FleeFromCombatCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FleeFromCombatValidator"/> class.
    /// </summary>
    public FleeFromCombatValidator()
    {
        RuleFor(x => x.Player)
            .NotNull().WithMessage("Player cannot be null");

        RuleFor(x => x.Player.Health)
            .GreaterThan(0).WithMessage("Player must be alive to flee")
            .When(x => x.Player != null);

        RuleFor(x => x.Enemy)
            .NotNull().WithMessage("Enemy cannot be null");
    }
}
