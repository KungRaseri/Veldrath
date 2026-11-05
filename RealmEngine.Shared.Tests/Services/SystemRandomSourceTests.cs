using FluentAssertions;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Services;
using Xunit;

namespace RealmEngine.Shared.Tests.Services;

/// <summary>
/// Contract tests for the production <see cref="IRandomSource"/> binding: every overload must
/// stay inside its documented bounds so callers can trust the interface without guarding.
/// </summary>
[Trait("Category", "Services")]
public class SystemRandomSourceTests
{
    [Fact]
    public void Shared_IsASingletonInstance()
    {
        // Act & Assert
        SystemRandomSource.Shared.Should().NotBeNull();
        SystemRandomSource.Shared.Should().BeSameAs(SystemRandomSource.Shared);
    }

    [Fact]
    public void Next_WithMaxValue_StaysBelowTheBound()
    {
        // Arrange
        var source = new SystemRandomSource();

        // Act
        var values = Enumerable.Range(0, 200).Select(_ => source.Next(1)).ToList();

        // Assert
        values.Should().OnlyContain(value => value >= 0 && value < 1);
    }

    [Fact]
    public void Next_WithMinAndMaxValues_StaysInsideTheHalfOpenRange()
    {
        // Arrange
        var source = new SystemRandomSource();

        // Act
        var values = Enumerable.Range(0, 500).Select(_ => source.Next(5, 10)).ToList();

        // Assert
        values.Should().OnlyContain(value => value >= 5 && value < 10);
    }

    [Fact]
    public void NextDouble_StaysInTheUnitInterval()
    {
        // Arrange
        var source = new SystemRandomSource();

        // Act
        var values = Enumerable.Range(0, 200).Select(_ => source.NextDouble()).ToList();

        // Assert
        values.Should().OnlyContain(value => value >= 0.0 && value < 1.0);
    }

    [Fact]
    public void ImplementsTheSharedAbstraction()
    {
        // Act & Assert
        new SystemRandomSource().Should().BeAssignableTo<IRandomSource>();
    }
}
