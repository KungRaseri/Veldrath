using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Integration tests for the editorial endpoints (patch notes, lore articles, announcements).
///
/// Uses the shared <see cref="IntegrationTestCollection"/> factory.
/// All slug / username values are prefixed with a unique short code per test so that
/// multiple tests can run against the same shared SQLite database without colliding.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class EditorialEndpointTests(WebAppFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    // ── Token helpers ──────────────────────────────────────────────────────────────

    /// <summary>Registers a plain user and returns their access token.</summary>
    private async Task<string> GetTokenAsync(string username)
    {
        var email = $"{username.ToLower()}@test.com";
        await _client.PostAsJsonAsync("/api/auth/register",
            new { Email = email, Username = username, Password = "TestP@ssword123" });
        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new { Email = email, Password = "TestP@ssword123" });
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        return auth!.AccessToken;
    }

    /// <summary>
    /// Registers a user, assigns the <c>ContentEditor</c> role (which grants the
    /// <c>manage_content</c> permission), then re-logs in so the JWT carries that claim.
    /// </summary>
    private async Task<string> GetContentEditorTokenAsync(string username)
    {
        var email = $"{username.ToLower()}@test.com";
        await _client.PostAsJsonAsync("/api/auth/register",
            new { Email = email, Username = username, Password = "TestP@ssword123" });

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<PlayerAccount>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        if (!await roleManager.RoleExistsAsync("ContentEditor"))
            await roleManager.CreateAsync(new IdentityRole<Guid>("ContentEditor"));

        var user = await userManager.FindByNameAsync(username);
        await userManager.AddToRoleAsync(user!, "ContentEditor");

        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new { Email = email, Password = "TestP@ssword123" });
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        return auth!.AccessToken;
    }

    // ── Authentication / authorisation guards ─────────────────────────────────────

    [Fact]
    public async Task AdminCreatePatchNote_Should_Return_401_Without_Token()
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/editorial/admin/patch-notes",
            new { Slug = "edt-auth-401", Title = "Auth Test", Content = "x", Summary = "x", Version = "0.0" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminCreatePatchNote_Should_Return_403_For_Plain_User()
    {
        using var client = factory.CreateClient();
        var token = await GetTokenAsync("EdtAuth_PlainUser");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/editorial/admin/patch-notes",
            new { Slug = "edt-auth-403", Title = "Auth Test", Content = "x", Summary = "x", Version = "0.0" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Public endpoints — anonymous access ───────────────────────────────────────

    [Fact]
    public async Task GetPatchNotes_Public_Should_Be_Accessible_Anonymously()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/editorial/patch-notes");

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task GetLoreArticles_Public_Should_Be_Accessible_Anonymously()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/editorial/lore");

        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task GetAnnouncements_Editorial_Public_Should_Be_Accessible_Anonymously()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/editorial/announcements");

        response.IsSuccessStatusCode.Should().BeTrue();
    }

    // ── Patch notes — CRUD + publish lifecycle ─────────────────────────────────────

    [Fact]
    public async Task CreatePatchNote_Should_Return_201_With_Draft_Status()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetContentEditorTokenAsync("EdtPnCreate_User"));

        var response = await client.PostAsJsonAsync("/api/editorial/admin/patch-notes",
            new { Slug = "edt-pn-create", Title = "Patch 1.0", Content = "# Notes", Summary = "Summary", Version = "1.0" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await response.Content.ReadFromJsonAsync<PatchNoteDto>();
        dto!.Slug.Should().Be("edt-pn-create");
        dto.Title.Should().Be("Patch 1.0");
        dto.Status.Should().Be("Draft");
        dto.PublishedAt.Should().BeNull();
    }

    [Fact]
    public async Task CreatePatchNote_Should_Set_Location_Header()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetContentEditorTokenAsync("EdtPnLocation_User"));

        var response = await client.PostAsJsonAsync("/api/editorial/admin/patch-notes",
            new { Slug = "edt-pn-location", Title = "Location Test", Content = "x", Summary = "x", Version = "1.0" });

        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain("/api/editorial/admin/patch-notes/");
    }

    [Fact]
    public async Task CreatePatchNote_Duplicate_Slug_Should_Return_409()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetContentEditorTokenAsync("EdtPnDupe_User"));

        await client.PostAsJsonAsync("/api/editorial/admin/patch-notes",
            new { Slug = "edt-pn-dupe", Title = "First", Content = "x", Summary = "x", Version = "1.0" });

        var second = await client.PostAsJsonAsync("/api/editorial/admin/patch-notes",
            new { Slug = "edt-pn-dupe", Title = "Second", Content = "x", Summary = "x", Version = "1.0" });

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetPatchNote_Admin_Should_Return_Created_Note()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetContentEditorTokenAsync("EdtPnGet_User"));

        var created = await client.PostAsJsonAsync("/api/editorial/admin/patch-notes",
            new { Slug = "edt-pn-get", Title = "Get Test", Content = "Content here", Summary = "Summary", Version = "2.0" });
        var dto = await created.Content.ReadFromJsonAsync<PatchNoteDto>();

        var fetched = await client.GetFromJsonAsync<PatchNoteDto>($"/api/editorial/admin/patch-notes/{dto!.Id}");

        fetched!.Id.Should().Be(dto.Id);
        fetched.Slug.Should().Be("edt-pn-get");
        fetched.Content.Should().Be("Content here");
        fetched.Version.Should().Be("2.0");
    }

    [Fact]
    public async Task GetPatchNote_Admin_Unknown_Id_Should_Return_404()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetContentEditorTokenAsync("EdtPnNotFound_User"));

        var response = await client.GetAsync($"/api/editorial/admin/patch-notes/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Draft_PatchNote_Should_Not_Appear_In_Public_List()
    {
        // Create a draft patch note (no publish step).
        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetContentEditorTokenAsync("EdtPnDraftVis_User"));

        await adminClient.PostAsJsonAsync("/api/editorial/admin/patch-notes",
            new { Slug = "edt-pn-draft-vis", Title = "Draft Note", Content = "x", Summary = "x", Version = "0.1" });

        // Public endpoint must not return it.
        using var publicClient = factory.CreateClient();
        var result = await publicClient.GetFromJsonAsync<PagedResult<PatchNoteSummaryDto>>("/api/editorial/patch-notes");

        result!.Items.Should().NotContain(x => x.Slug == "edt-pn-draft-vis");
    }

    [Fact]
    public async Task Published_PatchNote_Should_Appear_In_Public_List()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetContentEditorTokenAsync("EdtPnPubVis_User"));

        var created = await client.PostAsJsonAsync("/api/editorial/admin/patch-notes",
            new { Slug = "edt-pn-pub-vis", Title = "Published Note", Content = "x", Summary = "x", Version = "1.1" });
        var dto = await created.Content.ReadFromJsonAsync<PatchNoteDto>();

        // Publish it.
        await client.PostAsync($"/api/editorial/admin/patch-notes/{dto!.Id}/publish", null);

        // Public endpoint must now include it.
        var result = await factory.CreateClient().GetFromJsonAsync<PagedResult<PatchNoteSummaryDto>>("/api/editorial/patch-notes");

        result!.Items.Should().Contain(x => x.Slug == "edt-pn-pub-vis");
    }

    [Fact]
    public async Task PublishToggle_Should_Set_PublishedAt_And_Return_Published_Status()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetContentEditorTokenAsync("EdtPnToggle_User"));

        var created = await client.PostAsJsonAsync("/api/editorial/admin/patch-notes",
            new { Slug = "edt-pn-toggle", Title = "Toggle Test", Content = "x", Summary = "x", Version = "1.2" });
        var dto = await created.Content.ReadFromJsonAsync<PatchNoteDto>();

        var toggled = await client.PostAsync($"/api/editorial/admin/patch-notes/{dto!.Id}/publish", null);
        toggled.EnsureSuccessStatusCode();
        var published = await toggled.Content.ReadFromJsonAsync<PatchNoteDto>();

        published!.Status.Should().Be("Published");
        published.PublishedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Publishtoggle_Twice_Should_Return_To_Draft()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetContentEditorTokenAsync("EdtPnUnpub_User"));

        var created = await client.PostAsJsonAsync("/api/editorial/admin/patch-notes",
            new { Slug = "edt-pn-unpub", Title = "Unpublish Test", Content = "x", Summary = "x", Version = "1.3" });
        var dto = await created.Content.ReadFromJsonAsync<PatchNoteDto>();

        // Publish.
        await client.PostAsync($"/api/editorial/admin/patch-notes/{dto!.Id}/publish", null);
        // Unpublish (second toggle).
        var response = await client.PostAsync($"/api/editorial/admin/patch-notes/{dto.Id}/publish", null);
        var back = await response.Content.ReadFromJsonAsync<PatchNoteDto>();

        back!.Status.Should().Be("Draft");
    }

    [Fact]
    public async Task DeletePatchNote_Should_Return_204_Then_404_On_Get()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetContentEditorTokenAsync("EdtPnDel_User"));

        var created = await client.PostAsJsonAsync("/api/editorial/admin/patch-notes",
            new { Slug = "edt-pn-del", Title = "Delete Test", Content = "x", Summary = "x", Version = "1.4" });
        var dto = await created.Content.ReadFromJsonAsync<PatchNoteDto>();

        var deleted = await client.DeleteAsync($"/api/editorial/admin/patch-notes/{dto!.Id}");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var notFound = await client.GetAsync($"/api/editorial/admin/patch-notes/{dto.Id}");
        notFound.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetPatchNoteBySlug_Public_Draft_Should_Return_404()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetContentEditorTokenAsync("EdtPnSlugDraft_User"));

        await client.PostAsJsonAsync("/api/editorial/admin/patch-notes",
            new { Slug = "edt-pn-slug-draft", Title = "Slug Draft", Content = "x", Summary = "x", Version = "0.2" });

        var response = await factory.CreateClient().GetAsync("/api/editorial/patch-notes/edt-pn-slug-draft");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetPatchNoteBySlug_Public_Published_Should_Return_200_With_Content()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetContentEditorTokenAsync("EdtPnSlugPub_User"));

        var created = await client.PostAsJsonAsync("/api/editorial/admin/patch-notes",
            new { Slug = "edt-pn-slug-pub", Title = "Slug Published", Content = "## Notes", Summary = "x", Version = "2.1" });
        var dto = await created.Content.ReadFromJsonAsync<PatchNoteDto>();
        await client.PostAsync($"/api/editorial/admin/patch-notes/{dto!.Id}/publish", null);

        var fetched = await factory.CreateClient().GetFromJsonAsync<PatchNoteDto>("/api/editorial/patch-notes/edt-pn-slug-pub");

        fetched!.Slug.Should().Be("edt-pn-slug-pub");
        fetched.Content.Should().Be("## Notes");
    }

    // ── Lore articles — CRUD + publish lifecycle ───────────────────────────────────

    [Fact]
    public async Task CreateLoreArticle_Should_Return_201_With_Draft_Status()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetContentEditorTokenAsync("EdtLoreCreate_User"));

        var response = await client.PostAsJsonAsync("/api/editorial/admin/lore",
            new { Slug = "edt-lore-create", Title = "Lore Entry", Content = "# Lore", Summary = "Lore summary", Category = "History" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await response.Content.ReadFromJsonAsync<LoreArticleDto>();
        dto!.Slug.Should().Be("edt-lore-create");
        dto.Category.Should().Be("History");
        dto.Status.Should().Be("Draft");
    }

    [Fact]
    public async Task CreateLoreArticle_Duplicate_Slug_Should_Return_409()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetContentEditorTokenAsync("EdtLoreDupe_User"));

        await client.PostAsJsonAsync("/api/editorial/admin/lore",
            new { Slug = "edt-lore-dupe", Title = "First", Content = "x", Summary = "x", Category = "History" });

        var second = await client.PostAsJsonAsync("/api/editorial/admin/lore",
            new { Slug = "edt-lore-dupe", Title = "Second", Content = "x", Summary = "x", Category = "History" });

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Draft_LoreArticle_Should_Not_Appear_In_Public_List()
    {
        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetContentEditorTokenAsync("EdtLoreDraftVis_User"));

        await adminClient.PostAsJsonAsync("/api/editorial/admin/lore",
            new { Slug = "edt-lore-draft-vis", Title = "Draft Lore", Content = "x", Summary = "x", Category = "Factions" });

        var result = await factory.CreateClient().GetFromJsonAsync<PagedResult<LoreArticleSummaryDto>>("/api/editorial/lore");

        result!.Items.Should().NotContain(x => x.Slug == "edt-lore-draft-vis");
    }

    [Fact]
    public async Task Published_LoreArticle_Should_Appear_In_Public_List()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetContentEditorTokenAsync("EdtLorePubVis_User"));

        var created = await client.PostAsJsonAsync("/api/editorial/admin/lore",
            new { Slug = "edt-lore-pub-vis", Title = "Published Lore", Content = "x", Summary = "x", Category = "Geography" });
        var dto = await created.Content.ReadFromJsonAsync<LoreArticleDto>();
        await client.PostAsync($"/api/editorial/admin/lore/{dto!.Id}/publish", null);

        var result = await factory.CreateClient().GetFromJsonAsync<PagedResult<LoreArticleSummaryDto>>("/api/editorial/lore");

        result!.Items.Should().Contain(x => x.Slug == "edt-lore-pub-vis");
    }

    [Fact]
    public async Task GetLoreBySlug_Public_Draft_Should_Return_404()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetContentEditorTokenAsync("EdtLoreSlugDraft_User"));

        await client.PostAsJsonAsync("/api/editorial/admin/lore",
            new { Slug = "edt-lore-slug-draft", Title = "Slug Draft Lore", Content = "x", Summary = "x", Category = "History" });

        var response = await factory.CreateClient().GetAsync("/api/editorial/lore/edt-lore-slug-draft");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetLoreBySlug_Public_Published_Should_Return_200()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetContentEditorTokenAsync("EdtLoreSlugPub_User"));

        var created = await client.PostAsJsonAsync("/api/editorial/admin/lore",
            new { Slug = "edt-lore-slug-pub", Title = "Published Lore Slug", Content = "## Lore", Summary = "x", Category = "Mythology" });
        var dto = await created.Content.ReadFromJsonAsync<LoreArticleDto>();
        await client.PostAsync($"/api/editorial/admin/lore/{dto!.Id}/publish", null);

        var fetched = await factory.CreateClient().GetFromJsonAsync<LoreArticleDto>("/api/editorial/lore/edt-lore-slug-pub");

        fetched!.Content.Should().Be("## Lore");
        fetched.Category.Should().Be("Mythology");
    }

    [Fact]
    public async Task LoreArticles_Category_Filter_Should_Return_Only_Matching_Entries()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetContentEditorTokenAsync("EdtLoreCatFilter_User"));

        // Create two articles in different categories and publish both.
        var r1 = await client.PostAsJsonAsync("/api/editorial/admin/lore",
            new { Slug = "edt-lore-cat-history", Title = "History Lore", Content = "x", Summary = "x", Category = "History" });
        var d1 = await r1.Content.ReadFromJsonAsync<LoreArticleDto>();
        await client.PostAsync($"/api/editorial/admin/lore/{d1!.Id}/publish", null);

        var r2 = await client.PostAsJsonAsync("/api/editorial/admin/lore",
            new { Slug = "edt-lore-cat-myths", Title = "Myths Lore", Content = "x", Summary = "x", Category = "Mythology" });
        var d2 = await r2.Content.ReadFromJsonAsync<LoreArticleDto>();
        await client.PostAsync($"/api/editorial/admin/lore/{d2!.Id}/publish", null);

        var result = await factory.CreateClient().GetFromJsonAsync<PagedResult<LoreArticleSummaryDto>>("/api/editorial/lore?category=History");

        result!.Items.Should().Contain(x => x.Slug == "edt-lore-cat-history");
        result.Items.Should().NotContain(x => x.Slug == "edt-lore-cat-myths");
    }

    [Fact]
    public async Task DeleteLoreArticle_Should_Return_204_Then_404_On_Get()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetContentEditorTokenAsync("EdtLoreDel_User"));

        var created = await client.PostAsJsonAsync("/api/editorial/admin/lore",
            new { Slug = "edt-lore-del", Title = "Delete Lore", Content = "x", Summary = "x", Category = "History" });
        var dto = await created.Content.ReadFromJsonAsync<LoreArticleDto>();

        var deleted = await client.DeleteAsync($"/api/editorial/admin/lore/{dto!.Id}");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var notFound = await client.GetAsync($"/api/editorial/admin/lore/{dto.Id}");
        notFound.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Announcements — CRUD + publish lifecycle ───────────────────────────────────

    [Fact]
    public async Task CreateAnnouncement_Should_Return_201_With_Draft_Status()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetContentEditorTokenAsync("EdtAnnCreate_User"));

        var response = await client.PostAsJsonAsync("/api/editorial/admin/announcements",
            new { Title = "Server Maintenance", Body = "Scheduled downtime this weekend." });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await response.Content.ReadFromJsonAsync<EditorialAnnouncementDto>();
        dto!.Title.Should().Be("Server Maintenance");
        dto.Status.Should().Be("Draft");
        dto.PublishedAt.Should().BeNull();
    }

    [Fact]
    public async Task Draft_Announcement_Should_Not_Appear_In_Public_List()
    {
        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetContentEditorTokenAsync("EdtAnnDraftVis_User"));

        var created = await adminClient.PostAsJsonAsync("/api/editorial/admin/announcements",
            new { Title = "Hidden Announcement", Body = "Should not appear publicly." });
        var dto = await created.Content.ReadFromJsonAsync<EditorialAnnouncementDto>();

        var result = await factory.CreateClient().GetFromJsonAsync<PagedResult<EditorialAnnouncementDto>>("/api/editorial/announcements");

        result!.Items.Should().NotContain(x => x.Id == dto!.Id);
    }

    [Fact]
    public async Task Published_Announcement_Should_Appear_In_Public_List()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetContentEditorTokenAsync("EdtAnnPubVis_User"));

        var created = await client.PostAsJsonAsync("/api/editorial/admin/announcements",
            new { Title = "Visible Announcement", Body = "Server is live!" });
        var dto = await created.Content.ReadFromJsonAsync<EditorialAnnouncementDto>();
        await client.PostAsync($"/api/editorial/admin/announcements/{dto!.Id}/publish", null);

        var result = await factory.CreateClient().GetFromJsonAsync<PagedResult<EditorialAnnouncementDto>>("/api/editorial/announcements");

        result!.Items.Should().Contain(x => x.Id == dto.Id);
    }

    [Fact]
    public async Task AnnouncementPublishToggle_Should_Set_PublishedAt()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetContentEditorTokenAsync("EdtAnnToggle_User"));

        var created = await client.PostAsJsonAsync("/api/editorial/admin/announcements",
            new { Title = "Toggle Ann", Body = "Toggle test." });
        var dto = await created.Content.ReadFromJsonAsync<EditorialAnnouncementDto>();

        var toggled = await client.PostAsync($"/api/editorial/admin/announcements/{dto!.Id}/publish", null);
        toggled.EnsureSuccessStatusCode();
        var published = await toggled.Content.ReadFromJsonAsync<EditorialAnnouncementDto>();

        published!.Status.Should().Be("Published");
        published.PublishedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteAnnouncement_Should_Return_204_Then_404_On_Get()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetContentEditorTokenAsync("EdtAnnDel_User"));

        var created = await client.PostAsJsonAsync("/api/editorial/admin/announcements",
            new { Title = "Delete Ann", Body = "Will be deleted." });
        var dto = await created.Content.ReadFromJsonAsync<EditorialAnnouncementDto>();

        var deleted = await client.DeleteAsync($"/api/editorial/admin/announcements/{dto!.Id}");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var notFound = await client.GetAsync($"/api/editorial/admin/announcements/{dto.Id}");
        notFound.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAnnouncement_Admin_Unknown_Id_Should_Return_404()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetContentEditorTokenAsync("EdtAnnNF_User"));

        var response = await client.GetAsync($"/api/editorial/admin/announcements/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
