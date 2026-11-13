using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Integration tests for the operator lever over instance runs: listing them with live occupant counts, and
/// purging one. The purge destroys live world state, so it is worth an HTTP-level test rather than only a
/// service one.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class AdminInstanceEndpointTests(WebAppFactory factory)
{
    private async Task<HttpClient> AuthedClientAsync(string username, params string[] roles)
    {
        var (token, _) = await factory.CreateAccountTokenAsync(username, roles);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<(Guid InstanceId, Guid CharacterId)> SeedOccupiedRunAsync(string characterName)
    {
        var (_, accountId) = await factory.CreateAccountTokenAsync($"inst_{characterName}");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var character = new Character
        {
            AccountId = accountId,
            Name = characterName,
            ClassName = "Warrior",
            SlotIndex = 1,
            Attributes = "{}",
        };
        db.Characters.Add(character);

        var run = new ZoneInstance
        {
            ZoneId = "the-halrow",
            CreatedByCharacterId = character.Id,
            ZoneGroup = $"zone:the-halrow#i1:{Guid.NewGuid():D}",
        };
        db.ZoneInstances.Add(run);

        db.PlayerSessions.Add(new PlayerSession
        {
            CharacterId = character.Id,
            CharacterName = characterName,
            ConnectionId = $"conn-{Guid.NewGuid():N}",
            RegionId = "varenmark",
            ZoneId = "the-halrow",
            ZoneInstanceId = run.Id,
        });

        await db.SaveChangesAsync();
        return (run.Id, character.Id);
    }

    [Fact]
    public async Task List_ShowsOpenRunsWithTheirOccupantCount()
    {
        using var client = await AuthedClientAsync("AdmInst_Reader", "Admin");
        var (instanceId, _) = await SeedOccupiedRunAsync("InstListed");

        var response = await client.GetAsync("/api/admin/instances?zoneId=the-halrow");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var runs = payload.RootElement.EnumerateArray().ToList();

        runs.Should().Contain(run =>
            run.GetProperty("id").GetGuid() == instanceId &&
            run.GetProperty("occupants").GetInt32() == 1 &&
            run.GetProperty("state").GetString() == "Open");
    }

    [Fact]
    public async Task Purge_ReleasesTheOccupantsAndDeletesTheRun()
    {
        using var client = await AuthedClientAsync("AdmInst_Purger", "Admin");
        var (instanceId, characterId) = await SeedOccupiedRunAsync("InstPurged");

        var response = await client.DeleteAsync($"/api/admin/instances/{instanceId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        (await db.ZoneInstances.AnyAsync(run => run.Id == instanceId)).Should().BeFalse();

        var session = await db.PlayerSessions.SingleAsync(row => row.CharacterId == characterId);
        session.ZoneInstanceId.Should().BeNull("a purged run must not keep pointing at a row that is gone");

        (await db.AdminAuditEntries.AnyAsync(entry =>
            entry.Action == "purge_instance" && entry.Details!.Contains(instanceId.ToString())))
            .Should()
            .BeTrue("destroying live world state is audited");
    }

    [Fact]
    public async Task Purge_RequiresTheContentManagementPermission()
    {
        using var client = await AuthedClientAsync("AdmInst_Plain");
        var (instanceId, _) = await SeedOccupiedRunAsync("InstDenied");

        var response = await client.DeleteAsync($"/api/admin/instances/{instanceId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
