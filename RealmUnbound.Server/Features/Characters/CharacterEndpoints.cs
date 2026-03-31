using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using RealmEngine.Shared.Abstractions;
using RealmUnbound.Contracts.Characters;
using RealmUnbound.Server.Data.Entities;
using RealmUnbound.Server.Data.Repositories;
using RealmUnbound.Server.Services;

namespace RealmUnbound.Server.Features.Characters;

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
            IsHardcore: c.DifficultyMode == "hardcore");
}
