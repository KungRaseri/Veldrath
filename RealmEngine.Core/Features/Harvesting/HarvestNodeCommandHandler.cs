using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Services.Harvesting;
using RealmEngine.Data.Services;
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
    private readonly NodeLootTableService _lootTableService;
    // TODO: Add INodeRepository when persistence is implemented
    // TODO: Add ICharacterRepository for skill lookups

    public HarvestNodeCommandHandler(
        ILogger<HarvestNodeCommandHandler> logger,
        HarvestCalculatorService calculator,
        ToolValidationService toolValidator,
        CriticalHarvestService criticalService,
        NodeLootTableService lootTableService)
    {
        _logger = logger;
        _calculator = calculator;
        _toolValidator = toolValidator;
        _criticalService = criticalService;
        _lootTableService = lootTableService;
    }

    public async Task<HarvestResult> Handle(HarvestNodeCommand request, CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // TODO: Replace with actual async repository calls
        
        try
        {
            _logger.LogInformation(
                "Character {CharacterName} attempting to harvest node {NodeId}",
                request.CharacterName, request.NodeId
            );

            // TODO: Load node from repository
            // For now, create a mock node for demonstration
            var node = CreateMockNode(request.NodeId);

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

            // TODO: Get character's skill rank from repository
            var skillRank = request.SkillRankOverride ?? 0; // Default to 0 for now

            // Roll for critical harvest
            var isCritical = _criticalService.RollCritical(skillRank, toolTier, node.IsRichNode);

            // Calculate yield
            var yield = _calculator.CalculateYield(node, skillRank, toolTier, isCritical);

            // Roll loot table for actual materials
            var materials = _lootTableService.RollLootDrops(node.LootTableRef, yield, isCritical);

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

            // TODO: Save node state to repository
            // TODO: Award skill XP to character
            // TODO: Apply tool durability loss
            // TODO: Add materials to inventory

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

    // Mock methods - replace with actual repository/service calls
    private HarvestableNode CreateMockNode(string nodeId)
    {
        return new HarvestableNode
        {
            NodeId = nodeId,
            NodeType = "copper_vein",
            DisplayName = "Copper Vein",
            MaterialTier = "common",
            CurrentHealth = 80,
            MaxHealth = 100,
            MinToolTier = 0,
            BaseYield = 2,
            LocationId = "test_location",
            BiomeType = "mountains",
            LootTableRef = "@loot-tables/nodes/ores:copper",
            IsRichNode = false
        };
    }
}
