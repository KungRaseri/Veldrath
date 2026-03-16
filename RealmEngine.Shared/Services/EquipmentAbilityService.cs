using RealmEngine.Shared.Models;

namespace RealmEngine.Shared.Services;

/// <summary>
/// Service for managing abilities granted by equipped items.
/// Handles activation/deactivation when equipment changes.
/// </summary>
public static class EquipmentAbilityService
{
    /// <summary>
    /// Recalculates all equipment-granted abilities based on currently equipped items.
    /// Call this after equipping/unequipping any item.
    /// </summary>
    public static void RecalculateEquipmentAbilities(Character character)
    {
        // Clear existing equipment-granted abilities
        character.EquipmentGrantedAbilities.Clear();
        
        // Scan all equipped items for grantedAbilities trait
        foreach (var item in character.GetEquippedItems())
        {
            GrantAbilitiesFromItem(character, item);
        }
    }
    
    /// <summary>
    /// Grants abilities from a specific item to the character.
    /// </summary>
    public static void GrantAbilitiesFromItem(Character character, Item item)
    {
        if (item == null)
            return;
            
        // Check for grantedAbilities trait (should be StringArray)
        if (!item.Traits.TryGetValue("grantedAbilities", out var trait))
            return;
            
        if (trait.Type != TraitType.StringArray)
            return;
            
        var abilityReferences = trait.AsStringList();
        if (abilityReferences == null || abilityReferences.Count == 0)
            return;
            
        // Add each ability reference to equipment-granted abilities
        foreach (var abilityRef in abilityReferences)
        {
            if (string.IsNullOrWhiteSpace(abilityRef))
                continue;
                
            // Extract ability ID from reference (@abilities/category:ability-name -> category:ability-name)
            var abilityId = ExtractAbilityIdFromReference(abilityRef);
            
            // Don't override already-learned abilities
            if (!character.LearnedAbilities.ContainsKey(abilityId))
            {
                character.EquipmentGrantedAbilities[abilityId] = item.Id;
            }
        }
    }
    
    /// <summary>
    /// Removes abilities granted by a specific item.
    /// Call this before unequipping an item.
    /// </summary>
    public static void RevokeAbilitiesFromItem(Character character, Item item)
    {
        if (item == null)
            return;
            
        // Find and remove all abilities granted by this item
        var abilitiesToRemove = character.EquipmentGrantedAbilities
            .Where(kvp => kvp.Value == item.Id)
            .Select(kvp => kvp.Key)
            .ToList();
            
        foreach (var abilityId in abilitiesToRemove)
        {
            character.EquipmentGrantedAbilities.Remove(abilityId);
        }
    }
    
    /// <summary>
    /// Extracts ability ID from a JSON reference.
    /// Example: @abilities/active/offensive:power-attack -> active/offensive:power-attack
    /// </summary>
    private static string ExtractAbilityIdFromReference(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return string.Empty;
            
        // Remove @abilities/ prefix if present
        if (reference.StartsWith("@abilities/"))
        {
            return reference.Substring(11); // Skip "@abilities/"
        }
        
        // If no @ prefix, assume it's already a clean ability ID
        return reference;
    }
    
    /// <summary>
    /// Gets all abilities available to a character (learned + equipment-granted).
    /// </summary>
    public static List<string> GetAllAvailableAbilities(Character character)
    {
        var allAbilities = new HashSet<string>(character.LearnedAbilities.Keys);
        
        foreach (var abilityId in character.EquipmentGrantedAbilities.Keys)
        {
            allAbilities.Add(abilityId);
        }
        
        return allAbilities.ToList();
    }
    
    /// <summary>
    /// Gets the source of an ability (learned, or equipment item name).
    /// </summary>
    public static string GetAbilitySource(Character character, string abilityId)
    {
        if (character.LearnedAbilities.ContainsKey(abilityId))
            return "Learned";
            
        if (character.EquipmentGrantedAbilities.TryGetValue(abilityId, out var itemId))
        {
            var item = character.GetEquippedItems().FirstOrDefault(i => i.Id == itemId);
            return item != null ? item.Name : "Equipment";
        }
        
        return "Unknown";
    }
}
