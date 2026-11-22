using FluentAssertions;
using RealmEngine.Data.Content;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Validation.Content;

namespace RealmEngine.Data.Tests.Content;

/// <summary>
/// Guards the map between the content-type keys a route exposes and the family that validates and stores the row.
/// </summary>
/// <remarks>
/// A key in this map that resolves to a key the registry does not have is a <see cref="KeyNotFoundException"/> from
/// inside validation — a 500 on a route that exists, which is how a null child collection on
/// <c>/api/content/power/{slug}</c> used to surface.
/// </remarks>
[Trait("Category", "Content")]
public class ContentEntityTypesTests
{
    /// <summary>Every key the map accepts must resolve to a validator the registry actually has.</summary>
    [Fact]
    public void Every_known_type_key_resolves_to_a_registered_validator()
    {
        var registry = ContentValidatorRegistry.CreateDefault();

        foreach (var typeKey in ContentEntityTypes.KnownTypes)
        {
            ContentEntityTypes.TryResolve(typeKey, out var resolver).Should().BeTrue("'{0}' must resolve", typeKey);

            registry.TryGetValidator(resolver.ContentType, out _).Should().BeTrue(
                "'{0}' resolves to '{1}', which has no validator",
                typeKey,
                resolver.ContentType);
        }
    }

    /// <summary>
    /// <c>power</c> is the read alias of the <c>ability</c> family: the detail route already serves it, so the write
    /// route must fold it onto the same validator rather than fail.
    /// </summary>
    [Fact]
    public void The_power_read_alias_resolves_to_the_ability_validator()
    {
        ContentEntityTypes.TryResolve("power", out var resolver).Should().BeTrue();

        resolver.ContentType.Should().Be("ability");
        resolver.EntityType.Should().Be<Power>();
    }
}
