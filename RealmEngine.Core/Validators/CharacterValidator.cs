using FluentValidation;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Validators;

/// <summary>
/// Validator for Character model.
/// </summary>
public class CharacterValidator : AbstractValidator<Character>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CharacterValidator"/> class.
    /// </summary>
    public CharacterValidator()
    {
        RuleFor(c => c.Name)
            .NotEmpty().WithMessage("Character name cannot be empty")
            .Length(2, 20).WithMessage("Character name must be between 2 and 20 characters")
            .Matches("^[a-zA-Z ]+$").WithMessage("Character name can only contain letters and spaces");

        RuleFor(c => c.Level)
            .GreaterThan(0).WithMessage("Level must be greater than 0")
            .LessThanOrEqualTo(100).WithMessage("Level cannot exceed 100");

        RuleFor(c => c.Health)
            .GreaterThanOrEqualTo(0).WithMessage("Health cannot be negative")
            .LessThanOrEqualTo(c => c.MaxHealth).WithMessage("Health cannot exceed max health");

        RuleFor(c => c.MaxHealth)
            .GreaterThan(0).WithMessage("Max health must be greater than 0");

        RuleFor(c => c.Mana)
            .GreaterThanOrEqualTo(0).WithMessage("Mana cannot be negative")
            .LessThanOrEqualTo(c => c.MaxMana).WithMessage("Mana cannot exceed max mana");

        RuleFor(c => c.MaxMana)
            .GreaterThanOrEqualTo(0).WithMessage("Max mana cannot be negative");

        RuleFor(c => c.Experience)
            .GreaterThanOrEqualTo(0).WithMessage("Experience cannot be negative");

        RuleFor(c => c.Gold)
            .GreaterThanOrEqualTo(0).WithMessage("Gold cannot be negative");
    }
}
