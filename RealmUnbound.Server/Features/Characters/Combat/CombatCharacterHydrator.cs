using RealmEngine.Shared.Models;
using ServerCharacter = RealmUnbound.Server.Data.Entities.Character;

namespace RealmUnbound.Server.Features.Characters.Combat;

/// <summary>
/// Builds a lightweight <see cref="Character"/> shell from the server EF entity and its
/// deserialized attributes blob, suitable for passing to combat calculation helpers.
/// Only the fields actively used in combat (HP, mana, core stats, cooldowns) are populated.
/// </summary>
public static class CombatCharacterHydrator
{
    private const string PrefixAbility = "AbilityCooldown_";
    private const string PrefixSpell   = "SpellCooldown_";

    /// <summary>
    /// Hydrates a <see cref="Character"/> for combat use from the server character entity
    /// and its deserialized attributes blob.
    /// </summary>
    /// <param name="entity">The server EF character entity (level, name).</param>
    /// <param name="attrs">The deserialized attributes blob.</param>
    /// <returns>A <see cref="Character"/> suitable for use in combat calculations.</returns>
    public static Character Hydrate(ServerCharacter entity, Dictionary<string, int> attrs)
    {
        var maxHealth = attrs.TryGetValue("MaxHealth", out var mh) ? mh : entity.Level * 10;
        var maxMana   = attrs.TryGetValue("MaxMana",   out var mm) ? mm : entity.Level * 5;

        var abilityCooldowns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var spellCooldowns   = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in attrs)
        {
            if (key.StartsWith(PrefixAbility, StringComparison.OrdinalIgnoreCase))
                abilityCooldowns[key[PrefixAbility.Length..]] = value;
            else if (key.StartsWith(PrefixSpell, StringComparison.OrdinalIgnoreCase))
                spellCooldowns[key[PrefixSpell.Length..]] = value;
        }

        return new Character
        {
            Name             = entity.Name,
            Level            = entity.Level,
            Health           = attrs.TryGetValue("CurrentHealth", out var ch) ? ch : maxHealth,
            MaxHealth        = maxHealth,
            Mana             = attrs.TryGetValue("CurrentMana",   out var cm) ? cm : maxMana,
            MaxMana          = maxMana,
            Strength         = attrs.TryGetValue("Strength",     out var str)   ? str   : 10,
            Dexterity        = attrs.TryGetValue("Dexterity",    out var dex)   ? dex   : 10,
            Constitution     = attrs.TryGetValue("Constitution", out var con)   ? con   : 10,
            Intelligence     = attrs.TryGetValue("Intelligence", out var intel) ? intel : 10,
            Wisdom           = attrs.TryGetValue("Wisdom",       out var wis)   ? wis   : 10,
            Charisma         = attrs.TryGetValue("Charisma",     out var cha)   ? cha   : 10,
            AbilityCooldowns = abilityCooldowns,
            SpellCooldowns   = spellCooldowns,
            ActiveStatusEffects = [],
        };
    }
}
