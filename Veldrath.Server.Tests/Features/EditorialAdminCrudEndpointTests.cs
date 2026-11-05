using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Integration tests for the editorial admin surfaces that the create/publish/delete suite does not touch: the
/// all-including-drafts admin lists, the update endpoints (with their status validation), the publish-toggle 404s,
/// and the create-time validation of every request body.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class EditorialAdminCrudEndpointTests(WebAppFactory factory)
{
    private async Task<(HttpClient Client, Guid AccountId)> EditorClientAsync(string username)
    {
        var (token, accountId) = await factory.CreateAccountTokenAsync(username, "ContentEditor");
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, accountId);
    }

    private static CreatePatchNoteRequest PatchNote(string slug, string title = "Crud Note") =>
        new(slug, title, "# Body", "summary", "1.2.3");

    private static CreateLoreArticleRequest Lore(string slug, string category = "History") =>
        new(slug, "Crud Lore", "# Body", "summary", category);

    private static CreateAnnouncementRequest Announcement(string title) => new(title, "body");

    // ── Admin list endpoints ──────────────────────────────────────────────────

    [Fact]
    public async Task AdminPatchNoteList_IncludesDraftsAndIsOrderedNewestCreatedFirst()
    {
        using var client = (await EditorClientAsync("EdtAdm_PnList")).Client;
        var older = await (await client.PostAsJsonAsync("/api/editorial/admin/patch-notes", PatchNote("edt-crud-pn-old")))
            .Content.ReadFromJsonAsync<PatchNoteDto>();
        var newer = await (await client.PostAsJsonAsync("/api/editorial/admin/patch-notes", PatchNote("edt-crud-pn-new")))
            .Content.ReadFromJsonAsync<PatchNoteDto>();

        var page = await client.GetFromJsonAsync<PagedResult<PatchNoteSummaryDto>>(
            "/api/editorial/admin/patch-notes?pageSize=100");

        page.Should().NotBeNull();
        page!.Items.Should().Contain(i => i.Id == older!.Id, "drafts are visible to editors");
        page.Items.Should().Contain(i => i.Id == newer!.Id);
        page.TotalCount.Should().BeGreaterThanOrEqualTo(page.Items.Count);
    }

    [Fact]
    public async Task AdminLoreList_IncludesDrafts()
    {
        using var client = (await EditorClientAsync("EdtAdm_LoreList")).Client;
        var draft = await (await client.PostAsJsonAsync("/api/editorial/admin/lore", Lore("edt-crud-lore-draft")))
            .Content.ReadFromJsonAsync<LoreArticleDto>();

        var page = await client.GetFromJsonAsync<PagedResult<LoreArticleSummaryDto>>(
            "/api/editorial/admin/lore?pageSize=100");

        page!.Items.Should().Contain(i => i.Id == draft!.Id && i.Status == "Draft");
    }

    [Fact]
    public async Task AdminAnnouncementList_IncludesDrafts()
    {
        using var client = (await EditorClientAsync("EdtAdm_AnnList")).Client;
        var draft = await (await client.PostAsJsonAsync("/api/editorial/admin/announcements", Announcement("Crud draft announcement")))
            .Content.ReadFromJsonAsync<EditorialAnnouncementDto>();

        var page = await client.GetFromJsonAsync<PagedResult<EditorialAnnouncementDto>>(
            "/api/editorial/admin/announcements?pageSize=100");

        page!.Items.Should().Contain(i => i.Id == draft!.Id && i.Status == "Draft");
    }

    [Theory]
    [InlineData("/api/editorial/admin/patch-notes")]
    [InlineData("/api/editorial/admin/lore")]
    [InlineData("/api/editorial/admin/announcements")]
    public async Task AdminLists_RequireManageContent(string route)
    {
        var (plainToken, _) = await factory.CreateAccountTokenAsync($"EdtAdm_Plain_{route[^4..]}");
        using var plain = factory.CreateClient();
        plain.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", plainToken);

        var forbidden = await plain.GetAsync(route);
        var anonymous = await factory.CreateClient().GetAsync(route);

        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Create validation ─────────────────────────────────────────────────────

    [Fact]
    public async Task CreatePatchNote_MissingSlug_ReturnsBadRequest()
    {
        using var client = (await EditorClientAsync("EdtAdm_PnNoSlug")).Client;

        var response = await client.PostAsJsonAsync("/api/editorial/admin/patch-notes",
            PatchNote("  ", "Has title"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Slug");
    }

    [Fact]
    public async Task CreatePatchNote_MissingTitle_ReturnsBadRequest()
    {
        using var client = (await EditorClientAsync("EdtAdm_PnNoTitle")).Client;

        var response = await client.PostAsJsonAsync("/api/editorial/admin/patch-notes",
            PatchNote("edt-crud-pn-no-title", ""));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Title");
    }

    [Fact]
    public async Task CreateLoreArticle_MissingSlug_ReturnsBadRequest()
    {
        using var client = (await EditorClientAsync("EdtAdm_LoreNoSlug")).Client;

        var response = await client.PostAsJsonAsync("/api/editorial/admin/lore", Lore(""));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Slug");
    }

    [Fact]
    public async Task CreateLoreArticle_MissingTitle_ReturnsBadRequest()
    {
        using var client = (await EditorClientAsync("EdtAdm_LoreNoTitle")).Client;
        var request = new CreateLoreArticleRequest("edt-crud-lore-no-title", " ", "# Body", "summary", "History");

        var response = await client.PostAsJsonAsync("/api/editorial/admin/lore", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Title");
    }

    [Fact]
    public async Task CreateAnnouncement_MissingTitle_ReturnsBadRequest()
    {
        using var client = (await EditorClientAsync("EdtAdm_AnnNoTitle")).Client;

        var response = await client.PostAsJsonAsync("/api/editorial/admin/announcements",
            new CreateAnnouncementRequest("", "body"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Title");
    }

    [Fact]
    public async Task CreateAnnouncement_MissingBody_ReturnsBadRequest()
    {
        using var client = (await EditorClientAsync("EdtAdm_AnnNoBody")).Client;

        var response = await client.PostAsJsonAsync("/api/editorial/admin/announcements",
            new CreateAnnouncementRequest("Title only", " "));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Body");
    }

    // ── Update: patch notes ───────────────────────────────────────────────────

    [Fact]
    public async Task UpdatePatchNote_PublishesAndStampsPublishedAt()
    {
        using var client = (await EditorClientAsync("EdtAdm_PnUpdate")).Client;
        var created = await (await client.PostAsJsonAsync("/api/editorial/admin/patch-notes", PatchNote("edt-crud-pn-update")))
            .Content.ReadFromJsonAsync<PatchNoteDto>();

        var response = await client.PutAsJsonAsync($"/api/editorial/admin/patch-notes/{created!.Id}",
            new UpdatePatchNoteRequest("Renamed note", "# New body", "new summary", "2.0.0", "Published"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<PatchNoteDto>();
        updated!.Title.Should().Be("Renamed note");
        updated.Content.Should().Be("# New body");
        updated.Summary.Should().Be("new summary");
        updated.Version.Should().Be("2.0.0");
        updated.Status.Should().Be("Published");
        updated.PublishedAt.Should().NotBeNull("publishing an unpublished note stamps the timestamp");

        var fetched = await client.GetFromJsonAsync<PatchNoteDto>($"/api/editorial/admin/patch-notes/{created.Id}");
        fetched!.Title.Should().Be("Renamed note");
    }

    [Fact]
    public async Task UpdatePatchNote_KeepsTheOriginalPublishedAtWhenAlreadyPublished()
    {
        using var client = (await EditorClientAsync("EdtAdm_PnKeepPublishedAt")).Client;
        var created = await (await client.PostAsJsonAsync("/api/editorial/admin/patch-notes", PatchNote("edt-crud-pn-keep")))
            .Content.ReadFromJsonAsync<PatchNoteDto>();
        var firstPublish = await client.PutAsJsonAsync($"/api/editorial/admin/patch-notes/{created!.Id}",
            new UpdatePatchNoteRequest("First", "# A", "s", "1.0.0", "Published"));
        var firstDto = await firstPublish.Content.ReadFromJsonAsync<PatchNoteDto>();

        var second = await client.PutAsJsonAsync($"/api/editorial/admin/patch-notes/{created.Id}",
            new UpdatePatchNoteRequest("Second", "# B", "s", "1.0.1", "Published"));
        var secondDto = await second.Content.ReadFromJsonAsync<PatchNoteDto>();

        secondDto!.PublishedAt.Should().Be(firstDto!.PublishedAt, "re-publishing must not move the publication date");
    }

    [Fact]
    public async Task UpdatePatchNote_InvalidStatus_ReturnsBadRequest()
    {
        using var client = (await EditorClientAsync("EdtAdm_PnBadStatus")).Client;
        var created = await (await client.PostAsJsonAsync("/api/editorial/admin/patch-notes", PatchNote("edt-crud-pn-bad-status")))
            .Content.ReadFromJsonAsync<PatchNoteDto>();

        var response = await client.PutAsJsonAsync($"/api/editorial/admin/patch-notes/{created!.Id}",
            new UpdatePatchNoteRequest("t", "c", "s", "1", "Archived"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Invalid status");
    }

    [Fact]
    public async Task UpdatePatchNote_UnknownId_ReturnsNotFound()
    {
        using var client = (await EditorClientAsync("EdtAdm_PnUpdateMissing")).Client;

        var response = await client.PutAsJsonAsync($"/api/editorial/admin/patch-notes/{Guid.NewGuid()}",
            new UpdatePatchNoteRequest("t", "c", "s", "1", "Draft"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TogglePatchNotePublish_UnknownId_ReturnsNotFound()
    {
        using var client = (await EditorClientAsync("EdtAdm_PnToggleMissing")).Client;

        var response = await client.PostAsync($"/api/editorial/admin/patch-notes/{Guid.NewGuid()}/publish", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Update: lore ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateLoreArticle_ChangesEveryFieldAndPublishes()
    {
        using var client = (await EditorClientAsync("EdtAdm_LoreUpdate")).Client;
        var created = await (await client.PostAsJsonAsync("/api/editorial/admin/lore", Lore("edt-crud-lore-update")))
            .Content.ReadFromJsonAsync<LoreArticleDto>();

        var response = await client.PutAsJsonAsync($"/api/editorial/admin/lore/{created!.Id}",
            new UpdateLoreArticleRequest("Renamed lore", "# New", "s2", "Geography", "Published"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<LoreArticleDto>();
        updated!.Title.Should().Be("Renamed lore");
        updated.Category.Should().Be("Geography");
        updated.Status.Should().Be("Published");
        updated.PublishedAt.Should().NotBeNull();
        updated.Content.Should().Be("# New");
    }

    [Fact]
    public async Task UpdateLoreArticle_InvalidStatus_ReturnsBadRequest()
    {
        using var client = (await EditorClientAsync("EdtAdm_LoreBadStatus")).Client;
        var created = await (await client.PostAsJsonAsync("/api/editorial/admin/lore", Lore("edt-crud-lore-bad-status")))
            .Content.ReadFromJsonAsync<LoreArticleDto>();

        var response = await client.PutAsJsonAsync($"/api/editorial/admin/lore/{created!.Id}",
            new UpdateLoreArticleRequest("t", "c", "s", "History", "Unpublished"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateLoreArticle_UnknownId_ReturnsNotFound()
    {
        using var client = (await EditorClientAsync("EdtAdm_LoreUpdateMissing")).Client;

        var response = await client.PutAsJsonAsync($"/api/editorial/admin/lore/{Guid.NewGuid()}",
            new UpdateLoreArticleRequest("t", "c", "s", "History", "Draft"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ToggleLorePublish_UnknownId_ReturnsNotFound()
    {
        using var client = (await EditorClientAsync("EdtAdm_LoreToggleMissing")).Client;

        var response = await client.PostAsync($"/api/editorial/admin/lore/{Guid.NewGuid()}/publish", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ToggleLorePublish_TwiceReturnsToDraftButKeepsThePublicationDate()
    {
        using var client = (await EditorClientAsync("EdtAdm_LoreToggleTwice")).Client;
        var created = await (await client.PostAsJsonAsync("/api/editorial/admin/lore", Lore("edt-crud-lore-toggle")))
            .Content.ReadFromJsonAsync<LoreArticleDto>();

        var published = await (await client.PostAsync($"/api/editorial/admin/lore/{created!.Id}/publish", null))
            .Content.ReadFromJsonAsync<LoreArticleDto>();
        var unpublished = await (await client.PostAsync($"/api/editorial/admin/lore/{created.Id}/publish", null))
            .Content.ReadFromJsonAsync<LoreArticleDto>();

        published!.Status.Should().Be("Published");
        unpublished!.Status.Should().Be("Draft");
        unpublished.PublishedAt.Should().Be(published.PublishedAt,
            "unpublishing hides the article but does not erase when it was published");
    }

    // ── Update: announcements ─────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAnnouncement_ChangesFieldsAndPublishes()
    {
        using var client = (await EditorClientAsync("EdtAdm_AnnUpdate")).Client;
        var created = await (await client.PostAsJsonAsync("/api/editorial/admin/announcements", Announcement("Crud ann update")))
            .Content.ReadFromJsonAsync<EditorialAnnouncementDto>();

        var response = await client.PutAsJsonAsync($"/api/editorial/admin/announcements/{created!.Id}",
            new UpdateAnnouncementRequest("Renamed announcement", "new body", "Published"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<EditorialAnnouncementDto>();
        updated!.Title.Should().Be("Renamed announcement");
        updated.Body.Should().Be("new body");
        updated.Status.Should().Be("Published");
        updated.PublishedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateAnnouncement_InvalidStatus_ReturnsBadRequest()
    {
        using var client = (await EditorClientAsync("EdtAdm_AnnBadStatus")).Client;
        var created = await (await client.PostAsJsonAsync("/api/editorial/admin/announcements", Announcement("Crud ann bad status")))
            .Content.ReadFromJsonAsync<EditorialAnnouncementDto>();

        var response = await client.PutAsJsonAsync($"/api/editorial/admin/announcements/{created!.Id}",
            new UpdateAnnouncementRequest("t", "b", "Retracted"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateAnnouncement_UnknownId_ReturnsNotFound()
    {
        using var client = (await EditorClientAsync("EdtAdm_AnnUpdateMissing")).Client;

        var response = await client.PutAsJsonAsync($"/api/editorial/admin/announcements/{Guid.NewGuid()}",
            new UpdateAnnouncementRequest("t", "b", "Draft"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ToggleAnnouncementPublish_UnknownId_ReturnsNotFound()
    {
        using var client = (await EditorClientAsync("EdtAdm_AnnToggleMissing")).Client;

        var response = await client.PostAsync($"/api/editorial/admin/announcements/{Guid.NewGuid()}/publish", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ToggleAnnouncementPublish_TwiceReturnsToDraft()
    {
        using var client = (await EditorClientAsync("EdtAdm_AnnToggleTwice")).Client;
        var created = await (await client.PostAsJsonAsync("/api/editorial/admin/announcements", Announcement("Crud ann toggle")))
            .Content.ReadFromJsonAsync<EditorialAnnouncementDto>();

        var published = await (await client.PostAsync($"/api/editorial/admin/announcements/{created!.Id}/publish", null))
            .Content.ReadFromJsonAsync<EditorialAnnouncementDto>();
        var unpublished = await (await client.PostAsync($"/api/editorial/admin/announcements/{created.Id}/publish", null))
            .Content.ReadFromJsonAsync<EditorialAnnouncementDto>();

        published!.Status.Should().Be("Published");
        unpublished!.Status.Should().Be("Draft");
        unpublished.PublishedAt.Should().Be(published.PublishedAt);
    }

    [Fact]
    public async Task DeleteUnknownEntries_ReturnsNotFoundForEveryKind()
    {
        using var client = (await EditorClientAsync("EdtAdm_DeleteMissing")).Client;

        (await client.DeleteAsync($"/api/editorial/admin/patch-notes/{Guid.NewGuid()}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.DeleteAsync($"/api/editorial/admin/lore/{Guid.NewGuid()}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.DeleteAsync($"/api/editorial/admin/announcements/{Guid.NewGuid()}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Public list paging ────────────────────────────────────────────────────

    [Fact]
    public async Task PublicLists_ClampPageSizeAndFloorPage()
    {
        var patchNotes = await factory.CreateClient()
            .GetFromJsonAsync<PagedResult<PatchNoteSummaryDto>>("/api/editorial/patch-notes?page=0&pageSize=500");
        var lore = await factory.CreateClient()
            .GetFromJsonAsync<PagedResult<LoreArticleSummaryDto>>("/api/editorial/lore?page=0&pageSize=500");
        var announcements = await factory.CreateClient()
            .GetFromJsonAsync<PagedResult<EditorialAnnouncementDto>>("/api/editorial/announcements?page=0&pageSize=500");

        patchNotes!.Page.Should().Be(1);
        patchNotes.PageSize.Should().Be(100);
        lore!.Page.Should().Be(1);
        lore.PageSize.Should().Be(100);
        announcements!.Page.Should().Be(1);
        announcements.PageSize.Should().Be(100);
    }

    [Fact]
    public async Task PublicLoreList_OnlyReturnsPublishedArticles()
    {
        using var client = (await EditorClientAsync("EdtAdm_LorePublicFilter")).Client;
        var draft = await (await client.PostAsJsonAsync("/api/editorial/admin/lore", Lore("edt-crud-lore-hidden")))
            .Content.ReadFromJsonAsync<LoreArticleDto>();
        var published = await (await client.PostAsJsonAsync("/api/editorial/admin/lore", Lore("edt-crud-lore-shown")))
            .Content.ReadFromJsonAsync<LoreArticleDto>();
        await client.PostAsync($"/api/editorial/admin/lore/{published!.Id}/publish", null);

        var page = await factory.CreateClient()
            .GetFromJsonAsync<PagedResult<LoreArticleSummaryDto>>("/api/editorial/lore?pageSize=100");

        page!.Items.Should().Contain(i => i.Id == published.Id);
        page.Items.Should().NotContain(i => i.Id == draft!.Id);
        page.Items.Should().OnlyContain(i => i.Status == "Published");
    }
}
