using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using Veldrath.Contracts.Content;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Seeds one language entity into the shared in-memory database for the language endpoint tests.
/// </summary>
public sealed class ContentLanguageEndpointsFixture : IAsyncLifetime
{
    /// <summary>Gets the web application factory used across all tests in this fixture.</summary>
    public WebAppFactory Factory { get; }

    /// <summary>Gets the shared HTTP client for sending test requests.</summary>
    public HttpClient Client { get; private set; } = null!;

    /// <summary>Initializes a new instance of <see cref="ContentLanguageEndpointsFixture"/> with the shared collection factory.</summary>
    public ContentLanguageEndpointsFixture(WebAppFactory factory) => Factory = factory;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        Client = Factory.CreateClient();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();

        db.Languages.Add(new Language
        {
            Slug           = "typed-calethic",
            TypeKey        = "imperial",
            DisplayName    = "Typed Calethic",
            IsActive       = true,
            RarityWeight   = 60,
            TonalCharacter = "Hard + Formal",
            Description    = "The official tongue of the Caleth Empire.",
            Phonology      = new LanguagePhonology(),
            Morphology     = new LanguageMorphology(),
            RegisterSystem = new LanguageRegisters(),
        });

        await db.SaveChangesAsync();
    }

    /// <inheritdoc />
    public Task DisposeAsync()
    {
        Client.Dispose();
        return Task.CompletedTask;
    }
}

/// <summary>
/// Integration tests for the language catalog GET endpoints under
/// <c>/api/content/languages</c>. All routes are anonymous.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class ContentLanguageEndpointTests(ContentLanguageEndpointsFixture fixture)
    : IClassFixture<ContentLanguageEndpointsFixture>
{
    private readonly HttpClient _client = fixture.Client;

    // GET /api/content/languages
    [Fact]
    public async Task GetLanguages_Returns_OK_And_ContainsSeededLanguage()
    {
        var response = await _client.GetAsync("/api/content/languages");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<LanguageDto>>();
        items.Should().Contain(l => l.Slug == "typed-calethic");
    }

    [Fact]
    public async Task GetLanguages_Does_Not_Require_Auth()
    {
        using var anon = fixture.Factory.CreateClient();
        var response = await anon.GetAsync("/api/content/languages");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // GET /api/content/languages/{slug}
    [Fact]
    public async Task GetLanguageBySlug_Returns_Correct_Language()
    {
        var response = await _client.GetAsync("/api/content/languages/typed-calethic");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var item = await response.Content.ReadFromJsonAsync<LanguageDto>();
        item!.Slug.Should().Be("typed-calethic");
        item.DisplayName.Should().Be("Typed Calethic");
        item.TypeKey.Should().Be("imperial");
        item.TonalCharacter.Should().Be("Hard + Formal");
        item.Description.Should().Be("The official tongue of the Caleth Empire.");
    }

    [Fact]
    public async Task GetLanguageBySlug_Returns_404_For_Unknown_Slug()
    {
        var response = await _client.GetAsync("/api/content/languages/no-such-language");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // GET /api/content/languages?typeKey=...
    [Fact]
    public async Task GetLanguages_FilterByTypeKey_Returns_MatchingOnly()
    {
        var response = await _client.GetAsync("/api/content/languages?typeKey=imperial");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<LanguageDto>>();
        items.Should().Contain(l => l.Slug == "typed-calethic");
    }
}
