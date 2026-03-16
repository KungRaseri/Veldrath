using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Generators;
using RealmEngine.Data.Entities;
using Xunit;
using Xunit.Abstractions;

namespace RealmEngine.Core.Tests.Generators;

public class NameComposerTests
{
    private readonly ITestOutputHelper _output;
    private readonly NameComposer _composer;

    public NameComposerTests(ITestOutputHelper output)
    {
        _output = output;
        _composer = new NameComposer(NullLogger<NameComposer>.Instance);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static NamePatternSet BuildSet(string entityPath, string template, params (string key, string value, int weight)[] components)
    {
        var set = new NamePatternSet
        {
            Id = Guid.NewGuid(),
            EntityPath = entityPath,
            Patterns = [new NamePattern { Id = Guid.NewGuid(), Template = template, RarityWeight = 100 }],
            Components = components.Select((c, i) => new NameComponent
            {
                Id = Guid.NewGuid(),
                ComponentKey = c.key,
                Value = c.value,
                RarityWeight = c.weight,
                SortOrder = i
            }).ToList()
        };
        set.Patterns.First().SetId = set.Id;
        set.Components.Cast<NameComponent>().ToList().ForEach(c => c.SetId = set.Id);
        return set;
    }

    // ── ComposeName ───────────────────────────────────────────────────────────

    [Fact]
    public void ComposeName_SimpleBase_ReturnsBaseValue()
    {
        var set = BuildSet("test/base", "base", ("base", "Wolf", 100));

        var name = _composer.ComposeName(set, out var values);

        name.Should().Be("Wolf");
        values.Should().ContainKey("base").WhoseValue.Should().Be("Wolf");
        _output.WriteLine($"Name: {name}");
    }

    [Fact]
    public void ComposeName_MultipleTokens_ReturnsComposedString()
    {
        var set = BuildSet(
            "test/multi",
            "{size} {type} {base}",
            ("size", "Giant", 50),
            ("type", "Frost", 30),
            ("base", "Wolf", 100));

        var name = _composer.ComposeName(set, out var values);

        name.Should().Be("Giant Frost Wolf");
        values.Keys.Should().Contain(["size", "type", "base"]);
        _output.WriteLine($"Name: {name}");
    }

    [Fact]
    public void ComposeName_MissingOptionalToken_SkipsToken()
    {
        var set = BuildSet(
            "test/optional",
            "{type} {base} {title}",
            ("base", "Wolf", 100),
            ("title", "the Devourer", 50));
        // "type" is missing from components

        var name = _composer.ComposeName(set, out var values);

        // "{type} " removed, result is "Wolf the Devourer"
        name.Should().Be("Wolf the Devourer");
        values.Should().ContainKey("base");
        values.Should().ContainKey("title");
        values.Should().NotContainKey("type");
        _output.WriteLine($"Name: {name}");
    }

    [Fact]
    public void ComposeName_NoPatterns_ReturnsEmpty()
    {
        var set = new NamePatternSet
        {
            Id = Guid.NewGuid(),
            EntityPath = "test/empty",
            Patterns = [],
            Components = []
        };

        var name = _composer.ComposeName(set, out var values);

        name.Should().BeEmpty();
        values.Should().BeEmpty();
    }

    // ── SelectPattern ─────────────────────────────────────────────────────────

    [Fact]
    public void SelectPattern_SinglePattern_AlwaysReturnsThatPattern()
    {
        var pattern = new NamePattern { Id = Guid.NewGuid(), Template = "{base}", RarityWeight = 100 };

        var selected = _composer.SelectPattern([pattern]);

        selected.Should().Be(pattern);
    }

    [Fact]
    public void SelectPattern_EmptyCollection_ReturnsNull()
    {
        var result = _composer.SelectPattern([]);
        result.Should().BeNull();
    }

    // ── SelectWeightedComponent ───────────────────────────────────────────────

    [Fact]
    public void SelectWeightedComponent_SingleEntry_AlwaysReturnsThatEntry()
    {
        var comp = new NameComponent { Id = Guid.NewGuid(), ComponentKey = "base", Value = "Dragon", RarityWeight = 100 };

        var selected = _composer.SelectWeightedComponent([comp]);

        selected.Should().Be(comp);
    }

    [Fact]
    public void SelectWeightedComponent_EmptyPool_ReturnsNull()
    {
        var result = _composer.SelectWeightedComponent([]);
        result.Should().BeNull();
    }

    // ── ComposeFromTemplate ───────────────────────────────────────────────────

    [Fact]
    public void ComposeFromTemplate_TemplateAndComponents_SubstitutesTokens()
    {
        var components = new List<NameComponent>
        {
            new() { ComponentKey = "adjective", Value = "Dark", RarityWeight = 100 },
            new() { ComponentKey = "noun", Value = "Flame", RarityWeight = 100 }
        };

        var name = _composer.ComposeFromTemplate("{adjective} {noun}", components, out var values);

        name.Should().Be("Dark Flame");
        values["adjective"].Should().Be("Dark");
        values["noun"].Should().Be("Flame");
    }

    // ── ComposeNameStructured ─────────────────────────────────────────────────

    [Fact]
    public void ComposeNameStructured_PrefixBaseAndSuffix_CategorisedCorrectly()
    {
        var set = BuildSet(
            "test/structured",
            "{prefix} {base} {suffix}",
            ("prefix", "Shadow", 100),
            ("base", "Blade", 100),
            ("suffix", "of Doom", 100));

        var (prefixes, baseName, suffixes) = _composer.ComposeNameStructured(set, out _);

        prefixes.Should().ContainSingle().Which.Should().Be("Shadow");
        baseName.Should().Be("Blade");
        suffixes.Should().ContainSingle().Which.Should().Be("of Doom");
    }

    // ── Weight distribution (statistical) ────────────────────────────────────

    [Fact]
    public void SelectWeightedComponent_HighWeight_SelectedMoreOften()
    {
        var rare = new NameComponent { ComponentKey = "x", Value = "Rare", RarityWeight = 1 };
        var common = new NameComponent { ComponentKey = "x", Value = "Common", RarityWeight = 99 };
        var pool = new[] { rare, common };

        var counts = new Dictionary<string, int> { ["Rare"] = 0, ["Common"] = 0 };
        for (int i = 0; i < 1000; i++)
        {
            var selected = _composer.SelectWeightedComponent(pool);
            counts[selected!.Value]++;
        }

        counts["Common"].Should().BeGreaterThan(counts["Rare"],
            because: "the 99-weight component should dominate");
        _output.WriteLine($"Rare={counts["Rare"]} Common={counts["Common"]}");
    }
}
