using FluentValidation;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Inventory.Commands;

/// <summary>
/// Validates the EquipItem command.
/// </summary>
public class EquipItemValidator : AbstractValidator<EquipItemCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EquipItemValidator"/> class.
    /// </summary>
    public EquipItemValidator()
    {
        RuleFor(x => x.Player)
            .NotNull().WithMessage("Player cannot be null");

        RuleFor(x => x.Item)
            .NotNull().WithMessage("Item cannot be null");

        RuleFor(x => x.Item.Type)
            .NotEqual(ItemType.Consumable).WithMessage("Cannot equip consumable items")
            .When(x => x.Item != null);
    }
}
