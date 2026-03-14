using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Services;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Shared.Abstractions;

namespace RealmEngine.Core.Features.Harvesting;

/// <summary>
/// Handler for inspecting resource nodes.
/// </summary>
public class InspectNodeQueryHandler : IRequestHandler<InspectNodeQuery, NodeInspectionResult>
{
    private readonly ILogger<InspectNodeQueryHandler> _logger;
    private readonly INodeRepository _nodeRepository;
    private readonly ISaveGameService _saveGameService;
    private readonly LootTableService _lootTableService;

    /// <summary>
    /// Initializes a new instance of the InspectNodeQueryHandler class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="nodeRepository">Repository for accessing harvesting nodes.</param>
    /// <param name="saveGameService">Service for accessing character skill data.</param>
    /// <param name="lootTableService">Service for determining possible materials from loot tables.</param>
    public InspectNodeQueryHandler(
        ILogger<InspectNodeQueryHandler> logger,
        INodeRepository nodeRepository,
        ISaveGameService saveGameService,
        LootTableService lootTableService)
    {
        _logger = logger;
        _nodeRepository = nodeRepository;
        _saveGameService = saveGameService;
        _lootTableService = lootTableService;
    }

    /// <inheritdoc />
    public async Task<NodeInspectionResult> Handle(InspectNodeQuery request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Character {CharacterName} inspecting node {NodeId}",
                request.CharacterName, request.NodeId
            );

            // Load node from repository
            var node = await _nodeRepository.GetNodeByIdAsync(request.NodeId);
            if (node == null)
            {
                _logger.LogWarning("Node {NodeId} not found", request.NodeId);
                return new NodeInspectionResult
                {
                    Success = false,
                    ErrorMessage = $"Resource node '{request.NodeId}' not found."
                };
            }

            // Get character skills for yield estimation
            var currentSave = _saveGameService.GetCurrentSave();
            int skillRank = 0;
            if (currentSave != null)
            {
                var harvestingSkill = DetermineHarvestingSkill(node.NodeType);
                if (currentSave.Character.Skills.TryGetValue(harvestingSkill, out var skill))
                {
                    skillRank = skill.CurrentRank;
                }
            }

            // Calculate estimated yield based on skill
            var baseYield = node.BaseYield;
            var estimatedYield = skillRank > 0 
                ? baseYield + (int)(baseYield * skillRank * 0.01) // 1% bonus per skill rank
                : baseYield;

            // Get possible materials from loot table
            var possibleMaterials = new List<string>();
            if (!string.IsNullOrEmpty(node.LootTableRef))
            {
                try
                {
                    // Get a sample roll to show possible materials (NOT async)
                    var sampleDrops = await _lootTableService.RollHarvestingDrops(node.LootTableRef, baseYield, false);
                    possibleMaterials = sampleDrops
                        .Select(d => $"{d.ItemName} ({d.Quantity} typical)")
                        .Distinct()
                        .ToList();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load loot table {LootTableRef}", node.LootTableRef);
                    possibleMaterials.Add("Unknown materials");
                }
            }

            return new NodeInspectionResult
            {
                Success = true,
                NodeId = node.NodeId,
                DisplayName = node.DisplayName,
                NodeType = node.NodeType,
                MaterialTier = node.MaterialTier,
                CurrentHealth = node.CurrentHealth,
                MaxHealth = node.MaxHealth,
                HealthPercent = node.GetHealthPercent(),
                State = node.GetNodeState(),
                CanHarvest = node.CanHarvest(),
                MinToolTier = node.MinToolTier,
                RequiredToolType = node.MinToolTier > 0
                    ? $"Tier {node.MinToolTier} tool required"
                    : "Any tool (or hands for common materials)",
                RequiredSkill = DetermineHarvestingSkill(node.NodeType),
                BaseYield = baseYield,
                EstimatedYield = estimatedYield,
                PossibleMaterials = possibleMaterials,
                IsRichNode = node.IsRichNode,
                TimesHarvested = node.TimesHarvested,
                LastHarvestedAt = node.LastHarvestedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inspecting node {NodeId}", request.NodeId);

            return new NodeInspectionResult
            {
                Success = false,
                ErrorMessage = "Failed to inspect node."
            };
        }
    }

    /// <summary>
    /// Determines which skill applies to a specific node type.
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
            _ => "harvesting"
        };
    }}