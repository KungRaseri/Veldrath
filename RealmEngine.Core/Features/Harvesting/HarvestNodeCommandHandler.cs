using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Services;
using RealmEngine.Core.Services.Harvesting;
using RealmEngine.Core.Features.Progression.Services;
using RealmEngine.Data.Services;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using RealmEngine.Shared.Models.Harvesting;

namespace RealmEngine.Core.Features.Harvesting;

/// <summary>
/// Handler for harvesting materials from resource nodes.
/// </summary>
public class HarvestNodeCommandHandler : IRequestHandler<HarvestNodeCommand, HarvestResult>
{
    private readonly ILogger<HarvestNodeCommandHandler> _logger;
    private readonly HarvestCalculatorService _calculator;
    private readonly ToolValidationService _toolValidator;
    private readonly CriticalHarvestService _criticalService;
    private readonly LootTableService _lootTableService;
    private readonly INodeRepository _nodeRepository;
    private readonly IInventoryService _inventoryService;
    private readonly SkillProgressionService _skillProgressionService;
    private readonly ISaveGameService _saveGameService;

    /// <summary>
    /// Initializes a new instance of the HarvestNodeCommandHandler class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="calculator">Service for calculating harvest yields.</param>
    /// <param name="toolValidator">Service for validating tool requirements.</param>
    /// <param name="criticalService">Service for critical harvest calculations.</param>
    /// <param name="lootTableService">Service for loading loot tables.</param>
    /// <param name="nodeRepository">Repository for node persistence.</param>
    /// <param name="inventoryService">Service for inventory management.</param>
    /// <param name="skillProgressionService">Service for skill XP progression.</param>
    /// <param name="saveGameService">Service for loading and persisting save game state.</param>
    public HarvestNodeCommandHandler(
        ILogger<HarvestNodeCommandHandler> logger,
        HarvestCalculatorService calculator,
        ToolValidationService toolValidator,
        CriticalHarvestService criticalService,
        LootTableService lootTableService,
        INodeRepository nodeRepository,
        IInventoryService inventoryService,
        SkillProgressionService skillProgressionService,
        ISaveGameService saveGameService)
    {
        _logger = logger;
        _calculator = calculator;
        _toolValidator = toolValidator;
        _criticalService = criticalService;
        _lootTableService = lootTableService;
        _nodeRepository = nodeRepository;
        _inventoryService = inventoryService;
        _skillProgressionService = skillProgressionService;
        _saveGameService = saveGameService;
    }

    /// <inheritdoc />
    public async Task<HarvestResult> Handle(HarvestNodeCommand request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Character {CharacterName} attempting to harvest node {NodeId}",
                request.CharacterName, request.NodeId
            );

            // Load node from repository
            var node = await _nodeRepository.GetNodeByIdAsync(request.NodeId);
            if (node == null)
            {
                _logger.LogWarning("Node {NodeId} not found", request.NodeId);
                return new HarvestResult
                {
                    Success = false,
                    FailureReason = "Resource node not found.",
                    Message = "This resource no longer exists."
                };
            }

            // Validate node can be harvested
            if (!node.CanHarvest())
            {
                _logger.LogWarning("Node {NodeId} cannot be harvested (state: {State})", node.NodeId, node.GetNodeState());
                return new HarvestResult
                {
                    Success = false,
                    FailureReason = $"This {node.DisplayName} is depleted. It needs time to regenerate.",
                    NodeHealthRemaining = node.CurrentHealth,
                    NodeHealthPercent = node.GetHealthPercent(),
                    NodeState = node.GetNodeState()
                };
            }

            // Get tool tier and validate
            var toolTier = _toolValidator.GetToolTierFromItem(request.EquippedToolRef);
            var toolType = _toolValidator.GetToolTypeFromItem(request.EquippedToolRef);
            var toolValidation = _toolValidator.ValidateTool(node, toolTier, toolType);

            if (!toolValidation.IsValid)
            {
                _logger.LogWarning("Tool validation failed: {Message}", toolValidation.Message);
                return new HarvestResult
                {
                    Success = false,
                    FailureReason = toolValidation.Message,
                    NodeHealthRemaining = node.CurrentHealth,
                    NodeHealthPercent = node.GetHealthPercent(),
                    NodeState = node.GetNodeState()
                };
            }

            // Load character from save game to get actual skill rank
            var saveGame = _saveGameService.GetCurrentSave();
            var character = saveGame?.Character?.Name == request.CharacterName
                ? saveGame.Character
                : null;

            var harvestingSkill = DetermineHarvestingSkill(node.NodeType);
            var characterSkillRank = character?.Skills.GetValueOrDefault(harvestingSkill)?.CurrentRank ?? 0;
            var skillRank = request.SkillRankOverride ?? characterSkillRank;

            // Roll for critical harvest
            var isCritical = _criticalService.RollCritical(skillRank, toolTier, node.IsRichNode);

            // Calculate yield
            var yield = _calculator.CalculateYield(node, skillRank, toolTier, isCritical);

            // Roll loot table for actual materials
            var materials = await _lootTableService.RollHarvestingDrops(node.LootTableRef, yield, isCritical);

            // Calculate node depletion
            var depletion = _calculator.CalculateDepletion(node, skillRank, toolTier, toolValidation.HasNoTool);
            node.CurrentHealth = Math.Max(0, node.CurrentHealth - depletion);
            node.TimesHarvested++;
            node.LastHarvestedAt = DateTime.UtcNow;

            // Calculate XP gain
            var xpGain = _calculator.CalculateSkillXP(node, toolTier, isCritical);

            // Calculate durability loss
            var durabilityLoss = toolValidation.HasNoTool 
                ? 0 
                : _calculator.CalculateDurabilityLoss(node, toolTier, isCritical);

            // Save node state to repository
            await _nodeRepository.SaveNodeAsync(node);

            // Add materials to character inventory
            await _inventoryService.AddItemsAsync(request.CharacterName, materials);

            // Award skill XP to character
            try
            {
                if (character != null)
                {
                    _skillProgressionService.AwardSkillXP(character, harvestingSkill, xpGain, "harvesting");
                    _saveGameService.SaveGame(saveGame!);
                    _logger.LogDebug("Awarded {XP} XP to {Skill} for {Character}", xpGain, harvestingSkill, request.CharacterName);
                }
                else
                {
                    _logger.LogWarning("Could not find character '{CharacterName}' in active save; XP not awarded", request.CharacterName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to award skill XP to {Character}", request.CharacterName);
                // Non-critical: Continue with harvest result
            }

            // Apply tool durability loss
            if (!toolValidation.HasNoTool && !string.IsNullOrEmpty(request.EquippedToolRef))
            {
                _logger.LogDebug("Tool {Tool} lost {Durability} durability for {Character}",
                    request.EquippedToolRef, durabilityLoss, request.CharacterName);
                await _inventoryService.ReduceItemDurabilityAsync(request.CharacterName, request.EquippedToolRef, durabilityLoss);
            }

            var result = new HarvestResult
            {
                Success = true,
                MaterialsGained = materials,
                SkillXPGained = xpGain,
                WasCritical = isCritical,
                NodeHealthRemaining = node.CurrentHealth,
                NodeHealthPercent = node.GetHealthPercent(),
                NodeState = node.GetNodeState(),
                ToolDurabilityLost = durabilityLoss,
                Message = isCritical 
                    ? $"⭐ CRITICAL HARVEST! Gained {materials.Sum(m => m.Quantity)} materials from {node.DisplayName}!"
                    : $"Harvested {materials.Sum(m => m.Quantity)} materials from {node.DisplayName}."
            };

            _logger.LogInformation(
                "Harvest successful: {Materials} materials, {XP} XP, critical={Critical}, node health={Health}/{MaxHealth}",
                materials.Sum(m => m.Quantity), xpGain, isCritical, node.CurrentHealth, node.MaxHealth
            );

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error harvesting node {NodeId} for character {CharacterName}", 
                request.NodeId, request.CharacterName);

            return new HarvestResult
            {
                Success = false,
                FailureReason = "An unexpected error occurred while harvesting.",
                Message = "Harvest failed."
            };
        }
    }

    /// <summary>
    /// Determines which skill should receive XP based on node type.
    /// </summary>
    private static string DetermineHarvestingSkill(string nodeType)
    {
        return nodeType.ToLowerInvariant() switch
        {
            "ore_vein" or "ore" or "mineral" => "mining",
            "tree" or "wood" or "timber" => "woodcutting",
            "herb" or "plant" or "flower" => "herbalism",
            "animal" or "creature" => "skinning",
            "fish" or "fishing_spot" => "fishing",
            _ => "harvesting" // Generic fallback skill
        };
    }
}

