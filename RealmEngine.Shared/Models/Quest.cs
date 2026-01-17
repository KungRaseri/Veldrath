namespace RealmEngine.Shared.Models;

/// <summary>
/// Represents a quest that can be given to the player.
/// Implements ITraitable to support quest-specific traits from templates.
/// </summary>
public class Quest : ITraitable
{
    /// <summary>
    /// Gets or sets the unique identifier for this quest.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Gets or sets the URL-safe identifier for this quest (kebab-case).
    /// Used for lookups, references, and catalog identification. Maps to "slug" in JSON.
    /// </summary>
    public string Slug { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the quest title displayed to the player.
    /// </summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the display name shown in UI.
    /// May differ from internal Title. Maps to "displayName" in JSON.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the rarity weight for quest selection in procedural generation.
    /// Lower values = more rare/harder to get.
    /// </summary>
    public int RarityWeight { get; set; } = 50;
    
    /// <summary>
    /// Gets or sets the descriptive text explaining the quest objectives.
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the quest type (e.g., "kill", "fetch", "escort", "investigate", "delivery").
    /// </summary>
    public string QuestType { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the difficulty rating (e.g., "easy", "medium", "hard").
    /// </summary>
    public string Difficulty { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the quest category ("main", "side", "legendary").
    /// </summary>
    public string Type { get; set; } = "side";
    
    /// <summary>
    /// Gets or sets the types of NPCs that can give this quest.
    /// Examples: ["common", "service", "military"], ["merchants", "craftsmen"]
    /// </summary>
    public List<string> QuestGiverTypes { get; set; } = new();
    
    /// <summary>
    /// Gets or sets whether combat is optional for this quest.
    /// True = quest can be completed without fighting.
    /// </summary>
    public bool CombatOptional { get; set; } = false;
    
    /// <summary>
    /// Gets or sets the minimum number of clues required (for investigation quests).
    /// </summary>
    public int MinClues { get; set; } = 0;
    
    /// <summary>
    /// Gets or sets the maximum number of clues available (for investigation quests).
    /// </summary>
    public int MaxClues { get; set; } = 0;
    
    /// <summary>
    /// Gets or sets the base gold reward for this quest template.
    /// </summary>
    public int BaseGoldReward { get; set; } = 0;
    
    /// <summary>
    /// Gets or sets the base XP reward for this quest template.
    /// </summary>
    public int BaseXpReward { get; set; } = 0;
    
    /// <summary>
    /// Gets or sets the unique identifier of the NPC who gives this quest.
    /// </summary>
    public string QuestGiverId { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the display name of the quest giver NPC.
    /// </summary>
    public string QuestGiverName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of target for the objective (e.g., "beast", "undead", "demon").
    /// </summary>
    public string TargetType { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the specific name of the target (e.g., "Ancient Red Dragon").
    /// </summary>
    public string TargetName { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the required quantity to complete the objective.
    /// </summary>
    public int Quantity { get; set; } = 1;
    
    /// <summary>
    /// Gets or sets the location where the quest objective must be completed.
    /// </summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the collection of prerequisite quest IDs that must be completed before this quest becomes available.
    /// </summary>
    public List<string> Prerequisites { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the dictionary mapping objective names to their required completion counts.
    /// </summary>
    public Dictionary<string, int> Objectives { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the dictionary tracking current progress towards each objective.
    /// </summary>
    public Dictionary<string, int> ObjectiveProgress { get; set; } = new();

    /// <summary>
    /// Gets or sets the gold reward for completing this quest.
    /// </summary>
    public int GoldReward { get; set; }
    
    /// <summary>
    /// Gets or sets the experience points reward for completing this quest.
    /// </summary>
    public int XpReward { get; set; }
    
    /// <summary>
    /// Gets or sets the bonus time (in minutes) awarded in Apocalypse mode for completing this quest.
    /// </summary>
    public int ApocalypseBonusMinutes { get; set; } = 0;
    
    /// <summary>
    /// Collection of item reward reference IDs (v4.1 format) given to player upon quest completion.
    /// Each ID is a JSON reference like "@items/weapons/swords:magic-longsword".
    /// </summary>
    /// <remarks>
    /// <para><strong>✅ HOW TO RESOLVE - Use ReferenceResolverService:</strong></para>
    /// <code>
    /// // C# - Resolve quest reward items
    /// var resolver = new ReferenceResolverService(dataCache);
    /// var rewardItems = new List&lt;Item&gt;();
    /// foreach (var refId in quest.ItemRewardIds)
    /// {
    ///     var itemJson = await resolver.ResolveToObjectAsync(refId);
    ///     var item = itemJson.ToObject&lt;Item&gt;();
    ///     rewardItems.Add(item);
    /// }
    /// character.Inventory.AddRange(rewardItems);
    /// </code>
    /// <code>
    /// // GDScript - Resolve rewards in Godot
    /// var resolver = ReferenceResolverService.new(data_cache)
    /// for ref_id in quest.ItemRewardIds:
    ///     var item_data = await resolver.ResolveToObjectAsync(ref_id)
    ///     player.inventory.add_item(item_data)
    /// player.add_gold(quest.GoldReward)
    /// </code>
    /// </remarks>
    /// <example>
    /// Example reward reference IDs:
    /// <code>
    /// [
    ///   "@items/weapons/swords:magic-longsword",
    ///   "@items/consumables/potions:health-potion"
    /// ]
    /// </code>
    /// </example>
    public List<string> ItemRewardIds { get; set; } = new();
    
    /// <summary>
    /// Collection of ability reward reference IDs (v4.1 format) granted to player upon quest completion.
    /// Each ID is a JSON reference like "@abilities/active/offensive:power-strike".
    /// </summary>
    /// <remarks>
    /// <para><strong>✅ HOW TO RESOLVE - Use ReferenceResolverService:</strong></para>
    /// <code>
    /// // C# - Resolve ability rewards
    /// var resolver = new ReferenceResolverService(dataCache);
    /// var rewardAbilities = new List&lt;Ability&gt;();
    /// foreach (var refId in quest.AbilityRewardIds)
    /// {
    ///     var abilityJson = await resolver.ResolveToObjectAsync(refId);
    ///     var ability = abilityJson.ToObject&lt;Ability&gt;();
    ///     rewardAbilities.Add(ability);
    /// }
    /// character.LearnedSkills.AddRange(rewardAbilities);
    /// </code>
    /// <code>
    /// // GDScript - Resolve ability rewards in Godot
    /// var resolver = ReferenceResolverService.new(data_cache)
    /// for ref_id in quest.AbilityRewardIds:
    ///     var ability_data = await resolver.ResolveToObjectAsync(ref_id)
    ///     player.learn_ability(ability_data)
    ///     show_notification("New ability: " + ability_data.DisplayName)
    /// </code>
    /// </remarks>
    /// <example>
    /// Example ability reward reference IDs:
    /// <code>
    /// [
    ///   "@abilities/active/offensive:power-strike",
    ///   "@abilities/passive/utility:heroic-leap"
    /// ]
    /// </code>
    /// </example>
    public List<string> AbilityRewardIds { get; set; } = new();
    
    /// <summary>
    /// Collection of location reference IDs (v4.1 format) where quest objectives must be completed.
    /// Each ID is a JSON reference like "@world/locations/dungeons:dark-cavern".
    /// </summary>
    /// <remarks>
    /// <para><strong>✅ HOW TO RESOLVE - Use ReferenceResolverService:</strong></para>
    /// <code>
    /// // C# - Resolve quest objective locations
    /// var resolver = new ReferenceResolverService(dataCache);
    /// var locations = new List&lt;Location&gt;();
    /// foreach (var refId in quest.ObjectiveLocationIds)
    /// {
    ///     var locationJson = await resolver.ResolveToObjectAsync(refId);
    ///     var location = locationJson.ToObject&lt;Location&gt;();
    ///     locations.Add(location);
    ///     AddQuestMarkerToMap(location.Name, location.Coordinates);
    /// }
    /// </code>
    /// <code>
    /// // GDScript - Resolve locations in Godot
    /// var resolver = ReferenceResolverService.new(data_cache)
    /// for ref_id in quest.ObjectiveLocationIds:
    ///     var location_data = await resolver.ResolveToObjectAsync(ref_id)
    ///     add_quest_marker(location_data.name, location_data.coordinates)
    /// </code>
    /// </remarks>
    /// <example>
    /// Example location reference IDs:
    /// <code>
    /// [
    ///   "@world/locations/dungeons:dark-cavern",
    ///   "@world/locations/towns:riverside"
    /// ]
    /// </code>
    /// </example>
    public List<string> ObjectiveLocationIds { get; set; } = new();
    
    /// <summary>
    /// Collection of NPC reference IDs (v4.1 format) involved in quest objectives.
    /// Each ID is a JSON reference like "@npcs/merchants:blacksmith".
    /// </summary>
    /// <remarks>
    /// <para><strong>✅ HOW TO RESOLVE - Use ReferenceResolverService:</strong></para>
    /// <code>
    /// // C# - Resolve quest NPCs
    /// var resolver = new ReferenceResolverService(dataCache);
    /// var questNpcs = new List&lt;NPC&gt;();
    /// foreach (var refId in quest.ObjectiveNpcIds)
    /// {
    ///     var npcJson = await resolver.ResolveToObjectAsync(refId);
    ///     var npc = npcJson.ToObject&lt;NPC&gt;();
    ///     npc.HasQuestMarker = true;
    ///     questNpcs.Add(npc);
    /// }
    /// </code>
    /// <code>
    /// // GDScript - Resolve NPCs in Godot
    /// var resolver = ReferenceResolverService.new(data_cache)
    /// for ref_id in quest.ObjectiveNpcIds:
    ///     var npc_data = await resolver.ResolveToObjectAsync(ref_id)
    ///     npc_data.show_quest_marker = true
    ///     world.update_npc(npc_data)
    /// </code>
    /// </remarks>
    /// <example>
    /// Example NPC reference IDs:
    /// <code>
    /// [
    ///   "@npcs/merchants:blacksmith",
    ///   "@npcs/common:elder-sage"
    /// ]
    /// </code>
    /// </example>
    public List<string> ObjectiveNpcIds { get; set; } = new();
    
    /// <summary>
    /// Collection of enemy reference IDs (v4.1 format) that must be defeated for quest objectives.
    /// Each ID is a JSON reference like "@enemies/goblinoids:goblin-warrior".
    /// </summary>
    /// <remarks>
    /// <para><strong>✅ HOW TO RESOLVE - Use ReferenceResolverService:</strong></para>
    /// <code>
    /// // C# - Check if defeated enemy counts for quest
    /// var resolver = new ReferenceResolverService(dataCache);
    /// public async Task&lt;bool&gt; OnEnemyDefeated(string enemyRefId)
    /// {
    ///     if (quest.ObjectiveEnemyIds.Contains(enemyRefId))
    ///     {
    ///         quest.Progress++;
    ///         UpdateQuestLog(quest);
    ///         return true;
    ///     }
    ///     return false;
    /// }
    /// </code>
    /// <code>
    /// // GDScript - Track enemy kills in Godot
    /// func on_enemy_defeated(enemy_ref_id: String):
    ///     if enemy_ref_id in quest.ObjectiveEnemyIds:
    ///         quest.progress += 1
    ///         update_quest_ui()
    /// </code>
    /// <para><strong>Note:</strong> Enemy IDs are typically compared directly without resolving full objects.</para>
    /// </remarks>
    /// <example>
    /// Example enemy reference IDs:
    /// <code>
    /// [
    ///   "@enemies/goblinoids:goblin-warrior",
    ///   "@enemies/undead:skeleton"
    /// ]
    /// </code>
    /// </example>
    public List<string> ObjectiveEnemyIds { get; set; } = new();

    /// <summary>
    /// Fully resolved Item objects for quest rewards.
    /// Populated by QuestGenerator.GenerateAsync() when hydrating templates.
    /// Not serialized to JSON (template IDs stored in ItemRewardIds instead).
    /// </summary>
    /// <remarks>
    /// <para><strong>For Runtime Use:</strong></para>
    /// <list type="bullet">
    /// <item><description>Use this property when quest completes to award items</description></item>
    /// <item><description>Already resolved - no need to call ReferenceResolverService</description></item>
    /// <item><description>Null if quest loaded from template without hydration</description></item>
    /// </list>
    /// </remarks>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<Item> ItemRewards { get; set; } = new();

    /// <summary>
    /// Fully resolved Ability objects for quest rewards.
    /// Populated by QuestGenerator.GenerateAsync() when hydrating templates.
    /// Not serialized to JSON (template IDs stored in AbilityRewardIds instead).
    /// </summary>
    /// <remarks>
    /// <para><strong>For Runtime Use:</strong></para>
    /// <list type="bullet">
    /// <item><description>Use this property when quest completes to teach new abilities</description></item>
    /// <item><description>Already resolved - no need to call ReferenceResolverService</description></item>
    /// <item><description>Null if quest loaded from template without hydration</description></item>
    /// </list>
    /// </remarks>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<Ability> AbilityRewards { get; set; } = new();

    /// <summary>
    /// Fully resolved Location objects for quest objectives.
    /// Populated by QuestGenerator.GenerateAsync() when hydrating templates.
    /// Not serialized to JSON (template IDs stored in ObjectiveLocationIds instead).
    /// </summary>
    /// <remarks>
    /// <para><strong>For Runtime Use:</strong></para>
    /// <list type="bullet">
    /// <item><description>Use this property to display quest markers on map</description></item>
    /// <item><description>Already resolved - no need to call ReferenceResolverService</description></item>
    /// <item><description>Null if quest loaded from template without hydration</description></item>
    /// </list>
    /// </remarks>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<Location> ObjectiveLocations { get; set; } = new();

    /// <summary>
    /// Fully resolved NPC objects for quest objectives.
    /// Populated by QuestGenerator.GenerateAsync() when hydrating templates.
    /// Not serialized to JSON (template IDs stored in ObjectiveNpcIds instead).
    /// </summary>
    /// <remarks>
    /// <para><strong>For Runtime Use:</strong></para>
    /// <list type="bullet">
    /// <item><description>Use this property to mark quest NPCs with indicators</description></item>
    /// <item><description>Already resolved - no need to call ReferenceResolverService</description></item>
    /// <item><description>Null if quest loaded from template without hydration</description></item>
    /// </list>
    /// </remarks>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<NPC> ObjectiveNpcs { get; set; } = new();

    /// <summary>
    /// Gets or sets whether this quest is currently active in the player's quest log.
    /// </summary>
    public bool IsActive { get; set; } = false;
    
    /// <summary>
    /// Gets or sets whether this quest has been completed.
    /// </summary>
    public bool IsCompleted { get; set; } = false;
    
    /// <summary>
    /// Gets or sets the current progress count towards the quest objective.
    /// </summary>
    public int Progress { get; set; } = 0;

    /// <summary>
    /// Gets or sets the time limit for this quest in hours (0 = no time limit).
    /// </summary>
    public int TimeLimit { get; set; } = 0;
    
    /// <summary>
    /// Gets or sets the timestamp when this quest was started.
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// Gets or sets the trait system dictionary for dynamic quest properties.
    /// Implements ITraitable interface.
    /// </summary>
    public Dictionary<string, TraitValue> Traits { get; set; } = new();

    /// <summary>
    /// Check if the quest has expired (time limit reached).
    /// </summary>
    public bool IsExpired()
    {
        if (TimeLimit <= 0 || !StartTime.HasValue)
            return false;

        var elapsed = DateTime.Now - StartTime.Value;
        return elapsed.TotalHours >= TimeLimit;
    }

    /// <summary>
    /// Get the formatted time remaining.
    /// </summary>
    public string GetTimeRemaining()
    {
        if (TimeLimit <= 0 || !StartTime.HasValue)
            return "No time limit";

        var elapsed = DateTime.Now - StartTime.Value;
        var remaining = TimeSpan.FromHours(TimeLimit) - elapsed;

        if (remaining.TotalHours < 0)
            return "Expired";

        if (remaining.TotalHours < 1)
            return $"{remaining.Minutes} minutes";

        if (remaining.TotalDays >= 1)
            return $"{(int)remaining.TotalDays} days, {remaining.Hours} hours";

        return $"{(int)remaining.TotalHours} hours";
    }

    /// <summary>
    /// Check if all quest objectives are complete (Phase 4 enhancement).
    /// </summary>
    public bool IsObjectivesComplete()
    {
        // If no objectives defined, fall back to legacy Progress check
        if (!Objectives.Any())
        {
            return Progress >= Quantity;
        }

        // Check all objectives are met
        foreach (var objective in Objectives)
        {
            if (!ObjectiveProgress.ContainsKey(objective.Key) ||
                ObjectiveProgress[objective.Key] < objective.Value)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Update progress for a specific objective (Phase 4 enhancement).
    /// </summary>
    public void UpdateObjectiveProgress(string objectiveName, int progressIncrement = 1)
    {
        if (!Objectives.ContainsKey(objectiveName))
        {
            return;
        }

        if (!ObjectiveProgress.ContainsKey(objectiveName))
        {
            ObjectiveProgress[objectiveName] = 0;
        }

        ObjectiveProgress[objectiveName] += progressIncrement;

        // Also update legacy Progress field for backwards compatibility
        Progress = ObjectiveProgress.Values.Sum();
    }
}
