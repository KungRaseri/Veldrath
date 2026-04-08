using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using RealmUnbound.Client.Services;
using RealmUnbound.Contracts.Content;
using RealmUnbound.Contracts.Zones;

namespace RealmUnbound.Client.Tests;

// Mock HttpMessageHandler
/// <summary>
/// A configurable HttpMessageHandler stub that returns pre-built responses without
/// making actual network calls.
/// </summary>
internal sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _factory;

    public FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> factory)
        => _factory = factory;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(_factory(request));

    /// <summary>Convenience: return a fixed JSON response for any request.</summary>
    public static FakeHttpHandler Json<T>(T body, HttpStatusCode status = HttpStatusCode.OK)
        => new(_ => new HttpResponseMessage(status)
        {
            Content = JsonContent.Create(body)
        });

    /// <summary>Convenience: return a fixed plain-text response.</summary>
    public static FakeHttpHandler Text(string body, HttpStatusCode status)
        => new(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(body)
        });

    /// <summary>Convenience: throw on every request (simulates network failure).</summary>
    public static FakeHttpHandler Throws(Exception ex)
        => new(_ => throw ex);
}

// HttpAuthService tests
public class HttpAuthServiceTests : TestBase
{
    private static readonly AuthResponse SampleAuth = new(
        "access-token", "refresh-token",
        DateTimeOffset.UtcNow.AddMinutes(15),
        Guid.NewGuid(), "TestUser");

    private static HttpAuthService MakeSut(FakeHttpHandler handler, TokenStore? tokens = null)
    {
        tokens ??= new TokenStore();
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        return new HttpAuthService(http, tokens, new TokenPersistenceService(), NullLogger<HttpAuthService>.Instance);
    }

    // RegisterAsync
    [Fact]
    public async Task RegisterAsync_Should_Return_Auth_Response_On_Success()
    {
        var sut = MakeSut(FakeHttpHandler.Json(SampleAuth));

        var (response, error) = await sut.RegisterAsync("user@test.com", "User", "Password1!");

        response.Should().NotBeNull();
        error.Should().BeNull();
    }

    [Fact]
    public async Task RegisterAsync_Should_Populate_TokenStore_On_Success()
    {
        var tokens = new TokenStore();
        var sut    = MakeSut(FakeHttpHandler.Json(SampleAuth), tokens);

        await sut.RegisterAsync("user@test.com", "User", "Password1!");

        tokens.AccessToken.Should().Be("access-token");
        tokens.Username.Should().Be("TestUser");
    }

    [Fact]
    public async Task RegisterAsync_Should_Return_Error_On_Non_Success_Status()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Email already taken", HttpStatusCode.BadRequest));

        var (response, error) = await sut.RegisterAsync("dupe@test.com", "User", "Password1!");

        response.Should().BeNull();
        error.Should().NotBeNull();
        error!.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RegisterAsync_Should_Return_Network_Error_On_Exception()
    {
        var sut = MakeSut(FakeHttpHandler.Throws(new HttpRequestException("No connection")));

        var (response, error) = await sut.RegisterAsync("user@test.com", "User", "Password1!");

        response.Should().BeNull();
        error!.Message.Should().Be("Network error. Please check your connection.");
    }

    // LoginAsync
    [Fact]
    public async Task LoginAsync_Should_Return_Auth_Response_On_Success()
    {
        var sut = MakeSut(FakeHttpHandler.Json(SampleAuth));

        var (response, error) = await sut.LoginAsync("user@test.com", "Password1!");

        response.Should().NotBeNull();
        error.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_Should_Populate_TokenStore_On_Success()
    {
        var tokens = new TokenStore();
        var sut    = MakeSut(FakeHttpHandler.Json(SampleAuth), tokens);

        await sut.LoginAsync("user@test.com", "Password1!");

        tokens.AccessToken.Should().Be("access-token");
        tokens.Username.Should().Be("TestUser");
    }

    [Fact]
    public async Task LoginAsync_Should_Return_Error_On_Non_Success_Status()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Invalid credentials", HttpStatusCode.Unauthorized));

        var (response, error) = await sut.LoginAsync("wrong@test.com", "badpass");

        response.Should().BeNull();
        error.Should().NotBeNull();
        error!.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task LoginAsync_Should_Return_Network_Error_On_Exception()
    {
        var sut = MakeSut(FakeHttpHandler.Throws(new HttpRequestException("timeout")));

        var (response, error) = await sut.LoginAsync("user@test.com", "Password1!");

        error!.Message.Should().Be("Network error. Please check your connection.");
    }

    [Fact]
    public async Task LoginAsync_Should_Return_Lockout_Message_When_Server_Returns_Locked_Out()
    {
        var sut = MakeSut(FakeHttpHandler.Json(
            new { error = "Account is locked out" }, HttpStatusCode.Unauthorized));

        var (response, error) = await sut.LoginAsync("user@test.com", "Password1!");

        response.Should().BeNull();
        error!.Message.Should().Contain("locked");
    }

    // RefreshAsync
    [Fact]
    public async Task RefreshAsync_Should_Return_False_When_No_RefreshToken()
    {
        var tokens = new TokenStore(); // no refresh token set
        var sut    = MakeSut(FakeHttpHandler.Json(SampleAuth), tokens);

        var result = await sut.RefreshAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task RefreshAsync_Should_Return_True_On_Success()
    {
        var tokens = new TokenStore();
        tokens.Set("old-access", "old-refresh", "User", Guid.NewGuid());
        var sut = MakeSut(FakeHttpHandler.Json(SampleAuth), tokens);

        var result = await sut.RefreshAsync();

        result.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshAsync_Should_Update_TokenStore_On_Success()
    {
        var tokens = new TokenStore();
        tokens.Set("old-access", "old-refresh", "User", Guid.NewGuid());
        var sut = MakeSut(FakeHttpHandler.Json(SampleAuth), tokens);

        await sut.RefreshAsync();

        tokens.AccessToken.Should().Be("access-token");
    }

    [Fact]
    public async Task RefreshAsync_Should_Clear_Tokens_On_Failure()
    {
        var tokens = new TokenStore();
        tokens.Set("old-access", "old-refresh", "User", Guid.NewGuid());
        var sut = MakeSut(FakeHttpHandler.Text("Expired", HttpStatusCode.Unauthorized), tokens);

        var result = await sut.RefreshAsync();

        result.Should().BeFalse();
        tokens.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task RefreshAsync_Should_Return_False_When_Body_Deserializes_To_Null()
    {
        // Server returns 200 OK but body is "null" — ReadFromJsonAsync<AuthResponse> returns null
        var tokens = new TokenStore();
        tokens.Set("old-access", "old-refresh", "User", Guid.NewGuid());
        var fakeHandler = new FakeHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new System.Net.Http.StringContent("null", System.Text.Encoding.UTF8, "application/json")
        });
        var sut = MakeSut(fakeHandler, tokens);

        var result = await sut.RefreshAsync();

        result.Should().BeFalse();
        tokens.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task RefreshAsync_Should_Return_False_And_Not_Throw_On_Network_Exception()
    {
        var tokens = new TokenStore();
        tokens.Set("old-access", "old-refresh", "User", Guid.NewGuid());
        var sut = MakeSut(FakeHttpHandler.Throws(new HttpRequestException("timeout")), tokens);

        var result = await sut.RefreshAsync();

        result.Should().BeFalse();
    }

    // LogoutAsync
    [Fact]
    public async Task LogoutAsync_Should_Clear_TokenStore()
    {
        var tokens = new TokenStore();
        tokens.Set("access", "refresh", "User", Guid.NewGuid());
        var sut = MakeSut(FakeHttpHandler.Text("", HttpStatusCode.OK), tokens);

        await sut.LogoutAsync();

        tokens.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task LogoutAsync_Should_Not_Throw_When_No_Token()
    {
        var sut = MakeSut(FakeHttpHandler.Text("", HttpStatusCode.OK));

        var act = () => sut.LogoutAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task LogoutAsync_Should_Clear_Tokens_Even_When_Server_Unreachable()
    {
        var tokens = new TokenStore();
        tokens.Set("access", "refresh", "User", Guid.NewGuid());
        var sut = MakeSut(FakeHttpHandler.Throws(new HttpRequestException("No connection")), tokens);

        await sut.LogoutAsync();

        tokens.IsAuthenticated.Should().BeFalse();
    }
}

// HttpCharacterService tests
public class HttpCharacterServiceTests : TestBase
{
    private static readonly CharacterDto SampleChar = new(
        Guid.NewGuid(), 1, "Hero", "Warrior",
        1, 0, DateTimeOffset.UtcNow, "starting-zone");

    private static HttpCharacterService MakeSut(FakeHttpHandler handler, TokenStore? tokens = null)
    {
        tokens ??= new TokenStore();
        tokens.Set("test-access", "test-refresh", "User", Guid.NewGuid());
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        return new HttpCharacterService(http, tokens, NullLogger<HttpCharacterService>.Instance);
    }

    // GetCharactersAsync
    [Fact]
    public async Task GetCharactersAsync_Should_Return_List_On_Success()
    {
        var sut = MakeSut(FakeHttpHandler.Json(new List<CharacterDto> { SampleChar }));

        var result = await sut.GetCharactersAsync();

        result.Should().ContainSingle(c => c.Name == "Hero");
    }

    [Fact]
    public async Task GetCharactersAsync_Should_Return_Empty_On_Error()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Unauthorized", HttpStatusCode.Unauthorized));

        var result = await sut.GetCharactersAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCharactersAsync_Should_Return_Empty_On_Network_Exception()
    {
        var sut = MakeSut(FakeHttpHandler.Throws(new HttpRequestException("offline")));

        var result = await sut.GetCharactersAsync();

        result.Should().BeEmpty();
    }

    // CreateCharacterAsync
    [Fact]
    public async Task CreateCharacterAsync_Should_Return_Character_On_Success()
    {
        var sut = MakeSut(FakeHttpHandler.Json(SampleChar, HttpStatusCode.Created));

        var (character, error) = await sut.CreateCharacterAsync(new CreateCharacterRequest("Hero", "Fighter"));

        character.Should().NotBeNull();
        error.Should().BeNull();
    }

    [Fact]
    public async Task CreateCharacterAsync_Should_Return_Error_On_Non_Success()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Name taken", HttpStatusCode.BadRequest));

        var (character, error) = await sut.CreateCharacterAsync(new CreateCharacterRequest("Taken", "Fighter"));

        character.Should().BeNull();
        error.Should().NotBeNull();
        error!.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CreateCharacterAsync_Should_Return_Network_Error_On_Exception()
    {
        var sut = MakeSut(FakeHttpHandler.Throws(new HttpRequestException("offline")));

        var (character, error) = await sut.CreateCharacterAsync(new CreateCharacterRequest("Hero", "Fighter"));

        character.Should().BeNull();
        error!.Message.Should().Be("Network error. Please check your connection.");
    }

    // DeleteCharacterAsync
    [Fact]
    public async Task DeleteCharacterAsync_Should_Return_Null_Error_On_Success()
    {
        var sut = MakeSut(FakeHttpHandler.Text("", HttpStatusCode.NoContent));

        var error = await sut.DeleteCharacterAsync(Guid.NewGuid());

        error.Should().BeNull();
    }

    [Fact]
    public async Task DeleteCharacterAsync_Should_Return_Error_Message_On_Failure()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Character not found", HttpStatusCode.NotFound));

        var error = await sut.DeleteCharacterAsync(Guid.NewGuid());

        error.Should().NotBeNull();
        error!.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task DeleteCharacterAsync_Should_Return_Network_Error_On_Exception()
    {
        var sut = MakeSut(FakeHttpHandler.Throws(new HttpRequestException("offline")));

        var error = await sut.DeleteCharacterAsync(Guid.NewGuid());

        error!.Message.Should().Be("Network error. Please check your connection.");
    }

    // Lifecycle / edge-case
    [Fact]
    public async Task CreateCharacterAsync_Returns409_WithFriendlyNameTakenMessage()
    {
        var body = new { title = "Conflict", detail = "A character with that name already exists." };
        var sut = MakeSut(FakeHttpHandler.Json(body, HttpStatusCode.Conflict));

        var (character, error) = await sut.CreateCharacterAsync(new CreateCharacterRequest("Taken", "Fighter"));

        character.Should().BeNull();
        error.Should().NotBeNull();
        error!.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CreateCharacterAsync_Returns401_ReturnsError()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Unauthorized", HttpStatusCode.Unauthorized));

        var (character, error) = await sut.CreateCharacterAsync(new CreateCharacterRequest("Hero", "Fighter"));

        character.Should().BeNull();
        error.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteThenCreate_BothSucceed_When_Server_Cooperates()
    {
        // Two sequential calls: first DELETE 204, then POST 201 with a character
        var newChar = SampleChar with { Name = "Recycled" };
        int callCount = 0;
        var handler = new FakeHttpHandler(_ =>
        {
            callCount++;
            return callCount == 1
                ? new HttpResponseMessage(HttpStatusCode.NoContent)
                : new HttpResponseMessage(HttpStatusCode.Created)
                  { Content = System.Net.Http.Json.JsonContent.Create(newChar) };
        });
        var sut = MakeSut(handler);

        var deleteError = await sut.DeleteCharacterAsync(Guid.NewGuid());
        var (created, createError) = await sut.CreateCharacterAsync(new CreateCharacterRequest("Recycled", "Fighter"));

        deleteError.Should().BeNull();
        created.Should().NotBeNull();
        createError.Should().BeNull();
        created!.Name.Should().Be("Recycled");
    }
}

// HttpZoneService tests
public class HttpZoneServiceTests : TestBase
{
    private static readonly ZoneDto SampleZone = new(
        "starting-zone", "The Starting Vale", "A peaceful valley.",
        "outdoor", 1, 50, true, 0);

    private static HttpZoneService MakeSut(FakeHttpHandler handler, TokenStore? tokens = null)
    {
        if (tokens is null)
        {
            tokens = new TokenStore();
            tokens.Set("test-access", "test-refresh", "User", Guid.NewGuid());
        }
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        return new HttpZoneService(http, tokens, NullLogger<HttpZoneService>.Instance);
    }

    // GetZonesAsync
    [Fact]
    public async Task GetZonesAsync_Should_Return_List_On_Success()
    {
        var sut    = MakeSut(FakeHttpHandler.Json(new List<ZoneDto> { SampleZone }));
        var result = await sut.GetZonesAsync();
        result.Should().ContainSingle(z => z.Id == "starting-zone");
    }

    [Fact]
    public async Task GetZonesAsync_Should_Return_Empty_On_Error()
    {
        var sut    = MakeSut(FakeHttpHandler.Text("Forbidden", HttpStatusCode.Forbidden));
        var result = await sut.GetZonesAsync();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetZonesAsync_Should_Return_Empty_On_Network_Exception()
    {
        var sut    = MakeSut(FakeHttpHandler.Throws(new HttpRequestException("offline")));
        var result = await sut.GetZonesAsync();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetZonesAsync_Should_Send_Bearer_Token()
    {
        string? capturedAuth = null;
        var handler = new FakeHttpHandler(req =>
        {
            capturedAuth = req.Headers.Authorization?.Parameter;
            return new System.Net.Http.HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = System.Net.Http.Json.JsonContent.Create(new List<ZoneDto>())
            };
        });
        var tokens = new TokenStore();
        tokens.Set("my-token", "refresh", "User", Guid.NewGuid());
        var sut = MakeSut(handler, tokens);

        await sut.GetZonesAsync();

        capturedAuth.Should().Be("my-token");
    }

    // GetZoneAsync
    [Fact]
    public async Task GetZoneAsync_Should_Return_Zone_On_Success()
    {
        var sut    = MakeSut(FakeHttpHandler.Json(SampleZone));
        var result = await sut.GetZoneAsync("starting-zone");
        result.Should().NotBeNull();
        result!.Name.Should().Be("The Starting Vale");
    }

    [Fact]
    public async Task GetZoneAsync_Should_Return_Null_When_Not_Found()
    {
        var sut    = MakeSut(FakeHttpHandler.Text("Not Found", HttpStatusCode.NotFound));
        var result = await sut.GetZoneAsync("missing-zone");
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetZoneAsync_Should_Return_Null_On_Network_Exception()
    {
        var sut    = MakeSut(FakeHttpHandler.Throws(new HttpRequestException("offline")));
        var result = await sut.GetZoneAsync("zone-1");
        result.Should().BeNull();
    }

    private static readonly RegionDto SampleRegion =
        new("thornveil", "Thornveil", "Green region.", "Forest", 1, 20, true, "aethoria");

    private static readonly WorldDto SampleWorld =
        new("aethoria", "Aethoria", "The main world.", "Modern");

    // GetZonesByRegionAsync
    [Fact]
    public async Task GetZonesByRegionAsync_Should_Return_List_On_Success()
    {
        var sut = MakeSut(FakeHttpHandler.Json(new List<ZoneDto> { SampleZone }));
        var result = await sut.GetZonesByRegionAsync("thornveil");
        result.Should().ContainSingle(z => z.Id == "starting-zone");
    }

    [Fact]
    public async Task GetZonesByRegionAsync_Should_Return_Empty_On_Error()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Error", HttpStatusCode.InternalServerError));
        var result = await sut.GetZonesByRegionAsync("thornveil");
        result.Should().BeEmpty();
    }

    // GetRegionsAsync
    [Fact]
    public async Task GetRegionsAsync_Should_Return_List_On_Success()
    {
        var sut = MakeSut(FakeHttpHandler.Json(new List<RegionDto> { SampleRegion }));
        var result = await sut.GetRegionsAsync();
        result.Should().ContainSingle(r => r.Id == "thornveil");
    }

    [Fact]
    public async Task GetRegionsAsync_Should_Return_Empty_On_Error()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Error", HttpStatusCode.InternalServerError));
        var result = await sut.GetRegionsAsync();
        result.Should().BeEmpty();
    }

    // GetRegionAsync
    [Fact]
    public async Task GetRegionAsync_Should_Return_Dto_On_Success()
    {
        var sut = MakeSut(FakeHttpHandler.Json(SampleRegion));
        var result = await sut.GetRegionAsync("thornveil");
        result.Should().NotBeNull();
        result!.Id.Should().Be("thornveil");
    }

    [Fact]
    public async Task GetRegionAsync_Should_Return_Null_On_Not_Found()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Not Found", HttpStatusCode.NotFound));
        var result = await sut.GetRegionAsync("missing");
        result.Should().BeNull();
    }

    // GetRegionConnectionsAsync
    [Fact]
    public async Task GetRegionConnectionsAsync_Should_Return_List_On_Success()
    {
        var sut = MakeSut(FakeHttpHandler.Json(new List<RegionDto> { SampleRegion }));
        var result = await sut.GetRegionConnectionsAsync("thornveil");
        result.Should().ContainSingle(r => r.Id == "thornveil");
    }

    [Fact]
    public async Task GetRegionConnectionsAsync_Should_Return_Empty_On_Error()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Error", HttpStatusCode.InternalServerError));
        var result = await sut.GetRegionConnectionsAsync("thornveil");
        result.Should().BeEmpty();
    }

    // GetWorldsAsync
    [Fact]
    public async Task GetWorldsAsync_Should_Return_List_On_Success()
    {
        var sut = MakeSut(FakeHttpHandler.Json(new List<WorldDto> { SampleWorld }));
        var result = await sut.GetWorldsAsync();
        result.Should().ContainSingle(w => w.Id == "aethoria");
    }

    [Fact]
    public async Task GetWorldsAsync_Should_Return_Empty_On_Error()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Error", HttpStatusCode.InternalServerError));
        var result = await sut.GetWorldsAsync();
        result.Should().BeEmpty();
    }

    // GetWorldAsync
    [Fact]
    public async Task GetWorldAsync_Should_Return_Dto_On_Success()
    {
        var sut = MakeSut(FakeHttpHandler.Json(SampleWorld));
        var result = await sut.GetWorldAsync("aethoria");
        result.Should().NotBeNull();
        result!.Id.Should().Be("aethoria");
    }

    [Fact]
    public async Task GetWorldAsync_Should_Return_Null_On_Not_Found()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Not Found", HttpStatusCode.NotFound));
        var result = await sut.GetWorldAsync("missing");
        result.Should().BeNull();
    }

    // GetZoneLocationsAsync
    private static readonly ZoneLocationDto SampleLocation =
        new("fenwick-market", "Fenwick Market", "locations", "fenwick-crossing", 10, 1, 10);

    [Fact]
    public async Task GetZoneLocationsAsync_Should_Return_List_On_Success()
    {
        var sut    = MakeSut(FakeHttpHandler.Json(new List<ZoneLocationDto> { SampleLocation }));
        var result = await sut.GetZoneLocationsAsync("fenwick-crossing");
        result.Should().ContainSingle(l => l.Slug == "fenwick-market");
    }

    [Fact]
    public async Task GetZoneLocationsAsync_Should_Return_Empty_On_Error()
    {
        var sut    = MakeSut(FakeHttpHandler.Text("Not Found", HttpStatusCode.NotFound));
        var result = await sut.GetZoneLocationsAsync("fenwick-crossing");
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetZoneLocationsAsync_Should_Return_Empty_On_Network_Exception()
    {
        var sut    = MakeSut(FakeHttpHandler.Throws(new HttpRequestException("offline")));
        var result = await sut.GetZoneLocationsAsync("fenwick-crossing");
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetZoneLocationsAsync_WithCharacterId_Should_Include_QueryParam()
    {
        string? capturedUrl = null;
        var characterId     = Guid.Parse("12345678-0000-0000-0000-000000000000");
        var handler         = new FakeHttpHandler(req =>
        {
            capturedUrl = req.RequestUri?.ToString();
            return new System.Net.Http.HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = System.Net.Http.Json.JsonContent.Create(new List<ZoneLocationDto>())
            };
        });

        var sut = MakeSut(handler);
        await sut.GetZoneLocationsAsync("fenwick-crossing", characterId);

        capturedUrl.Should().Contain("characterId=12345678-0000-0000-0000-000000000000");
    }

    [Fact]
    public async Task GetZoneLocationsAsync_WithoutCharacterId_Should_Not_Include_QueryParam()
    {
        string? capturedUrl = null;
        var handler         = new FakeHttpHandler(req =>
        {
            capturedUrl = req.RequestUri?.ToString();
            return new System.Net.Http.HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = System.Net.Http.Json.JsonContent.Create(new List<ZoneLocationDto>())
            };
        });

        var sut = MakeSut(handler);
        await sut.GetZoneLocationsAsync("fenwick-crossing");

        capturedUrl.Should().NotContain("characterId");
    }
}
