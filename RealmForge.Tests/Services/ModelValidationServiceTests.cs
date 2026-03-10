using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using RealmForge.Services;
using RealmForge.Validators;

namespace RealmForge.Tests.Services;

public class ModelValidationServiceTests
{
    private static ModelValidationService CreateSut() =>
        new(NullLogger<ModelValidationService>.Instance);

    private static JObject ValidCatalog() => JObject.Parse("""
        {
          "description": "Test catalog",
          "version": "4.0",
          "lastUpdated": "2026-01-01",
          "type": "test_catalog",
          "items": [
            { "name": "Iron Sword", "rarityWeight": 80 }
          ]
        }
        """);

    private static JObject ValidNamesFile() => JObject.Parse("""
        {
          "version": "4.0",
          "type": "pattern_generation",
          "supportsTraits": true,
          "lastUpdated": "2026-01-01",
          "description": "Test names",
          "patterns": [
            { "rarityWeight": 50, "pattern": "{prefix} {base}" }
          ],
          "components": { "prefix": ["iron"], "base": ["sword"] }
        }
        """);

    [Fact]
    public async Task ValidateAsync_Returns_Valid_For_Valid_Catalog()
    {
        var sut = CreateSut();
        var result = await sut.ValidateAsync(ValidCatalog(), "test_catalog.json");
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_Returns_Invalid_When_Version_Missing()
    {
        var sut = CreateSut();
        var jobj = ValidCatalog();
        jobj.Remove("version");
        var result = await sut.ValidateAsync(jobj, "test_catalog.json");
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAsync_Returns_Valid_For_Valid_NamesFile()
    {
        var sut = CreateSut();
        var result = await sut.ValidateAsync(ValidNamesFile(), "test_names.json");
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateJsonSyntax_Returns_Valid_For_Good_Json()
    {
        var sut = CreateSut();
        var result = sut.ValidateJsonSyntax("""{"key":"value"}""");
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateJsonSyntax_Returns_Invalid_For_Bad_Json()
    {
        var sut = CreateSut();
        var result = sut.ValidateJsonSyntax("{ not valid json }");
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAsync_Returns_Empty_Result_For_Unknown_Type()
    {
        var sut = CreateSut();
        var jobj = new JObject { ["someKey"] = "someValue" };
        var result = await sut.ValidateAsync(jobj, "unknown.json");
        result.IsValid.Should().BeTrue(); // unknown types pass through
    }
}
