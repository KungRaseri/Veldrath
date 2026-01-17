using FluentAssertions;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace RealmEngine.Data.Tests;

[Trait("Category", "Compliance")]
[Trait("Category", "DataValidation")]
[Trait("FileType", "Catalog")]
/// <summary>
/// Tests for JSON v5.1 catalog standards compliance
/// Validates attributes/stats/properties/traits structure, formula syntax, and structured damage objects
/// </summary>
public class CatalogJsonV5ComplianceTests
{
  private readonly string _dataPath;
  private readonly List<string> _catalogPaths;

  public CatalogJsonV5ComplianceTests()
  {
    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
    var solutionRoot = Directory.GetParent(baseDir)?.Parent?.Parent?.Parent?.Parent?.FullName;
    if (solutionRoot == null)
      throw new DirectoryNotFoundException($"Could not find solution root from: {baseDir}");

    _dataPath = Path.Combine(solutionRoot, "RealmEngine.Data", "Data", "Json");

    if (!Directory.Exists(_dataPath))
      throw new DirectoryNotFoundException($"Data directory not found: {_dataPath}");

    _catalogPaths = new List<string>
        {
            // Enemies domain (13 catalogs + examples for testing)
            "enemies/wolves/catalog.json",  // v5.1 example
            "enemies/humanoids/catalog_v5.1_test.json",  // Migrated test
            "enemies/beasts/catalog.json",
            "enemies/demons/catalog.json",
            "enemies/dragons/catalog.json",
            "enemies/elementals/catalog.json",
            "enemies/goblinoids/catalog.json",
            "enemies/humanoids/catalog.json",
            "enemies/insects/catalog.json",
            "enemies/orcs/catalog.json",
            "enemies/plants/catalog.json",
            "enemies/reptilians/catalog.json",
            "enemies/trolls/catalog.json",
            "enemies/undead/catalog.json",
            "enemies/vampires/catalog.json",
            
            // Items domain (14 catalogs)
            "items/armor/catalog.json",
            "items/consumables/catalog.json",
            "items/crystals/life/catalog.json",
            "items/crystals/mana/catalog.json",
            "items/essences/fire/catalog.json",
            "items/essences/shadow/catalog.json",
            "items/gems/blue/catalog.json",
            "items/gems/red/catalog.json",
            "items/materials/essence/catalog.json",
            "items/materials/gem/catalog.json",
            "items/materials/ingot/catalog.json",
            "items/materials/leather/catalog.json",
            "items/materials/ore/catalog.json",
            "items/materials/organic/catalog.json",
            "items/materials/reagent/catalog.json",
            "items/materials/scrap/catalog.json",
            "items/materials/stone/catalog.json",
            "items/materials/wood/catalog.json",
            "items/orbs/combat/catalog.json",
            "items/orbs/magic/catalog.json",
            "items/runes/defensive/catalog.json",
            "items/runes/offensive/catalog.json",
            "items/weapons/catalog.json"
        };
  }

  #region Version and Metadata Tests

  [Theory]
  [MemberData(nameof(GetAllCatalogs))]
  public void Should_Have_Version(string catalogPath)
  {
    // Arrange
    var fullPath = Path.Combine(_dataPath, catalogPath);
    if (!File.Exists(fullPath)) return; // Skip if not migrated yet

    var json = JObject.Parse(File.ReadAllText(fullPath));

    // Act
    var metadata = json["metadata"] as JObject;
    var version = metadata?["version"]?.ToString();

    // Assert
    version.Should().NotBeNullOrEmpty($"{catalogPath} missing version in metadata");
  }

  [Theory]
  [MemberData(nameof(GetAllCatalogs))]
  public void Should_Have_Required_V5_Metadata(string catalogPath)
  {
    // Arrange
    var fullPath = Path.Combine(_dataPath, catalogPath);
    if (!File.Exists(fullPath)) return;

    var json = JObject.Parse(File.ReadAllText(fullPath));
    var metadata = json["metadata"] as JObject;

    // Assert
    metadata.Should().NotBeNull($"{catalogPath} missing metadata");
    metadata.Should().ContainKey("version", $"{catalogPath} metadata missing version");
    metadata.Should().ContainKey("type", $"{catalogPath} metadata missing type");
    metadata.Should().ContainKey("lastUpdated", $"{catalogPath} metadata missing lastUpdated");
    metadata.Should().ContainKey("description", $"{catalogPath} metadata missing description");
  }

  #endregion

  #region Attributes Object Tests

  [Theory]
  [MemberData(nameof(GetAllCatalogs))]
  public void All_Items_Should_Have_Attributes_Object(string catalogPath)
  {
    // Arrange
    var fullPath = Path.Combine(_dataPath, catalogPath);
    if (!File.Exists(fullPath)) return;

    var json = JObject.Parse(File.ReadAllText(fullPath));
    var allItems = GetAllItemsFromCatalog(json);

    // Only check enemies and NPCs - items don't have attributes (they have requirements/traits)
    var isEnemyOrNpc = catalogPath.StartsWith("enemies/") || catalogPath.StartsWith("npcs/");
    if (!isEnemyOrNpc) return;

    // Assert - Enemies and NPCs MUST have attributes
    foreach (var item in allItems)
    {
      var itemName = item["name"]?.ToString() ?? "Unknown";
      item.Should().ContainKey("attributes",
          $"{catalogPath} - Enemy/NPC '{itemName}' missing attributes object");

      var attributes = item["attributes"] as JObject;
      attributes.Should().NotBeNull(
          $"{catalogPath} - Enemy/NPC '{itemName}' attributes is not an object");
    }
  }

  [Theory]
  [MemberData(nameof(GetAllCatalogs))]
  public void Attributes_Should_Have_Standard_Six_Attributes(string catalogPath)
  {
    // Arrange
    var fullPath = Path.Combine(_dataPath, catalogPath);
    if (!File.Exists(fullPath)) return;

    var json = JObject.Parse(File.ReadAllText(fullPath));
    var allItems = GetAllItemsFromCatalog(json);
    var requiredAttributes = new[] { "strength", "dexterity", "constitution", "intelligence", "wisdom", "charisma" };

    // Only check enemies and NPCs - items don't have attributes (they have requirements/traits)
    var isEnemyOrNpc = catalogPath.StartsWith("enemies/") || catalogPath.StartsWith("npcs/");
    if (!isEnemyOrNpc) return;

    // Assert - Enemies and NPCs MUST have all 6 attributes
    foreach (var item in allItems)
    {
      var itemName = item["name"]?.ToString() ?? "Unknown";
      var attributes = item["attributes"] as JObject;

      attributes.Should().NotBeNull($"{catalogPath} - Enemy/NPC '{itemName}' must have attributes object");

      foreach (var attr in requiredAttributes)
      {
        attributes.Should().ContainKey(attr,
            $"{catalogPath} - Enemy/NPC '{itemName}' missing attribute: {attr}");

        var value = attributes[attr]?.Value<int>();
        value.Should().NotBeNull()
            .And.BeGreaterThan(0)
            .And.BeLessThanOrEqualTo(30,
            $"{catalogPath} - Enemy/NPC '{itemName}' has invalid {attr} value: {value}");
      }
    }
  }

  [Theory]
  [MemberData(nameof(GetAllCatalogs))]
  public void Attributes_Should_Only_Contain_Valid_Keys(string catalogPath)
  {
    // Arrange
    var fullPath = Path.Combine(_dataPath, catalogPath);
    if (!File.Exists(fullPath)) return;

    var json = JObject.Parse(File.ReadAllText(fullPath));
    var allItems = GetAllItemsFromCatalog(json);
    var validAttributes = new[] { "strength", "dexterity", "constitution", "intelligence", "wisdom", "charisma" };

    // Assert
    foreach (var item in allItems)
    {
      var itemName = item["name"]?.ToString() ?? "Unknown";
      var attributes = item["attributes"] as JObject;

      if (attributes == null) continue;

      foreach (var property in attributes.Properties())
      {
        validAttributes.Should().Contain(property.Name,
            $"{catalogPath} - Item '{itemName}' has invalid attribute: {property.Name}");
      }
    }
  }

  #endregion

  #region Stats Object Tests

  [Theory]
  [MemberData(nameof(GetEnemyCatalogs))]
  public void Enemies_Should_Have_Stats_Object(string catalogPath)
  {
    // Arrange
    var fullPath = Path.Combine(_dataPath, catalogPath);
    if (!File.Exists(fullPath)) return;

    var json = JObject.Parse(File.ReadAllText(fullPath));
    var allItems = GetAllItemsFromCatalog(json);

    // Assert - Enemies require stats
    foreach (var item in allItems)
    {
      var itemName = item["name"]?.ToString() ?? "Unknown";
      item.Should().ContainKey("stats",
          $"{catalogPath} - Enemy '{itemName}' missing stats object");

      var stats = item["stats"] as JObject;
      stats.Should().NotBeNull(
          $"{catalogPath} - Enemy '{itemName}' stats is not an object");
    }
  }

  [Theory]
  [MemberData(nameof(GetAllCatalogs))]
  public void Items_May_Have_Stats_Object(string catalogPath)
  {
    // Arrange
    var fullPath = Path.Combine(_dataPath, catalogPath);
    if (!File.Exists(fullPath)) return;

    var json = JObject.Parse(File.ReadAllText(fullPath));
    var allItems = GetAllItemsFromCatalog(json);

    // Assert - Stats optional for items, but if present must be object
    foreach (var item in allItems)
    {
      var itemName = item["name"]?.ToString() ?? "Unknown";
      if (item.ContainsKey("stats"))
      {
        var stats = item["stats"];
        (stats is JObject).Should().BeTrue(
            $"{catalogPath} - Item '{itemName}' stats must be an object");
      }
    }
  }

  [Theory]
  [MemberData(nameof(GetAllCatalogs))]
  public void Stats_Should_Use_Formula_Strings(string catalogPath)
  {
    // Arrange
    var fullPath = Path.Combine(_dataPath, catalogPath);
    if (!File.Exists(fullPath)) return;

    var json = JObject.Parse(File.ReadAllText(fullPath));
    var allItems = GetAllItemsFromCatalog(json);

    // Assert
    foreach (var item in allItems)
    {
      var itemName = item["name"]?.ToString() ?? "Unknown";
      var stats = item["stats"] as JObject;

      if (stats == null) continue;

      foreach (var stat in stats.Properties())
      {
        var value = stat.Value.ToString();

        // Should be a formula string (contains operators or _mod references)
        var isFormula = value.Contains("_mod") ||
                        value.Contains("+") ||
                        value.Contains("-") ||
                        value.Contains("*") ||
                        value.Contains("/") ||
                        value.Contains("level");

        isFormula.Should().BeTrue(
            $"{catalogPath} - Item '{itemName}' stat '{stat.Name}' should use formula syntax: {value}");
      }
    }
  }

  [Theory]
  [MemberData(nameof(GetAllCatalogs))]
  public void Stats_Formulas_Should_Use_Valid_Modifier_References(string catalogPath)
  {
    // Arrange
    var fullPath = Path.Combine(_dataPath, catalogPath);
    if (!File.Exists(fullPath)) return;

    var json = JObject.Parse(File.ReadAllText(fullPath));
    var allItems = GetAllItemsFromCatalog(json);
    var validModifiers = new[] { "strength_mod", "dexterity_mod", "constitution_mod",
                                     "intelligence_mod", "wisdom_mod", "charisma_mod" };
    var validContexts = new[] { "wielder.", "wearer.", "consumer.", "caster.", "target." };

    // Assert
    foreach (var item in allItems)
    {
      var itemName = item["name"]?.ToString() ?? "Unknown";
      var stats = item["stats"] as JObject;

      if (stats == null) continue;

      foreach (var stat in stats.Properties())
      {
        var formula = stat.Value.ToString();

        // Find all _mod references
        var modMatches = Regex.Matches(formula, @"(\w+\.)?(\w+_mod)");

        foreach (Match match in modMatches)
        {
          var context = match.Groups[1].Value;
          var modifier = match.Groups[2].Value;

          // Validate modifier name
          validModifiers.Should().Contain(modifier,
              $"{catalogPath} - Item '{itemName}' stat '{stat.Name}' uses invalid modifier: {modifier}");

          // If context prefix exists, validate it
          if (!string.IsNullOrEmpty(context))
          {
            validContexts.Should().Contain(context,
                $"{catalogPath} - Item '{itemName}' stat '{stat.Name}' uses invalid context: {context}");
          }
        }
      }
    }
  }

  #endregion

  #region Properties Tests

  [Theory]
  [MemberData(nameof(GetEnemyCatalogs))]
  public void Enemy_Types_Should_Have_Properties_Object(string catalogPath)
  {
    // Arrange
    var fullPath = Path.Combine(_dataPath, catalogPath);
    if (!File.Exists(fullPath)) return;

    var json = JObject.Parse(File.ReadAllText(fullPath));
    var types = GetAllTypesFromCatalog(json);

    // Assert - Properties required for enemies
    foreach (var type in types)
    {
      var typeName = type.Key;
      type.Value.Should().ContainKey("properties",
          $"{catalogPath} - Enemy type '{typeName}' missing properties object");

      var properties = type.Value["properties"] as JObject;
      properties.Should().NotBeNull(
          $"{catalogPath} - Enemy type '{typeName}' properties is not an object");
    }
  }

  [Theory]
  [MemberData(nameof(GetAllCatalogs))]
  public void Item_Types_May_Have_Properties_Object(string catalogPath)
  {
    // Arrange
    var fullPath = Path.Combine(_dataPath, catalogPath);
    if (!File.Exists(fullPath)) return;

    var json = JObject.Parse(File.ReadAllText(fullPath));
    var types = GetAllTypesFromCatalog(json);

    // Assert - Properties optional for items, but if present must be object
    foreach (var type in types)
    {
      var typeName = type.Key;
      if (type.Value.ContainsKey("properties"))
      {
        var properties = type.Value["properties"];
        (properties is JObject).Should().BeTrue(
            $"{catalogPath} - Type '{typeName}' properties must be an object");
      }
    }
  }

  [Theory]
  [MemberData(nameof(GetAllCatalogs))]
  public void Items_Should_Not_Have_Redundant_Type_Properties(string catalogPath)
  {
    // Arrange
    var fullPath = Path.Combine(_dataPath, catalogPath);
    if (!File.Exists(fullPath)) return;

    var json = JObject.Parse(File.ReadAllText(fullPath));
    var types = GetAllTypesFromCatalog(json);

    // Assert
    foreach (var type in types)
    {
      var typeName = type.Key;
      var properties = type.Value["properties"] as JObject;
      var items = type.Value["items"] as JArray;

      if (properties == null || items == null) continue;

      var propertyKeys = properties.Properties().Select(p => p.Name).ToList();

      foreach (var item in items.OfType<JObject>())
      {
        var itemName = item["name"]?.ToString() ?? "Unknown";

        // Items should not duplicate type-level properties
        foreach (var propKey in propertyKeys)
        {
          // Allow "weight" (item weight in lbs) but not type properties
          if (propKey != "weight")
          {
            item.Should().NotContainKey(propKey,
                $"{catalogPath} - Item '{itemName}' has redundant type property: {propKey} " +
                $"(should be at type '{typeName}' level only)");
          }
        }
      }
    }
  }

  #endregion

  #region Traits Object Tests

  [Theory]
  [MemberData(nameof(GetAllCatalogs))]
  public void All_Items_Should_Have_Traits_Object(string catalogPath)
  {
    // Arrange
    var fullPath = Path.Combine(_dataPath, catalogPath);
    if (!File.Exists(fullPath)) return;

    var json = JObject.Parse(File.ReadAllText(fullPath));
    var allItems = GetAllItemsFromCatalog(json);

    // Assert
    foreach (var item in allItems)
    {
      var itemName = item["name"]?.ToString() ?? "Unknown";
      item.Should().ContainKey("traits",
          $"{catalogPath} - Item '{itemName}' missing traits object");

      var traits = item["traits"];
      traits.Should().NotBeNull(
          $"{catalogPath} - Item '{itemName}' traits is null (should be empty object {{}})");
    }
  }

  [Theory]
  [MemberData(nameof(GetAllCatalogs))]
  public void Traits_Should_Be_Object_Not_Array(string catalogPath)
  {
    // Arrange
    var fullPath = Path.Combine(_dataPath, catalogPath);
    if (!File.Exists(fullPath)) return;

    var json = JObject.Parse(File.ReadAllText(fullPath));
    var allItems = GetAllItemsFromCatalog(json);

    // Assert
    foreach (var item in allItems)
    {
      var itemName = item["name"]?.ToString() ?? "Unknown";
      var traits = item["traits"];

      (traits is JObject).Should().BeTrue(
          $"{catalogPath} - Item '{itemName}' traits must be an object, not array");
    }
  }

  #endregion

  #region Structured Damage Tests

  [Theory]
  [MemberData(nameof(GetWeaponCatalogs))]
  public void Weapons_Should_Have_Structured_Damage_Object(string catalogPath)
  {
    // Arrange
    var fullPath = Path.Combine(_dataPath, catalogPath);
    if (!File.Exists(fullPath)) return;

    var json = JObject.Parse(File.ReadAllText(fullPath));
    var allItems = GetAllItemsFromCatalog(json);

    // Assert
    foreach (var item in allItems)
    {
      var itemName = item["name"]?.ToString() ?? "Unknown";

      if (item.ContainsKey("damage"))
      {
        var damage = item["damage"] as JObject;
        damage.Should().NotBeNull(
            $"{catalogPath} - Weapon '{itemName}' damage must be an object");

        damage.Should().ContainKey("min",
            $"{catalogPath} - Weapon '{itemName}' damage missing 'min' field");
        damage.Should().ContainKey("max",
            $"{catalogPath} - Weapon '{itemName}' damage missing 'max' field");

        var min = damage["min"]?.Value<int>();
        var max = damage["max"]?.Value<int>();

        min.Should().NotBeNull()
            .And.BeGreaterThan(0,
            $"{catalogPath} - Weapon '{itemName}' damage.min invalid");

        max.Should().NotBeNull()
            .And.BeGreaterThanOrEqualTo(min.Value,
            $"{catalogPath} - Weapon '{itemName}' damage.max must be >= min");

        // Modifier is optional but if present, must be valid
        if (damage.ContainsKey("modifier"))
        {
          var modifier = damage["modifier"]?.ToString();
          modifier.Should().NotBeNullOrEmpty(
              $"{catalogPath} - Weapon '{itemName}' damage.modifier is empty");
        }
      }
    }
  }

  [Theory]
  [MemberData(nameof(GetAllCatalogs))]
  public void Should_Not_Use_Old_Dice_Notation(string catalogPath)
  {
    // Arrange
    var fullPath = Path.Combine(_dataPath, catalogPath);
    if (!File.Exists(fullPath)) return;

    var json = JObject.Parse(File.ReadAllText(fullPath));
    var jsonText = File.ReadAllText(fullPath);

    // Assert - Should not contain dice notation like "1d8", "2d6", etc.
    jsonText.Should().NotMatchRegex(@"\d+d\d+",
        $"{catalogPath} contains old dice notation (e.g., '1d8'). Use structured damage objects instead.");
  }

  #endregion

  #region Combat Section Tests (Enemies Only)

  [Theory]
  [MemberData(nameof(GetEnemyCatalogs))]
  public void Enemies_Should_Have_Combat_Section(string catalogPath)
  {
    // Arrange
    var fullPath = Path.Combine(_dataPath, catalogPath);
    if (!File.Exists(fullPath)) return;

    var json = JObject.Parse(File.ReadAllText(fullPath));
    var allItems = GetAllItemsFromCatalog(json);

    // Assert
    foreach (var item in allItems)
    {
      var itemName = item["name"]?.ToString() ?? "Unknown";
      item.Should().ContainKey("combat",
          $"{catalogPath} - Enemy '{itemName}' missing combat section");

      var combat = item["combat"] as JObject;
      combat.Should().NotBeNull(
          $"{catalogPath} - Enemy '{itemName}' combat is not an object");

      combat.Should().ContainKey("abilities",
          $"{catalogPath} - Enemy '{itemName}' combat missing abilities array");
    }
  }

  [Theory]
  [MemberData(nameof(GetEnemyCatalogs))]
  public void Combat_Abilities_Should_Use_References(string catalogPath)
  {
    // Arrange
    var fullPath = Path.Combine(_dataPath, catalogPath);
    if (!File.Exists(fullPath)) return;

    var json = JObject.Parse(File.ReadAllText(fullPath));
    var allItems = GetAllItemsFromCatalog(json);

    // Assert
    foreach (var item in allItems)
    {
      var itemName = item["name"]?.ToString() ?? "Unknown";
      var combat = item["combat"] as JObject;
      var abilities = combat?["abilities"] as JArray;

      if (abilities == null) continue;

      foreach (var ability in abilities)
      {
        var abilityRef = ability.ToString();
        abilityRef.Should().StartWith("@abilities/",
            $"{catalogPath} - Enemy '{itemName}' has invalid ability reference: {abilityRef}");
      }
    }
  }

  #endregion

  #region Rarity Tests

  [Theory]
  [MemberData(nameof(GetAllCatalogs))]
  public void All_Items_Should_Have_RarityWeight_Field(string catalogPath)
  {
    // Arrange
    var fullPath = Path.Combine(_dataPath, catalogPath);
    if (!File.Exists(fullPath)) return;

    var json = JObject.Parse(File.ReadAllText(fullPath));
    var allItems = GetAllItemsFromCatalog(json);

    // Assert
    foreach (var item in allItems)
    {
      var itemName = item["name"]?.ToString() ?? "Unknown";
      item.Should().ContainKey("rarityWeight",
          $"{catalogPath} - Item '{itemName}' missing rarityWeight field");

      var weight = item["rarityWeight"]?.Value<int>();
      weight.Should().NotBeNull()
          .And.BeGreaterThan(0,
          $"{catalogPath} - Item '{itemName}' has invalid rarityWeight value: {weight}");
    }
  }

  #endregion

  #region Helper Methods

  private List<JObject> GetAllItemsFromCatalog(JObject catalog)
  {
    var items = new List<JObject>();

    // v5.1 structure: Look for *_types properties (enemy_types, weapon_types, etc.)
    foreach (var property in catalog.Properties())
    {
      if (property.Name == "metadata") continue;

      if (property.Name.EndsWith("_types") && property.Value is JObject typesObj)
      {
        foreach (var type in typesObj.Properties())
        {
          var typeObj = type.Value as JObject;
          var itemsArray = typeObj?["items"] as JArray;

          if (itemsArray != null)
          {
            items.AddRange(itemsArray.OfType<JObject>());
          }
        }
      }
    }

    return items;
  }

  private Dictionary<string, JObject> GetAllTypesFromCatalog(JObject catalog)
  {
    var types = new Dictionary<string, JObject>();

    foreach (var property in catalog.Properties())
    {
      if (property.Name == "metadata") continue;

      if (property.Name.EndsWith("_types") && property.Value is JObject typesObj)
      {
        foreach (var type in typesObj.Properties())
        {
          types[type.Name] = type.Value as JObject;
        }
      }
    }

    return types;
  }

  public static IEnumerable<object[]> GetAllCatalogs()
  {
    var instance = new CatalogJsonV5ComplianceTests();
    return instance._catalogPaths.Select(path => new object[] { path });
  }

  public static IEnumerable<object[]> GetEnemyCatalogs()
  {
    var instance = new CatalogJsonV5ComplianceTests();
    return instance._catalogPaths
        .Where(p => p.StartsWith("enemies/"))
        .Select(path => new object[] { path });
  }

  public static IEnumerable<object[]> GetWeaponCatalogs()
  {
    var instance = new CatalogJsonV5ComplianceTests();
    return instance._catalogPaths
        .Where(p => p.Contains("weapons/"))
        .Select(path => new object[] { path });
  }

  #endregion
}
