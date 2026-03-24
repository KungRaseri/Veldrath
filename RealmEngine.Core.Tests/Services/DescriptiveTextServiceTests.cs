using FluentAssertions;
using RealmEngine.Core.Services;
using RealmEngine.Data.Services;

namespace RealmEngine.Core.Tests.Services;

[Trait("Category", "Service")]
public class DescriptiveTextServiceTests
{
    // Stub: returns null → DescriptiveTextService uses empty DescriptiveTextData
    private sealed class NullTextConfig : GameConfigService
    {
        public override string? GetData(string key) => null;
    }

    // Stub: returns a fixed JSON string
    private sealed class JsonTextConfig(string json) : GameConfigService
    {
        public override string? GetData(string key) => json;
    }

    private static DescriptiveTextService CreateNull() =>
        new(new NullTextConfig());

    private const string FullJson = """
        {
          "components": {
            "positive":   ["brave", "noble"],
            "negative":   ["cowardly"],
            "condition":  ["worn", "polished"],
            "size":       ["huge"],
            "appearance": ["gleaming"]
          },
          "colors": {
            "base_color": ["red", "blue"],
            "modifier":   ["dark", "bright"]
          },
          "smells":   ["pine", "smoke"],
          "sounds":   ["thunder", "whispers"],
          "textures": ["rough", "silky"],
          "verbs":    ["slashes", "pierces"],
          "weather_conditions": {
            "clear": ["sunny skies", "gentle breeze"],
            "rain":  ["heavy rain"]
          },
          "time_periods": {
            "dawn":  ["first light", "morning glow"],
            "night": ["deep darkness"]
          }
        }
        """;

    // Fallback paths (null config)
    [Fact]
    public void GetAdjective_ReturnsFallback_WhenConfigIsNull()
        => CreateNull().GetAdjective("positive").Should().Be("strange");

    [Fact]
    public void GetAdjective_ReturnsFallback_WhenCategoryMissing()
        => new DescriptiveTextService(new JsonTextConfig(FullJson))
            .GetAdjective("nonexistent").Should().Be("strange");

    [Fact]
    public void GetColor_ReturnsFallback_WhenConfigIsNull()
        => CreateNull().GetColor().Should().Be("grey");

    [Fact]
    public void GetSmell_ReturnsFallback_WhenConfigIsNull()
        => CreateNull().GetSmell().Should().Be("musty air");

    [Fact]
    public void GetSound_ReturnsFallback_WhenConfigIsNull()
        => CreateNull().GetSound().Should().Be("distant echoes");

    [Fact]
    public void GetTexture_ReturnsFallback_WhenConfigIsNull()
        => CreateNull().GetTexture().Should().Be("smooth");

    [Fact]
    public void GetVerb_ReturnsFallback_WhenConfigIsNull()
        => CreateNull().GetVerb().Should().Be("strikes");

    [Fact]
    public void GetWeather_ReturnsFallback_WhenConfigIsNull()
        => CreateNull().GetWeather("clear").Should().Be("pleasant weather");

    [Fact]
    public void GetTimeOfDay_ReturnsFallback_WhenConfigIsNull()
        => CreateNull().GetTimeOfDay("dawn").Should().Be("midday sun");

    // Valid config paths
    [Theory]
    [InlineData("positive")]
    [InlineData("negative")]
    public void GetAdjective_ReturnsWordFromList_WhenCategoryExists(string category)
    {
        var svc = new DescriptiveTextService(new JsonTextConfig(FullJson));
        svc.GetAdjective(category).Should().NotBe("strange");
    }

    [Fact]
    public void GetColor_ReturnsBaseColor_WhenModifierNotRequested()
    {
        var svc = new DescriptiveTextService(new JsonTextConfig(FullJson));
        var color = svc.GetColor(includeModifier: false);
        new[] { "red", "blue" }.Should().Contain(color);
    }

    [Fact]
    public void GetColor_ReturnsModifiedColor_WhenModifierRequested()
    {
        var svc = new DescriptiveTextService(new JsonTextConfig(FullJson));
        var color = svc.GetColor(includeModifier: true);
        // e.g. "dark red", "bright blue"
        color.Should().Match(c => c.Contains("dark") || c.Contains("bright"));
    }

    [Fact]
    public void GetSmell_ReturnsValueFromList_WhenConfigLoaded()
    {
        var svc = new DescriptiveTextService(new JsonTextConfig(FullJson));
        new[] { "pine", "smoke" }.Should().Contain(svc.GetSmell());
    }

    [Fact]
    public void GetSound_ReturnsValueFromList_WhenConfigLoaded()
    {
        var svc = new DescriptiveTextService(new JsonTextConfig(FullJson));
        new[] { "thunder", "whispers" }.Should().Contain(svc.GetSound());
    }

    [Fact]
    public void GetTexture_ReturnsValueFromList_WhenConfigLoaded()
    {
        var svc = new DescriptiveTextService(new JsonTextConfig(FullJson));
        new[] { "rough", "silky" }.Should().Contain(svc.GetTexture());
    }

    [Fact]
    public void GetVerb_ReturnsValueFromList_WhenConfigLoaded()
    {
        var svc = new DescriptiveTextService(new JsonTextConfig(FullJson));
        new[] { "slashes", "pierces" }.Should().Contain(svc.GetVerb());
    }

    [Fact]
    public void GetWeather_ReturnsValueFromList_ForKnownCategory()
    {
        var svc = new DescriptiveTextService(new JsonTextConfig(FullJson));
        new[] { "sunny skies", "gentle breeze" }.Should().Contain(svc.GetWeather("clear"));
    }

    [Fact]
    public void GetTimeOfDay_ReturnsValueFromList_ForKnownPeriod()
    {
        var svc = new DescriptiveTextService(new JsonTextConfig(FullJson));
        new[] { "first light", "morning glow" }.Should().Contain(svc.GetTimeOfDay("dawn"));
    }

    // Composite methods
    [Fact]
    public void GenerateAtmosphere_ReturnsNonEmptyString()
    {
        var svc = new DescriptiveTextService(new JsonTextConfig(FullJson));
        svc.GenerateAtmosphere().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateItemDescription_ContainsItemName()
    {
        var svc = new DescriptiveTextService(new JsonTextConfig(FullJson));
        svc.GenerateItemDescription("Sword of Truth").Should().Contain("Sword of Truth");
    }
}
