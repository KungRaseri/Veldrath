using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using RealmEngine.Shared.Abstractions;
using Veldrath.Contracts.Characters;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Services;

namespace Veldrath.Server.Features.Characters;

/// <summary>
/// Minimal API endpoints for character management.
/// All endpoints require a valid JWT (bearer token).
///
/// GET    /api/characters        — list the caller's characters
/// POST   /api/characters        — create a character (up to MaxCharacterSlots)
/// DELETE /api/characters/{id}   — soft-delete a character (must be the owner)
/// </summary>
public static class CharacterEndpoints
{
    public static IEndpointRouteBuilder MapCharacterEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/characters")
            .WithTags("Characters")
            .RequireAuthorization();

        group.MapGet("/",            ListAsync);
        group.MapPost("/",           CreateAsync);
        group.MapDelete("/{id:guid}", DeleteAsync);
        group.MapGet("/last-session", GetLastSessionAsync);

        return app;
    }

    private static async Task<IResult> ListAsync(
        ClaimsPrincipal user,
        ICharacterRepository repo,
        IActiveCharacterTracker activeCharacters,
        CancellationToken ct)
    {
        var accountId = GetAccountId(user);
        var characters = await repo.GetByAccountIdAsync(accountId, ct);
        return Results.Ok(characters.Select(c => ToDto(c, activeCharacters.IsActive(c.Id))));
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] CreateCharacterRequest request,
        ClaimsPrincipal user,
        ICharacterRepository repo,
        IPlayerAccountRepository accountRepo,
        ICharacterClassRepository classRepo,
        CancellationToken ct)
    {
        var accountId = GetAccountId(user);

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { error = "Character name is required" });

        if (request.Name.Length < 2 || request.Name.Length > 30)
            return Results.BadRequest(new { error = "Character name must be between 2 and 30 characters" });

        if (!System.Text.RegularExpressions.Regex.IsMatch(request.Name, @"^[a-zA-Z0-9 \-'_]+$"))
            return Results.BadRequest(new { error = "Character name may only contain letters, numbers, spaces, hyphens, apostrophes, and underscores" });

        var normalizedMode = request.DifficultyMode?.ToLowerInvariant() ?? "normal";
        if (normalizedMode is not "normal" and not "hardcore")
            return Results.BadRequest(new { error = "DifficultyMode must be \"normal\" or \"hardcore\"" });

        var account = await accountRepo.FindByIdAsync(accountId, ct);
        if (account is null) return Results.Unauthorized();

        var activeCount = await repo.GetActiveCountAsync(accountId, ct);
        if (activeCount >= account.MaxCharacterSlots)
            return Results.BadRequest(new { error = $"Character slot limit reached ({account.MaxCharacterSlots})" });

        if (await repo.NameExistsAsync(request.Name, ct))
            return Results.Conflict(new { error = "Character name already taken" });

        // Assign the lowest available slot index within 1..MaxCharacterSlots.
        var existing = await repo.GetByAccountIdAsync(accountId, ct);
        var usedSlots = existing.Select(c => c.SlotIndex).ToHashSet();
        var slotIndex = Enumerable.Range(1, account.MaxCharacterSlots)
            .First(i => !usedSlots.Contains(i));

        // Seed starting ability slugs from the character class definition.
        var characterClass = classRepo.GetByName(request.ClassName);
        var starterSlugs = characterClass?.StartingPowerIds
            .Select(id => id.Contains(':') ? id[(id.LastIndexOf(':') + 1)..] : id)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList() ?? [];

        var character = new Character
        {
            AccountId = accountId,
            SlotIndex = slotIndex,
            Name = request.Name,
            ClassName = request.ClassName,
            DifficultyMode = normalizedMode,
            AbilitiesBlob = JsonSerializer.Serialize(starterSlugs),
        };

        var created = await repo.CreateAsync(character, ct);
        return Results.Created($"/api/characters/{created.Id}", ToDto(created));
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        ClaimsPrincipal user,
        ICharacterRepository repo,
        IActiveCharacterTracker activeCharacters,
        CancellationToken ct)
    {
        var accountId = GetAccountId(user);
        var character = await repo.GetByIdAsync(id, ct);

        if (character is null)
            return Results.NotFound();

        if (character.AccountId != accountId)
            return Results.Forbid();

        if (activeCharacters.IsActive(id))
            return Results.Conflict(new { error = "Cannot delete a character that is currently in game." });

        await repo.SoftDeleteAsync(id, ct);
        return Results.NoContent();
    }

    /// <summary>
    /// Returns the authenticated account's most recently played character
    /// and its last known location. Used by the Blazor client to restore
    /// game state after a page refresh without requiring the user to
    /// manually re-select a character.
    /// </summary>
    private static async Task<IResult> GetLastSessionAsync(
        ClaimsPrincipal user,
        ICharacterRepository repo,
        IZoneRepository zoneRepo,
        CancellationToken ct)
    {
        var accountId = GetAccountId(user);
        var lastChar = await repo.GetLastPlayedAsync(accountId, ct);

        if (lastChar is null)
            return Results.NoContent();

        string? regionId = null;
        if (!string.IsNullOrEmpty(lastChar.CurrentZoneId))
        {
            var zone = await zoneRepo.GetByIdAsync(lastChar.CurrentZoneId);
            regionId = zone?.RegionId;
        }

        return Results.Ok(new LastSessionDto(
            CharacterId: lastChar.Id,
            CharacterName: lastChar.Name,
            ZoneId: lastChar.CurrentZoneId,
            RegionId: regionId,
            TileX: lastChar.TileX,
            TileY: lastChar.TileY));
    }

    // Helpers
    private static Guid GetAccountId(ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
                 ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
                 ?? throw new InvalidOperationException("Account ID claim missing from token.");
        return Guid.Parse(value);
    }

    private static CharacterDto ToDto(Character c, bool isOnline = false) =>
        new(c.Id, c.SlotIndex, c.Name, c.ClassName, c.Level, c.Experience, c.LastPlayedAt, c.CurrentZoneId,
            DifficultyMode: c.DifficultyMode,
            IsOnline: isOnline,
            IsHardcore: c.DifficultyMode == "hardcore",
            BackgroundId: c.BackgroundId,
            SpeciesSlug: c.SpeciesSlug,
            CurrentZoneLocationSlug: c.CurrentZoneLocationSlug);
}
