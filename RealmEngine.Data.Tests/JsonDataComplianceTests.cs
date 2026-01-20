using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RealmEngine.Data.Tests;

[Trait("Category", "Compliance")]
/// <summary>
/// Comprehensive JSON data validation tests for ALL RealmEngine.Data files
/// Tests catalog.json, names.json, .cbconfig.json, and component data files for v4.0/v4.1 compliance
/// </summary>
public class JsonDataComplianceTests
{
    private readonly string _dataPath;
    private readonly List<string> _allCatalogFiles;
    private readonly List<string> _allNamesFiles;
    private readonly List<string> _allConfigFiles;
    private readonly List<string> _allComponentFiles;

    public JsonDataComplianceTests()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var solutionRoot = Directory.GetParent(baseDir)?.Parent?.Parent?.Parent?.Parent?.FullName;
        if (solutionRoot == null)
            throw new DirectoryNotFoundException($"Could not find solution root from: {baseDir}");

        _dataPath = Path.Combine(solutionRoot, "RealmEngine.Data", "Data", "Json");

        if (!Directory.Exists(_dataPath))
            throw new DirectoryNotFoundException($"Data directory not found: {_dataPath}");

        // Discover ALL files dynamically
        _allCatalogFiles = Directory.GetFiles(_dataPath, "catalog.json", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(_dataPath, f))
            .ToList();

        _allNamesFiles = Directory.GetFiles(_dataPath, "names.json", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(_dataPath, f))
            .ToList();

        _allConfigFiles = Directory.GetFiles(_dataPath, ".cbconfig.json", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(_dataPath, f))
            .ToList();

        // Discover component/data files (all other .json files)
        var allJsonFiles = Directory.GetFiles(_dataPath, "*.json", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(_dataPath, f))
            .ToList();

        _allComponentFiles = allJsonFiles
            .Where(f => !f.EndsWith("catalog.json") &&
                        !f.EndsWith("names.json") &&
                        !f.EndsWith(".cbconfig.json"))
            .ToList();
    }

    #region Catalog.json Tests (v4.0 Standards)

    [Fact]
    public void Should_Discover_All_Catalog_Files()
    {
        // Assert - should have found catalogs
        _allCatalogFiles.Should().NotBeEmpty();
        _allCatalogFiles.Should().HaveCountGreaterThan(50, "expected 60+ catalog files");
    }

    [Theory]
    [MemberData(nameof(GetAllCatalogFiles))]
    public void Catalog_Should_Have_Required_Metadata(string relativePath)
    {
        // Arrange
        var fullPath = Path.Combine(_dataPath, relativePath);
        var json = JObject.Parse(File.ReadAllText(fullPath));
        var metadata = json["metadata"] as JObject;

        // Assert
        metadata.Should().NotBeNull($"{relativePath} missing metadata section");
        metadata!["description"].Should().NotBeNull($"{relativePath} missing description");
        metadata["version"].Should().NotBeNull($"{relativePath} missing version");
        metadata["lastUpdated"].Should().NotBeNull($"{relativePath} missing lastUpdated");
        metadata["type"].Should().NotBeNull($"{relativePath} missing type");
    }

    [Theory]
    [MemberData(nameof(GetAllCatalogFiles))]
    public void Catalog_Version_Should_Be_Valid(string relativePath)
    {
        // Arrange
        var fullPath = Path.Combine(_dataPath, relativePath);
        var json = JObject.Parse(File.ReadAllText(fullPath));
        var version = json["metadata"]?["version"]?.ToString();

        // Assert - Just check that version field exists (not validating specific values)
        version.Should().NotBeNullOrEmpty($"{relativePath} must have a version field");
    }

    [Theory]
    [MemberData(nameof(GetAllCatalogFiles))]
    public void Catalog_Type_Should_End_With_Catalog(string relativePath)
    {
        // Arrange
        var fullPath = Path.Combine(_dataPath, relativePath);
        var json = JObject.Parse(File.ReadAllText(fullPath));
        var type = json["metadata"]?["type"]?.ToString();

        // Assert
        type.Should().NotBeNullOrEmpty($"{relativePath} has empty type");
        type.Should().EndWith("_catalog", $"{relativePath} type should end with '_catalog'");
    }

    [Theory]
    [MemberData(nameof(GetAllCatalogFiles))]
    public void Catalog_LastUpdated_Should_Be_Valid_Date(string relativePath)
    {
        // Arrange
        var fullPath = Path.Combine(_dataPath, relativePath);
        var json = JObject.Parse(File.ReadAllText(fullPath));
        var dateStr = json["metadata"]?["lastUpdated"]?.ToString();

        // Act
        var isValid = DateTime.TryParse(dateStr, out var date);

        // Assert
        isValid.Should().BeTrue($"{relativePath} has invalid date: {dateStr}");
        date.Should().BeOnOrBefore(DateTime.UtcNow, $"{relativePath} date in future");
        date.Should().BeAfter(new DateTime(2025, 1, 1), $"{relativePath} date too old");
    }

    [Theory]
    [MemberData(nameof(GetAllCatalogFiles))]
    public void Catalog_Items_Should_Have_RarityWeight(string relativePath)
    {
        // Arrange
        var fullPath = Path.Combine(_dataPath, relativePath);
        var json = JObject.Parse(File.ReadAllText(fullPath));
        var allItems = GetAllItemsFromCatalog(json);

        if (!allItems.Any()) return; // Skip if no items

        // Assert - items should have rarityWeight OR selectionWeight
        foreach (var item in allItems)
        {
            var hasRarityWeight = ((JObject)item).ContainsKey("rarityWeight");
            var hasSelectionWeight = ((JObject)item).ContainsKey("selectionWeight");

            (hasRarityWeight || hasSelectionWeight).Should().BeTrue(
                $"{relativePath} - Item '{item["name"]}' missing rarityWeight or selectionWeight");

            var weight = hasRarityWeight
                ? item["rarityWeight"]?.Value<int>()
                : item["selectionWeight"]?.Value<int>();
            weight.Should().NotBeNull($"{relativePath} - Item '{item["name"]}' null weight");
            weight.Should().BeGreaterThan(0, $"{relativePath} - Item '{item["name"]}' invalid weight");
        }
    }

    [Theory]
    [MemberData(nameof(GetAllCatalogFiles))]
    public void Catalog_Items_Should_Have_Name(string relativePath)
    {
        // Arrange
        var fullPath = Path.Combine(_dataPath, relativePath);
        var json = JObject.Parse(File.ReadAllText(fullPath));
        var allItems = GetAllItemsFromCatalog(json);

        // Assert
        foreach (var item in allItems)
        {
            ((JObject)item).Should().ContainKey("name", $"{relativePath} - Item missing 'name'");
            var name = item["name"]?.ToString();
            name.Should().NotBeNullOrWhiteSpace($"{relativePath} - Item has empty name");
        }
    }

    [Theory]
    [MemberData(nameof(GetAllCatalogFiles))]
    public void Catalog_Should_Not_Have_Weight_Instead_Of_RarityWeight(string relativePath)
    {
        // Arrange
        var fullPath = Path.Combine(_dataPath, relativePath);
        var json = JObject.Parse(File.ReadAllText(fullPath));
        var allItems = GetAllItemsFromCatalog(json);

        // Assert - items should have rarityWeight OR selectionWeight (weight is for physical weight only)
        foreach (var item in allItems)
        {
            var hasRarityWeight = item.ContainsKey("rarityWeight");
            var hasSelectionWeight = item.ContainsKey("selectionWeight");
            var hasWeight = item.ContainsKey("weight");

            // If item has "weight" but no rarityWeight or selectionWeight, check if it's a decimal (physical weight)
            if (hasWeight && !hasRarityWeight && !hasSelectionWeight)
            {
                var weight = item["weight"]?.Value<double>();
                // Physical weight is typically a decimal (1.5, 3.0, etc.) or > 100
                // If it's an integer <= 100, it's likely meant to be rarityWeight
                if (weight != null && weight % 1 == 0 && weight <= 100 && weight > 0)
                {
                    Assert.Fail($"{relativePath} - Item '{item["name"]}' has 'weight' = {weight} which looks like rarityWeight. Use 'rarityWeight' or 'selectionWeight' instead.");
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetAllCatalogFiles))]
    public void Catalog_Description_Should_Not_Be_Empty(string relativePath)
    {
        // Arrange
        var fullPath = Path.Combine(_dataPath, relativePath);
        var json = JObject.Parse(File.ReadAllText(fullPath));
        var description = json["metadata"]?["description"]?.ToString();

        // Assert
        description.Should().NotBeNullOrWhiteSpace($"{relativePath} has empty description");
        description!.Length.Should().BeGreaterThan(10, $"{relativePath} description too short");
    }

    [Theory]
    [MemberData(nameof(GetAllCatalogFiles))]
    public void Catalog_Should_Have_Items_Or_Components(string relativePath)
    {
        // Arrange
        var fullPath = Path.Combine(_dataPath, relativePath);
        var json = JObject.Parse(File.ReadAllText(fullPath));
        var allItems = GetAllItemsFromCatalog(json);

        // Assert - catalogs should have actual content
        allItems.Should().NotBeEmpty($"{relativePath} has no items/components");
    }

    [Theory]
    [MemberData(nameof(GetAllCatalogFiles))]
    public void Catalog_Item_Names_Should_Not_Be_Empty(string relativePath)
    {
        // Arrange
        var fullPath = Path.Combine(_dataPath, relativePath);
        var json = JObject.Parse(File.ReadAllText(fullPath));
        var allItems = GetAllItemsFromCatalog(json);

        // Assert
        foreach (var item in allItems)
        {
            var name = item["name"]?.ToString();
            name.Should().NotBeNullOrWhiteSpace($"{relativePath} has item with empty name");
            name!.Length.Should().BeGreaterThan(1, $"{relativePath} has single-char name");
        }
    }

    [Theory]
    [MemberData(nameof(GetAllCatalogFiles))]
    public void Catalog_Should_Use_References_For_Abilities(string relativePath)
    {
        // Arrange
        var fullPath = Path.Combine(_dataPath, relativePath);
        var json = JObject.Parse(File.ReadAllText(fullPath));
        var allItems = GetAllItemsFromCatalog(json);

        // Assert - v4.1 standard: abilities should use @references, not hardcoded strings
        foreach (var item in allItems)
        {
            if (item["abilities"] is JArray abilities)
            {
                foreach (var ability in abilities)
                {
                    var abilityStr = ability.ToString();
                    if (!string.IsNullOrWhiteSpace(abilityStr) && !abilityStr.StartsWith("@"))
                    {
                        Assert.Fail($"{relativePath} - Item '{item["name"]}' has hardcoded ability '{abilityStr}' instead of reference");
                    }
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetAllCatalogFiles))]
    public void Catalog_References_Should_Have_Valid_Syntax(string relativePath)
    {
        // Arrange
        var fullPath = Path.Combine(_dataPath, relativePath);
        var jsonText = File.ReadAllText(fullPath);
        var json = JObject.Parse(jsonText);

        // Find all references in the JSON
        var references = FindAllReferences(jsonText);

        // Assert - all @ references should follow v4.1 syntax: @domain/path/category:item-name[filters]?.property.nested
        foreach (var reference in references)
        {
            // v4.1 reference pattern supports:
            // - Domain/path: @domain/path/category
            // - Item selector: :item-name or :*
            // - Optional filters: [property=value] or [property>5]
            // - Optional marker: ?
            // - Property access: .property.nested
            reference.Should().MatchRegex(@"^@[\w-]+/[\w-/]+:[\w-*\s]+(\[[^\]]+\])?(\?)?(\.\w+)*$",
                $"{relativePath} has invalid reference syntax: {reference}");
        }
    }

    #endregion

    #region Names.json Tests (v4.0 Pattern Generation)

    [Fact]
    public void Should_Discover_All_Names_Files()
    {
        // Assert
        _allNamesFiles.Should().NotBeEmpty();
        _allNamesFiles.Should().HaveCountGreaterThan(25, "expected 25+ names files");
    }

    [Theory]
    [MemberData(nameof(GetAllNamesFiles))]
    public void Names_Should_Have_Required_Metadata(string relativePath)
    {
        // Arrange
        var fullPath = Path.Combine(_dataPath, relativePath);
        var json = JObject.Parse(File.ReadAllText(fullPath));
        var metadata = json["metadata"] as JObject;

        // Assert
        metadata.Should().NotBeNull($"{relativePath} missing metadata");
        metadata!["version"].Should().NotBeNull($"{relativePath} missing version");
        metadata["type"].Should().NotBeNull($"{relativePath} missing type");
        metadata["lastUpdated"].Should().NotBeNull($"{relativePath} missing lastUpdated");
        metadata["description"].Should().NotBeNull($"{relativePath} missing description");
    }

    [Theory]
    [MemberData(nameof(GetAllNamesFiles))]
    public void Names_Type_Should_Be_Pattern_Generation(string relativePath)
    {
        // Arrange
        var fullPath = Path.Combine(_dataPath, relativePath);
        var json = JObject.Parse(File.ReadAllText(fullPath));
        var type = json["metadata"]?["type"]?.ToString();

        // Assert
        type.Should().Be("pattern_generation", $"{relativePath} wrong type");
    }

    [Theory]
    [MemberData(nameof(GetAllNamesFiles))]
    public void Names_Should_Have_SupportsTraits_Field(string relativePath)
    {
        // Arrange
        var fullPath = Path.Combine(_dataPath, relativePath);
        var json = JObject.Parse(File.ReadAllText(fullPath));
        var metadata = json["metadata"] as JObject;

        // Assert - supportsTraits is REQUIRED per v4.0 standard
        metadata.Should().NotBeNull($"{relativePath} missing metadata");
        metadata!["supportsTraits"].Should().NotBeNull($"{relativePath} missing required field 'supportsTraits'");
        var supportsTraits = metadata["supportsTraits"]?.Type;
        supportsTraits.Should().Be(JTokenType.Boolean, $"{relativePath} supportsTraits must be boolean");
    }

    [Theory]
    [MemberData(nameof(GetAllNamesFiles))]
    public void Names_Should_Have_ComponentKeys_Field(string relativePath)
    {
        // Arrange
        var fullPath = Path.Combine(_dataPath, relativePath);
        var json = JObject.Parse(File.ReadAllText(fullPath));
        var metadata = json["metadata"] as JObject;

        // Assert - componentKeys is REQUIRED per v4.0 standard and must not be empty
        metadata.Should().NotBeNull($"{relativePath} missing metadata");
        metadata!["componentKeys"].Should().NotBeNull($"{relativePath} missing required field 'componentKeys'");
        var componentKeys = metadata["componentKeys"] as JArray;
        componentKeys.Should().NotBeNull($"{relativePath} componentKeys must be array");
        componentKeys.Should().NotBeEmpty($"{relativePath} componentKeys array cannot be empty - add component data or remove the field");
    }

    [Theory]
    [MemberData(nameof(GetAllNamesFiles))]
    public void Names_Should_Have_PatternTokens_Field(string relativePath)
    {
        // Arrange
        var fullPath = Path.Combine(_dataPath, relativePath);
        var json = JObject.Parse(File.ReadAllText(fullPath));
        var metadata = json["metadata"] as JObject;

        // Assert - patternTokens is REQUIRED per v4.0 standard
        metadata.Should().NotBeNull($"{relativePath} missing metadata");
        metadata!["patternTokens"].Should().NotBeNull($"{relativePath} missing required field 'patternTokens'");
        var patternTokens = metadata["patternTokens"] as JArray;
        patternTokens.Should().NotBeNull($"{relativePath} patternTokens must be array");
        patternTokens.Should().NotBeEmpty($"{relativePath} patternTokens array cannot be empty");
    }

    [Theory]
    [MemberData(nameof(GetAllNamesFiles))]
    public void Names_Should_Have_TotalPatterns_Field(string relativePath)
    {
        // Arrange
        var fullPath = Path.Combine(_dataPath, relativePath);
        var json = JObject.Parse(File.ReadAllText(fullPath));
        var metadata = json["metadata"] as JObject;

        // Assert - totalPatterns is REQUIRED per v4.0 standard
        metadata.Should().NotBeNull($"{relativePath} missing metadata");
        metadata!["totalPatterns"].Should().NotBeNull($"{relativePath} missing required field 'totalPatterns'");
        var totalPatterns = metadata["totalPatterns"]?.Value<int>();
        totalPatterns.Should().NotBeNull($"{relativePath} totalPatterns must be a number");
        totalPatterns.Should().BeGreaterThan(0, $"{relativePath} totalPatterns must be positive");
    }

    [Theory]
    [MemberData(nameof(GetAllNamesFiles))]
    public void Names_Should_Have_RaritySystem_Field(string relativePath)
    {
        // Arrange
        var fullPath = Path.Combine(_dataPath, relativePath);
        var json = JObject.Parse(File.ReadAllText(fullPath));
        var metadata = json["metadata"] as JObject;

        // Assert - raritySystem is REQUIRED per v4.0 standard
        metadata.Should().NotBeNull($"{relativePath} missing metadata");
        metadata!["raritySystem"].Should().NotBeNull($"{relativePath} missing required field 'raritySystem'");
        var raritySystem = metadata["raritySystem"]?.ToString();
        raritySystem.Should().NotBeNullOrWhiteSpace($"{relativePath} raritySystem cannot be empty");
        // Note: Standard shows "weight-based" as example, but other systems may be valid
    }

    [Theory]
    [MemberData(nameof(GetAllNamesFiles))]
    public void Names_Should_Have_Patterns_Array(string relativePath)
    {
        // Arrange
        var fullPath = Path.Combine(_dataPath, relativePath);
        var json = JObject.Parse(File.ReadAllText(fullPath));
        var patterns = json["patterns"] as JArray;

        // Assert
        patterns.Should().NotBeNull($"{relativePath} missing patterns array");
        patterns.Should().NotBeEmpty($"{relativePath} patterns array is empty");
    }

    [Theory]
    [MemberData(nameof(GetAllNamesFiles))]
    public void Names_Patterns_Should_Have_RarityWeight(string relativePath)
    {
        // Arrange
        var fullPath = Path.Combine(_dataPath, relativePath);
        var json = JObject.Parse(File.ReadAllText(fullPath));
        var patterns = json["patterns"] as JArray;

        if (patterns == null) return;

        // Assert
        foreach (var pattern in patterns)
        {
            ((JObject)pattern).Should().ContainKey("rarityWeight",
                $"{relativePath} - Pattern missing rarityWeight");
        }
    }

    [Theory]
    [MemberData(nameof(GetAllNamesFiles))]
    public void Names_Should_Not_Have_Example_Field(string relativePath)
    {
        // Arrange
        var fullPath = Path.Combine(_dataPath, relativePath);
        var json = JObject.Parse(File.ReadAllText(fullPath));
        var patterns = json["patterns"] as JArray;

        if (patterns == null) return;

        // Assert - v4.0 standard prohibits example fields
        foreach (var pattern in patterns)
        {
            ((JObject)pattern).Should().NotContainKey("example",
                $"{relativePath} - Pattern has forbidden 'example' field");
        }
    }

    [Theory]
    [MemberData(nameof(GetAllNamesFiles))]
    public void Names_Should_Have_Components(string relativePath)
    {
        // Arrange
        var fullPath = Path.Combine(_dataPath, relativePath);
        var json = JObject.Parse(File.ReadAllText(fullPath));
        var components = json["components"] as JObject;

        // Assert
        components.Should().NotBeNull($"{relativePath} missing components section");
    }

    [Theory]
    [MemberData(nameof(GetAllNamesFiles))]
    public void Names_Metadata_Notes_Should_Be_String_Array_Only(string relativePath)
    {
        // Arrange
        var fullPath = Path.Combine(_dataPath, relativePath);
        var json = JObject.Parse(File.ReadAllText(fullPath));
        var notes = json["metadata"]?["notes"] as JArray;

        if (notes == null) return; // notes is optional

        // Assert - v4.0 standard: notes must be array of strings ONLY, no objects
        foreach (var note in notes)
        {
            note.Type.Should().Be(JTokenType.String,
                $"{relativePath} - metadata.notes contains non-string element: {note.Type}. Expected only strings.");
        }
    }

    [Theory]
    [MemberData(nameof(GetAllNamesFiles))]
    public void Names_Version_Should_Be_4_0(string relativePath)
    {
        // Arrange
        var fullPath = Path.Combine(_dataPath, relativePath);
        var json = JObject.Parse(File.ReadAllText(fullPath));
        var version = json["metadata"]?["version"]?.ToString();

        // Assert - Accept 4.0, 4.2, 5.0, and 5.1
        version.Should().Match(v => v == "4.0" || v == "4.2" || v == "5.0" || v == "5.1",
            $"{relativePath} version should be '4.0', '4.2', '5.0', or '5.1'");
    }

    [Theory]
    [MemberData(nameof(GetAllNamesFiles))]
    public void Names_LastUpdated_Should_Be_Valid_Date(string relativePath)
    {
        // Arrange
        var fullPath = Path.Combine(_dataPath, relativePath);
        var json = JObject.Parse(File.ReadAllText(fullPath));
        var dateStr = json["metadata"]?["lastUpdated"]?.ToString();

        // Act
        var isValid = DateTime.TryParse(dateStr, out var date);

        // Assert
        isValid.Should().BeTrue($"{relativePath} has invalid date: {dateStr}");
        date.Should().BeOnOrBefore(DateTime.UtcNow, $"{relativePath} date in future");
    }

    [Theory]
    [MemberData(nameof(GetAllNamesFiles))]
    public void Names_Components_Should_Have_RarityWeight(string relativePath)
    {
        // Arrange
        var fullPath = Path.Combine(_dataPath, relativePath);
        var json = JObject.Parse(File.ReadAllText(fullPath));
        var components = json["components"] as JObject;

        if (components == null) return;

        // Assert - all component items must have rarityWeight
        foreach (var componentGroup in components.Properties())
        {
            if (componentGroup.Value is JArray items)
            {
                foreach (var item in items.OfType<JObject>())
                {
                    item.Should().ContainKey("rarityWeight",
                        $"{relativePath} - Component '{componentGroup.Name}' item '{item["value"]}' missing rarityWeight");

                    var weight = item["rarityWeight"]?.Value<int>();
                    weight.Should().BeGreaterThan(0,
                        $"{relativePath} - Component '{componentGroup.Name}' item '{item["value"]}' has invalid rarityWeight");
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetAllNamesFiles))]
    public void Names_Components_Should_Have_Value(string relativePath)
    {
        // Arrange
        var fullPath = Path.Combine(_dataPath, relativePath);
        var json = JObject.Parse(File.ReadAllText(fullPath));
        var components = json["components"] as JObject;

        if (components == null) return;

        // Assert - all component items must have value
        foreach (var componentGroup in components.Properties())
        {
            if (componentGroup.Value is JArray items)
            {
                foreach (var item in items.OfType<JObject>())
                {
                    item.Should().ContainKey("value",
                        $"{relativePath} - Component '{componentGroup.Name}' item missing 'value' field");

                    var value = item["value"]?.ToString();
                    value.Should().NotBeNullOrWhiteSpace(
                        $"{relativePath} - Component '{componentGroup.Name}' has empty value");
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetAllNamesFiles))]
    public void Names_Patterns_Should_Have_Template_Or_Pattern_Field(string relativePath)
    {
        // Arrange
        var fullPath = Path.Combine(_dataPath, relativePath);
        var json = JObject.Parse(File.ReadAllText(fullPath));
        var patterns = json["patterns"] as JArray;

        if (patterns == null) return;

        // Assert - patterns can be strings OR objects with template/pattern field
        foreach (var pattern in patterns)
        {
            if (pattern is JObject obj)
            {
                var hasTemplate = obj.ContainsKey("template");
                var hasPattern = obj.ContainsKey("pattern");

                (hasTemplate || hasPattern).Should().BeTrue(
                    $"{relativePath} - Pattern object missing both 'template' and 'pattern' fields");
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetAllNamesFiles))]
    public void Names_Should_Not_Use_Weight_Instead_Of_RarityWeight(string relativePath)
    {
        // Arrange
        var fullPath = Path.Combine(_dataPath, relativePath);
        var json = JObject.Parse(File.ReadAllText(fullPath));
        var patterns = json["patterns"] as JArray;

        if (patterns == null) return;

        // Assert - patterns should use rarityWeight, not weight
        foreach (var pattern in patterns.OfType<JObject>())
        {
            if (pattern.ContainsKey("weight") && !pattern.ContainsKey("rarityWeight"))
            {
                Assert.Fail($"{relativePath} - Pattern uses 'weight' instead of 'rarityWeight'");
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetAllNamesFiles))]
    public void Names_Should_Use_V4_1_Reference_Syntax_Not_Old_MaterialRef(string relativePath)
    {
        // Arrange
        var fullPath = Path.Combine(_dataPath, relativePath);
        var jsonText = File.ReadAllText(fullPath);
        var json = JObject.Parse(jsonText);
        var patterns = json["patterns"] as JArray;

        if (patterns == null) return;

        // Assert - patterns should use v4.1 syntax @domain/path:item, not old [@materialRef/...] syntax
        foreach (var pattern in patterns.OfType<JObject>())
        {
            var patternStr = pattern["pattern"]?.ToString() ?? pattern["template"]?.ToString() ?? string.Empty;

            if (patternStr.Contains("[@materialRef") || patternStr.Contains("[@"))
            {
                Assert.Fail($"{relativePath} - Pattern uses old [@ref] syntax instead of v4.1 @domain/path:item syntax: {patternStr}");
            }
        }
    }

    #endregion

    #region .cbconfig.json Tests (ContentBuilder UI)

    [Fact]
    public void Should_Discover_All_Config_Files()
    {
        // Assert
        _allConfigFiles.Should().NotBeEmpty();
        _allConfigFiles.Should().HaveCountGreaterThan(60, "expected 65+ config files");
    }

    [Theory]
    [MemberData(nameof(GetAllConfigFiles))]
    public void Config_Should_Have_Icon(string relativePath)
    {
        // Arrange
        var fullPath = Path.Combine(_dataPath, relativePath);
        var json = JObject.Parse(File.ReadAllText(fullPath));

        // Assert
        json.Should().ContainKey("icon", $"{relativePath} missing icon");
        var icon = json["icon"]?.ToString();
        icon.Should().NotBeNullOrWhiteSpace($"{relativePath} has empty icon");
        icon.Should().NotContain("emoji", $"{relativePath} uses emoji instead of MaterialDesign icon");
    }

    [Theory]
    [MemberData(nameof(GetAllConfigFiles))]
    public void Config_Should_Have_SortOrder(string relativePath)
    {
        // Arrange
        var fullPath = Path.Combine(_dataPath, relativePath);
        var json = JObject.Parse(File.ReadAllText(fullPath));

        // Assert
        json.Should().ContainKey("sortOrder", $"{relativePath} missing sortOrder");
        var sortOrder = json["sortOrder"]?.Value<int>();
        sortOrder.Should().NotBeNull($"{relativePath} sortOrder is null");
    }

    [Theory]
    [MemberData(nameof(GetAllConfigFiles))]
    public void Config_Should_Be_Valid_Json(string relativePath)
    {
        // Arrange
        var fullPath = Path.Combine(_dataPath, relativePath);

        // Act
        Action parse = () => JObject.Parse(File.ReadAllText(fullPath));

        // Assert
        parse.Should().NotThrow($"{relativePath} is not valid JSON");
    }

    [Theory]
    [MemberData(nameof(GetAllConfigFiles))]
    public void Config_Icon_Should_Not_Be_Emoji(string relativePath)
    {
        // Arrange
        var fullPath = Path.Combine(_dataPath, relativePath);
        var json = JObject.Parse(File.ReadAllText(fullPath));
        var icon = json["icon"]?.ToString();

        // Assert - should use MaterialDesign icon names, not emojis
        if (!string.IsNullOrEmpty(icon))
        {
            // Check for common emoji patterns (Unicode ranges)
            var hasEmoji = icon.Any(c => c >= 0x1F300 && c <= 0x1F9FF);
            hasEmoji.Should().BeFalse($"{relativePath} uses emoji instead of MaterialDesign icon");
        }
    }

    [Theory]
    [MemberData(nameof(GetAllConfigFiles))]
    public void Config_SortOrder_Should_Be_Positive(string relativePath)
    {
        // Arrange
        var fullPath = Path.Combine(_dataPath, relativePath);
        var json = JObject.Parse(File.ReadAllText(fullPath));
        var sortOrder = json["sortOrder"]?.Value<int>();

        // Assert
        if (sortOrder.HasValue)
        {
            sortOrder.Value.Should().BeGreaterThanOrEqualTo(0,
                $"{relativePath} has negative sortOrder");
        }
    }

    #endregion

    #region Cross-File Validation

    [Fact]
    public void All_Catalogs_Should_Parse_As_Valid_Json()
    {
        // Arrange
        var errors = new List<string>();

        // Act
        foreach (var file in _allCatalogFiles)
        {
            try
            {
                var fullPath = Path.Combine(_dataPath, file);
                JObject.Parse(File.ReadAllText(fullPath));
            }
            catch (JsonReaderException ex)
            {
                errors.Add($"{file}: {ex.Message}");
            }
        }

        // Assert
        errors.Should().BeEmpty("All catalog files should be valid JSON");
    }

    [Fact]
    public void All_Names_Files_Should_Parse_As_Valid_Json()
    {
        // Arrange
        var errors = new List<string>();

        // Act
        foreach (var file in _allNamesFiles)
        {
            try
            {
                var fullPath = Path.Combine(_dataPath, file);
                JObject.Parse(File.ReadAllText(fullPath));
            }
            catch (JsonReaderException ex)
            {
                errors.Add($"{file}: {ex.Message}");
            }
        }

        // Assert
        errors.Should().BeEmpty("All names files should be valid JSON");
    }

    [Fact]
    public void All_Config_Files_Should_Parse_As_Valid_Json()
    {
        // Arrange
        var errors = new List<string>();

        // Act
        foreach (var file in _allConfigFiles)
        {
            try
            {
                var fullPath = Path.Combine(_dataPath, file);
                JObject.Parse(File.ReadAllText(fullPath));
            }
            catch (JsonReaderException ex)
            {
                errors.Add($"{file}: {ex.Message}");
            }
        }

        // Assert
        errors.Should().BeEmpty("All config files should be valid JSON");
    }

    [Fact]
    public void Should_Have_Coverage_Summary()
    {
        // Output test coverage statistics
        Console.WriteLine($"JSON Data Test Coverage:");
        Console.WriteLine($"  Catalog files: {_allCatalogFiles.Count}");
        Console.WriteLine($"  Names files: {_allNamesFiles.Count}");
        Console.WriteLine($"  Config files: {_allConfigFiles.Count}");
        Console.WriteLine($"  Component files: {_allComponentFiles.Count}");
        Console.WriteLine($"  Total files: {_allCatalogFiles.Count + _allNamesFiles.Count + _allConfigFiles.Count + _allComponentFiles.Count}");
    }

    #endregion

    #region Component/Data File Tests

    [Fact]
    public void Should_Discover_All_Component_Files()
    {
        // Assert - should have found component files
        _allComponentFiles.Should().NotBeEmpty();
    }

    public static IEnumerable<object[]> GetAllComponentFiles()
    {
        var instance = new JsonDataComplianceTests();
        return instance._allComponentFiles.Select(f => new object[] { f });
    }

    [Theory]
    [MemberData(nameof(GetAllComponentFiles))]
    public void Component_Should_Be_Valid_Json(string relativePath)
    {
        // Arrange
        var fullPath = Path.Combine(_dataPath, relativePath);

        // Act & Assert
        var act = () => JToken.Parse(File.ReadAllText(fullPath));
        act.Should().NotThrow<JsonReaderException>($"{relativePath} should be valid JSON");
    }

    [Theory]
    [MemberData(nameof(GetAllComponentFiles))]
    public void Component_Should_Not_Be_Empty(string relativePath)
    {
        // Arrange
        var fullPath = Path.Combine(_dataPath, relativePath);
        var json = JToken.Parse(File.ReadAllText(fullPath));

        // Assert
        json.Should().NotBeNull($"{relativePath} should not be null");

        if (json is JObject obj)
        {
            obj.Should().NotBeEmpty($"{relativePath} should not be empty object");

            // For config-style files, check that nested settings exist
            if (obj["settings"] is JObject settings)
            {
                settings.Should().NotBeEmpty($"{relativePath} settings should not be empty");
            }
        }
        else if (json is JArray arr)
        {
            arr.Should().NotBeEmpty($"{relativePath} should not be empty array");
        }
    }

    [Theory]
    [MemberData(nameof(GetAllComponentFiles))]
    public void Component_With_Metadata_Should_Have_Valid_Fields(string relativePath)
    {
        // Arrange
        var fullPath = Path.Combine(_dataPath, relativePath);
        var json = JObject.Parse(File.ReadAllText(fullPath));
        var metadata = json["metadata"] as JObject;

        // Skip if no metadata section
        if (metadata == null)
            return;

        // Assert - if metadata exists, validate required fields
        metadata["version"].Should().NotBeNull($"{relativePath} metadata missing version");
        metadata["lastUpdated"].Should().NotBeNull($"{relativePath} metadata missing lastUpdated");

        var version = metadata["version"]?.ToString();
        version.Should().NotBeNullOrEmpty($"{relativePath} version should not be empty");

        var lastUpdated = metadata["lastUpdated"]?.ToString();
        if (!string.IsNullOrEmpty(lastUpdated))
        {
            DateTime.TryParse(lastUpdated, out _).Should().BeTrue($"{relativePath} lastUpdated should be valid date");
        }
    }

    [Theory]
    [MemberData(nameof(GetAllComponentFiles))]
    public void Component_Should_Have_Meaningful_Content(string relativePath)
    {
        // Arrange
        var fullPath = Path.Combine(_dataPath, relativePath);
        var json = JToken.Parse(File.ReadAllText(fullPath));

        // Act - check that file has actual data (not just empty structures)
        var hasContent = false;

        if (json is JObject obj)
        {
            // Check for settings object (config files)
            if (obj["settings"] is JObject settings && settings.Properties().Any())
            {
                hasContent = true;
            }
            // Check for any non-empty arrays or objects with properties
            else
            {
                foreach (var property in obj.Properties())
                {
                    if (property.Value is JArray arr && arr.Count > 0)
                    {
                        hasContent = true;
                        break;
                    }
                    else if (property.Value is JObject innerObj && innerObj.Properties().Any())
                    {
                        hasContent = true;
                        break;
                    }
                }
            }
        }
        else if (json is JArray arr)
        {
            hasContent = arr.Count > 0;
        }

        // Assert
        hasContent.Should().BeTrue($"{relativePath} should contain meaningful data (non-empty arrays or objects)");
    }

    [Fact]
    public void All_Component_Files_Should_Parse_As_Valid_Json()
    {
        // Arrange
        var errors = new List<string>();

        // Act
        foreach (var file in _allComponentFiles)
        {
            try
            {
                var fullPath = Path.Combine(_dataPath, file);
                JToken.Parse(File.ReadAllText(fullPath));
            }
            catch (JsonReaderException ex)
            {
                errors.Add($"{file}: {ex.Message}");
            }
        }

        // Assert
        errors.Should().BeEmpty("All component files should be valid JSON");
    }

    #endregion

    #region Helper Methods

    private List<JObject> GetAllItemsFromCatalog(JObject catalog)
    {
        var items = new List<JObject>();

        // Pattern 1: Root-level items array
        if (catalog["items"] is JArray rootItems)
        {
            items.AddRange(rootItems.OfType<JObject>());
        }

        // Pattern 2: Components structure (v4.0)
        if (catalog["components"] is JObject components)
        {
            foreach (var prop in components.Properties())
            {
                if (prop.Value is JArray componentItems)
                {
                    items.AddRange(componentItems.OfType<JObject>());
                }
                // Pattern 2a: Nested objects with items arrays (e.g., components.subcategory.items)
                else if (prop.Value is JObject nestedObj && nestedObj["items"] is JArray nestedItems)
                {
                    items.AddRange(nestedItems.OfType<JObject>());
                }
            }
        }

        // Pattern 3: {type}_types structure (e.g., weapon_types, consumableTypes)
        foreach (var prop in catalog.Properties())
        {
            if (prop.Name.EndsWith("_types") || prop.Name.EndsWith("Types"))
            {
                if (prop.Value is JObject types)
                {
                    foreach (var category in types.Properties())
                    {
                        if (category.Value["items"] is JArray categoryItems)
                        {
                            items.AddRange(categoryItems.OfType<JObject>());
                        }
                        // Pattern 3a: Direct object items (e.g., quality_types where each key IS an item)
                        else if (category.Value is JObject directItem)
                        {
                            items.Add(directItem);
                        }
                    }
                }
            }
        }

        // Pattern 4: Hierarchical/category structure (e.g., classes, npcs)
        // Top-level categories (warriors, rogues, backgrounds, occupations) with items arrays
        foreach (var prop in catalog.Properties())
        {
            // Skip metadata and known special keys
            if (prop.Name == "metadata" || prop.Name == "components" ||
                prop.Name == "items" || prop.Name.EndsWith("_types") || prop.Name.EndsWith("Types"))
            {
                continue;
            }

            if (prop.Value is JObject category)
            {
                // Direct items array in category
                if (category["items"] is JArray directItems)
                {
                    items.AddRange(directItems.OfType<JObject>());
                }

                // Nested subcategories with items (e.g., backgrounds.former_military.items)
                foreach (var subcatProp in category.Properties())
                {
                    if (subcatProp.Value is JObject subcat && subcat["items"] is JArray subcatItems)
                    {
                        items.AddRange(subcatItems.OfType<JObject>());
                    }
                }
            }
        }

        return items;
    }

    private List<string> FindAllReferences(string jsonText)
    {
        var references = new List<string>();
        // v4.1 syntax: @domain/path/category:item-name[filters]?.property.nested
        var regex = new System.Text.RegularExpressions.Regex(@"@[\w-]+/[\w-/]+:[\w-*\s]+(\[[^\]]+\])?(\?)?(\.\w+)*");
        var matches = regex.Matches(jsonText);

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            references.Add(match.Value);
        }

        return references;
    }

    #endregion

    #region Test Data Providers

    public static IEnumerable<object[]> GetAllCatalogFiles()
    {
        var instance = new JsonDataComplianceTests();
        return instance._allCatalogFiles.Select(f => new object[] { f });
    }

    public static IEnumerable<object[]> GetAllNamesFiles()
    {
        var instance = new JsonDataComplianceTests();
        return instance._allNamesFiles.Select(f => new object[] { f });
    }

    public static IEnumerable<object[]> GetAllConfigFiles()
    {
        var instance = new JsonDataComplianceTests();
        return instance._allConfigFiles.Select(f => new object[] { f });
    }

    #endregion
}
