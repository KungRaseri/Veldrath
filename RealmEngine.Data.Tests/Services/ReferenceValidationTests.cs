using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using RealmEngine.Data.Services;
using System.Text.RegularExpressions;
using Xunit.Abstractions;

namespace RealmEngine.Data.Tests.Services;

[Trait("Category", "Integration")]
[Trait("Category", "ReferenceValidation")]
public class ReferenceValidationTests : IDisposable
{
    private readonly GameDataCache _cache;
    private readonly ReferenceResolverService _resolver;
    private readonly string _testDataPath;
    private readonly ITestOutputHelper _output;

    public ReferenceValidationTests(ITestOutputHelper output)
    {
        _output = output;
        
        // Get the solution root and navigate to the data directory
        var currentDir = Directory.GetCurrentDirectory();
        var solutionRoot = currentDir;
        while (solutionRoot != null &&
               !File.Exists(Path.Combine(solutionRoot, "RealmEngine.sln")) &&
               !File.Exists(Path.Combine(solutionRoot, "RealmEngine.slnx")))
        {
            solutionRoot = Directory.GetParent(solutionRoot)?.FullName;
        }

        if (solutionRoot == null)
            throw new InvalidOperationException("Could not find solution root");

        _testDataPath = Path.Combine(solutionRoot, "RealmEngine.Data", "Data", "Json");

        if (!Directory.Exists(_testDataPath))
            throw new DirectoryNotFoundException($"Data directory not found: {_testDataPath}");

        _cache = new GameDataCache(_testDataPath, new MemoryCache(new MemoryCacheOptions()));
        _cache.LoadAllData();

        // Create a simple logger that doesn't output to console
        using var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.None));
        _resolver = new ReferenceResolverService(_cache, loggerFactory.CreateLogger<ReferenceResolverService>());
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    public static TheoryData<string, string, string, int> GetAllReferences()
    {
        var theoryData = new TheoryData<string, string, string, int>();
        
        // Get the solution root
        var currentDir = Directory.GetCurrentDirectory();
        var solutionRoot = currentDir;
        while (solutionRoot != null &&
               !File.Exists(Path.Combine(solutionRoot, "RealmEngine.sln")) &&
               !File.Exists(Path.Combine(solutionRoot, "RealmEngine.slnx")))
        {
            solutionRoot = Directory.GetParent(solutionRoot)?.FullName;
        }

        if (solutionRoot == null)
            return theoryData;

        var testDataPath = Path.Combine(solutionRoot, "RealmEngine.Data", "Data", "Json");
        if (!Directory.Exists(testDataPath))
            return theoryData;

        // Regex to match @domain/path:item references (item names can contain spaces, but not before delimiters)
        // Pattern stops at quotes, curly braces, commas, or line breaks to avoid capturing trailing text
        var referencePattern = new Regex(@"@[\w-]+(?:/[\w-]+)*:(?:[\w-]+(?:\s+[\w-]+)*|\*)(?:\[.*?\])?(?:\?)?(?:\.[\w.]+)?(?=[\s,\""{}]|$)", RegexOptions.Compiled);

        // Scan all JSON files
        var jsonFiles = Directory.GetFiles(testDataPath, "*.json", SearchOption.AllDirectories);

        foreach (var filePath in jsonFiles)
        {
            try
            {
                var content = File.ReadAllText(filePath);
                var relativePath = Path.GetRelativePath(testDataPath, filePath);
                
                // Find all references in the file
                var matches = referencePattern.Matches(content);
                
                // Track line numbers
                var lines = content.Split('\n');
                foreach (Match match in matches)
                {
                    var reference = match.Value;
                    
                    // Find the line number
                    int lineNumber = 1;
                    int currentPos = 0;
                    for (int i = 0; i < lines.Length; i++)
                    {
                        currentPos += lines[i].Length + 1; // +1 for newline
                        if (currentPos > match.Index)
                        {
                            lineNumber = i + 1;
                            break;
                        }
                    }
                    
                    theoryData.Add(reference, relativePath, filePath, lineNumber);
                }
            }
            catch (Exception ex)
            {
                // Skip files that can't be read
                Console.WriteLine($"Error reading {filePath}: {ex.Message}");
            }
        }

        return theoryData;
    }

    [Theory]
    [MemberData(nameof(GetAllReferences))]
    public async Task All_References_Should_Resolve_Successfully(string reference, string relativeFilePath, string fullFilePath, int lineNumber)
    {
        // Act
        var result = await _resolver.ResolveToObjectAsync(reference);

        // Assert
        if (result == null)
        {
            _output.WriteLine("════════════════════════════════════════════════════════════════");
            _output.WriteLine($"FAILED REFERENCE RESOLUTION");
            _output.WriteLine("════════════════════════════════════════════════════════════════");
            _output.WriteLine($"Reference: {reference}");
            _output.WriteLine($"File:      {relativeFilePath}");
            _output.WriteLine($"Line:      {lineNumber}");
            _output.WriteLine($"Full Path: {fullFilePath}");
            _output.WriteLine("════════════════════════════════════════════════════════════════");
            _output.WriteLine("");
        }

        result.Should().NotBeNull(
            $"Reference '{reference}' should resolve successfully.\n" +
            $"  File: {relativeFilePath}\n" +
            $"  Line: {lineNumber}\n" +
            $"  Expected: A valid JSON object\n" +
            $"  Actual:   null"
        );
    }

    [Fact]
    public void Should_Find_All_References_In_Data_Files()
    {
        // Arrange
        var allReferences = GetAllReferences();
        
        // Act
        var count = allReferences.Count();
        
        // Assert
        _output.WriteLine("════════════════════════════════════════════════════════════════");
        _output.WriteLine($"TOTAL REFERENCES FOUND: {count}");
        _output.WriteLine("════════════════════════════════════════════════════════════════");
        
        count.Should().BeGreaterThan(0, "Should find at least some references in the data files");
    }

    [Fact]
    public async Task Generate_Full_Reference_Report()
    {
        // Arrange
        var allReferences = GetAllReferences();
        var totalCount = 0;
        var successCount = 0;
        var failureCount = 0;
        var failures = new List<(string reference, string file, int line)>();

        _output.WriteLine("════════════════════════════════════════════════════════════════");
        _output.WriteLine("COMPLETE REFERENCE VALIDATION REPORT");
        _output.WriteLine("════════════════════════════════════════════════════════════════");
        _output.WriteLine("");

        // Act - Test all references
        foreach (var item in allReferences)
        {
            totalCount++;
            var reference = item[0] as string;
            var relativeFile = item[1] as string;
            var lineNumber = (int)item[3];

            var result = await _resolver.ResolveToObjectAsync(reference!);
            
            if (result == null)
            {
                failureCount++;
                failures.Add((reference!, relativeFile!, lineNumber));
            }
            else
            {
                successCount++;
            }
        }

        // Report results
        _output.WriteLine($"Total References:     {totalCount}");
        _output.WriteLine($"Successful:           {successCount} ({(successCount * 100.0 / totalCount):F1}%)");
        _output.WriteLine($"Failed:               {failureCount} ({(failureCount * 100.0 / totalCount):F1}%)");
        _output.WriteLine("");

        if (failures.Any())
        {
            _output.WriteLine("════════════════════════════════════════════════════════════════");
            _output.WriteLine($"FAILED REFERENCES ({failureCount} total)");
            _output.WriteLine("════════════════════════════════════════════════════════════════");
            _output.WriteLine("");

            // Group by reference for easier reading
            var groupedFailures = failures.GroupBy(f => f.reference)
                                          .OrderBy(g => g.Key);

            foreach (var group in groupedFailures)
            {
                _output.WriteLine($"Reference: {group.Key}");
                _output.WriteLine($"  Occurrences: {group.Count()}");
                
                foreach (var failure in group.Take(5)) // Show first 5 occurrences
                {
                    _output.WriteLine($"    • {failure.file}:{failure.line}");
                }
                
                if (group.Count() > 5)
                {
                    _output.WriteLine($"    ... and {group.Count() - 5} more occurrences");
                }
                
                _output.WriteLine("");
            }
        }

        // Assert - This test is informational, but we want it to fail if there are issues
        failureCount.Should().Be(0, 
            $"All {totalCount} references should resolve successfully. " +
            $"Found {failureCount} failures. See test output for details.");
    }

    [Theory]
    [InlineData("abilities")]
    [InlineData("classes")]
    [InlineData("enemies")]
    [InlineData("items")]
    [InlineData("npcs")]
    [InlineData("quests")]
    [InlineData("world")]
    [InlineData("organizations")]
    [InlineData("social")]
    public async Task All_References_In_Domain_Should_Resolve(string domain)
    {
        // Arrange
        var allReferences = GetAllReferences();
        var domainReferences = allReferences.Where(item => 
        {
            var reference = item[0] as string;
            return reference?.StartsWith($"@{domain}/") ?? false;
        }).ToList();

        var failures = new List<string>();

        _output.WriteLine($"════════════════════════════════════════════════════════════════");
        _output.WriteLine($"Testing {domain.ToUpper()} domain references");
        _output.WriteLine($"════════════════════════════════════════════════════════════════");
        _output.WriteLine($"Total references: {domainReferences.Count}");
        _output.WriteLine("");

        // Act
        foreach (var item in domainReferences)
        {
            var reference = item[0] as string;
            var result = await _resolver.ResolveToObjectAsync(reference!);
            
            if (result == null)
            {
                failures.Add(reference!);
            }
        }

        // Assert
        if (failures.Any())
        {
            _output.WriteLine($"FAILED: {failures.Count} references");
            foreach (var failure in failures)
            {
                _output.WriteLine($"  ✗ {failure}");
            }
        }
        else
        {
            _output.WriteLine($"SUCCESS: All {domainReferences.Count} references resolved");
        }

        failures.Should().BeEmpty(
            $"All references in {domain} domain should resolve successfully");
    }
}
