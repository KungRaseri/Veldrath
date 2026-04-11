using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Veldrath.Client.Services;
using Veldrath.Client.Tests.Infrastructure;
using Veldrath.Contracts.Characters;

namespace Veldrath.Client.Tests;

public class HttpCharacterCreationServiceTests : TestBase
{
    private static HttpCharacterCreationService MakeSut(FakeHttpHandler handler)
    {
        var tokens = new TokenStore();
        var http   = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        return new HttpCharacterCreationService(http, tokens, NullLogger<HttpCharacterCreationService>.Instance);
    }

    // BeginSessionAsync
    [Fact]
    public async Task BeginSessionAsync_Returns_SessionId_On_Success()
    {
        var sessionId = Guid.NewGuid();
        var sut = MakeSut(FakeHttpHandler.Json(new { SessionId = sessionId, Success = true }));

        var result = await sut.BeginSessionAsync();

        result.Should().Be(sessionId);
    }

    [Fact]
    public async Task BeginSessionAsync_Returns_Null_On_Error_Response()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Unauthorized", HttpStatusCode.Unauthorized));

        var result = await sut.BeginSessionAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task BeginSessionAsync_Returns_Null_On_Exception()
    {
        var sut = MakeSut(FakeHttpHandler.Throws(new HttpRequestException("timeout")));

        var result = await sut.BeginSessionAsync();

        result.Should().BeNull();
    }

    // SetNameAsync
    [Fact]
    public async Task SetNameAsync_Returns_True_On_Success()
    {
        var sut = MakeSut(FakeHttpHandler.Text("", HttpStatusCode.NoContent));

        var result = await sut.SetNameAsync(Guid.NewGuid(), "Hero");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task SetNameAsync_Returns_False_On_BadRequest()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Invalid name", HttpStatusCode.BadRequest));

        var result = await sut.SetNameAsync(Guid.NewGuid(), "");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task SetNameAsync_Returns_False_On_Exception()
    {
        var sut = MakeSut(FakeHttpHandler.Throws(new HttpRequestException("timeout")));

        var result = await sut.SetNameAsync(Guid.NewGuid(), "Hero");

        result.Should().BeFalse();
    }

    // SetClassAsync
    [Fact]
    public async Task SetClassAsync_Returns_True_On_Success()
    {
        var sut = MakeSut(FakeHttpHandler.Text("", HttpStatusCode.NoContent));

        var result = await sut.SetClassAsync(Guid.NewGuid(), "Warrior");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task SetClassAsync_Returns_False_On_BadRequest()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Unknown class", HttpStatusCode.BadRequest));

        var result = await sut.SetClassAsync(Guid.NewGuid(), "Unknown");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task SetClassAsync_Returns_False_On_Exception()
    {
        var sut = MakeSut(FakeHttpHandler.Throws(new HttpRequestException("timeout")));

        var result = await sut.SetClassAsync(Guid.NewGuid(), "Warrior");

        result.Should().BeFalse();
    }

    // SetSpeciesAsync
    [Fact]
    public async Task SetSpeciesAsync_Returns_True_On_Success()
    {
        var sut = MakeSut(FakeHttpHandler.Text("", HttpStatusCode.NoContent));

        var result = await sut.SetSpeciesAsync(Guid.NewGuid(), "human");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task SetSpeciesAsync_Returns_False_On_BadRequest()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Unknown species", HttpStatusCode.BadRequest));

        var result = await sut.SetSpeciesAsync(Guid.NewGuid(), "unknown");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task SetSpeciesAsync_Returns_False_On_Exception()
    {
        var sut = MakeSut(FakeHttpHandler.Throws(new HttpRequestException("timeout")));

        var result = await sut.SetSpeciesAsync(Guid.NewGuid(), "human");

        result.Should().BeFalse();
    }

    // SetBackgroundAsync
    [Fact]
    public async Task SetBackgroundAsync_Returns_True_On_Success()
    {
        var sut = MakeSut(FakeHttpHandler.Text("", HttpStatusCode.NoContent));

        var result = await sut.SetBackgroundAsync(Guid.NewGuid(), "soldier");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task SetBackgroundAsync_Returns_False_On_BadRequest()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Unknown background", HttpStatusCode.BadRequest));

        var result = await sut.SetBackgroundAsync(Guid.NewGuid(), "unknown");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task SetBackgroundAsync_Returns_False_On_Exception()
    {
        var sut = MakeSut(FakeHttpHandler.Throws(new HttpRequestException("timeout")));

        var result = await sut.SetBackgroundAsync(Guid.NewGuid(), "soldier");

        result.Should().BeFalse();
    }

    // FinalizeAsync
    [Fact]
    public async Task FinalizeAsync_Returns_Character_On_Success()
    {
        var character = new CharacterDto(Guid.NewGuid(), 1, "Hero", "Warrior", 1, 0, DateTimeOffset.UtcNow, "starting-zone");
        var sut = MakeSut(FakeHttpHandler.Json(character));

        var (result, error) = await sut.FinalizeAsync(Guid.NewGuid(), new FinalizeCreationSessionRequest(null, "normal"));

        result.Should().NotBeNull();
        result!.Name.Should().Be("Hero");
        error.Should().BeNull();
    }

    [Fact]
    public async Task FinalizeAsync_Returns_Error_On_BadRequest()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Name already taken", HttpStatusCode.BadRequest));

        var (character, error) = await sut.FinalizeAsync(Guid.NewGuid(), new FinalizeCreationSessionRequest(null, "normal"));

        character.Should().BeNull();
        error.Should().NotBeNull();
    }

    [Fact]
    public async Task FinalizeAsync_Returns_NetworkError_On_Exception()
    {
        var sut = MakeSut(FakeHttpHandler.Throws(new HttpRequestException("no connection")));

        var (character, error) = await sut.FinalizeAsync(Guid.NewGuid(), new FinalizeCreationSessionRequest(null, "normal"));

        character.Should().BeNull();
        error!.Message.Should().Contain("Network error");
    }

    // SetEquipmentPreferencesAsync
    [Fact]
    public async Task SetEquipmentPreferencesAsync_Returns_True_On_Success()
    {
        var sut  = MakeSut(FakeHttpHandler.Text("", HttpStatusCode.NoContent));
        var prefs = new SetCreationEquipmentPreferencesRequest("light-armor", "sword", false);

        var result = await sut.SetEquipmentPreferencesAsync(Guid.NewGuid(), prefs);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task SetEquipmentPreferencesAsync_Returns_False_On_BadRequest()
    {
        var sut   = MakeSut(FakeHttpHandler.Text("Invalid prefs", HttpStatusCode.BadRequest));
        var prefs = new SetCreationEquipmentPreferencesRequest(null, null);

        var result = await sut.SetEquipmentPreferencesAsync(Guid.NewGuid(), prefs);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task SetEquipmentPreferencesAsync_Returns_False_On_Exception()
    {
        var sut   = MakeSut(FakeHttpHandler.Throws(new HttpRequestException("timeout")));
        var prefs = new SetCreationEquipmentPreferencesRequest("heavy-armor", "axe");

        var result = await sut.SetEquipmentPreferencesAsync(Guid.NewGuid(), prefs);

        result.Should().BeFalse();
    }

    // SetLocationAsync
    [Fact]
    public async Task SetLocationAsync_Returns_True_On_Success()
    {
        var sut = MakeSut(FakeHttpHandler.Text("", HttpStatusCode.NoContent));

        var result = await sut.SetLocationAsync(Guid.NewGuid(), "town-square");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task SetLocationAsync_Returns_False_On_BadRequest()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Unknown location", HttpStatusCode.BadRequest));

        var result = await sut.SetLocationAsync(Guid.NewGuid(), "unknown-place");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task SetLocationAsync_Returns_False_On_Exception()
    {
        var sut = MakeSut(FakeHttpHandler.Throws(new HttpRequestException("timeout")));

        var result = await sut.SetLocationAsync(Guid.NewGuid(), "town-square");

        result.Should().BeFalse();
    }

    // SetAttributesAsync
    [Fact]
    public async Task SetAttributesAsync_Returns_True_On_Success()    {
        var sut = MakeSut(FakeHttpHandler.Text("", HttpStatusCode.NoContent));
        var allocs = new Dictionary<string, int> { ["Strength"] = 10 };

        var result = await sut.SetAttributesAsync(Guid.NewGuid(), allocs);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task SetAttributesAsync_Returns_False_On_BadRequest()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Invalid allocation", HttpStatusCode.BadRequest));
        var allocs = new Dictionary<string, int> { ["Strength"] = 20 };

        var result = await sut.SetAttributesAsync(Guid.NewGuid(), allocs);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task SetAttributesAsync_Returns_False_On_Exception()
    {
        var sut = MakeSut(FakeHttpHandler.Throws(new HttpRequestException("timeout")));
        var allocs = new Dictionary<string, int> { ["Strength"] = 10 };

        var result = await sut.SetAttributesAsync(Guid.NewGuid(), allocs);

        result.Should().BeFalse();
    }

    // AbandonAsync
    [Fact]
    public async Task AbandonAsync_Does_Not_Throw_On_Error_Response()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Not Found", HttpStatusCode.NotFound));

        await sut.Invoking(s => s.AbandonAsync(Guid.NewGuid())).Should().NotThrowAsync();
    }

    [Fact]
    public async Task AbandonAsync_Does_Not_Throw_On_Exception()
    {
        var sut = MakeSut(FakeHttpHandler.Throws(new HttpRequestException("timeout")));

        await sut.Invoking(s => s.AbandonAsync(Guid.NewGuid())).Should().NotThrowAsync();
    }
}
