using FluentAssertions;
using RealmEngine.Data.Services;
using Microsoft.Extensions.Caching.Memory;

namespace RealmEngine.Data.Tests.Services;

[Trait("Category", "Unit")]
public class GameDataCacheDomainHierarchyTests : IDisposable
{
    private readonly GameDataCache _cache;
    private readonly string _testDataPath;

    public GameDataCacheDomainHierarchyTests()
    {
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
    }

    [Fact]
    public void GetAllDomains_Should_Return_Expected_Domains()
    {
        // Act
        var domains = _cache.AllDomains;

        // Assert
        domains.Should().NotBeEmpty();
        domains.Should().Contain("abilities");
        domains.Should().Contain("npcs");
        domains.Should().Contain("items");
    }

    [Fact]
    public void AllDomains_Property_Should_Work()
    {
        // Act
        var domains = _cache.AllDomains;

        // Assert
        domains.Should().NotBeEmpty();
        domains.Should().Contain("abilities");
    }

    [Fact]
    public void GetSubdomainsForDomain_Should_Return_Subdomains_For_Abilities()
    {
        // Act
        var subdomains = _cache.GetSubdomainsForDomain("abilities");

        // Assert
        subdomains.Should().NotBeEmpty();
        subdomains.Should().Contain("active");
        subdomains.Should().Contain("passive");
    }

    [Fact]
    public void GetSubdomainsForDomain_Should_Return_Subdomains_For_Npcs()
    {
        // Act
        var subdomains = _cache.GetSubdomainsForDomain("npcs");

        // Assert
        subdomains.Should().NotBeEmpty();
        subdomains.Should().Contain("common");
        subdomains.Should().Contain("noble");
    }

    [Fact]
    public void GetSubdomainsForDomain_Should_Return_Empty_For_NonExistent_Domain()
    {
        // Act
        var subdomains = _cache.GetSubdomainsForDomain("nonexistent");

        // Assert
        subdomains.Should().BeEmpty();
    }

    [Fact]
    public void GetFilesBySubdomain_Should_Return_Files_For_Abilities_Active()
    {
        // Act
        var files = _cache.GetFilesBySubdomain("abilities", "active");

        // Assert
        files.Should().NotBeEmpty();
        files.All(f => f.Domain == "abilities").Should().BeTrue();
        files.All(f => f.Subdomain == "active").Should().BeTrue();
    }

    [Fact]
    public void GetFilesBySubdomain_Should_Return_Empty_For_NonExistent_Combination()
    {
        // Act
        var files = _cache.GetFilesBySubdomain("abilities", "nonexistent");

        // Assert
        files.Should().BeEmpty();
    }

    [Fact]
    public void GetDomainHierarchy_Should_Return_Complete_Structure()
    {
        // Act
        var hierarchy = _cache.DomainHierarchy;

        // Assert
        hierarchy.Should().NotBeEmpty();
        hierarchy.Should().ContainKey("abilities");

        if (hierarchy.TryGetValue("abilities", out var abilitiesSubdomains))
        {
            abilitiesSubdomains.Should().ContainKey("active");
            abilitiesSubdomains.Should().ContainKey("passive");
        }
    }

    [Fact]
    public void DomainHierarchy_Property_Should_Work()
    {
        // Act
        var hierarchy = _cache.DomainHierarchy;

        // Assert
        hierarchy.Should().NotBeEmpty();
        hierarchy.Should().ContainKey("abilities");
    }

    [Fact]
    public void CachedJsonFile_Should_Have_Subdomain_Property()
    {
        // Act
        var file = _cache.GetFile("abilities/active/catalog.json");

        // Assert
        file.Should().NotBeNull();
        file!.Domain.Should().Be("abilities");
        file.Subdomain.Should().Be("active");
    }

    [Fact]
    public void GetFilesBySubdomain_Should_Not_Include_CbconfigFiles()
    {
        // Act - Get all files from all domains/subdomains
        var allDomains = _cache.AllDomains;
        var hasAnyFiles = false;
        var hasCbconfigFiles = false;

        foreach (var domain in allDomains)
        {
            var subdomains = _cache.GetSubdomainsForDomain(domain);
            foreach (var subdomain in subdomains)
            {
                var files = _cache.GetFilesBySubdomain(domain, subdomain);
                if (files.Any())
                {
                    hasAnyFiles = true;
                    
                    // Check if any file is a .cbconfig.json file
                    var cbconfigFiles = files.Where(f => f.RelativePath.EndsWith(".cbconfig.json")).ToList();
                    if (cbconfigFiles.Any())
                    {
                        hasCbconfigFiles = true;
                        break;
                    }
                }
            }
            if (hasCbconfigFiles)
                break;
        }

        // Assert
        hasAnyFiles.Should().BeTrue("we should find game data files");
        hasCbconfigFiles.Should().BeFalse(".cbconfig.json files should be excluded from hierarchy by default");
    }

    [Fact]
    public void GetFilesByDomain_Should_Exclude_ConfigFiles_By_Default()
    {
        // Act
        var files = _cache.GetFilesByDomain("abilities");

        // Assert
        files.Should().NotBeEmpty();
        files.Where(f => f.FileType == JsonFileType.ConfigFile).Should().BeEmpty(".cbconfig.json should be excluded by default");
    }

    [Fact]
    public void GetFilesByDomain_Should_Include_ConfigFiles_When_Requested()
    {
        // Act
        var filesWithoutConfig = _cache.GetFilesByDomain("abilities", excludeConfigFiles: true);
        var filesWithConfig = _cache.GetFilesByDomain("abilities", excludeConfigFiles: false);

        // Assert
        filesWithConfig.Count().Should().BeGreaterThanOrEqualTo(filesWithoutConfig.Count());
        
        // There should be at least one .cbconfig.json in the abilities domain
        filesWithConfig.Where(f => f.FileType == JsonFileType.ConfigFile).Should().NotBeEmpty();
    }

    [Fact]
    public void GetFilesBySubdomain_Should_Exclude_ConfigFiles_By_Default()
    {
        // Act
        var files = _cache.GetFilesBySubdomain("abilities", "active");

        // Assert
        files.Should().NotBeEmpty();
        files.Where(f => f.FileType == JsonFileType.ConfigFile).Should().BeEmpty(".cbconfig.json should be excluded by default");
    }

    [Fact]
    public void GetCatalogsByDomain_Should_Return_Only_Catalogs()
    {
        // Act
        var catalogs = _cache.GetCatalogsByDomain("abilities");

        // Assert
        catalogs.Should().NotBeEmpty();
        catalogs.All(f => f.FileType == JsonFileType.GenericCatalog).Should().BeTrue();
        catalogs.All(f => f.Domain == "abilities").Should().BeTrue();
    }

    [Fact]
    public void GetCatalogsBySubdomain_Should_Return_Only_Catalogs_In_Subdomain()
    {
        // Act
        var catalogs = _cache.GetCatalogsBySubdomain("abilities", "active");

        // Assert
        catalogs.Should().NotBeEmpty();
        catalogs.All(f => f.FileType == JsonFileType.GenericCatalog).Should().BeTrue();
        catalogs.All(f => f.Domain == "abilities").Should().BeTrue();
        catalogs.All(f => f.Subdomain == "active").Should().BeTrue();
    }

    [Fact]
    public void GetNamesByDomain_Should_Return_Only_Names()
    {
        // Act - use enemies domain which has names.json files
        var names = _cache.GetNamesByDomain("enemies");

        // Assert
        names.Should().NotBeEmpty();
        names.All(f => f.FileType == JsonFileType.NamesFile).Should().BeTrue();
        names.All(f => f.Domain == "enemies").Should().BeTrue();
    }

    [Fact]
    public void GetNamesBySubdomain_Should_Return_Only_Names_In_Subdomain()
    {
        // Act
        var names = _cache.GetNamesBySubdomain("abilities", "active");

        // Assert
        // Note: might be empty if no names.json at this level
        names.All(f => f.FileType == JsonFileType.NamesFile).Should().BeTrue();
        names.All(f => f.Domain == "abilities").Should().BeTrue();
        names.All(f => f.Subdomain == "active").Should().BeTrue();
    }

    public void Dispose()
    {
        _cache?.Dispose();
    }
}