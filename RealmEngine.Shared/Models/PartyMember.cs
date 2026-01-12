namespace RealmEngine.Shared.Models;

/// <summary>
/// Represents an NPC recruited as a party member.
/// </summary>
public class PartyMember
{
    /// <summary>
    /// Gets or sets the unique identifier.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the member's name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the member's class.
    /// </summary>
    public string ClassName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the member's level.
    /// </summary>
    public int Level { get; set; } = 1;

    /// <summary>
    /// Gets or sets current health.
    /// </summary>
    public int Health { get; set; } = 100;

    /// <summary>
    /// Gets or sets maximum health.
    /// </summary>
    public int MaxHealth { get; set; } = 100;

    /// <summary>
    /// Gets or sets current mana.
    /// </summary>
    public int Mana { get; set; } = 50;

    /// <summary>
    /// Gets or sets maximum mana.
    /// </summary>
    public int MaxMana { get; set; } = 50;

    /// <summary>
    /// Gets or sets the Strength attribute.
    /// </summary>
    public int Strength { get; set; } = 10;

    /// <summary>
    /// Gets or sets the Dexterity attribute.
    /// </summary>
    public int Dexterity { get; set; } = 10;

    /// <summary>
    /// Gets or sets the Constitution attribute.
    /// </summary>
    public int Constitution { get; set; } = 10;

    /// <summary>
    /// Gets or sets the Intelligence attribute.
    /// </summary>
    public int Intelligence { get; set; } = 10;

    /// <summary>
    /// Gets or sets the Wisdom attribute.
    /// </summary>
    public int Wisdom { get; set; } = 10;

    /// <summary>
    /// Gets or sets the Charisma attribute.
    /// </summary>
    public int Charisma { get; set; } = 10;

    /// <summary>
    /// Gets or sets the member's experience points.
    /// </summary>
    public int Experience { get; set; } = 0;

    /// <summary>
    /// Gets or sets the member's inventory.
    /// </summary>
    public List<Item> Inventory { get; set; } = new();

    /// <summary>
    /// Gets or sets equipped weapon.
    /// </summary>
    public Item? EquippedWeapon { get; set; }

    /// <summary>
    /// Gets or sets equipped armor.
    /// </summary>
    public Item? EquippedArmor { get; set; }

    /// <summary>
    /// Gets or sets list of known ability IDs.
    /// </summary>
    public List<string> AbilityIds { get; set; } = new();

    /// <summary>
    /// Gets or sets list of known spell IDs.
    /// </summary>
    public List<string> SpellIds { get; set; } = new();

    /// <summary>
    /// Gets or sets active status effects.
    /// </summary>
    public List<StatusEffect> ActiveStatusEffects { get; set; } = new();

    /// <summary>
    /// Gets or sets the NPC's role in combat (Tank, DPS, Healer, Support).
    /// </summary>
    public PartyRole Role { get; set; } = PartyRole.DPS;

    /// <summary>
    /// Gets or sets the NPC's AI behavior setting.
    /// </summary>
    public AIBehavior Behavior { get; set; } = AIBehavior.Balanced;

    /// <summary>
    /// Gets or sets whether this member is alive.
    /// </summary>
    public bool IsAlive => Health > 0;

    /// <summary>
    /// Gets attack stat (Strength-based + equipped weapon).
    /// </summary>
    public int GetAttack()
    {
        int attack = Strength + (Level * 2);
        
        // Add weapon attack from traits
        if (EquippedWeapon != null && EquippedWeapon.Traits.TryGetValue("attack", out var attackTrait))
        {
            attack += attackTrait.AsInt();
        }
        
        return attack;
    }

    /// <summary>
    /// Gets defense stat (Constitution-based + equipped armor).
    /// </summary>
    public int GetDefense()
    {
        int defense = Constitution + (Level * 2);
        
        // Add armor defense from traits
        if (EquippedArmor != null && EquippedArmor.Traits.TryGetValue("defense", out var defenseTrait))
        {
            defense += defenseTrait.AsInt();
        }
        
        return defense;
    }

    /// <summary>
    /// Gets dodge chance (Dexterity-based).
    /// </summary>
    public double GetDodgeChance() => (Dexterity / 200.0) + 0.05;

    /// <summary>
    /// Gets critical chance (Dexterity-based).
    /// </summary>
    public double GetCriticalChance() => (Dexterity / 100.0) * 0.10;

    /// <summary>
    /// Restores health.
    /// </summary>
    public void Heal(int amount)
    {
        Health = Math.Min(Health + amount, MaxHealth);
    }

    /// <summary>
    /// Restores mana.
    /// </summary>
    public void RestoreMana(int amount)
    {
        Mana = Math.Min(Mana + amount, MaxMana);
    }

    /// <summary>
    /// Takes damage.
    /// </summary>
    public void TakeDamage(int amount)
    {
        Health = Math.Max(0, Health - amount);
    }

    /// <summary>
    /// Gains experience and levels up if threshold met.
    /// </summary>
    public bool GainExperience(int amount)
    {
        Experience += amount;
        int xpNeeded = Level * 100;
        
        if (Experience >= xpNeeded)
        {
            Experience -= xpNeeded;
            Level++;
            LevelUp();
            return true;
        }
        
        return false;
    }

    /// <summary>
    /// Applies stat increases on level up.
    /// </summary>
    private void LevelUp()
    {
        // Increase stats based on class
        MaxHealth += 10 + Constitution;
        MaxMana += 5 + Wisdom;
        Health = MaxHealth;
        Mana = MaxMana;
        
        // Role-based stat increases
        switch (Role)
        {
            case PartyRole.Tank:
                Constitution += 2;
                Strength += 1;
                break;
            case PartyRole.DPS:
                Strength += 2;
                Dexterity += 1;
                break;
            case PartyRole.Healer:
                Wisdom += 2;
                Intelligence += 1;
                break;
            case PartyRole.Support:
                Intelligence += 2;
                Charisma += 1;
                break;
        }
    }
}

/// <summary>
/// Party role types.
/// </summary>
public enum PartyRole
{
    /// <summary>Tank role - High health, draws aggro.</summary>
    Tank,
    /// <summary>DPS role - High damage output.</summary>
    DPS,
    /// <summary>Healer role - Restores ally health.</summary>
    Healer,
    /// <summary>Support role - Buffs and debuffs.</summary>
    Support
}

/// <summary>
/// AI behavior patterns.
/// </summary>
public enum AIBehavior
{
    /// <summary>Aggressive - Prioritizes offense.</summary>
    Aggressive,
    /// <summary>Balanced - Mix of offense and defense.</summary>
    Balanced,
    /// <summary>Defensive - Prioritizes survival.</summary>
    Defensive,
    /// <summary>Support Focus - Prioritizes helping allies.</summary>
    SupportFocus
}
