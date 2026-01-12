using FluentAssertions;
using Newtonsoft.Json.Linq;
using Xunit.Abstractions;

namespace RealmEngine.Data.Tests;

/// <summary>
/// Generates a detailed report of all data validation failures
/// Run this test individually to see a comprehensive list of issues
/// </summary>
[Trait("Category", "DataValidation")]
[Trait("Type", "Report")]
public class DataValidationFailureReport
{
  private readonly ITestOutputHelper _output;
  private readonly string _dataPath;
  private readonly string[] _validVersions = ["4.0", "4.2", "5.0", "5.1"];

  public DataValidationFailureReport(ITestOutputHelper output)
  {
    _output = output;
    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
    var solutionRoot = Directory.GetParent(baseDir)?.Parent?.Parent?.Parent?.Parent?.FullName;
    if (solutionRoot == null)
      throw new DirectoryNotFoundException($"Could not find solution root from: {baseDir}");

    _dataPath = Path.Combine(solutionRoot, "RealmEngine.Data", "Data", "Json");
  }

  [Fact]
  public void Generate_Complete_Validation_Failure_Report()
  {
    var issues = new List<ValidationIssue>();

    // Check all catalog files
    var catalogFiles = Directory.GetFiles(_dataPath, "catalog.json", SearchOption.AllDirectories);
    foreach (var file in catalogFiles)
    {
      var relativePath = Path.GetRelativePath(_dataPath, file);
      issues.AddRange(ValidateCatalogFile(relativePath));
    }

    // Check all names files
    var namesFiles = Directory.GetFiles(_dataPath, "names.json", SearchOption.AllDirectories);
    foreach (var file in namesFiles)
    {
      var relativePath = Path.GetRelativePath(_dataPath, file);
      issues.AddRange(ValidateNamesFile(relativePath));
    }

    // Check all config files
    var configFiles = Directory.GetFiles(_dataPath, "*.cbconfig.json", SearchOption.AllDirectories);
    foreach (var file in configFiles)
    {
      var relativePath = Path.GetRelativePath(_dataPath, file);
      issues.AddRange(ValidateConfigFile(relativePath));
    }

    // Output report
    _output.WriteLine("==========================================");
    _output.WriteLine("JSON DATA VALIDATION FAILURE REPORT");
    _output.WriteLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    _output.WriteLine("==========================================");
    _output.WriteLine("");

    if (!issues.Any())
    {
      _output.WriteLine("✅ ALL VALIDATION TESTS PASSING!");
      _output.WriteLine($"   {catalogFiles.Length} catalogs");
      _output.WriteLine($"   {namesFiles.Length} names files");
      _output.WriteLine($"   {configFiles.Length} config files");
      _output.WriteLine("");
      _output.WriteLine("No issues found. All JSON data files are compliant.");
      return;
    }

    // Group by category
    var byCategory = issues.GroupBy(i => i.Category).OrderBy(g => g.Key);
    var byFile = issues.GroupBy(i => i.File).OrderBy(g => g.Key);

    _output.WriteLine($"Total Issues: {issues.Count}");
    _output.WriteLine($"Files Affected: {byFile.Count()}");
    _output.WriteLine("");

    // Summary by category
    _output.WriteLine("ISSUES BY CATEGORY:");
    _output.WriteLine("------------------------------------------");
    foreach (var category in byCategory)
    {
      _output.WriteLine($"  {category.Key,-30} {category.Count(),4} issues");
    }
    _output.WriteLine("");

    // Detailed issues by file
    _output.WriteLine("DETAILED ISSUES BY FILE:");
    _output.WriteLine("==========================================");
    foreach (var fileGroup in byFile)
    {
      _output.WriteLine("");
      _output.WriteLine($"📄 {fileGroup.Key}");
      _output.WriteLine(new string('-', 80));
      foreach (var issue in fileGroup.OrderBy(i => i.Category))
      {
        _output.WriteLine($"  ❌ [{issue.Category}] {issue.Description}");
      }
    }

    _output.WriteLine("");
    _output.WriteLine("==========================================");
    _output.WriteLine("END OF REPORT");
    _output.WriteLine("==========================================");

    // Fail the test to make it visible
    Assert.Fail($"Found {issues.Count} validation issues. See output above for details.");
  }

  private List<ValidationIssue> ValidateCatalogFile(string relativePath)
  {
    var issues = new List<ValidationIssue>();
    var fullPath = Path.Combine(_dataPath, relativePath);

    try
    {
      var json = JObject.Parse(File.ReadAllText(fullPath));

      // Check root properties
      var actualProperties = json.Properties().Select(p => p.Name).ToList();
      if (!actualProperties.Contains("metadata"))
      {
        issues.Add(new ValidationIssue(relativePath, "Schema", "Missing required 'metadata' property"));
      }

      var unexpectedRoot = actualProperties
          .Where(p => p != "metadata" && !p.EndsWith("_types"))
          .ToList();

      if (unexpectedRoot.Any())
      {
        issues.Add(new ValidationIssue(relativePath, "Schema",
            $"Unexpected root properties: {string.Join(", ", unexpectedRoot)}. Only 'metadata' and '*_types' allowed."));
      }

      // Check metadata
      var metadata = json["metadata"] as JObject;
      if (metadata != null)
      {
        var required = new[] { "description", "version", "lastUpdated", "type" };
        foreach (var req in required)
        {
          if (metadata[req] == null)
          {
            issues.Add(new ValidationIssue(relativePath, "Metadata", $"Missing required '{req}' field"));
          }
        }

        // Check if version exists
        var version = metadata["version"]?.ToString();
        if (string.IsNullOrEmpty(version))
        {
          issues.Add(new ValidationIssue(relativePath, "Version", $"Version is missing or empty"));
        }
      }
    }
    catch (Exception ex)
    {
      issues.Add(new ValidationIssue(relativePath, "Parse Error", $"Failed to parse JSON: {ex.Message}"));
    }

    return issues;
  }

  private List<ValidationIssue> ValidateNamesFile(string relativePath)
  {
    var issues = new List<ValidationIssue>();
    var fullPath = Path.Combine(_dataPath, relativePath);

    try
    {
      var json = JObject.Parse(File.ReadAllText(fullPath));
      var metadata = json["metadata"] as JObject;
      var patterns = json["patterns"] as JArray;
      var components = json["components"] as JObject;

      // Check version
      if (metadata?["version"]?.ToString() != "4.0")
      {
        issues.Add(new ValidationIssue(relativePath, "Version", "Must be version '4.0'"));
      }

      // Check patterns not empty
      if (patterns == null || !patterns.Any())
      {
        issues.Add(new ValidationIssue(relativePath, "Patterns", "Patterns array is empty or missing"));
      }

      // Check pattern tokens
      var componentKeys = metadata?["componentKeys"] as JArray;
      if (patterns != null && componentKeys != null)
      {
        var validKeys = componentKeys.Select(k => k.ToString()).ToList();
        validKeys.Add("base");

        foreach (var pattern in patterns.OfType<JObject>())
        {
          var patternStr = pattern["pattern"]?.ToString() ?? pattern["template"]?.ToString();
          if (string.IsNullOrEmpty(patternStr)) continue;

          var tokenMatches = System.Text.RegularExpressions.Regex.Matches(patternStr, @"\{([^}]+)\}");
          foreach (System.Text.RegularExpressions.Match match in tokenMatches)
          {
            var token = match.Groups[1].Value;
            if (token.StartsWith("@")) continue; // Reference token

            if (!validKeys.Contains(token))
            {
              issues.Add(new ValidationIssue(relativePath, "Pattern Token",
                  $"Pattern '{patternStr}' uses undefined token '{{{token}}}'. Valid: {string.Join(", ", validKeys.Select(k => $"{{{k}}}"))}"));
            }
          }
        }
      }

      // Check componentKeys match components
      if (componentKeys != null && componentKeys.Any())
      {
        if (components == null || !components.Properties().Any())
        {
          issues.Add(new ValidationIssue(relativePath, "Components", "componentKeys declares items but components section is empty"));
        }
        else
        {
          foreach (var key in componentKeys)
          {
            var keyStr = key.ToString();
            if (keyStr == "base") continue;

            if (!components.ContainsKey(keyStr))
            {
              issues.Add(new ValidationIssue(relativePath, "Components", $"componentKeys declares '{keyStr}' but it's not in components section"));
            }
          }
        }
      }

      // Check component items have required fields
      if (components != null)
      {
        foreach (var componentGroup in components.Properties())
        {
          if (componentGroup.Value is JArray items)
          {
            foreach (var item in items.OfType<JObject>())
            {
              if (!item.ContainsKey("value"))
              {
                issues.Add(new ValidationIssue(relativePath, "Component Item",
                    $"Component '{componentGroup.Name}' has item missing 'value' field"));
              }
              if (!item.ContainsKey("rarityWeight"))
              {
                issues.Add(new ValidationIssue(relativePath, "Component Item",
                    $"Component '{componentGroup.Name}' item missing 'rarityWeight' field"));
              }
            }
          }
        }
      }
    }
    catch (Exception ex)
    {
      issues.Add(new ValidationIssue(relativePath, "Parse Error", $"Failed to parse JSON: {ex.Message}"));
    }

    return issues;
  }

  private List<ValidationIssue> ValidateConfigFile(string relativePath)
  {
    var issues = new List<ValidationIssue>();
    var fullPath = Path.Combine(_dataPath, relativePath);

    try
    {
      var json = JObject.Parse(File.ReadAllText(fullPath));

      if (!json.ContainsKey("icon"))
      {
        issues.Add(new ValidationIssue(relativePath, "Config", "Missing required 'icon' field"));
      }
      if (!json.ContainsKey("sortOrder"))
      {
        issues.Add(new ValidationIssue(relativePath, "Config", "Missing required 'sortOrder' field"));
      }
    }
    catch (Exception ex)
    {
      issues.Add(new ValidationIssue(relativePath, "Parse Error", $"Failed to parse JSON: {ex.Message}"));
    }

    return issues;
  }

  private class ValidationIssue
  {
    public string File { get; }
    public string Category { get; }
    public string Description { get; }

    public ValidationIssue(string file, string category, string description)
    {
      File = file;
      Category = category;
      Description = description;
    }
  }
}
