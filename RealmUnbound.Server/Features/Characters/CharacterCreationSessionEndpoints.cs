using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using RealmEngine.Core.Features.CharacterCreation.Commands;
using RealmEngine.Core.Features.CharacterCreation.Queries;
using RealmEngine.Shared.Abstractions;
using RealmUnbound.Contracts.Characters;
using RealmUnbound.Server.Data.Entities;
using RealmUnbound.Server.Data.Repositories;

namespace RealmUnbound.Server.Features.Characters;

/// <summary>
/// Minimal API endpoints for the guided character creation wizard.
/// All endpoints require a valid JWT (bearer token).
///
/// POST   /api/character-creation/sessions                      — begin a new session
/// GET    /api/character-creation/sessions/{id}                 — get current session state
/// PATCH  /api/character-creation/sessions/{id}/name            — set character name
/// PATCH  /api/character-creation/sessions/{id}/class           — set class choice
/// PATCH  /api/character-creation/sessions/{id}/species         — set species choice
/// PATCH  /api/character-creation/sessions/{id}/background      — set background choice
/// PATCH  /api/character-creation/sessions/{id}/attributes      — set point-buy allocations
/// PATCH  /api/character-creation/sessions/{id}/equipment       — set equipment preferences
/// PATCH  /api/character-creation/sessions/{id}/location        — set starting location
/// GET    /api/character-creation/sessions/{id}/preview         — get non-persisted character preview
/// POST   /api/character-creation/sessions/{id}/finalize        — create and persist the character
/// DELETE /api/character-creation/sessions/{id}                 — abandon session
/// </summary>
public static class CharacterCreationSessionEndpoints
{
    /// <summary>Maps all character creation session endpoints onto the application.</summary>
    /// <param name="app">The endpoint route builder to register routes on.</param>
    /// <returns>The same <paramref name="app"/> for chaining.</returns>
    public static IEndpointRouteBuilder MapCharacterCreationSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/character-creation/sessions")
            .WithTags("Character Creation")
            .RequireAuthorization();

        group.MapPost("/",                          BeginAsync);
        group.MapGet("/{id:guid}",                 GetSessionAsync);
        group.MapPatch("/{id:guid}/name",          SetNameAsync);
        group.MapPatch("/{id:guid}/class",         SetClassAsync);
        group.MapPatch("/{id:guid}/species",       SetSpeciesAsync);
        group.MapPatch("/{id:guid}/background",    SetBackgroundAsync);
        group.MapPatch("/{id:guid}/attributes",    SetAttributesAsync);
        group.MapPatch("/{id:guid}/equipment",     SetEquipmentAsync);
        group.MapPatch("/{id:guid}/location",      SetLocationAsync);
        group.MapGet("/{id:guid}/preview",         GetPreviewAsync);
        group.MapPost("/{id:guid}/finalize",       FinalizeAsync);
        group.MapDelete("/{id:guid}",              AbandonAsync);

        return app;
    }

    private static async Task<IResult> BeginAsync(
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new BeginCreationSessionCommand(), ct);
        return result.Success
            ? Results.Created($"/api/character-creation/sessions/{result.SessionId}", result)
            : Results.StatusCode(StatusCodes.Status500InternalServerError);
    }

    private static async Task<IResult> GetSessionAsync(
        Guid id,
        ICharacterCreationSessionStore sessionStore,
        CancellationToken ct)
    {
        var session = await sessionStore.GetSessionAsync(id);
        return session is null
            ? Results.NotFound(new { error = $"Session {id} not found." })
            : Results.Ok(session);
    }

    private static async Task<IResult> SetNameAsync(
        Guid id,
        [FromBody] SetCreationNameRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new SetCreationNameCommand(id, request.CharacterName), ct);
        return result.Success
            ? Results.Ok(result)
            : Results.BadRequest(new { error = result.Message });
    }

    private static async Task<IResult> SetClassAsync(
        Guid id,
        [FromBody] SetCreationClassRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new SetCreationClassCommand(id, request.ClassName), ct);
        return result.Success
            ? Results.Ok(result)
            : Results.BadRequest(new { error = result.Message });
    }

    private static async Task<IResult> SetSpeciesAsync(
        Guid id,
        [FromBody] SetCreationSpeciesRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new SetCreationSpeciesCommand(id, request.SpeciesSlug), ct);
        return result.Success
            ? Results.Ok(result)
            : Results.BadRequest(new { error = result.Message });
    }

    private static async Task<IResult> SetBackgroundAsync(
        Guid id,
        [FromBody] SetCreationBackgroundRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new SetCreationBackgroundCommand(id, request.BackgroundId), ct);
        return result.Success
            ? Results.Ok(result)
            : Results.BadRequest(new { error = result.Message });
    }

    private static async Task<IResult> SetAttributesAsync(
        Guid id,
        [FromBody] SetCreationAttributesRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new AllocateCreationAttributesCommand(id, request.Allocations), ct);
        return result.Success
            ? Results.Ok(result)
            : Results.BadRequest(new { error = result.Message });
    }

    private static async Task<IResult> SetEquipmentAsync(
        Guid id,
        [FromBody] SetCreationEquipmentPreferencesRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new SetCreationEquipmentPreferencesCommand(id, request.PreferredArmorType, request.PreferredWeaponType, request.IncludeShield),
            ct);
        return result.Success
            ? Results.Ok(result)
            : Results.BadRequest(new { error = result.Message });
    }

    private static async Task<IResult> SetLocationAsync(
        Guid id,
        [FromBody] SetCreationLocationRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new SetCreationLocationCommand(id, request.LocationId), ct);
        return result.Success
            ? Results.Ok(result)
            : Results.BadRequest(new { error = result.Message });
    }

    private static async Task<IResult> GetPreviewAsync(
        Guid id,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new GetCreationPreviewQuery(id), ct);
        return result.Success
            ? Results.Ok(result)
            : Results.BadRequest(new { error = result.Message });
    }

    private static async Task<IResult> FinalizeAsync(
        Guid id,
        [FromBody] FinalizeCreationSessionRequest request,
        ClaimsPrincipal user,
        ICharacterCreationSessionStore sessionStore,
        ICharacterRepository repo,
        IPlayerAccountRepository accountRepo,
        IMediator mediator,
        CancellationToken ct)
    {
        var session = await sessionStore.GetSessionAsync(id);
        if (session is null)
            return Results.NotFound(new { error = $"Session {id} not found." });

        if (session.SelectedClass is null)
            return Results.BadRequest(new { error = "A character class must be selected before finalizing." });

        var resolvedName = request.CharacterName ?? session.CharacterName;
        if (string.IsNullOrWhiteSpace(resolvedName))
            return Results.BadRequest(new { error = "Character name is required. Provide it here or use the name step first." });

        var normalizedMode = request.DifficultyMode?.ToLowerInvariant() ?? "normal";
        if (normalizedMode is not "normal" and not "hardcore")
            return Results.BadRequest(new { error = "DifficultyMode must be \"normal\" or \"hardcore\"." });

        var accountId = GetAccountId(user);
        var account = await accountRepo.FindByIdAsync(accountId, ct);
        if (account is null) return Results.Unauthorized();

        var activeCount = await repo.GetActiveCountAsync(accountId, ct);
        if (activeCount >= account.MaxCharacterSlots)
            return Results.BadRequest(new { error = $"Character slot limit reached ({account.MaxCharacterSlots})." });

        if (await repo.NameExistsAsync(resolvedName, ct))
            return Results.Conflict(new { error = "Character name already taken." });

        var existing = await repo.GetByAccountIdAsync(accountId, ct);
        var usedSlots = existing.Select(c => c.SlotIndex).ToHashSet();
        var slotIndex = Enumerable.Range(1, account.MaxCharacterSlots)
            .First(i => !usedSlots.Contains(i));

        var engineResult = await mediator.Send(
            new FinalizeCreationSessionCommand
            {
                SessionId       = id,
                CharacterName   = resolvedName,
                DifficultyLevel = normalizedMode == "hardcore" ? "Hardcore" : "Normal",
            }, ct);

        if (!engineResult.Success || engineResult.Character is null)
            return Results.BadRequest(new { error = engineResult.Message });

        var engineChar = engineResult.Character;
        var attrsJson = JsonSerializer.Serialize(new Dictionary<string, int>
        {
            ["Strength"]     = engineChar.Strength,
            ["Dexterity"]    = engineChar.Dexterity,
            ["Constitution"] = engineChar.Constitution,
            ["Intelligence"] = engineChar.Intelligence,
            ["Wisdom"]       = engineChar.Wisdom,
            ["Charisma"]     = engineChar.Charisma,
            ["CurrentHealth"] = engineChar.Health,
            ["MaxHealth"]    = engineChar.MaxHealth,
            ["Gold"]         = engineChar.Gold,
        });

        var abilitySlugs = engineChar.LearnedAbilities.Keys
            .Select(pid => pid.Contains(':') ? pid[(pid.LastIndexOf(':') + 1)..] : pid)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        var character = new Character
        {
            AccountId      = accountId,
            SlotIndex      = slotIndex,
            Name           = resolvedName,
            ClassName      = session.SelectedClass.Name,
            DifficultyMode = normalizedMode,
            Attributes     = attrsJson,
            AbilitiesBlob  = JsonSerializer.Serialize(abilitySlugs),
        };

        var created = await repo.CreateAsync(character, ct);

        return Results.Created(
            $"/api/characters/{created.Id}",
            new CharacterDto(
                created.Id, created.SlotIndex, created.Name, created.ClassName,
                created.Level, created.Experience, created.LastPlayedAt, created.CurrentZoneId,
                DifficultyMode: created.DifficultyMode,
                IsOnline: false,
                IsHardcore: created.DifficultyMode == "hardcore"));
    }

    private static async Task<IResult> AbandonAsync(
        Guid id,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new AbandonCreationSessionCommand(id), ct);
        return result.Success
            ? Results.NoContent()
            : Results.NotFound(new { error = result.Message });
    }

    // Helpers
    private static Guid GetAccountId(ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
                 ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
                 ?? throw new InvalidOperationException("Account ID claim missing from token.");
        return Guid.Parse(value);
    }
}
