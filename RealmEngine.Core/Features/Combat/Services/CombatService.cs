using RealmEngine.Shared.Models;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Shared.Utilities;
using Serilog;
using MediatR;
using RealmEngine.Core.Features.Progression.Commands;
using RealmEngine.Core.Services;
using RealmEngine.Core.Features.Progression.Services;
using RealmEngine.Core.Features.Combat.Services;
using RealmEngine.Core.Features.Combat.Commands;
using RealmEngine.Core.Features.Quests.Commands;

namespace RealmEngine.Core.Features.Combat;

/// <summary>
/// Service for handling combat mechanics and calculations.
/// </summary>
public class CombatService
{
    private readonly Random _random = new();
    private readonly SaveGameService _saveGameService;
    private readonly IMediator _mediator;
    private readonly ReactiveAbilityService _reactiveAbilityService;
    private readonly EnemyAbilityAIService _enemyAbilityAI;
    private readonly EnemySpellCastingService _enemySpellCastingAI;

    /// <summary>
    /// Initialize the combat service with required dependencies.
    /// </summary>
    public CombatService(
        SaveGameService saveGameService,
        IMediator mediator,
        AbilityCatalogService abilityCatalogService,
        SpellCatalogService spellCatalogService)
    {
        _saveGameService = saveGameService;
        _mediator = mediator;
        _reactiveAbilityService = new ReactiveAbilityService(abilityCatalogService);
        _enemyAbilityAI = new EnemyAbilityAIService(abilityCatalogService);
        _enemySpellCastingAI = new EnemySpellCastingService(spellCatalogService);
    }

    /// <summary>
    /// Parameterless constructor for testing/mocking purposes.
    /// </summary>
    protected CombatService()
    {
        _saveGameService = null!;
        _mediator = null!;
        _reactiveAbilityService = new ReactiveAbilityService();
        _enemyAbilityAI = new EnemyAbilityAIService();
        _enemySpellCastingAI = new EnemySpellCastingService();
    }

    /// <summary>
    /// Initialize combat by applying difficulty multipliers to enemy stats.
    /// This should be called before combat begins.
    /// </summary>
    public virtual void InitializeCombat(Enemy enemy)
    {
        var difficulty = _saveGameService.GetDifficultySettings();

        // Scale enemy health based on difficulty
        enemy.MaxHealth = (int)(enemy.MaxHealth * difficulty.EnemyHealthMultiplier);
        enemy.Health = enemy.MaxHealth;

        Log.Information("Enemy {Name} initialized with {Health} HP (difficulty: {Difficulty}, multiplier: {Multiplier})",
            enemy.Name, enemy.Health, difficulty.Name, difficulty.EnemyHealthMultiplier);
    }

    /// <summary>
    /// Execute a player attack on an enemy.
    /// </summary>
    public virtual async Task<CombatResult> ExecutePlayerAttack(Character player, Enemy enemy, bool isDefending = false)
    {
        var result = new CombatResult { Success = true };

        // Process status effects on player at start of turn
        var playerStatusResult = await _mediator.Send(new ProcessStatusEffectsCommand { TargetCharacter = player });
        result.DotDamage = playerStatusResult.TotalDamageTaken;
        result.HotHealing = playerStatusResult.TotalHealingReceived;
        result.ActiveStatusEffects = player.ActiveStatusEffects?.ToList() ?? new List<StatusEffect>();
        result.StatusEffectsExpired = playerStatusResult.ExpiredEffectTypes.ToList();
        
        // Check if player can act (not crowd controlled)
        if (!CanAct(player))
        {
            result.Success = false;
            result.Message = GetCrowdControlMessage(player);
            return result;
        }

        // Check if enemy dodges
        if (RollDodge(enemy.GetDodgeChance()))
        {
            result.IsDodged = true;
            result.Message = $"{enemy.Name} dodged your attack!";
            return result;
        }

        // Check for critical hit (base + skill bonus)
        double critChance = player.GetCriticalChance() + SkillEffectCalculator.GetCriticalChanceBonus(player);
        bool isCritical = RollCritical(critChance);
        result.IsCritical = isCritical;

        // Trigger reactive abilities on critical hit
        if (isCritical)
        {
            _reactiveAbilityService.CheckAndTriggerReactiveAbilities(player, "onCrit");
        }

        // Determine weapon skill for XP awards (from item's skillReference trait)
        string? weaponSkillSlug = player.GetEquippedWeaponSkillSlug();

        // Calculate base damage (weapon + attribute bonuses)
        int baseDamage = CalculatePlayerDamage(player);

        // Calculate elemental damage from weapon traits
        var (elementalDamage, damageType) = CalculateElementalDamage(player.EquippedMainHand);
        baseDamage += elementalDamage;

        // Apply status effect stat modifiers
        if (playerStatusResult.TotalStatModifiers.TryGetValue("attack", out int attackMod))
        {
            baseDamage += attackMod;
        }

        // Apply skill damage multiplier (pass weapon skill slug for bonus)
        double skillMultiplier = SkillEffectCalculator.GetPhysicalDamageMultiplier(player, weaponSkillSlug);
        baseDamage = (int)(baseDamage * skillMultiplier);

        // Apply critical multiplier
        if (isCritical)
        {
            baseDamage = (int)(baseDamage * 2.0);
        }

        // Apply damage type modifier (resistance/vulnerability)
        double damageTypeModifier = CalculateDamageTypeModifier(damageType, enemy);
        baseDamage = (int)(baseDamage * damageTypeModifier);

        // Apply enemy defense
        int finalDamage = Math.Max(1, baseDamage - enemy.GetPhysicalDefense());

        // Apply difficulty multiplier to player damage
        var difficulty = _saveGameService.GetDifficultySettings();
        finalDamage = (int)(finalDamage * difficulty.PlayerDamageMultiplier);
        finalDamage = Math.Max(1, finalDamage); // Ensure at least 1 damage

        // Apply damage to enemy
        enemy.Health = Math.Max(0, enemy.Health - finalDamage);
        result.Damage = finalDamage;

        // Award skill XP for successful hit
        if (!string.IsNullOrEmpty(weaponSkillSlug))
        {
            int xpAmount = isCritical ? 15 : 5; // More XP for critical hits
            await _mediator.Send(new AwardSkillXPCommand
            {
                Character = player,
                SkillId = weaponSkillSlug,
                XPAmount = xpAmount,
                ActionSource = isCritical ? "critical_hit" : $"{weaponSkillSlug.Replace("-", "_")}_hit"
            });
        }

        // Award precision skill XP for critical hits
        if (isCritical)
        {
            await _mediator.Send(new AwardSkillXPCommand
            {
                Character = player,
                SkillId = "precision",
                XPAmount = 10,
                ActionSource = "critical_hit"
            });
        }

        // Apply elemental status effects (20% chance per hit)
        if (damageType != "physical" && _random.Next(100) < 20)
        {
            StatusEffectType statusEffect = damageType switch
            {
                "fire" => StatusEffectType.Burning,
                "ice" => StatusEffectType.Frozen,
                "lightning" => StatusEffectType.Stunned, // Lightning stuns
                "poison" => StatusEffectType.Poisoned,
                _ => (StatusEffectType)(-1) // Invalid sentinel value
            };

            // Only apply if valid status effect
            if ((int)statusEffect >= 0)
            {
                int duration = damageType switch
                {
                    "fire" => 3,      // Burning: 3 turns
                    "ice" => 2,       // Frozen: 2 turns (stun)
                    "lightning" => 2, // Stunned: 2 turns
                    "poison" => 5,    // Poisoned: 5 turns
                    _ => 0
                };

                int tickDamage = damageType switch
                {
                    "fire" => 5,      // Burning: 5 damage/turn
                    "poison" => 4,    // Poisoned: 4 damage/turn
                    _ => 0
                };

                var category = statusEffect switch
                {
                    StatusEffectType.Burning => StatusEffectCategory.DamageOverTime,
                    StatusEffectType.Poisoned => StatusEffectCategory.DamageOverTime,
                    StatusEffectType.Frozen => StatusEffectCategory.CrowdControl,
                    StatusEffectType.Stunned => StatusEffectCategory.CrowdControl,
                    _ => StatusEffectCategory.Debuff
                };

                var effect = new StatusEffect
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = statusEffect,
                    Category = category,
                    Name = statusEffect.ToString(),
                    Description = $"Afflicted by {damageType} damage",
                    RemainingDuration = duration,
                    OriginalDuration = duration,
                    TickDamage = tickDamage,
                    DamageType = damageType,
                    Source = $"Elemental weapon"
                };

                await _mediator.Send(new ApplyStatusEffectCommand
                {
                    TargetEnemy = enemy,
                    Effect = effect
                });

                result.Message += $"\n{enemy.Name} is now {statusEffect.ToString().ToLower()}!";
            }
        }

        // Generate message
        if (isCritical)
        {
            result.Message = $"CRITICAL HIT! You dealt {finalDamage} damage to {enemy.Name}!";
        }
        else
        {
            result.Message = $"You dealt {finalDamage} damage to {enemy.Name}.";
        }

        return result;
    }

    /// <summary>
    /// Execute an enemy attack on the player.
    /// </summary>
    public async Task<CombatResult> ExecuteEnemyAttack(Enemy enemy, Character player, bool isDefending = false)
    {
        var result = new CombatResult { Success = true };

        // Process status effects on enemy at start of turn
        var enemyStatusResult = await _mediator.Send(new ProcessStatusEffectsCommand { TargetEnemy = enemy });
        result.DotDamage = enemyStatusResult.TotalDamageTaken;
        result.HotHealing = enemyStatusResult.TotalHealingReceived;
        result.ActiveStatusEffects = enemy.ActiveStatusEffects?.ToList() ?? new List<StatusEffect>();
        result.StatusEffectsExpired = enemyStatusResult.ExpiredEffectTypes.ToList();
        
        // Check if enemy can act (not crowd controlled)
        if (!CanAct(enemy))
        {
            result.Success = false;
            result.Message = $"{enemy.Name} is crowd controlled and cannot act!";
            return result;
        }

        // Process player status effects to get stat modifiers (for defense calculation later)
        var playerStatusResult = await _mediator.Send(new ProcessStatusEffectsCommand { TargetCharacter = player });

        // Check if player dodges (base + skill bonus)
        double dodgeChance = player.GetDodgeChance() + SkillEffectCalculator.GetDodgeChanceBonus(player);
        if (RollDodge(dodgeChance))
        {
            result.IsDodged = true;
            result.Message = $"You dodged {enemy.Name}'s attack!";
            
            // Trigger reactive abilities on dodge
            _reactiveAbilityService.CheckAndTriggerReactiveAbilities(player, "onDodge");
            
            // Award acrobatics XP for successful dodge
            await _mediator.Send(new AwardSkillXPCommand
            {
                Character = player,
                SkillId = "acrobatics",
                XPAmount = 8,
                ActionSource = "successful_dodge"
            });
            
            return result;
        }

        // If defending, chance to block
        if (isDefending && RollBlock(50.0)) // 50% block chance when defending
        {
            result.IsBlocked = true;
            result.Message = $"You blocked {enemy.Name}'s attack!";
            
            // Trigger reactive abilities on block
            _reactiveAbilityService.CheckAndTriggerReactiveAbilities(player, "onBlock");
            
            // Award block XP for successful block
            await _mediator.Send(new AwardSkillXPCommand
            {
                Character = player,
                SkillId = "block",
                XPAmount = 8,
                ActionSource = "successful_block"
            });
            
            return result;
        }

        // Check for critical hit
        bool isCritical = RollCritical(enemy.GetCriticalChance());
        result.IsCritical = isCritical;

        // Calculate base damage
        int baseDamage = enemy.BasePhysicalDamage + enemy.GetPhysicalDamageBonus();

        // Apply status effect stat modifiers
        if (enemyStatusResult.TotalStatModifiers.TryGetValue("attack", out int attackMod))
        {
            baseDamage += attackMod;
        }

        // Roll variance (±20%)
        baseDamage = (int)(baseDamage * _random.Next(80, 121) / 100.0);

        // Apply critical multiplier
        if (isCritical)
        {
            baseDamage = (int)(baseDamage * 2.0);
        }

        // If defending, reduce damage by 50%
        if (isDefending)
        {
            baseDamage = baseDamage / 2;
            
            // Award armor skill XP for defending (taking reduced damage)
            await _mediator.Send(new AwardSkillXPCommand
            {
                Character = player,
                SkillId = "armor",
                XPAmount = 5,
                ActionSource = "damage_reduction"
            });
        }

        // Apply player defense (base + skill multiplier)
        int playerDefense = player.GetPhysicalDefense();
        double defenseMultiplier = SkillEffectCalculator.GetPhysicalDefenseMultiplier(player);
        playerDefense = (int)(playerDefense * defenseMultiplier);

        // Apply status effect stat modifiers to defense
        if (playerStatusResult.TotalStatModifiers.TryGetValue("defense", out int defenseMod))
        {
            playerDefense += defenseMod;
        }

        int finalDamage = Math.Max(1, baseDamage - playerDefense);

        // Apply difficulty multiplier to enemy damage
        var difficulty = _saveGameService.GetDifficultySettings();
        finalDamage = (int)(finalDamage * difficulty.EnemyDamageMultiplier);
        finalDamage = Math.Max(1, finalDamage); // Ensure at least 1 damage

        // Apply damage to player
        player.Health = Math.Max(0, player.Health - finalDamage);
        result.Damage = finalDamage;

        // Trigger reactive abilities on damage taken
        _reactiveAbilityService.CheckAndTriggerReactiveAbilities(player, "onDamageTaken");

        // Award armor skill XP when taking damage (not defending)
        if (!isDefending && finalDamage > 0)
        {
            await AwardArmorSkillXP(player, finalDamage);
        }

        // Generate message
        if (isCritical)
        {
            result.Message = $"{enemy.Name} scored a CRITICAL HIT! You took {finalDamage} damage!";
        }
        else if (isDefending)
        {
            result.Message = $"{enemy.Name} attacked! You defended and took {finalDamage} damage.";
        }
        else
        {
            result.Message = $"{enemy.Name} attacked! You took {finalDamage} damage.";
        }

        return result;
    }

    /// <summary>
    /// Execute an enemy ability on the player (decided by AI).
    /// </summary>
    /// <param name="enemy">The enemy using the ability</param>
    /// <param name="player">The target player</param>
    /// <param name="abilityStates">Dictionary tracking ability cooldowns (abilityId -> turns remaining)</param>
    /// <returns>Result of the ability usage, or null if no ability was used</returns>
    public UseAbilityResult? ExecuteEnemyAbility(Enemy enemy, Character player, Dictionary<string, int> abilityStates)
    {
        // AI decides whether to use an ability
        var chosenAbilityId = _enemyAbilityAI.DecideAbilityUsage(enemy, player, abilityStates);
        
        if (chosenAbilityId == null)
        {
            return null; // AI chose to use basic attack instead
        }

        // Find the chosen ability
        var ability = enemy.Abilities.FirstOrDefault(a => a.Id == chosenAbilityId);
        if (ability == null)
        {
            Log.Warning($"Enemy {enemy.Name} tried to use ability {chosenAbilityId} but it was not found");
            return null;
        }

        // Check if ability requires mana (future enhancement - enemies don't have mana yet)
        // For now, assume all enemy abilities are usable
        
        // Apply ability cooldown
        abilityStates[chosenAbilityId] = ability.Cooldown;

        // Execute ability effect
        var result = new UseAbilityResult
        {
            Success = true,
            Message = $"{enemy.Name} used {ability.Name}!",
            AbilityUsed = ability,
            ManaCost = 0, // Enemies don't use mana for now
            DamageDealt = 0,
            HealingDone = 0
        };

        // Apply ability effects based on type
        switch (ability.Type)
        {
            case AbilityTypeEnum.Offensive:
                // Calculate ability damage
                int abilityDamage = CalculateAbilityDamage(enemy, ability);
                player.Health = Math.Max(0, player.Health - abilityDamage);
                result = result with 
                { 
                    DamageDealt = abilityDamage,
                    Message = $"{enemy.Name} used {ability.Name}! You took {abilityDamage} damage!"
                };
                
                // Trigger reactive abilities on damage taken
                _reactiveAbilityService.CheckAndTriggerReactiveAbilities(player, "onDamageTaken");
                break;

            case AbilityTypeEnum.Defensive:
            case AbilityTypeEnum.Healing:
                // Healing ability
                int healAmount = CalculateAbilityHealing(enemy, ability);
                enemy.Health = Math.Min(enemy.MaxHealth, enemy.Health + healAmount);
                result = result with 
                { 
                    HealingDone = healAmount,
                    Message = $"{enemy.Name} used {ability.Name} and restored {healAmount} health!"
                };
                break;

            case AbilityTypeEnum.Buff:
                // Buff abilities (future enhancement - apply buffs to enemy)
                result = result with { Message = $"{enemy.Name} used {ability.Name}!" };
                break;

            case AbilityTypeEnum.Debuff:
                // Debuff abilities (future enhancement - apply debuffs to player)
                result = result with { Message = $"{enemy.Name} used {ability.Name} on you!" };
                break;
        }

        Log.Information($"Enemy {enemy.Name} used ability {ability.Name} (Type: {ability.Type})");
        return result;
    }

    /// <summary>
    /// Execute spell casting by enemy using AI to decide which spell to use.
    /// Returns null if no spell was cast.
    /// </summary>
    public CastSpellResult? ExecuteEnemySpell(Enemy enemy, Character player, SpellCatalogService spellCatalog)
    {
        // AI decides which spell to cast (if any)
        string? chosenSpellId = _enemySpellCastingAI.DecideSpellCasting(enemy, player);
        
        if (chosenSpellId == null)
        {
            return null; // AI chose not to cast spell
        }

        // Get spell from catalog
        var spell = spellCatalog.GetSpell(chosenSpellId);
            
        if (spell == null)
        {
            Log.Warning($"Enemy {enemy.Name} tried to cast unknown spell {chosenSpellId}");
            return null;
        }

        // Calculate actual mana cost (Intelligence reduces cost)
        int manaCost = _enemySpellCastingAI.CalculateManaCost(spell, enemy);
        
        // Deduct mana
        enemy.Mana -= manaCost;

        // Initialize result
        var result = new CastSpellResult
        {
            Success = true,
            Message = string.Empty, // Will be set below based on effect
            ManaCostPaid = manaCost,
            SpellCast = spell
        };

        // Apply spell effects based on type
        switch (spell.EffectType)
        {
            case SpellEffectType.Damage:
                // Damage spell - apply damage to player
                int spellDamage = CalculateSpellDamage(enemy, spell);
                player.Health = Math.Max(0, player.Health - spellDamage);
                result = result with 
                { 
                    EffectValue = spellDamage.ToString(),
                    Message = $"{enemy.Name} cast {spell.DisplayName} and dealt {spellDamage} damage!"
                };

                // Trigger reactive abilities (player takes damage)
                _reactiveAbilityService.CheckAndTriggerReactiveAbilities(player, "onDamageTaken");
                break;

            case SpellEffectType.Heal:
                // Healing spell
                int healAmount = CalculateSpellHealing(enemy, spell);
                enemy.Health = Math.Min(enemy.MaxHealth, enemy.Health + healAmount);
                result = result with 
                { 
                    EffectValue = healAmount.ToString(),
                    Message = $"{enemy.Name} cast {spell.DisplayName} and restored {healAmount} health!"
                };
                break;

            case SpellEffectType.Buff:
            case SpellEffectType.Protection:
                // Defensive buff (future enhancement - apply buffs to enemy)
                result = result with { Message = $"{enemy.Name} cast {spell.DisplayName}!" };
                break;

            case SpellEffectType.Debuff:
            case SpellEffectType.Control:
                // Debuff spell (future enhancement - apply debuffs to player)
                result = result with { Message = $"{enemy.Name} cast {spell.DisplayName} on you!" };
                break;

            case SpellEffectType.Utility:
            case SpellEffectType.Summon:
                // Utility spell (future enhancement)
                result = result with { Message = $"{enemy.Name} cast {spell.DisplayName}!" };
                break;
        }

        // Set cooldown
        if (!enemy.SpellCooldowns.ContainsKey(spell.SpellId))
        {
            enemy.SpellCooldowns[spell.SpellId] = spell.Cooldown;
        }
        else
        {
            enemy.SpellCooldowns[spell.SpellId] = spell.Cooldown;
        }

        Log.Information($"Enemy {enemy.Name} cast spell {spell.DisplayName} (Type: {spell.EffectType}, Mana: {manaCost})");
        return result;
    }

    /// <summary>
    /// Calculate damage for an enemy ability.
    /// </summary>
    private int CalculateAbilityDamage(Enemy enemy, Ability ability)
    {
        // Base damage from ability (if no BaseDamage string, use flat 10)
        int baseDamage = 10;
        
        if (!string.IsNullOrEmpty(ability.BaseDamage))
        {
            // Roll dice for damage (e.g., "2d6" rolls 2 six-sided dice)
            baseDamage = RollDice(ability.BaseDamage);
        }

        // Add enemy's intelligence bonus (for magic attacks)
        baseDamage += enemy.GetMagicDamageBonus();

        // Apply variance (±20%)
        baseDamage = (int)(baseDamage * _random.Next(80, 121) / 100.0);

        return Math.Max(1, baseDamage);
    }

    /// <summary>
    /// Calculate damage for an enemy spell.
    /// </summary>
    private int CalculateSpellDamage(Enemy enemy, Spell spell)
    {
        // Base damage from spell
        int baseDamage = 15; // Spells generally stronger than abilities
        
        if (!string.IsNullOrEmpty(spell.BaseEffectValue))
        {
            baseDamage = RollDice(spell.BaseEffectValue);
        }

        // Add enemy's intelligence bonus (spells scale with intelligence)
        baseDamage += enemy.GetMagicDamageBonus();

        // Apply variance (±15% for spells, more reliable than physical attacks)
        baseDamage = (int)(baseDamage * _random.Next(85, 116) / 100.0);

        return Math.Max(1, baseDamage);
    }

    /// <summary>
    /// Calculate healing for an enemy ability.
    /// </summary>
    private int CalculateAbilityHealing(Enemy enemy, Ability ability)
    {
        // Base healing from ability (if no BaseDamage string, use flat 10)
        int baseHealing = 10;
        
        if (!string.IsNullOrEmpty(ability.BaseDamage))
        {
            // Use BaseDamage for healing amount
            baseHealing = RollDice(ability.BaseDamage);
        }

        // Add intelligence bonus
        baseHealing += enemy.Intelligence / 2;

        // Apply variance (±20%)
        baseHealing = (int)(baseHealing * _random.Next(80, 121) / 100.0);

        return Math.Max(1, baseHealing);
    }

    /// <summary>
    /// Calculate healing for an enemy spell.
    /// </summary>
    private int CalculateSpellHealing(Enemy enemy, Spell spell)
    {
        // Base healing from spell
        int baseHealing = 20; // Healing spells generally stronger
        
        if (!string.IsNullOrEmpty(spell.BaseEffectValue))
        {
            baseHealing = RollDice(spell.BaseEffectValue);
        }

        // Add intelligence bonus (spells scale with intelligence)
        baseHealing += enemy.Intelligence / 2;

        // Apply variance (±15% for spells)
        baseHealing = (int)(baseHealing * _random.Next(85, 116) / 100.0);

        return Math.Max(1, baseHealing);
    }

    /// <summary>
    /// Roll dice using standard notation (e.g., "2d6" rolls 2 six-sided dice).
    /// Supports modifiers like "2d6+3" or "1d20-2".
    /// </summary>
    private int RollDice(string diceNotation)
    {
        try
        {
            // Parse "XdY" or "XdY+Z" or "XdY-Z" format
            var parts = diceNotation.ToLower().Split('d');
            if (parts.Length == 2)
            {
                int count = int.Parse(parts[0].Trim());
                
                // Handle modifiers (+ or -)
                int sides = 0;
                int modifier = 0;
                
                if (parts[1].Contains('+'))
                {
                    var sideParts = parts[1].Split('+');
                    sides = int.Parse(sideParts[0].Trim());
                    modifier = int.Parse(sideParts[1].Trim());
                }
                else if (parts[1].Contains('-'))
                {
                    var sideParts = parts[1].Split('-');
                    sides = int.Parse(sideParts[0].Trim());
                    modifier = -int.Parse(sideParts[1].Trim());
                }
                else
                {
                    sides = int.Parse(parts[1].Trim());
                }
                
                // Roll the dice
                int total = 0;
                for (int i = 0; i < count; i++)
                {
                    total += _random.Next(1, sides + 1);
                }
                
                return total + modifier;
            }
        }
        catch
        {
            // Fallback to default if parsing fails
        }
        
        return 10; // Default fallback
    }

    /// <summary>
    /// Calculate player's attack damage based on equipped weapon and attributes.
    /// Includes elemental damage traits from weapons.
    /// </summary>
    private int CalculatePlayerDamage(Character player)
    {
        int weaponDamage = 5; // Base unarmed damage

        // Check if player has a weapon equipped
        if (player.EquippedMainHand != null && player.EquippedMainHand.Type == ItemType.Weapon)
        {
            weaponDamage = 8 + (player.EquippedMainHand.Rarity switch
            {
                ItemRarity.Common => 2,
                ItemRarity.Uncommon => 5,
                ItemRarity.Rare => 10,
                ItemRarity.Epic => 18,
                ItemRarity.Legendary => 30,
                _ => 2
            });

            // Add weapon's bonus strength
            weaponDamage += (int)player.EquippedMainHand.GetTotalTrait("Strength", 0);
        }

        // Add STR bonus
        int totalDamage = weaponDamage + player.GetPhysicalDamageBonus();

        // Add variance (±15%)
        totalDamage = (int)(totalDamage * _random.Next(85, 116) / 100.0);

        return Math.Max(1, totalDamage);
    }

    /// <summary>
    /// Calculate elemental damage bonus from weapon traits.
    /// Checks for fire, ice, lightning, and poison damage traits.
    /// </summary>
    /// <param name="weapon">The weapon to check for elemental traits.</param>
    /// <returns>The elemental damage bonus and damage type.</returns>
    private (int elementalDamage, string damageType) CalculateElementalDamage(Item? weapon)
    {
        if (weapon == null)
            return (0, "physical");

        var traits = weapon.GetTotalTraits();
        int elementalDamage = 0;
        string damageType = "physical";

        // Check for elemental damage traits
        if (traits.TryGetValue("fireDamage", out var fireDamage))
        {
            elementalDamage = fireDamage.AsInt();
            damageType = "fire";
        }
        else if (traits.TryGetValue("iceDamage", out var iceDamage))
        {
            elementalDamage = iceDamage.AsInt();
            damageType = "ice";
        }
        else if (traits.TryGetValue("lightningDamage", out var lightningDamage))
        {
            elementalDamage = lightningDamage.AsInt();
            damageType = "lightning";
        }
        else if (traits.TryGetValue("poisonDamage", out var poisonDamage))
        {
            elementalDamage = poisonDamage.AsInt();
            damageType = "poison";
        }
        else if (traits.TryGetValue("damageType", out var dmgType))
        {
            // Check generic damageType trait
            damageType = dmgType.AsString().ToLower();
        }

        return (elementalDamage, damageType);
    }

    /// <summary>
    /// Calculate damage resistance/vulnerability modifier based on damage type and enemy traits.
    /// Returns a multiplier (0.5 for resistance, 1.0 for normal, 1.5 for weakness, 2.0 for vulnerability).
    /// </summary>
    /// <param name="damageType">The type of damage being dealt (fire, ice, lightning, poison, physical).</param>
    /// <param name="enemy">The enemy being damaged.</param>
    /// <returns>Damage multiplier based on resistances/vulnerabilities.</returns>
    private double CalculateDamageTypeModifier(string damageType, Enemy enemy)
    {
        if (damageType == "physical")
            return 1.0; // Physical damage is always neutral

        var traits = enemy.Traits;
        
        // Check for immunity
        if (traits.TryGetValue($"immuneTo{CapitalizeFirst(damageType)}", out var immunity) && immunity.AsBool())
            return 0.0; // Immune - no damage

        // Check for resistance
        if (traits.TryGetValue($"resist{CapitalizeFirst(damageType)}", out var resistance))
        {
            int resistValue = resistance.AsInt();
            if (resistValue >= 50)
                return 0.5; // Strong resistance - half damage
            else if (resistValue > 0)
                return 0.75; // Moderate resistance
        }

        // Check for vulnerability/weakness
        if (traits.TryGetValue("vulnerability", out var vuln))
        {
            var vulnType = vuln.AsString().ToLower();
            if (vulnType == damageType)
                return 2.0; // Vulnerable - double damage
        }

        // Check for weakness (alternative name)
        if (traits.TryGetValue("weakness", out var weakness))
        {
            var weakType = weakness.AsString().ToLower();
            if (weakType == damageType)
                return 1.5; // Weak - 50% bonus damage
        }

        return 1.0; // Normal damage
    }

    /// <summary>
    /// Capitalize first letter of a string.
    /// </summary>
    private static string CapitalizeFirst(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;
        return char.ToUpper(input[0]) + input.Substring(1);
    }

    /// <summary>
    /// Attempt to flee from combat.
    /// </summary>
    public CombatResult AttemptFlee(Character player, Enemy enemy)
    {
        // Success chance based on player DEX vs enemy DEX
        double baseChance = 50.0;
        double dexDifference = (player.Dexterity - enemy.Dexterity) * 5.0;
        double fleeChance = Math.Clamp(baseChance + dexDifference, 10.0, 90.0);

        bool success = _random.Next(0, 100) < fleeChance;

        return new CombatResult
        {
            Success = success,
            Message = success
                ? "You successfully fled from combat!"
                : "Failed to escape! The enemy blocks your path!"
        };
    }

    /// <summary>
    /// Use a consumable item in combat.
    /// </summary>
    public CombatResult UseItemInCombat(Character player, Item item)
    {
        var result = new CombatResult { Success = true };

        if (item.Type != ItemType.Consumable)
        {
            result.Success = false;
            result.Message = $"{item.Name} cannot be used in combat.";
            return result;
        }

        // Apply consumable effects (simplified - expand later)
        if (item.Name.Contains("Health", StringComparison.OrdinalIgnoreCase) ||
            item.Name.Contains("Potion", StringComparison.OrdinalIgnoreCase))
        {
            int healing = 30; // Base healing
            player.Health = Math.Min(player.MaxHealth, player.Health + healing);
            result.Healing = healing;
            result.Message = $"You used {item.Name} and restored {healing} health!";
        }
        else if (item.Name.Contains("Mana", StringComparison.OrdinalIgnoreCase))
        {
            int manaRestore = 20;
            player.Mana = Math.Min(player.MaxMana, player.Mana + manaRestore);
            result.Message = $"You used {item.Name} and restored {manaRestore} mana!";
        }
        else
        {
            result.Message = $"You used {item.Name}.";
        }

        // Remove item from inventory
        player.Inventory.Remove(item);

        return result;
    }

    /// <summary>
    /// Roll for dodge based on chance percentage.
    /// </summary>
    private bool RollDodge(double dodgeChance)
    {
        return _random.Next(0, 10000) < (dodgeChance * 100);
    }

    /// <summary>
    /// Roll for critical hit based on chance percentage.
    /// </summary>
    private bool RollCritical(double critChance)
    {
        return _random.Next(0, 10000) < (critChance * 100);
    }

    /// <summary>
    /// Roll for block when defending.
    /// </summary>
    private bool RollBlock(double blockChance)
    {
        return _random.Next(0, 100) < blockChance;
    }

    /// <summary>
    /// Generate combat outcome after victory.
    /// </summary>
    public async Task<CombatOutcome> GenerateVictoryOutcome(Character player, Enemy enemy)
    {
        var outcome = new CombatOutcome
        {
            PlayerVictory = true,
            XPGained = enemy.XPReward,
            GoldGained = enemy.GoldReward,
            DefeatedEnemyId = enemy.Id,
            DefeatedEnemyType = enemy.Type.ToString()
        };

        // Award weapon skill XP for kill (from item's skillReference trait)
        string? weaponSkillSlug = player.GetEquippedWeaponSkillSlug();
        if (!string.IsNullOrEmpty(weaponSkillSlug))
        {
            await _mediator.Send(new AwardSkillXPCommand
            {
                Character = player,
                SkillId = weaponSkillSlug,
                XPAmount = 20,
                ActionSource = $"{weaponSkillSlug.Replace("-", "_")}_kill"
            });
        }

        // Update quest progress for enemy kills
        await UpdateQuestProgressForKill(enemy, outcome);

        // Note: Loot generation is handled by ItemGenerator in modern implementation
        // Enemy gold drops are already added to outcome.GoldEarned above

        // Generate summary
        outcome.Summary = GenerateVictorySummary(enemy, outcome);

        return outcome;
    }

    /// <summary>
    /// Generate loot item based on enemy difficulty and player stats.
    /// </summary>
    private Item? GenerateLoot(Character player, Enemy enemy)
    {
        double lootChance = GetLootChance(enemy.Difficulty);
        lootChance += player.GetRareItemChance();

        if (_random.Next(0, 100) >= lootChance)
        {
            return null;
        }

        var lootRarity = DetermineLootRarity(enemy.Difficulty);
        var lootType = (ItemType)_random.Next(1, 14); // Skip Consumable at 0
        // TODO: Modernize - // TODO: Modernize - var lootItems = Generators.ItemGenerator.GenerateByType(lootType, 1);

        // TODO: Modernize - if (lootItems.Count == 0)
        {
            return null;
        }

        // TODO: Modernize - var loot = lootItems[0];
        // TODO: Modernize - loot.Rarity = lootRarity;
        // TODO: Modernize - return loot;
    }

    /// <summary>
    /// Get base loot drop chance for enemy difficulty.
    /// </summary>
    private double GetLootChance(EnemyDifficulty difficulty)
    {
        return difficulty switch
        {
            EnemyDifficulty.Easy => 20.0,
            EnemyDifficulty.Normal => 40.0,
            EnemyDifficulty.Hard => 60.0,
            EnemyDifficulty.Elite => 80.0,
            EnemyDifficulty.Boss => 100.0,
            _ => 30.0
        };
    }

    /// <summary>
    /// Determine loot rarity based on enemy difficulty.
    /// </summary>
    private ItemRarity DetermineLootRarity(EnemyDifficulty difficulty)
    {
        return difficulty switch
        {
            EnemyDifficulty.Easy => ItemRarity.Common,
            EnemyDifficulty.Normal => _random.Next(0, 100) < 70 ? ItemRarity.Common : ItemRarity.Uncommon,
            EnemyDifficulty.Hard => _random.Next(0, 100) < 50 ? ItemRarity.Uncommon : ItemRarity.Rare,
            EnemyDifficulty.Elite => _random.Next(0, 100) < 60 ? ItemRarity.Rare : ItemRarity.Epic,
            EnemyDifficulty.Boss => _random.Next(0, 100) < 30 ? ItemRarity.Epic : ItemRarity.Legendary,
            _ => ItemRarity.Common
        };
    }

    /// <summary>
    /// Generate victory summary message.
    /// </summary>
    private string GenerateVictorySummary(Enemy enemy, CombatOutcome outcome)
    {
        var summary = $"Victory! Defeated {enemy.Name}!\n\n";
        summary += $"[green]+{outcome.XPGained} XP[/]\n";
        summary += $"[yellow]+{outcome.GoldGained} Gold[/]\n";

        if (outcome.LootDropped.Any())
        {
        // TODO: Modernize - summary += $"\n[cyan]Loot:[/]\n";
            foreach (var item in outcome.LootDropped)
            {
                summary += $"  • {item.Name} ({item.Rarity})\n";
            }
        }

        return summary;
    }

    /// <summary>
    /// Award armor skill XP based on equipped armor pieces and damage taken.
    /// </summary>
    /// <param name="player">The character taking damage.</param>
    /// <param name="damageTaken">Amount of damage taken.</param>
    private async Task AwardArmorSkillXP(Character player, int damageTaken)
    {
        // Calculate base XP from damage taken (minimum 2 XP, scale with damage)
        int baseXP = Math.Max(2, damageTaken / 10);

        // Get all equipped armor pieces
        var armorPieces = new[]
        {
            player.EquippedHelmet,
            player.EquippedShoulders,
            player.EquippedChest,
            player.EquippedBracers,
            player.EquippedGloves,
            player.EquippedBelt,
            player.EquippedLegs,
            player.EquippedBoots
        }.Where(item => item != null).ToList();

        // Award XP to each armor piece's associated skill
        foreach (var armor in armorPieces)
        {
            // Get the skillReference trait from the armor
            var totalTraits = armor!.GetTotalTraits();
            
            if (totalTraits.TryGetValue("skillReference", out var skillRefTrait) &&
                skillRefTrait.Type == TraitType.String &&
                !string.IsNullOrWhiteSpace(skillRefTrait.AsString()))
            {
                // Extract skill ID from reference (e.g., "@skills/armor:plate-armor" -> "plate-armor")
                string skillReference = skillRefTrait.AsString();
                string skillId = ExtractSkillIdFromReference(skillReference);

                if (!string.IsNullOrWhiteSpace(skillId))
                {
                    await _mediator.Send(new AwardSkillXPCommand
                    {
                        Character = player,
                        SkillId = skillId,
                        XPAmount = baseXP,
                        ActionSource = "armor_damage_taken"
                    });
                }
            }
        }
    }

    /// <summary>
    /// Extract the skill ID from a reference string (e.g., "@skills/armor:plate-armor" -> "plate-armor").
    /// </summary>
    private string ExtractSkillIdFromReference(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return string.Empty;

        // Format: @domain/path:item-name
        int colonIndex = reference.IndexOf(':');
        if (colonIndex > 0 && colonIndex < reference.Length - 1)
        {
            return reference.Substring(colonIndex + 1);
        }

        return string.Empty;
    }

    /// <summary>
    /// Check if a character can act (not crowd controlled).
    /// </summary>
    /// <param name="character">The character to check.</param>
    /// <returns>True if character can act, false if crowd controlled.</returns>
    private bool CanAct(Character character)
    {
        // Check for crowd control effects that prevent actions
        if (character.ActiveStatusEffects == null || !character.ActiveStatusEffects.Any())
            return true;

        return !character.ActiveStatusEffects.Any(e =>
            e.Type == StatusEffectType.Stunned ||
            e.Type == StatusEffectType.Frozen ||
            e.Type == StatusEffectType.Paralyzed);
    }

    /// <summary>
    /// Check if an enemy can act (not crowd controlled).
    /// </summary>
    /// <param name="enemy">The enemy to check.</param>
    /// <returns>True if enemy can act, false if crowd controlled.</returns>
    private bool CanAct(Enemy enemy)
    {
        // Check for crowd control effects that prevent actions
        if (enemy.ActiveStatusEffects == null || !enemy.ActiveStatusEffects.Any())
            return true;

        return !enemy.ActiveStatusEffects.Any(e =>
            e.Type == StatusEffectType.Stunned ||
            e.Type == StatusEffectType.Frozen ||
            e.Type == StatusEffectType.Paralyzed);
    }

    /// <summary>
    /// Get crowd control message for character.
    /// </summary>
    /// <param name="character">The character who is crowd controlled.</param>
    /// <returns>Message describing the crowd control effect.</returns>
    private string GetCrowdControlMessage(Character character)
    {
        if (character.ActiveStatusEffects == null || !character.ActiveStatusEffects.Any())
            return "You cannot act!";

        var ccEffect = character.ActiveStatusEffects.FirstOrDefault(e =>
            e.Type == StatusEffectType.Stunned ||
            e.Type == StatusEffectType.Frozen ||
            e.Type == StatusEffectType.Paralyzed);

        if (ccEffect == null)
            return "You cannot act!";

        return ccEffect.Type switch
        {
            StatusEffectType.Stunned => "You are stunned and cannot act!",
            StatusEffectType.Frozen => "You are frozen solid and cannot move!",
            StatusEffectType.Paralyzed => "You are paralyzed and cannot act!",
            _ => "You cannot act!"
        };
    }

    /// <summary>
    /// Updates quest progress for all active quests based on enemy kill.
    /// Checks for objectives matching defeat_{enemy_id} or defeat_{enemy_type}.
    /// </summary>
    /// <param name="enemy">The defeated enemy.</param>
    /// <param name="outcome">The combat outcome to populate with quest progress information.</param>
    private async Task UpdateQuestProgressForKill(Enemy enemy, CombatOutcome outcome)
    {
        var saveGame = _saveGameService.GetCurrentSave();
        if (saveGame == null || !saveGame.ActiveQuests.Any())
            return;

        var enemyId = enemy.Id.Replace(" ", "_").ToLowerInvariant();
        var enemyType = enemy.Type.ToString().Replace(" ", "_").ToLowerInvariant();
        var enemyIdObjective = "defeat_" + enemyId;
        var enemyTypeObjective = "defeat_" + enemyType;

        foreach (var quest in saveGame.ActiveQuests)
        {
            if (quest.Objectives.ContainsKey(enemyIdObjective))
            {
                var result = await _mediator.Send(new UpdateQuestProgressCommand(
                    quest.Id,
                    enemyIdObjective,
                    1
                ));

                if (result.ObjectiveCompleted)
                {
                    outcome.QuestObjectivesCompleted.Add($"{quest.Title}: {enemyIdObjective}");
                    Log.Information("Quest objective completed: {QuestId}/{ObjectiveId}", quest.Id, enemyIdObjective);
                }

                if (result.QuestCompleted)
                {
                    outcome.QuestsCompleted.Add(quest.Title);
                    Log.Information("Quest completed: {QuestId}", quest.Id);
                }
            }

            if (quest.Objectives.ContainsKey(enemyTypeObjective))
            {
                var result = await _mediator.Send(new UpdateQuestProgressCommand(
                    quest.Id,
                    enemyTypeObjective,
                    1
                ));

                if (result.ObjectiveCompleted)
                {
                    outcome.QuestObjectivesCompleted.Add($"{quest.Title}: {enemyTypeObjective}");
                    Log.Information("Quest objective completed: {QuestId}/{ObjectiveId}", quest.Id, enemyTypeObjective);
                }

                if (result.QuestCompleted)
                {
                    outcome.QuestsCompleted.Add(quest.Title);
                    Log.Information("Quest completed: {QuestId}", quest.Id);
                }
            }
        }
    }

}