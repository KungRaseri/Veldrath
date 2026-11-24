using FluentAssertions;
using Veldrath.Server.Features.Characters;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Unit coverage for the bag helpers crafting reads and writes: the slug/full-reference equivalence that keeps
/// harvest-written stacks spendable by a recipe, and the entries <see cref="CharacterBag.Take"/> must leave alone.
/// </summary>
/// <remarks>
/// The craft paths write bare slugs (harvest, chests, combat, character creation, shop) while recipes name the
/// full <c>@domain:slug</c> reference, so every helper matches either form. These tests pin that contract
/// directly rather than through a hub call.
/// </remarks>
public class CharacterBagTests
{
    private const string FullIngotRef = "@items/materials/ingots:iron-ingot";

    [Fact]
    public void Count_And_Take_Should_Match_A_Full_Reference_Stored_Under_Its_Bare_Slug()
    {
        var bag = CharacterBag.Read("""[{ "ItemRef": "iron-ingot", "Quantity": 3 }]""");

        CharacterBag.Count(bag, FullIngotRef).Should().Be(3, "harvest stores the slug and the recipe names the reference");

        CharacterBag.Take(bag, FullIngotRef, 2).Should().BeTrue();
        CharacterBag.Count(bag, "iron-ingot").Should().Be(1, "two of the three were spent");
    }

    [Fact]
    public void Take_Should_Keep_An_Entry_Whose_Quantity_Is_Not_A_Whole_Number()
    {
        // Count already reads an unreadable quantity as zero, so it can never pay towards a take. Take has to
        // agree: deleting the entry would lose whatever it actually holds.
        var bag = CharacterBag.Read("""
            [
              { "ItemRef": "iron-ingot", "Quantity": 5 },
              { "ItemRef": "iron-ingot", "Quantity": "five" }
            ]
            """);

        CharacterBag.Take(bag, "iron-ingot", 5).Should().BeTrue();

        bag.Should().ContainSingle("only the readable stack paid the five ingots");
        bag[0]["Quantity"].ValueKind.Should().Be(System.Text.Json.JsonValueKind.String);
        CharacterBag.Count(bag, "iron-ingot").Should().Be(0);
    }

    [Fact]
    public void Add_Should_Stack_A_Full_Reference_Onto_The_Slug_Stack()
    {
        var bag = CharacterBag.Read("""[{ "ItemRef": "iron-ingot", "Quantity": 2 }]""");

        CharacterBag.Add(bag, FullIngotRef, 1);

        bag.Should().ContainSingle("one item must not become two stacks");
        CharacterBag.Count(bag, "iron-ingot").Should().Be(3);
    }
}
