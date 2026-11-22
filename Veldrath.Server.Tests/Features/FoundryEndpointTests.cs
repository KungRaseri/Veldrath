using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

[Collection("Integration")]
[Trait("Category", "Integration")]
public class FoundryEndpointTests(WebAppFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    // Helpers
    private async Task<string> GetTokenAsync(string username)
    {
        return (await factory.CreateAccountTokenAsync(username)).Token;
    }

    // Register the user, assign "Curator" via UserManager, then re-login so the JWT
    // carries the role claim (IssueTokenPairAsync calls IsInRoleAsync at login time).
    private async Task<string> GetCuratorTokenAsync(string username)
    {
        var (token, _) = await factory.CreateAccountTokenAsync(username, "Curator");
        return token;
    }

    /// <summary>
    /// A payload that passes the content rules, so an approval can actually promote it.
    /// </summary>
    /// <remarks>
    /// Approval writes the payload through the content write path, so a submission carrying <c>{}</c> can no
    /// longer be approved — that is the gate working, not a broken fixture.
    /// </remarks>
    private static string PromotableItem(string slug) =>
        $"{{\"slug\":\"{slug}\",\"displayName\":\"{slug}\",\"typeKey\":\"material\",\"itemType\":\"material\",\"rarityWeight\":50,\"isActive\":true}}";

    private async Task<FoundrySubmissionDto> CreateSubmissionAsync(
        HttpClient client, string contentType, string title, string payload = "{}")
    {
        var response = await client.PostAsJsonAsync("/api/foundry/submissions",
            new { ContentType = contentType, Title = title, Payload = payload, Description = "Test submission" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<FoundrySubmissionDto>())!;
    }

    // Create
    [Fact]
    public async Task CreateSubmission_Should_Return_201_With_Submission_Details()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndCreate_User"));

        var response = await _client.PostAsJsonAsync("/api/foundry/submissions",
            new { ContentType = "Item", Title = "FndCreate_Title", Payload = "{}", Description = "A sword." });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await response.Content.ReadFromJsonAsync<FoundrySubmissionDto>();
        dto!.Id.Should().NotBe(Guid.Empty);
        dto.Title.Should().Be("FndCreate_Title");
        dto.ContentType.Should().Be("Item");
        dto.Status.Should().Be("Pending");
        dto.SubmitterName.Should().Be("FndCreate_User");
        dto.VoteScore.Should().Be(0);
        dto.Description.Should().Be("A sword.");
    }

    [Fact]
    public async Task CreateSubmission_Should_Require_Authentication()
    {
        using var unauthClient = factory.CreateClient();
        var response = await unauthClient.PostAsJsonAsync("/api/foundry/submissions",
            new { ContentType = "Item", Title = "Unauthorized", Payload = "{}" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateSubmission_Should_Return_400_For_Unknown_ContentType()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndBadType_User"));

        var response = await _client.PostAsJsonAsync("/api/foundry/submissions",
            new { ContentType = "NotARealType", Title = "BadType", Payload = "{}" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateSubmission_Should_Accept_Language_ContentType()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndLanguage_User"));

        var response = await _client.PostAsJsonAsync("/api/foundry/submissions",
            new { ContentType = "language", Title = "FndLanguage_Title", Payload = "{}" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await response.Content.ReadFromJsonAsync<FoundrySubmissionDto>();
        dto!.ContentType.Should().Be("Language");
    }

    [Fact]
    public async Task CreateSubmission_Should_Set_Location_Header()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndLocation_User"));

        var response = await _client.PostAsJsonAsync("/api/foundry/submissions",
            new { ContentType = "Spell", Title = "FndLocation_Spell", Payload = "{}" });

        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain("/api/foundry/submissions/");
    }

    // List
    [Fact]
    public async Task ListSubmissions_Should_Require_Authentication()
    {
        using var anonymous = factory.CreateClient();

        var response = await anonymous.GetAsync("/api/foundry/submissions");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListSubmissions_Should_Return_Paged_Result()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndList_Paged"));

        var response = await client.GetAsync("/api/foundry/submissions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<FoundrySubmissionSummaryDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().NotBeNull();
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task ListSubmissions_Should_Include_Created_Submission()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndList_User"));

        await CreateSubmissionAsync(client, "Spell", "FndList_Fireball");

        var result = await client.GetFromJsonAsync<PagedResult<FoundrySubmissionSummaryDto>>(
            "/api/foundry/submissions");

        result!.Items.Should().Contain(s => s.Title == "FndList_Fireball");
        result.TotalCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task ListSubmissions_Should_Filter_By_ContentType()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndFilterCT_User"));

        await CreateSubmissionAsync(client, "Item", "FndFilterCT_Item");

        // Filter by Spell — the Item submission should not appear
        var result = await client.GetFromJsonAsync<PagedResult<FoundrySubmissionSummaryDto>>(
            "/api/foundry/submissions?contentType=Spell");

        result!.Items.Should().NotContain(s => s.Title == "FndFilterCT_Item");
    }

    [Fact]
    public async Task ListSubmissions_Should_Filter_By_Status()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndFilterSt_User"));

        await CreateSubmissionAsync(client, "Quest", "FndFilterSt_Quest");

        // New submissions are Pending; querying Approved should exclude them
        var result = await client.GetFromJsonAsync<PagedResult<FoundrySubmissionSummaryDto>>(
            "/api/foundry/submissions?status=Approved");

        result!.Items.Should().NotContain(s => s.Title == "FndFilterSt_Quest");
    }

    [Fact]
    public async Task ListSubmissions_Should_Apply_Search_Filter()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndSearch_User"));

        await CreateSubmissionAsync(client, "Ability", "FndSearch_FireStrike");

        var found = await client.GetFromJsonAsync<PagedResult<FoundrySubmissionSummaryDto>>(
            "/api/foundry/submissions?search=FndSearch_Fire");
        var notFound = await client.GetFromJsonAsync<PagedResult<FoundrySubmissionSummaryDto>>(
            "/api/foundry/submissions?search=FndSearch_Ice");

        found!.Items.Should().Contain(s => s.Title == "FndSearch_FireStrike");
        notFound!.Items.Should().NotContain(s => s.Title == "FndSearch_FireStrike");
    }

    [Fact]
    public async Task ListSubmissions_Should_Narrow_To_The_Caller_With_Mine()
    {
        using var author = factory.CreateClient();
        author.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndMine_Author"));

        using var stranger = factory.CreateClient();
        stranger.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndMine_Stranger"));

        await CreateSubmissionAsync(author, "Item", "FndMine_AuthorItem");
        await CreateSubmissionAsync(stranger, "Item", "FndMine_StrangerItem");

        var mine = await author.GetFromJsonAsync<PagedResult<FoundrySubmissionSummaryDto>>(
            "/api/foundry/submissions?mine=true");

        mine!.Items.Should().Contain(s => s.Title == "FndMine_AuthorItem");
        mine.Items.Should().NotContain(s => s.Title == "FndMine_StrangerItem");
    }

    [Fact]
    public async Task ListSubmissions_Should_Hide_Pending_Work_From_Other_Accounts()
    {
        using var author = factory.CreateClient();
        author.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndHide_Author"));

        using var stranger = factory.CreateClient();
        stranger.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndHide_Stranger"));

        await CreateSubmissionAsync(author, "Item", "FndHide_PendingItem");

        // A player sees their own work at any status and other people's only once it is in the catalog.
        var feed = await stranger.GetFromJsonAsync<PagedResult<FoundrySubmissionSummaryDto>>(
            "/api/foundry/submissions");

        feed!.Items.Should().NotContain(s => s.Title == "FndHide_PendingItem");
    }

    [Fact]
    public async Task ListSubmissions_Should_Show_Every_Submission_To_A_Reviewer()
    {
        using var author = factory.CreateClient();
        author.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndReviewerFeed_Author"));

        using var curator = factory.CreateClient();
        curator.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetCuratorTokenAsync("FndReviewerFeed_Curator"));

        await CreateSubmissionAsync(author, "Item", "FndReviewerFeed_PendingItem");

        var feed = await curator.GetFromJsonAsync<PagedResult<FoundrySubmissionSummaryDto>>(
            "/api/foundry/submissions?status=Pending");

        feed!.Items.Should().Contain(s => s.Title == "FndReviewerFeed_PendingItem");
    }

    [Fact]
    public async Task ListSubmissions_Should_Show_Approved_Work_To_Other_Accounts()
    {
        using var author = factory.CreateClient();
        author.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndApprovedFeed_Author"));

        using var curator = factory.CreateClient();
        curator.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetCuratorTokenAsync("FndApprovedFeed_Curator"));

        using var stranger = factory.CreateClient();
        stranger.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndApprovedFeed_Stranger"));

        var submission = await CreateSubmissionAsync(
            author, "Item", "FndApprovedFeed_Item", PromotableItem("fnd-approved-feed-item"));
        var approved = await curator.PostAsJsonAsync(
            $"/api/foundry/submissions/{submission.Id}/review", new { Approved = true });
        approved.StatusCode.Should().Be(HttpStatusCode.OK);

        var feed = await stranger.GetFromJsonAsync<PagedResult<FoundrySubmissionSummaryDto>>(
            "/api/foundry/submissions?search=FndApprovedFeed_Item");

        feed!.Items.Should().Contain(s => s.Title == "FndApprovedFeed_Item");
    }

    // Get
    [Fact]
    public async Task GetSubmission_Should_Require_Authentication()
    {
        using var anonymous = factory.CreateClient();

        var response = await anonymous.GetAsync($"/api/foundry/submissions/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetSubmission_Should_Return_Full_Detail()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndGet_User"));

        var created = await CreateSubmissionAsync(client, "Item", "FndGet_Sword", "{\"name\":\"Sword\"}");

        var dto = await client.GetFromJsonAsync<FoundrySubmissionDto>(
            $"/api/foundry/submissions/{created.Id}");

        dto!.Id.Should().Be(created.Id);
        dto.Title.Should().Be("FndGet_Sword");
        dto.Payload.Should().Be("{\"name\":\"Sword\"}");
        dto.Description.Should().Be("Test submission");
        dto.SubmitterId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task GetSubmission_Should_Return_404_For_Unknown_Id()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndGet_Unknown"));

        var response = await client.GetAsync($"/api/foundry/submissions/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetSubmission_Should_Hide_A_Pending_Submission_From_A_Stranger()
    {
        using var author = factory.CreateClient();
        author.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndGetHide_Author"));

        using var stranger = factory.CreateClient();
        stranger.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndGetHide_Stranger"));

        var created = await CreateSubmissionAsync(author, "Item", "FndGetHide_Item");

        // 404 rather than 403: a stranger does not get to confirm that the row exists.
        var response = await stranger.GetAsync($"/api/foundry/submissions/{created.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetSubmission_Should_Let_A_Reviewer_Read_Any_Submission()
    {
        using var author = factory.CreateClient();
        author.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndGetReview_Author"));

        using var curator = factory.CreateClient();
        curator.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetCuratorTokenAsync("FndGetReview_Curator"));

        var created = await CreateSubmissionAsync(author, "Item", "FndGetReview_Item");

        var dto = await curator.GetFromJsonAsync<FoundrySubmissionDto>(
            $"/api/foundry/submissions/{created.Id}");

        dto!.Id.Should().Be(created.Id);
        dto.Status.Should().Be("Pending");
    }

    // Vote
    [Fact]
    public async Task Vote_Should_Upvote_Submission()
    {
        using var submitterClient = factory.CreateClient();
        submitterClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndVoteUp_Sub"));

        using var voterClient = factory.CreateClient();
        voterClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndVoteUp_Voter"));

        var submission = await CreateSubmissionAsync(submitterClient, "Item", "FndVoteUp_Item");

        var response = await voterClient.PostAsJsonAsync(
            $"/api/foundry/submissions/{submission.Id}/vote", new { Value = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<FoundrySubmissionSummaryDto>();
        dto!.VoteScore.Should().Be(1);
    }

    [Fact]
    public async Task Vote_Should_Downvote_Submission()
    {
        using var submitterClient = factory.CreateClient();
        submitterClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndVoteDn_Sub"));

        using var voterClient = factory.CreateClient();
        voterClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndVoteDn_Voter"));

        var submission = await CreateSubmissionAsync(submitterClient, "Spell", "FndVoteDn_Spell");

        var response = await voterClient.PostAsJsonAsync(
            $"/api/foundry/submissions/{submission.Id}/vote", new { Value = -1 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<FoundrySubmissionSummaryDto>();
        dto!.VoteScore.Should().Be(-1);
    }

    [Fact]
    public async Task Vote_Should_Require_Authentication()
    {
        using var submitterClient = factory.CreateClient();
        submitterClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndVoteAuth_Sub"));

        var submission = await CreateSubmissionAsync(submitterClient, "Item", "FndVoteAuth_Item");

        using var unauthClient = factory.CreateClient();
        var response = await unauthClient.PostAsJsonAsync(
            $"/api/foundry/submissions/{submission.Id}/vote", new { Value = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Vote_Should_Return_400_For_Invalid_Value()
    {
        using var submitterClient = factory.CreateClient();
        submitterClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndVoteInv_Sub"));

        using var voterClient = factory.CreateClient();
        voterClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndVoteInv_Voter"));

        var submission = await CreateSubmissionAsync(submitterClient, "Ability", "FndVoteInv_Ability");

        var response = await voterClient.PostAsJsonAsync(
            $"/api/foundry/submissions/{submission.Id}/vote", new { Value = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Vote_Should_Change_Existing_Vote()
    {
        using var submitterClient = factory.CreateClient();
        submitterClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndVoteChg_Sub"));

        using var voterClient = factory.CreateClient();
        voterClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndVoteChg_Voter"));

        var submission = await CreateSubmissionAsync(submitterClient, "Quest", "FndVoteChg_Quest");

        // First vote: +1
        await voterClient.PostAsJsonAsync(
            $"/api/foundry/submissions/{submission.Id}/vote", new { Value = 1 });

        // Change to -1; score must reflect the updated value only
        var response = await voterClient.PostAsJsonAsync(
            $"/api/foundry/submissions/{submission.Id}/vote", new { Value = -1 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<FoundrySubmissionSummaryDto>();
        dto!.VoteScore.Should().Be(-1);
    }

    // Review
    [Fact]
    public async Task Review_Should_Approve_Submission()
    {
        using var submitterClient = factory.CreateClient();
        submitterClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndRevAppr_Sub"));

        using var curatorClient = factory.CreateClient();
        curatorClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetCuratorTokenAsync("FndRevAppr_Curator"));

        var submission = await CreateSubmissionAsync(
            submitterClient, "Item", "FndRevAppr_Item", PromotableItem("fnd-rev-appr-item"));

        var response = await curatorClient.PostAsJsonAsync(
            $"/api/foundry/submissions/{submission.Id}/review",
            new { Approved = true, Notes = "Looks great!" });

        var reason = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, reason);
        var dto = await response.Content.ReadFromJsonAsync<FoundrySubmissionDto>();
        dto!.Status.Should().Be("Approved");
        dto.ReviewNotes.Should().Be("Looks great!");
        dto.ReviewedAt.Should().NotBeNull();
        dto.ReviewerName.Should().Be("FndRevAppr_Curator");
        // Approval is the gate: the payload landed in the catalog and the submission records where.
        dto.PromotedSlug.Should().Be("fnd-rev-appr-item");

        var promoted = await curatorClient.GetAsync("/api/content/browse/item/fnd-rev-appr-item");
        promoted.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Review_Should_Refuse_An_Unpromotable_Payload_And_Leave_The_Submission_Pending()
    {
        using var submitterClient = factory.CreateClient();
        submitterClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndRevBad_Sub"));

        using var curatorClient = factory.CreateClient();
        curatorClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetCuratorTokenAsync("FndRevBad_Curator"));

        // An empty payload has no slug, so it cannot be written; the decision must be refused, not recorded.
        var submission = await CreateSubmissionAsync(submitterClient, "Item", "FndRevBad_Item");

        var response = await curatorClient.PostAsJsonAsync(
            $"/api/foundry/submissions/{submission.Id}/review",
            new { Approved = true });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var after = await curatorClient.GetFromJsonAsync<FoundrySubmissionDto>(
            $"/api/foundry/submissions/{submission.Id}");

        after!.Status.Should().Be("Pending");
        after.PromotedSlug.Should().BeNull();
    }

    [Fact]
    public async Task Review_Should_Replace_The_Entry_When_The_Slug_Already_Exists()
    {
        using var firstSubmitter = factory.CreateClient();
        firstSubmitter.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndRevDup_Sub1"));

        using var secondSubmitter = factory.CreateClient();
        secondSubmitter.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndRevDup_Sub2"));

        using var curatorClient = factory.CreateClient();
        curatorClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetCuratorTokenAsync("FndRevDup_Curator"));

        var first = await CreateSubmissionAsync(
            firstSubmitter, "Item", "FndRevDup_First", PromotableItem("fnd-rev-dup-item"));

        var approved = await curatorClient.PostAsJsonAsync(
            $"/api/foundry/submissions/{first.Id}/review",
            new { Approved = true });

        approved.StatusCode.Should().Be(HttpStatusCode.OK);

        var before = await curatorClient.GetFromJsonAsync<JsonElement>(
            "/api/content/browse/item/fnd-rev-dup-item");

        // A second submission for the same slug is a proposed correction, which is exactly what the review
        // page's diff promises: approving it replaces the stored row instead of refusing the write.
        var revision = PromotableItem("fnd-rev-dup-item")
            .Replace("\"displayName\":\"fnd-rev-dup-item\"", "\"displayName\":\"Fnd Rev Dup (revised)\"");
        var second = await CreateSubmissionAsync(
            secondSubmitter, "Item", "FndRevDup_Second", revision);

        var replaced = await curatorClient.PostAsJsonAsync(
            $"/api/foundry/submissions/{second.Id}/review",
            new { Approved = true });

        replaced.StatusCode.Should().Be(HttpStatusCode.OK);

        var after = await curatorClient.GetFromJsonAsync<JsonElement>(
            "/api/content/browse/item/fnd-rev-dup-item");

        after.GetProperty("summary").GetProperty("displayName").GetString()
            .Should().Be("Fnd Rev Dup (revised)");
        // The row survives: live game data holds its id.
        after.GetProperty("summary").GetProperty("id").GetString()
            .Should().Be(before.GetProperty("summary").GetProperty("id").GetString());
    }

    /// <summary>
    /// An approval is a write, so a correction that only touches owned JSON has to land too.
    /// </summary>
    /// <remarks>
    /// The approval path replaces the stored row the same way the editor's PUT does, and for a while both copied only
    /// the owner's scalar columns — an approved <c>stats.damageMin</c> correction was reported as written and silently
    /// dropped. This asserts the second write path directly, because it is not reachable through the PUT endpoint.
    /// </remarks>
    [Fact]
    public async Task Review_Should_Persist_An_Owned_Json_Correction_On_Approval()
    {
        const string slug = "fnd-rev-owned-item";

        static string WeaponPayload(int damageMin) => JsonSerializer.Serialize(new
        {
            slug,
            displayName = "Foundry Owned Revision",
            typeKey = "heavy-blades",
            itemType = "weapon",
            weaponType = "sword",
            damageType = "physical",
            handsRequired = 1,
            rarityWeight = 80,
            isActive = true,
            stats = new { damageMin, damageMax = damageMin + 4, value = 25 },
        });

        using var submitterClient = factory.CreateClient();
        submitterClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndRevOwned_Sub"));

        using var curatorClient = factory.CreateClient();
        curatorClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetCuratorTokenAsync("FndRevOwned_Curator"));

        var first = await CreateSubmissionAsync(submitterClient, "Item", "FndRevOwned_First", WeaponPayload(4));
        (await curatorClient.PostAsJsonAsync($"/api/foundry/submissions/{first.Id}/review", new { Approved = true }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var revision = await CreateSubmissionAsync(submitterClient, "Item", "FndRevOwned_Second", WeaponPayload(9));
        var reviewed = await curatorClient.PostAsJsonAsync($"/api/foundry/submissions/{revision.Id}/review", new { Approved = true });
        reviewed.StatusCode.Should().Be(HttpStatusCode.OK, await reviewed.Content.ReadAsStringAsync());

        var detail = await curatorClient.GetFromJsonAsync<JsonElement>($"/api/content/browse/item/{slug}");
        detail.GetProperty("payload").GetProperty("stats").GetProperty("damageMin").GetInt32().Should().Be(9);
        detail.GetProperty("payload").GetProperty("version").GetInt32().Should().Be(2);
    }

    /// <summary>
    /// Reads a recipe and its ingredient rows back through a fresh context — the approval under test writes through a
    /// different context than the one this assertion opens.
    /// </summary>
    private async Task<RealmEngine.Data.Entities.Recipe> StoredRecipeAsync(string slug)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RealmEngine.Data.Persistence.ContentDbContext>();
        return await db.Recipes.AsNoTracking()
            .Include(recipe => recipe.Ingredients)
            .SingleAsync(recipe => recipe.Slug == slug);
    }

    /// <summary>
    /// A recipe submission whose ingredients are child rows rather than owned JSON. Approval is the second write path,
    /// so an ingredient correction has to land there too — the HTTP endpoint's tests cannot cover it.
    /// </summary>
    [Fact]
    public async Task Review_Should_Persist_An_Authored_Ingredient_Edit_On_Approval()
    {
        const string slug = "fnd-rev-child-recipe";

        static string RecipePayload(int ironQuantity) => JsonSerializer.Serialize(new
        {
            slug,
            displayName = "Foundry Ingredient Revision",
            typeKey = "blacksmithing",
            rarityWeight = 70,
            isActive = true,
            outputItemDomain = "items/general",
            outputItemSlug = "iron-sword",
            outputQuantity = 1,
            craftingSkill = "blacksmithing",
            craftingLevel = 1,
            ingredients = new[]
            {
                new { itemDomain = "items/materials", itemSlug = "iron", quantity = ironQuantity },
                new { itemDomain = "items/materials", itemSlug = "pine", quantity = 1 },
            },
        });

        using var submitterClient = factory.CreateClient();
        submitterClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndRevIngredient_Sub"));

        using var curatorClient = factory.CreateClient();
        curatorClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetCuratorTokenAsync("FndRevIngredient_Curator"));

        var first = await CreateSubmissionAsync(submitterClient, "Recipe", "FndRevIngredient_First", RecipePayload(3));
        (await curatorClient.PostAsJsonAsync($"/api/foundry/submissions/{first.Id}/review", new { Approved = true }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var revision = await CreateSubmissionAsync(submitterClient, "Recipe", "FndRevIngredient_Second", RecipePayload(9));
        var reviewed = await curatorClient.PostAsJsonAsync($"/api/foundry/submissions/{revision.Id}/review", new { Approved = true });
        reviewed.StatusCode.Should().Be(HttpStatusCode.OK, await reviewed.Content.ReadAsStringAsync());

        // Ingredient rows live in their own table, so the browse payload is not the proof — the stored rows are.
        var stored = await StoredRecipeAsync(slug);
        stored.Version.Should().Be(2);
        stored.Ingredients.Should().HaveCount(2);
        stored.Ingredients.Single(ingredient => ingredient.ItemSlug == "iron").Quantity.Should().Be(9);
        stored.Ingredients.Single(ingredient => ingredient.ItemSlug == "pine").Quantity.Should().Be(1);
    }

    /// <summary>
    /// A schema-shaped submission only names the fields Foundry's form renders. Approving one must not delete the
    /// owned JSON it never mentions.
    /// </summary>
    /// <remarks>
    /// The language schema exposes four <c>phonology.*</c> lists and nothing else, while the entity also carries
    /// <c>phonology.consonants</c>/<c>vowels</c>/<c>junctionRules</c>, <c>morphology</c> and <c>registerSystem</c>.
    /// Copying an owned value wholesale turned the omission into a deletion: the correction was reported as written and
    /// every unnamed list was zeroed. This is the approval half of that regression; the form half is seeded from the
    /// stored row in the web app.
    /// </remarks>
    [Fact]
    public async Task Review_Should_Preserve_Owned_Json_A_Schema_Shaped_Submission_Does_Not_Name()
    {
        const string slug = "fnd-rev-partial-language";

        static string RichLanguage(string displayName) => JsonSerializer.Serialize(new
        {
            slug,
            displayName,
            typeKey = "imperial",
            rarityWeight = 50,
            isActive = true,
            description = "The old tongue.",
            tonalCharacter = "Hard + Formal",
            phonology = new
            {
                consonants = new[]
                {
                    new { symbol = "v", description = "Voiced labiodental fricative" },
                    new { symbol = "r", description = "Alveolar trill" },
                },
                vowels = new[] { new { symbol = "ae", sound = "/eɪ/ as in 'day'" } },
                allowedSyllablePatterns = new[] { "CVC" },
                allowedInitialClusters = new[] { "VR-" },
                forbiddenClusters = Array.Empty<string>(),
                allowedFinalClusters = new[] { "-LD" },
                junctionRules = new[]
                {
                    new
                    {
                        cluster = "–l + vr–",
                        rule = "The v elides into the following r.",
                        formalExample = "vaeld + raeth → vaeld",
                    },
                },
            },
            morphology = new
            {
                roots = new[] { new { token = "vael", coreMeaning = "stronghold" } },
                prefixes = Array.Empty<object>(),
                suffixes = Array.Empty<object>(),
            },
            registerSystem = new
            {
                registers = new[]
                {
                    new { name = "formal", usage = "titles and law", wordOrder = "SVO", vowelQuality = "long" },
                },
            },
        });

        // Exactly what the generic Foundry submission form can produce for a language: the schema's fields and nothing
        // else. Only displayName differs from the stored row.
        static string SchemaShapedCorrection(string displayName) => JsonSerializer.Serialize(new
        {
            slug,
            displayName,
            typeKey = "imperial",
            rarityWeight = 50,
            isActive = true,
            description = "The old tongue.",
            tonalCharacter = "Hard + Formal",
            phonology = new
            {
                allowedSyllablePatterns = new[] { "CVC" },
                allowedInitialClusters = new[] { "VR-" },
                forbiddenClusters = Array.Empty<string>(),
                allowedFinalClusters = new[] { "-LD" },
            },
        });

        using var submitterClient = factory.CreateClient();
        submitterClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndRevPartial_Sub"));

        using var curatorClient = factory.CreateClient();
        curatorClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetCuratorTokenAsync("FndRevPartial_Curator"));

        var first = await CreateSubmissionAsync(
            submitterClient, "Language", "FndRevPartial_First", RichLanguage("Calethic"));
        (await curatorClient.PostAsJsonAsync($"/api/foundry/submissions/{first.Id}/review", new { Approved = true }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var correction = await CreateSubmissionAsync(
            submitterClient, "Language", "FndRevPartial_Second", SchemaShapedCorrection("Calethic (revised)"));
        var approved = await curatorClient.PostAsJsonAsync(
            $"/api/foundry/submissions/{correction.Id}/review", new { Approved = true });
        approved.StatusCode.Should().Be(HttpStatusCode.OK, await approved.Content.ReadAsStringAsync());

        var payload = (await curatorClient.GetFromJsonAsync<JsonElement>($"/api/content/browse/language/{slug}"))
            .GetProperty("payload");

        // The field the submission named changed …
        payload.GetProperty("displayName").GetString().Should().Be("Calethic (revised)");
        payload.GetProperty("version").GetInt32().Should().Be(2);

        // … and everything the schema-shaped payload did not name is still there.
        var phonology = payload.GetProperty("phonology");
        phonology.GetProperty("junctionRules").GetArrayLength().Should().Be(1);
        phonology.GetProperty("junctionRules")[0].GetProperty("formalExample").GetString()
            .Should().Be("vaeld + raeth → vaeld");
        phonology.GetProperty("consonants").GetArrayLength().Should().Be(2);
        phonology.GetProperty("vowels").GetArrayLength().Should().Be(1);
        payload.GetProperty("morphology").GetProperty("roots").GetArrayLength().Should().Be(1);
        payload.GetProperty("registerSystem").GetProperty("registers").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Review_Should_Reject_Submission_With_Notes()
    {
        using var submitterClient = factory.CreateClient();
        submitterClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndRevRej_Sub"));

        using var curatorClient = factory.CreateClient();
        curatorClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetCuratorTokenAsync("FndRevRej_Curator"));

        var submission = await CreateSubmissionAsync(submitterClient, "Spell", "FndRevRej_Spell");

        var response = await curatorClient.PostAsJsonAsync(
            $"/api/foundry/submissions/{submission.Id}/review",
            new { Approved = false, Notes = "Needs balancing." });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<FoundrySubmissionDto>();
        dto!.Status.Should().Be("Rejected");
        dto.ReviewNotes.Should().Be("Needs balancing.");
    }

    [Fact]
    public async Task Review_Should_Require_The_Review_Permission()
    {
        using var submitterClient = factory.CreateClient();
        submitterClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndRevFrbd_Sub"));

        using var regularClient = factory.CreateClient();
        regularClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndRevFrbd_Regular"));

        var submission = await CreateSubmissionAsync(submitterClient, "Item", "FndRevFrbd_Item");

        var response = await regularClient.PostAsJsonAsync(
            $"/api/foundry/submissions/{submission.Id}/review",
            new { Approved = true });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Review_Should_Accept_An_Admin_Who_Holds_No_Curator_Role()
    {
        using var submitterClient = factory.CreateClient();
        submitterClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndRevAdmin_Sub"));

        // Admin holds every permission but is not in the Curator role. The decision used to be a role policy,
        // so this account could not approve anything it could otherwise manage.
        using var adminClient = factory.CreateClient();
        var (adminToken, _) = await factory.CreateAccountTokenAsync("FndRevAdmin_Admin", "Admin");
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var submission = await CreateSubmissionAsync(
            submitterClient, "Item", "FndRevAdmin_Item", PromotableItem("fnd-rev-admin-item"));

        var response = await adminClient.PostAsJsonAsync(
            $"/api/foundry/submissions/{submission.Id}/review",
            new { Approved = true, Notes = "Admin decision." });

        var reason = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, reason);
        var dto = await response.Content.ReadFromJsonAsync<FoundrySubmissionDto>();
        dto!.Status.Should().Be("Approved");
        dto.ReviewerName.Should().Be("FndRevAdmin_Admin");
    }

    [Fact]
    public async Task Review_Should_Require_Authentication()
    {
        using var unauthClient = factory.CreateClient();
        var response = await unauthClient.PostAsJsonAsync(
            $"/api/foundry/submissions/{Guid.NewGuid()}/review",
            new { Approved = true });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Review_Should_Return_BadRequest_For_Unknown_Submission()
    {
        using var curatorClient = factory.CreateClient();
        curatorClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetCuratorTokenAsync("FndRevUnk_Curator"));

        var response = await curatorClient.PostAsJsonAsync(
            $"/api/foundry/submissions/{Guid.NewGuid()}/review",
            new { Approved = true });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Review_Should_Create_Notification_For_Submitter_On_Approve()
    {
        using var submitterClient = factory.CreateClient();
        var submitterToken = await GetTokenAsync("FndNotifA_Sub");
        submitterClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", submitterToken);

        using var curatorClient = factory.CreateClient();
        curatorClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetCuratorTokenAsync("FndNotifA_Curator"));

        var submission = await CreateSubmissionAsync(
            submitterClient, "Item", "FndNotifA_Item", PromotableItem("fnd-notif-a-item"));

        await curatorClient.PostAsJsonAsync(
            $"/api/foundry/submissions/{submission.Id}/review",
            new { Approved = true, Notes = "Well done." });

        var notifications = await submitterClient.GetFromJsonAsync<FoundryNotificationDto[]>(
            "/api/foundry/notifications");

        notifications!.Should().ContainSingle(n => n.SubmissionId == submission.Id);
        notifications.First(n => n.SubmissionId == submission.Id).IsRead.Should().BeFalse();
    }

    [Fact]
    public async Task Review_Should_Create_Notification_For_Submitter_On_Reject()
    {
        using var submitterClient = factory.CreateClient();
        submitterClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndNotifR_Sub"));

        using var curatorClient = factory.CreateClient();
        curatorClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetCuratorTokenAsync("FndNotifR_Curator"));

        var submission = await CreateSubmissionAsync(submitterClient, "Spell", "FndNotifR_Spell");

        await curatorClient.PostAsJsonAsync(
            $"/api/foundry/submissions/{submission.Id}/review",
            new { Approved = false, Notes = "Not balanced." });

        var notifications = await submitterClient.GetFromJsonAsync<FoundryNotificationDto[]>(
            "/api/foundry/notifications");

        var notif = notifications!.First(n => n.SubmissionId == submission.Id);
        notif.Message.Should().Contain("rejected");
        notif.Message.Should().Contain("Not balanced.");
    }

    // Notifications
    [Fact]
    public async Task GetNotifications_Should_Return_Empty_For_New_Account()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndNotifEmpty_User"));

        var response = await _client.GetAsync("/api/foundry/notifications");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var notifications = await response.Content.ReadFromJsonAsync<FoundryNotificationDto[]>();
        notifications.Should().BeEmpty();
    }

    [Fact]
    public async Task GetNotifications_Should_Require_Authentication()
    {
        using var unauthClient = factory.CreateClient();
        var response = await unauthClient.GetAsync("/api/foundry/notifications");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUnreadCount_Should_Track_Only_The_Callers_Unread_Notifications()
    {
        using var submitterClient = factory.CreateClient();
        submitterClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndUnread_Sub"));

        using var curatorClient = factory.CreateClient();
        curatorClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetCuratorTokenAsync("FndUnread_Curator"));

        using var otherClient = factory.CreateClient();
        otherClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndUnread_Other"));

        var before = await submitterClient.GetFromJsonAsync<FoundryUnreadNotificationCountDto>(
            "/api/foundry/notifications/unread-count");
        before!.Count.Should().Be(0);

        var submission = await CreateSubmissionAsync(submitterClient, "Quest", "FndUnread_Quest");

        await curatorClient.PostAsJsonAsync(
            $"/api/foundry/submissions/{submission.Id}/review",
            new { Approved = false, Notes = "Needs work." });

        var afterReview = await submitterClient.GetFromJsonAsync<FoundryUnreadNotificationCountDto>(
            "/api/foundry/notifications/unread-count");
        afterReview!.Count.Should().Be(1);

        // The count is per recipient: an unrelated account reads zero while the author still has one.
        var other = await otherClient.GetFromJsonAsync<FoundryUnreadNotificationCountDto>(
            "/api/foundry/notifications/unread-count");
        other!.Count.Should().Be(0);

        var inbox = await submitterClient.GetFromJsonAsync<FoundryNotificationDto[]>(
            "/api/foundry/notifications");
        var notification = inbox!.First(n => n.SubmissionId == submission.Id);
        await submitterClient.PostAsJsonAsync(
            $"/api/foundry/notifications/{notification.Id}/read", new { });

        var afterRead = await submitterClient.GetFromJsonAsync<FoundryUnreadNotificationCountDto>(
            "/api/foundry/notifications/unread-count");
        afterRead!.Count.Should().Be(0);
    }

    [Fact]
    public async Task GetUnreadCount_Should_Require_Authentication()
    {
        using var unauthClient = factory.CreateClient();
        var response = await unauthClient.GetAsync("/api/foundry/notifications/unread-count");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // Mark Read
    [Fact]
    public async Task MarkNotificationRead_Should_Return_NoContent_And_Mark_IsRead()
    {
        using var submitterClient = factory.CreateClient();
        submitterClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndMarkRead_Sub"));

        using var curatorClient = factory.CreateClient();
        curatorClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetCuratorTokenAsync("FndMarkRead_Curator"));

        var submission = await CreateSubmissionAsync(submitterClient, "Quest", "FndMarkRead_Quest");

        await curatorClient.PostAsJsonAsync(
            $"/api/foundry/submissions/{submission.Id}/review",
            new { Approved = false, Notes = "Try again." });

        var notifs = await submitterClient.GetFromJsonAsync<FoundryNotificationDto[]>(
            "/api/foundry/notifications");
        var notif = notifs!.First(n => n.SubmissionId == submission.Id);

        var markRead = await submitterClient.PostAsJsonAsync(
            $"/api/foundry/notifications/{notif.Id}/read", new { });

        markRead.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Refresh and verify IsRead = true
        var updated = await submitterClient.GetFromJsonAsync<FoundryNotificationDto[]>(
            "/api/foundry/notifications");
        updated!.First(n => n.Id == notif.Id).IsRead.Should().BeTrue();
    }

    [Fact]
    public async Task MarkNotificationRead_Should_Return_404_For_Unknown_Id()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndMarkUnk_User"));

        var response = await _client.PostAsJsonAsync(
            $"/api/foundry/notifications/{Guid.NewGuid()}/read", new { });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MarkNotificationRead_Should_Require_Authentication()
    {
        using var unauthClient = factory.CreateClient();
        var response = await unauthClient.PostAsJsonAsync(
            $"/api/foundry/notifications/{Guid.NewGuid()}/read", new { });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MarkNotificationRead_Should_Return_404_For_Another_Users_Notification()
    {
        using var submitterClient = factory.CreateClient();
        submitterClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndMarkOwn_Sub"));

        using var curatorClient = factory.CreateClient();
        curatorClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetCuratorTokenAsync("FndMarkOwn_Curator"));

        using var otherClient = factory.CreateClient();
        otherClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndMarkOwn_Other"));

        var submission = await CreateSubmissionAsync(
            submitterClient, "Item", "FndMarkOwn_Item", PromotableItem("fnd-mark-own-item"));

        await curatorClient.PostAsJsonAsync(
            $"/api/foundry/submissions/{submission.Id}/review",
            new { Approved = true });

        var notifs = await submitterClient.GetFromJsonAsync<FoundryNotificationDto[]>(
            "/api/foundry/notifications");
        var notif = notifs!.First(n => n.SubmissionId == submission.Id);

        // A different account should get 404 (ownership guard in MarkNotificationReadAsync)
        var response = await otherClient.PostAsJsonAsync(
            $"/api/foundry/notifications/{notif.Id}/read", new { });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
