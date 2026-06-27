using System.Net;
using System.Text.Json;
using FluentAssertions;
using Veldrath.Contracts.Zones;
using Veldrath.Discord.Services;
using Xunit;

namespace Veldrath.Discord.Tests.Services;

public class ServerStatusServiceTests
{
    private static readonly List<ZoneDto> SampleZones =
    [
        new("crestfall", "Crestfall", "A town", "Town", 0, 50, true, 3, "varenmark", true, true),
        new("the-droveway", "The Droveway", "A road", "Wilderness", 1, 10, false, 1, "varenmark", false, false)
    ];

    private static string SampleJson => JsonSerializer.Serialize(SampleZones);

    [Fact]
    public async Task GetZonesAsync_Returns_Zones_On_Success()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(SampleJson, HttpStatusCode.OK);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:9000") };
        var service = new ServerStatusService(httpClient);

        // Act
        var result = await service.GetZonesAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result![0].Id.Should().Be("crestfall");
        result![1].Id.Should().Be("the-droveway");
    }

    [Fact]
    public async Task GetZonesAsync_Returns_Null_On_HttpError()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler("", HttpStatusCode.InternalServerError);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:9000") };
        var service = new ServerStatusService(httpClient);

        // Act
        var result = await service.GetZonesAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetZonesAsync_Returns_Null_On_Invalid_Json()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler("not-json", HttpStatusCode.OK);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:9000") };
        var service = new ServerStatusService(httpClient);

        // Act
        var result = await service.GetZonesAsync();

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task GetZonesAsync_Returns_Null_On_Empty_Response(string? content)
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(content ?? "", HttpStatusCode.OK);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:9000") };
        var service = new ServerStatusService(httpClient);

        // Act
        var result = await service.GetZonesAsync();

        // Assert
        result.Should().BeNull();
    }
}
