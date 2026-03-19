using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Combat.Commands.EncounterBoss;

/// <summary>
/// Handler for boss encounter command.
/// Generates boss enemy and provides detailed information.
/// </summary>
public class EncounterBossCommandHandler : IRequestHandler<EncounterBossCommand, BossEncounterResult>
{
    private readonly EnemyGenerator _enemyGenerator;
    private readonly ILogger<EncounterBossCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EncounterBossCommandHandler"/> class.
    /// </summary>
    /// <param name="enemyGenerator">The enemy generator.</param>
    /// <param name="logger">The logger.</param>
    public EncounterBossCommandHandler(
        EnemyGenerator enemyGenerator,
        ILogger<EncounterBossCommandHandler> logger)
    {
        _enemyGenerator = enemyGenerator ?? throw new ArgumentNullException(nameof(enemyGenerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handles the boss encounter command and returns detailed boss information.
    /// </summary>
    /// <param name="request">The boss encounter command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, containing the boss encounter result.</returns>
    public async Task<BossEncounterResult> Handle(EncounterBossCommand request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Initiating boss encounter: {BossName} from {Category}", request.BossName, request.BossCategory);

            // Generate the boss enemy with full hydration
            var boss = await _enemyGenerator.GenerateEnemyByNameAsync(request.BossCategory, request.BossName, hydrate: true);

            if (boss == null)
            {
                return new BossEncounterResult
                {
                    Success = false,
                    ErrorMessage = $"Boss '{request.BossName}' not found in category '{request.BossCategory}'"
                };
            }

            // Verify this is actually a boss
            if (boss.Difficulty != EnemyDifficulty.Boss && boss.Type != EnemyType.Boss)
            {
                _logger.LogWarning("Enemy {Name} is not classified as a boss (Type: {Type}, Difficulty: {Difficulty})",
                    boss.Name, boss.Type, boss.Difficulty);
            }

            // Build detailed boss information
            var bossInfo = new BossInfo
            {
                Name = boss.Name,
                Title = GenerateBossTitle(boss),
                Level = boss.Level,
                RecommendedPlayerLevel = boss.Level - 2, // Bosses are tough, recommend being 2 levels below max
                Difficulty = boss.Difficulty,
                EstimatedXP = boss.XP,
                EstimatedGold = boss.GoldReward,
                HealthTotal = boss.MaxHealth,
                AttackPower = boss.BasePhysicalDamage + boss.BaseMagicDamage,
                DefenseRating = CalculateDefenseRating(boss),
                Abilities = boss.Abilities?.Select(a => a.DisplayName ?? a.Name).ToList() ?? new List<string>(),
                SpecialTraits = ExtractSpecialTraits(boss),
                WarningMessage = GenerateWarningMessage(boss)
            };

            _logger.LogInformation("Boss encounter ready: {Boss} (Level {Level}, {XP} XP, {Gold} gold)",
                boss.Name, boss.Level, boss.XP, boss.GoldReward);

            return new BossEncounterResult
            {
                Success = true,
                Boss = boss,
                Info = bossInfo
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating boss encounter for {BossName}", request.BossName);
            return new BossEncounterResult
            {
                Success = false,
                ErrorMessage = $"Failed to create boss encounter: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Generates a dramatic title for the boss based on type and prefixes.
    /// </summary>
    private static string GenerateBossTitle(Enemy boss)
    {
        if (boss.Prefixes.Any())
        {
            return string.Join(" ", boss.Prefixes.Select(p => p.Value)) + " " + boss.BaseName;
        }

        return boss.Type switch
        {
            EnemyType.Demon => $"{boss.Name}, Demon Lord",
            EnemyType.Dragon => $"{boss.Name}, Ancient Dragon",
            EnemyType.Undead => $"{boss.Name}, Undead Overlord",
            EnemyType.Elemental => $"{boss.Name}, Elemental Sovereign",
            _ => $"{boss.Name}, The Legendary"
        };
    }

    /// <summary>
    /// Calculates an estimated defense rating from boss attributes.
    /// </summary>
    private static int CalculateDefenseRating(Enemy boss)
    {
        // Estimate defense from attributes
        return 10 + boss.Dexterity + boss.Constitution + (boss.Level / 2);
    }

    /// <summary>
    /// Extracts and formats special traits from boss data.
    /// </summary>
    private static List<string> ExtractSpecialTraits(Enemy boss)
    {
        var traits = new List<string>();

        // Extract from traits dictionary
        if (boss.Traits != null)
        {
            foreach (var trait in boss.Traits)
            {
                if (trait.Value?.Value != null && trait.Value.Value.ToString() == "True")
                {
                    traits.Add(FormatTraitName(trait.Key));
                }
                else if (trait.Value?.Value is string strValue && !string.IsNullOrEmpty(strValue))
                {
                    traits.Add($"{FormatTraitName(trait.Key)}: {strValue}");
                }
            }
        }

        // Add difficulty-based traits
        if (boss.Difficulty == EnemyDifficulty.Boss)
        {
            traits.Add("Boss-tier Threat");
        }

        if (boss.MaxHealth > 500)
        {
            traits.Add("Massive Health Pool");
        }

        if (boss.Abilities?.Count >= 5)
        {
            traits.Add($"{boss.Abilities.Count} Unique Abilities");
        }

        return traits;
    }

    /// <summary>
    /// Converts camelCase trait keys to Title Case display names.
    /// </summary>
    private static string FormatTraitName(string traitKey)
    {
        // Convert camelCase to Title Case
        return string.Concat(traitKey.Select((x, i) =>
            i > 0 && char.IsUpper(x) ? " " + x : x.ToString())).Trim();
    }

    /// <summary>
    /// Generates warning messages based on boss threat level.
    /// </summary>
    private static string GenerateWarningMessage(Enemy boss)
    {
        var messages = new List<string>();

        if (boss.Level >= 15)
        {
            messages.Add("Extremely dangerous foe");
        }
        else if (boss.Level >= 10)
        {
            messages.Add("High-level threat");
        }

        if (boss.MaxHealth > 300)
        {
            messages.Add("exceptional durability");
        }

        if (boss.Abilities?.Count >= 6)
        {
            messages.Add("extensive combat abilities");
        }

        if (messages.Count == 0)
        {
            return "Approach with caution - this is a boss encounter.";
        }

        return $"⚠️ WARNING: {string.Join(", ", messages)}. Prepare thoroughly before engaging!";
    }
}
