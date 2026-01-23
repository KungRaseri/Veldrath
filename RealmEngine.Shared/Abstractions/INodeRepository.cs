using RealmEngine.Shared.Models.Harvesting;

namespace RealmEngine.Shared.Abstractions;

/// <summary>
/// Repository for managing harvestable resource nodes.
/// </summary>
public interface INodeRepository
{
    /// <summary>
    /// Gets a harvestable node by its unique identifier.
    /// </summary>
    /// <param name="nodeId">The unique identifier of the node.</param>
    /// <returns>The harvestable node, or null if not found.</returns>
    Task<HarvestableNode?> GetNodeByIdAsync(string nodeId);

    /// <summary>
    /// Gets all nodes in a specific location.
    /// </summary>
    /// <param name="locationId">The location identifier.</param>
    /// <returns>A list of nodes in the location.</returns>
    Task<List<HarvestableNode>> GetNodesByLocationAsync(string locationId);

    /// <summary>
    /// Gets nearby nodes within a specified radius of a location.
    /// </summary>
    /// <param name="locationId">The location identifier.</param>
    /// <param name="radius">The search radius.</param>
    /// <returns>A list of nearby nodes.</returns>
    Task<List<HarvestableNode>> GetNearbyNodesAsync(string locationId, int radius);

    /// <summary>
    /// Saves or updates a harvestable node.
    /// </summary>
    /// <param name="node">The node to save.</param>
    /// <returns>True if the save was successful.</returns>
    Task<bool> SaveNodeAsync(HarvestableNode node);

    /// <summary>
    /// Updates the health of a specific node.
    /// </summary>
    /// <param name="nodeId">The node identifier.</param>
    /// <param name="newHealth">The new health value.</param>
    /// <returns>True if the update was successful.</returns>
    Task<bool> UpdateNodeHealthAsync(string nodeId, int newHealth);

    /// <summary>
    /// Spawns a new node in a location.
    /// </summary>
    /// <param name="node">The node to spawn.</param>
    /// <returns>The spawned node with generated ID.</returns>
    Task<HarvestableNode> SpawnNodeAsync(HarvestableNode node);

    /// <summary>
    /// Removes a depleted or expired node.
    /// </summary>
    /// <param name="nodeId">The node identifier.</param>
    /// <returns>True if the removal was successful.</returns>
    Task<bool> RemoveNodeAsync(string nodeId);

    /// <summary>
    /// Gets all nodes that need regeneration (health less than max and enough time has passed).
    /// </summary>
    /// <returns>A list of nodes ready for regeneration.</returns>
    Task<List<HarvestableNode>> GetNodesReadyForRegenerationAsync();
}
