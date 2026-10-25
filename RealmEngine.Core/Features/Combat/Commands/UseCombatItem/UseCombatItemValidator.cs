using FluentValidation;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Combat.Commands.UseCombatItem;

/// <summary>
/// Validates the UseCombatItem command.
/// </summary>
public class UseCombatItemValidator : AbstractValidator<UseCombatItemCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UseCombatItemValidator"/> class.
    /// </summary>
    public UseCombatItemValidator()
    {
        RuleFor(x => x.Player)
            .NotNull().WithMessage("Player cannot be null");

        RuleFor(x => x.Player.Health)
            .GreaterThan(0).WithMessage("Player must be alive to use items")
            .When(x => x.Player != null);

        RuleFor(x => x.Item)
            .NotNull().WithMessage("Item cannot be null");

        RuleFor(x => x.Item.Type)
            .Equal(ItemType.Consumable).WithMessage("Only consumable items can be used in combat")
            .When(x => x.Item != null);
    }
}
