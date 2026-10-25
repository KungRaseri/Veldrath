using RealmEngine.Shared.Models;
using RealmEngine.Shared.Utilities;

namespace RealmEngine.Core.Features.Combat;

/// <summary>
/// Extension methods and helpers for CombatService.
/// </summary>
public static class CombatServiceExtensions
{
    /// <summary>
    /// Get the skill slug for the player's equipped weapon.
    /// Reads the skillReference trait from the equipped weapon.
    /// </summary>
    /// <returns>Skill slug (e.g., "light-blades", "heavy-blades") or null if no weapon equipped</returns>
    public static string? GetEquippedWeaponSkillSlug(this Character player)
    {
        // Check main hand weapon first
        Item? equippedWeapon = player.EquippedMainHand;

        // If no main hand weapon, check off hand (could be a weapon in dual wield)
        if (equippedWeapon == null || equippedWeapon.Type != ItemType.Weapon)
        {
            equippedWeapon = player.EquippedOffHand;
        }

        // Get skill slug from item's traits (uses skillReference or fallback to skillType)
        return SkillEffectCalculator.GetSkillSlugFromItem(equippedWeapon);
    }
}
