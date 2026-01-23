using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Models.Harvesting;

namespace RealmEngine.Core.Services.Harvesting;

/// <summary>
/// Service for validating tool requirements and capabilities for harvesting.
/// </summary>
public class ToolValidationService
{
    private readonly ILogger<ToolValidationService> _logger;
    private readonly HarvestingConfig _config;

    public ToolValidationService(
        ILogger<ToolValidationService> logger,
        HarvestingConfig config)
    {
        _logger = logger;
        _config = config;
    }

    /// <summary>
    /// Validate if the player has an appropriate tool for harvesting the node.
    /// </summary>
    public ToolValidationResult ValidateTool(
        HarvestableNode node,
        int? toolTier,
        string? toolType)
    {
        // No tool provided
        if (!toolTier.HasValue || toolTier.Value == 0)
        {
            // Common nodes allow no tool if configured
            if (node.MaterialTier == "common" && _config.ToolRequirements.AllowNoToolForCommon)
            {
                _logger.LogDebug("No tool provided, but allowed for common node: {NodeName}", node.DisplayName);
                return new ToolValidationResult
                {
                    IsValid = true,
                    HasNoTool = true,
                    ToolTier = 0,
                    Message = "Harvesting with bare hands (reduced yield, increased depletion)"
                };
            }

            // Higher tier nodes require tools
            _logger.LogWarning("Tool required for {Tier} node: {NodeName}", node.MaterialTier, node.DisplayName);
            return new ToolValidationResult
            {
                IsValid = false,
                HasNoTool = true,
                ToolTier = 0,
                Message = $"You need a harvesting tool to gather from this {node.MaterialTier} node. Minimum tier {node.MinToolTier + 1} required."
            };
        }

        // Check minimum tool tier requirement
        if (_config.ToolRequirements.EnforceMinimum && toolTier.Value < node.MinToolTier)
        {
            _logger.LogWarning(
                "Tool tier {ToolTier} below minimum {MinTier} for node: {NodeName}",
                toolTier.Value, node.MinToolTier, node.DisplayName
            );

            return new ToolValidationResult
            {
                IsValid = false,
                HasNoTool = false,
                ToolTier = toolTier.Value,
                Message = $"Your tool (tier {toolTier.Value}) is too weak. Minimum tier {node.MinToolTier} required for {node.MaterialTier} materials."
            };
        }

        // Valid tool
        _logger.LogDebug(
            "Tool validation passed: tier {ToolTier} for {Tier} node {NodeName}",
            toolTier.Value, node.MaterialTier, node.DisplayName
        );

        var bonusInfo = toolTier.Value > node.MinToolTier
            ? $" (+{(toolTier.Value - node.MinToolTier) * 10}% yield bonus)"
            : "";

        return new ToolValidationResult
        {
            IsValid = true,
            HasNoTool = false,
            ToolTier = toolTier.Value,
            ToolType = toolType,
            Message = $"Using tier {toolTier.Value} {toolType ?? "tool"}{bonusInfo}"
        };
    }

    /// <summary>
    /// Get the tool tier from an item reference or equipped tool.
    /// </summary>
    public int GetToolTierFromItem(string? itemRef)
    {
        if (string.IsNullOrEmpty(itemRef))
            return 0;

        // Extract tier from common naming patterns
        // Examples: "bronze_pickaxe" = tier 1, "steel_pickaxe" = tier 3
        if (itemRef.Contains("bronze", StringComparison.OrdinalIgnoreCase) ||
            itemRef.Contains("basic", StringComparison.OrdinalIgnoreCase) ||
            itemRef.Contains("crude", StringComparison.OrdinalIgnoreCase))
            return 1;

        if (itemRef.Contains("iron", StringComparison.OrdinalIgnoreCase) ||
            itemRef.Contains("quality", StringComparison.OrdinalIgnoreCase))
            return 2;

        if (itemRef.Contains("steel", StringComparison.OrdinalIgnoreCase) ||
            itemRef.Contains("master", StringComparison.OrdinalIgnoreCase))
            return 3;

        if (itemRef.Contains("mithril", StringComparison.OrdinalIgnoreCase) ||
            itemRef.Contains("enchanted", StringComparison.OrdinalIgnoreCase))
            return 4;

        if (itemRef.Contains("adamantine", StringComparison.OrdinalIgnoreCase) ||
            itemRef.Contains("legendary", StringComparison.OrdinalIgnoreCase) ||
            itemRef.Contains("ancient", StringComparison.OrdinalIgnoreCase))
            return 5;

        // Default to tier 1 if can't determine
        _logger.LogWarning("Could not determine tool tier from item: {ItemRef}", itemRef);
        return 1;
    }

    /// <summary>
    /// Get the tool type (pickaxe, axe, sickle, etc.) from an item reference.
    /// </summary>
    public string? GetToolTypeFromItem(string? itemRef)
    {
        if (string.IsNullOrEmpty(itemRef))
            return null;

        var lowerRef = itemRef.ToLowerInvariant();

        if (lowerRef.Contains("pickaxe") || lowerRef.Contains("pick"))
            return "pickaxe";

        if (lowerRef.Contains("axe") || lowerRef.Contains("hatchet"))
            return "axe";

        if (lowerRef.Contains("sickle") || lowerRef.Contains("scythe"))
            return "sickle";

        if (lowerRef.Contains("fishing") || lowerRef.Contains("rod"))
            return "fishing rod";

        if (lowerRef.Contains("knife") || lowerRef.Contains("skinner"))
            return "skinning knife";

        return "tool";
    }
}

/// <summary>
/// Result of tool validation check.
/// </summary>
public class ToolValidationResult
{
    /// <summary>
    /// Whether the tool is valid for the node.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Whether the player has no tool equipped.
    /// </summary>
    public bool HasNoTool { get; set; }

    /// <summary>
    /// Tool tier (0 = no tool, 1-5 = tier).
    /// </summary>
    public int ToolTier { get; set; }

    /// <summary>
    /// Tool type name (pickaxe, axe, etc.).
    /// </summary>
    public string? ToolType { get; set; }

    /// <summary>
    /// Message to display to the player.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
