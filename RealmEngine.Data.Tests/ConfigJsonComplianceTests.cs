using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RealmEngine.Data.Tests;

[Trait("Category", "Compliance")]
/// <summary>
/// Validation tests for .cbconfig.json files (ContentBuilder UI configuration)
/// Ensures all config files follow CBCONFIG_STANDARD.md specifications
/// </summary>
[Trait("Category", "DataValidation")]
[Trait("FileType", "Config")]
public class ConfigJsonComplianceTests
{
  private readonly string _dataPath;
  private readonly List<string> _allConfigFiles;

  public ConfigJsonComplianceTests()
  {
    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
    var solutionRoot = Directory.GetParent(baseDir)?.Parent?.Parent?.Parent?.Parent?.FullName;
    if (solutionRoot == null)
      throw new DirectoryNotFoundException($"Could not find solution root from: {baseDir}");

    _dataPath = Path.Combine(solutionRoot, "RealmEngine.Data", "Data", "Json");

    if (!Directory.Exists(_dataPath))
      throw new DirectoryNotFoundException($"Data directory not found: {_dataPath}");

    _allConfigFiles = Directory.GetFiles(_dataPath, ".cbconfig.json", SearchOption.AllDirectories)
        .Select(f => Path.GetRelativePath(_dataPath, f))
        .ToList();
  }

  #region Discovery Tests

  [Fact]
  public void Should_Discover_All_Config_Files()
  {
    _allConfigFiles.Should().NotBeEmpty();
    _allConfigFiles.Should().HaveCountGreaterThan(60, "expected 65+ config files");
  }

  #endregion

  #region Strict Schema Validation

  [Theory]
  [MemberData(nameof(GetAllConfigFiles))]
  public void Config_Should_Only_Have_Expected_Properties(string relativePath)
  {
    // Arrange
    var fullPath = Path.Combine(_dataPath, relativePath);
    var json = JObject.Parse(File.ReadAllText(fullPath));
    
    var allowedProperties = new[] { 
      "icon", "sortOrder", "displayName", "description", "color", "isHidden",
      // RealmForge-specific properties for advanced catalogs
      "catalogType", "allowAdd", "allowEdit", "allowDelete", "structure"
    };

    // Assert - ONLY these properties are allowed
    var actualProperties = json.Properties().Select(p => p.Name).ToList();
    var unexpectedProperties = actualProperties.Except(allowedProperties).ToList();
    
    unexpectedProperties.Should().BeEmpty(
        $"{relativePath} contains unexpected properties: {string.Join(", ", unexpectedProperties)}. " +
        $"Only allowed: {string.Join(", ", allowedProperties)}");
  }

  #endregion

  #region Required Fields Validation

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

  #endregion

  #region Icon Validation

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

  #endregion

  #region SortOrder Validation

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

  #region JSON Validation

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

  #endregion

  #region Test Data Providers

  public static IEnumerable<object[]> GetAllConfigFiles()
  {
    var instance = new ConfigJsonComplianceTests();
    return instance._allConfigFiles.Select(f => new object[] { f });
  }

  #endregion
}
