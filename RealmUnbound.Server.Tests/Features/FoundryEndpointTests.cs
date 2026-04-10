using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using RealmUnbound.Server.Data.Entities;
using RealmUnbound.Server.Tests.Infrastructure;

namespace RealmUnbound.Server.Tests.Features;

[Trait("Category", "Integration")]
public class FoundryEndpointTests(WebAppFactory factory) : IClassFixture<WebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // Helpers
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

    // Register the user, assign "Curator" via UserManager, then re-login so the JWT
    // carries the role claim (IssueTokenPairAsync calls IsInRoleAsync at login time).
    private async Task<string> GetCuratorTokenAsync(string username)
    {
        var email = $"{username.ToLower()}@test.com";
        await _client.PostAsJsonAsync("/api/auth/register",
            new { Email = email, Username = username, Password = "TestP@ssword123" });

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<PlayerAccount>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        if (!await roleManager.RoleExistsAsync("Curator"))
            await roleManager.CreateAsync(new IdentityRole<Guid>("Curator"));

        var user = await userManager.FindByNameAsync(username);
        await userManager.AddToRoleAsync(user!, "Curator");

        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new { Email = email, Password = "TestP@ssword123" });
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        return auth!.AccessToken;
    }

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
    public async Task ListSubmissions_Should_Return_Paged_Result()
    {
        var response = await _client.GetAsync("/api/foundry/submissions");

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

        var result = await _client.GetFromJsonAsync<PagedResult<FoundrySubmissionSummaryDto>>(
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
        var result = await _client.GetFromJsonAsync<PagedResult<FoundrySubmissionSummaryDto>>(
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
        var result = await _client.GetFromJsonAsync<PagedResult<FoundrySubmissionSummaryDto>>(
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

        var found = await _client.GetFromJsonAsync<PagedResult<FoundrySubmissionSummaryDto>>(
            "/api/foundry/submissions?search=FndSearch_Fire");
        var notFound = await _client.GetFromJsonAsync<PagedResult<FoundrySubmissionSummaryDto>>(
            "/api/foundry/submissions?search=FndSearch_Ice");

        found!.Items.Should().Contain(s => s.Title == "FndSearch_FireStrike");
        notFound!.Items.Should().NotContain(s => s.Title == "FndSearch_FireStrike");
    }

    // Get
    [Fact]
    public async Task GetSubmission_Should_Return_Full_Detail()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("FndGet_User"));

        var created = await CreateSubmissionAsync(client, "Item", "FndGet_Sword", "{\"name\":\"Sword\"}");

        var dto = await _client.GetFromJsonAsync<FoundrySubmissionDto>(
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
        var response = await _client.GetAsync($"/api/foundry/submissions/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
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

        var submission = await CreateSubmissionAsync(submitterClient, "Item", "FndRevAppr_Item");

        var response = await curatorClient.PostAsJsonAsync(
            $"/api/foundry/submissions/{submission.Id}/review",
            new { Approved = true, Notes = "Looks great!" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<FoundrySubmissionDto>();
        dto!.Status.Should().Be("Approved");
        dto.ReviewNotes.Should().Be("Looks great!");
        dto.ReviewedAt.Should().NotBeNull();
        dto.ReviewerName.Should().Be("FndRevAppr_Curator");
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
    public async Task Review_Should_Require_Curator_Role()
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

        var submission = await CreateSubmissionAsync(submitterClient, "Item", "FndNotifA_Item");

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

        var submission = await CreateSubmissionAsync(submitterClient, "Ability", "FndMarkOwn_Ability");

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
