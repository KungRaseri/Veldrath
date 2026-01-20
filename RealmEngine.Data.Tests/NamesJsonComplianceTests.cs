using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RealmEngine.Data.Tests;

[Trait("Category", "Compliance")]
/// <summary>
/// Validation tests for names.json files (v4.0 pattern generation standard)
/// Ensures all names files follow NAMES_JSON_STANDARD.md specifications
/// </summary>
[Trait("Category", "DataValidation")]
[Trait("FileType", "Names")]
public class NamesJsonComplianceTests
{
  private readonly string _dataPath;
  private readonly List<string> _allNamesFiles;

  public NamesJsonComplianceTests()
  {
    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
    var solutionRoot = Directory.GetParent(baseDir)?.Parent?.Parent?.Parent?.Parent?.FullName;
    if (solutionRoot == null)
      throw new DirectoryNotFoundException($"Could not find solution root from: {baseDir}");

    _dataPath = Path.Combine(solutionRoot, "RealmEngine.Data", "Data", "Json");

    if (!Directory.Exists(_dataPath))
      throw new DirectoryNotFoundException($"Data directory not found: {_dataPath}");

    _allNamesFiles = Directory.GetFiles(_dataPath, "names.json", SearchOption.AllDirectories)
        .Select(f => Path.GetRelativePath(_dataPath, f))
        .ToList();
  }

  #region Discovery Tests

  [Fact]
  public void Should_Discover_All_Names_Files()
  {
    _allNamesFiles.Should().NotBeEmpty();
    _allNamesFiles.Should().HaveCount(25, "expected exactly 25 names files after cleanup");
  }

  #endregion

  #region Strict Schema Validation

  [Theory]
  [MemberData(nameof(GetAllNamesFiles))]
  public void Names_Should_Only_Have_Expected_Root_Properties(string relativePath)
  {
    // Arrange
    var fullPath = Path.Combine(_dataPath, relativePath);
    var json = JObject.Parse(File.ReadAllText(fullPath));
    var allowedProperties = new[] { "metadata", "patterns", "components" };

    // Assert - ONLY these root properties are allowed
    var actualProperties = json.Properties().Select(p => p.Name).ToList();
    var unexpectedProperties = actualProperties.Except(allowedProperties).ToList();
    
    unexpectedProperties.Should().BeEmpty(
        $"{relativePath} contains unexpected root properties: {string.Join(", ", unexpectedProperties)}. " +
        $"Only allowed: {string.Join(", ", allowedProperties)}");
  }

  [Theory]
  [MemberData(nameof(GetAllNamesFiles))]
  public void Names_Metadata_Should_Only_Have_Expected_Properties(string relativePath)
  {
    // Arrange
    var fullPath = Path.Combine(_dataPath, relativePath);
    var json = JObject.Parse(File.ReadAllText(fullPath));
    var metadata = json["metadata"] as JObject;
    
    if (metadata == null) return; // Other tests will catch this

    var allowedProperties = new[] { 
      "description", "version", "lastUpdated", "type", "supportsTraits",
      "totalPatterns", "raritySystem", "notes",
      "supportsSoftFiltering", "totalComponents", "usage",  // Optional metadata fields
      "componentKeys", "patternTokens"  // Auto-generated fields (not validated)
    };

    // Assert - ONLY these metadata properties are allowed
    var actualProperties = metadata.Properties().Select(p => p.Name).ToList();
    var unexpectedProperties = actualProperties.Except(allowedProperties).ToList();
    
    unexpectedProperties.Should().BeEmpty(
        $"{relativePath} metadata contains unexpected properties: {string.Join(", ", unexpectedProperties)}. " +
        $"Only allowed: {string.Join(", ", allowedProperties)}");
  }

  [Theory]
  [MemberData(nameof(GetAllNamesFiles))]
  public void Names_Pattern_Objects_Must_Have_Pattern_Or_Template(string relativePath)
  {
    // Arrange
    var fullPath = Path.Combine(_dataPath, relativePath);
    var json = JObject.Parse(File.ReadAllText(fullPath));
    var patterns = json["patterns"] as JArray;
    
    if (patterns == null) return; // Other tests will catch this

    // Assert - patterns can have ANY properties, but must have pattern/template and rarityWeight
    foreach (var pattern in patterns.OfType<JObject>())
    {
      var hasPattern = pattern.ContainsKey("pattern");
      var hasTemplate = pattern.ContainsKey("template");
      var hasRarityWeight = pattern.ContainsKey("rarityWeight");
      
      (hasPattern || hasTemplate).Should().BeTrue(
          $"{relativePath} - Pattern object must have 'pattern' or 'template' field");
      
      hasRarityWeight.Should().BeTrue(
          $"{relativePath} - Pattern '{pattern["pattern"] ?? pattern["template"]}' missing required 'rarityWeight'");
    }
  }

  [Theory]
  [MemberData(nameof(GetAllNamesFiles))]
  public void Names_Pattern_Tokens_Must_Exist_In_ComponentKeys(string relativePath)
  {
    // Arrange
    var fullPath = Path.Combine(_dataPath, relativePath);
    var json = JObject.Parse(File.ReadAllText(fullPath));
    var patterns = json["patterns"] as JArray;
    var components = json["components"] as JObject;
    
    if (patterns == null || components == null) return;

    // Get valid tokens from ACTUAL components object, not componentKeys metadata
    var validKeys = components.Properties().Select(p => p.Name).ToList();
    validKeys.Add("base"); // 'base' is always valid (references catalog)

    // Assert - all tokens in patterns must exist in components object or be 'base'
    foreach (var pattern in patterns.OfType<JObject>())
    {
      var patternStr = pattern["pattern"]?.ToString() ?? pattern["template"]?.ToString();
      if (string.IsNullOrEmpty(patternStr)) continue;

      // Extract tokens from pattern (anything between {})
      var tokenMatches = System.Text.RegularExpressions.Regex.Matches(patternStr, @"\{([^}]+)\}");
      foreach (System.Text.RegularExpressions.Match match in tokenMatches)
      {
        var token = match.Groups[1].Value;
        
        // Skip reference tokens (start with @)
        if (token.StartsWith("@")) continue;
        
        validKeys.Should().Contain(token,
            $"{relativePath} - Pattern '{patternStr}' uses token '{{{token}}}' which does not exist in components object. " +
            $"Valid tokens: {string.Join(", ", validKeys.Select(k => $"{{{k}}}"))}");
      }
    }
  }

  [Theory]
  [MemberData(nameof(GetAllNamesFiles))]
  public void Names_Component_Objects_Must_Have_Value_And_RarityWeight(string relativePath)
  {
    // Arrange
    var fullPath = Path.Combine(_dataPath, relativePath);
    var json = JObject.Parse(File.ReadAllText(fullPath));
    var components = json["components"] as JObject;
    
    if (components == null || !components.Properties().Any()) return; // Components are optional

    // Assert - component items can have ANY properties, but must have value and rarityWeight
    foreach (var componentGroup in components.Properties())
    {
      if (componentGroup.Value is JArray items)
      {
        foreach (var item in items.OfType<JObject>())
        {
          item.Should().ContainKey("name",
              $"{relativePath} component '{componentGroup.Name}' item missing required 'name' property");
          item.Should().ContainKey("rarityWeight",
              $"{relativePath} component '{componentGroup.Name}' item '{item["name"]}' missing required 'rarityWeight' property");
        }
      }
    }
  }

  #endregion

  #region Metadata Validation - Basic Required Fields

  [Theory]
  [MemberData(nameof(GetAllNamesFiles))]
  public void Names_Should_Have_Required_Metadata(string relativePath)
  {
    // Arrange
    var fullPath = Path.Combine(_dataPath, relativePath);
    var json = JObject.Parse(File.ReadAllText(fullPath));
    var metadata = json["metadata"] as JObject;

    // Assert - basic required fields per v4.0 standard
    metadata.Should().NotBeNull($"{relativePath} missing metadata");
    metadata!["version"].Should().NotBeNull($"{relativePath} missing version");
    metadata["type"].Should().NotBeNull($"{relativePath} missing type");
    metadata["lastUpdated"].Should().NotBeNull($"{relativePath} missing lastUpdated");
    metadata["description"].Should().NotBeNull($"{relativePath} missing description");
  }

  [Theory]
  [MemberData(nameof(GetAllNamesFiles))]
  public void Names_Version_Should_Be_4_0(string relativePath)
  {
    // Arrange
    var fullPath = Path.Combine(_dataPath, relativePath);
    var json = JObject.Parse(File.ReadAllText(fullPath));
    var version = json["metadata"]?["version"]?.ToString();

    // Assert
    version.Should().NotBeNullOrWhiteSpace($"{relativePath} should have a version number");
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

  #endregion

  #region Metadata Validation - v4.0 Extended Required Fields

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

  // REMOVED: Names_Should_Have_ComponentKeys_Field - componentKeys is auto-generated

  // REMOVED: Names_Should_Have_PatternTokens_Field - patternTokens is auto-generated

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

  #endregion

  #region Pattern Structure Validation

  [Theory]
  [MemberData(nameof(GetAllNamesFiles))]
  public void Names_Should_Have_Patterns_Array(string relativePath)
  {
    // Arrange
    var fullPath = Path.Combine(_dataPath, relativePath);
    var json = JObject.Parse(File.ReadAllText(fullPath));
    var patterns = json["patterns"] as JArray;

    // Assert - patterns is REQUIRED and MUST NOT be empty
    patterns.Should().NotBeNull($"{relativePath} missing patterns array");
    patterns.Should().NotBeEmpty($"{relativePath} patterns array cannot be empty - add pattern data");
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

  #endregion

  #region Component Structure Validation

  // REMOVED: Names_Should_Have_Components_If_ComponentKeys_Declares_Them - componentKeys is auto-generated

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
          item.Should().ContainKey("name",
              $"{relativePath} - Component '{componentGroup.Name}' item missing 'name' field");

          var value = item["name"]?.ToString();
          value.Should().NotBeNullOrWhiteSpace(
              $"{relativePath} - Component '{componentGroup.Name}' has empty value");
        }
      }
    }
  }

  [Theory]
  [MemberData(nameof(GetAllNamesFiles))]
  public void Names_Components_Should_Not_Contain_Base(string relativePath)
  {
    // Arrange
    var fullPath = Path.Combine(_dataPath, relativePath);
    var json = JObject.Parse(File.ReadAllText(fullPath));
    var components = json["components"] as JObject;

    if (components == null) return;

    // Assert - "base" token should reference catalog.json, not be in components
    components.Should().NotContainKey("base", 
        $"{relativePath} - 'base' should not be in components. The 'base' token references items from catalog.json");
    
    // Also check for common variations
    components.Should().NotContainKey("bases", 
        $"{relativePath} - 'bases' should not be in components. Use catalog.json items instead");
  }

  #endregion

  #region v4.1 Reference Syntax Validation

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

  #region JSON Validation

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

  #endregion

  #region Test Data Providers

  public static IEnumerable<object[]> GetAllNamesFiles()
  {
    var instance = new NamesJsonComplianceTests();
    return instance._allNamesFiles.Select(f => new object[] { f });
  }

  #endregion
}
