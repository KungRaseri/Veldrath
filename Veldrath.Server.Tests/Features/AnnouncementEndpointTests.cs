using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Veldrath.Contracts.Announcements;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Seeds announcement data once into a dedicated in-memory database for
/// use by <see cref="AnnouncementEndpointTests"/>.
/// Seeded rows cover: one active, one inactive, one expired, one unpinned
/// regular, one pinned, and one with known field values.
/// </summary>
public sealed class AnnouncementsFixture : IAsyncLifetime
{
    /// <summary>Gets the web application factory used across all tests in this fixture.</summary>
    public WebAppFactory Factory { get; }

    /// <summary>Gets the shared HTTP client for sending test requests.</summary>
    public HttpClient Client { get; private set; } = null!;

    /// <summary>Initializes a new instance of <see cref="AnnouncementsFixture"/> with the shared collection factory.</summary>
    public AnnouncementsFixture(WebAppFactory factory) => Factory = factory;

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        Client = Factory.CreateClient();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        db.Announcements.AddRange(
            // Active — must appear in results.
            new Announcement { Title = "Welcome!",   Body = "Server is live.",   Category = "News",   IsActive = true,  PublishedAt = DateTimeOffset.UtcNow.AddMinutes(-5) },
            // Inactive — must be excluded.
            new Announcement { Title = "Hidden",     Body = "Should not appear.",                    IsActive = false },
            // Expired — must be excluded.
            new Announcement { Title = "Old News",   Body = "Expired.",                             IsActive = true,  ExpiresAt   = DateTimeOffset.UtcNow.AddMinutes(-1) },
            // Ordering: unpinned entry (comes after all pinned entries).
            new Announcement { Title = "Regular",    Body = "Body",                                 IsActive = true,  IsPinned    = false, PublishedAt = DateTimeOffset.UtcNow.AddMinutes(-2) },
            // Ordering: most-recent pinned entry — must be first in results.
            new Announcement { Title = "Pinned",     Body = "Body",                                 IsActive = true,  IsPinned    = true,  PublishedAt = DateTimeOffset.UtcNow.AddMinutes(-10) },
            // Field-shape: older pinned entry used to verify DTO mapping.
            new Announcement { Title = "Field Test", Body = "Body text here.",   Category = "Update", IsActive = true,  IsPinned    = true,  PublishedAt = DateTimeOffset.UtcNow.AddHours(-1) }
        );

        await db.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public Task DisposeAsync()
    {
        Client.Dispose();
        return Task.CompletedTask;
    }
}

/// <summary>
/// Integration tests for <c>GET /api/announcements</c> using a pre-seeded database.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class AnnouncementEndpointTests(AnnouncementsFixture fixture) : IClassFixture<AnnouncementsFixture>
{
    private readonly HttpClient _client = fixture.Client;

    [Fact]
    public async Task GetAnnouncements_Should_Return_Active_Announcements()
    {
        var dtos = await _client.GetFromJsonAsync<AnnouncementDto[]>("/api/announcements");

        dtos.Should().NotBeNull();
        dtos!.Should().Contain(d => d.Title == "Welcome!");
    }

    [Fact]
    public async Task GetAnnouncements_Should_Not_Return_Inactive_Announcements()
    {
        var dtos = await _client.GetFromJsonAsync<AnnouncementDto[]>("/api/announcements");

        dtos!.Should().NotContain(d => d.Title == "Hidden");
    }

    [Fact]
    public async Task GetAnnouncements_Should_Not_Return_Expired_Announcements()
    {
        var dtos = await _client.GetFromJsonAsync<AnnouncementDto[]>("/api/announcements");

        dtos!.Should().NotContain(d => d.Title == "Old News");
    }

    [Fact]
    public async Task GetAnnouncements_Should_Return_Pinned_First()
    {
        var dtos = await _client.GetFromJsonAsync<AnnouncementDto[]>("/api/announcements");

        // "Pinned" was seeded with a more-recent PublishedAt than "Field Test", so it
        // wins the tie-break and must occupy position 0 within the pinned group.
        dtos!.Should().NotBeEmpty();
        dtos![0].IsPinned.Should().BeTrue();
        dtos![0].Title.Should().Be("Pinned");
    }

    [Fact]
    public async Task GetAnnouncements_Should_Be_Accessible_Without_Authentication()
    {
        var response = await _client.GetAsync("/api/announcements");

        // Endpoint is AllowAnonymous — no auth header needed.
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAnnouncements_Should_Include_Correct_Fields()
    {
        var dtos = await _client.GetFromJsonAsync<AnnouncementDto[]>("/api/announcements");

        var dto = dtos!.Single(d => d.Title == "Field Test");
        dto.Body.Should().Be("Body text here.");
        dto.Category.Should().Be("Update");
        dto.IsPinned.Should().BeTrue();
    }
}

/// <summary>
/// Integration test for <c>GET /api/announcements</c> against an empty database
/// (no seeded announcements). Uses a dedicated factory instance per test run.
/// </summary>
[Trait("Category", "Integration")]
public sealed class AnnouncementEmptyEndpointTests : IAsyncLifetime
{
    private WebAppFactory _factory = null!;
    private HttpClient _client = null!;

    /// <inheritdoc/>
    public Task InitializeAsync()
    {
        _factory = new WebAppFactory();
        _client  = _factory.CreateClient();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task GetAnnouncements_Should_Return_200_And_Empty_List_When_No_Announcements()
    {
        var response = await _client.GetAsync("/api/announcements");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dtos = await response.Content.ReadFromJsonAsync<AnnouncementDto[]>();
        dtos.Should().NotBeNull();
        dtos!.Should().BeEmpty();
    }
}
