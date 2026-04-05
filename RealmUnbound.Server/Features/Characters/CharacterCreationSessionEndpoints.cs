using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
    // All new characters start at this location regardless of wizard choices.
    // Location selection is not exposed in the wizard; this will remain static until zone progression is designed.
    private const string DefaultStartingLocationSlug = "fenwick-market";

    private const int NameMinLength = 2;
    private const int NameMaxLength = 20;
    private static readonly System.Text.RegularExpressions.Regex NamePattern =
        new(@"^[a-zA-Z]+$", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static string? ValidateNameFormat(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Name is required.";
        var trimmed = name.Trim();
        if (trimmed.Length < NameMinLength) return $"Name must be at least {NameMinLength} characters.";
        if (trimmed.Length > NameMaxLength) return $"Name must be at most {NameMaxLength} characters.";
        if (!NamePattern.IsMatch(trimmed)) return "Name may only contain letters.";
        return null;
    }

    /// <summary>Maps all character creation session endpoints onto the application.</summary>
    /// <param name="app">The endpoint route builder to register routes on.</param>
    /// <returns>The same <paramref name="app"/> for chaining.</returns>
    public static IEndpointRouteBuilder MapCharacterCreationSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/character-creation/sessions")
            .WithTags("Character Creation")
            .RequireAuthorization();

        group.MapPost("/", BeginAsync);
        group.MapGet("/check-name", CheckNameAsync);
        group.MapGet("/{id:guid}", GetSessionAsync);
        group.MapPatch("/{id:guid}/name", SetNameAsync);
        group.MapPatch("/{id:guid}/class", SetClassAsync);
        group.MapPatch("/{id:guid}/species", SetSpeciesAsync);
        group.MapPatch("/{id:guid}/background", SetBackgroundAsync);
        group.MapPatch("/{id:guid}/attributes", SetAttributesAsync);
        group.MapPatch("/{id:guid}/equipment", SetEquipmentAsync);
        group.MapPatch("/{id:guid}/location", SetLocationAsync);
        group.MapGet("/{id:guid}/preview", GetPreviewAsync);
        group.MapPost("/{id:guid}/finalize", FinalizeAsync);
        group.MapDelete("/{id:guid}", AbandonAsync);

        return app;
    }

    private static async Task<IResult> BeginAsync(
        ClaimsPrincipal user,
        ICharacterCreationSessionStore sessionStore,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new BeginCreationSessionCommand(), ct);
        if (!result.Success)
            return Results.Problem("Could not initialize session content.", statusCode: StatusCodes.Status503ServiceUnavailable);

        // Stamp owner so subsequent requests can be ownership-checked.
        var session = await sessionStore.GetSessionAsync(result.SessionId);
        if (session is not null)
        {
            session.AccountId = GetAccountId(user);
            await sessionStore.UpdateSessionAsync(session);
        }

        return Results.Created($"/api/character-creation/sessions/{result.SessionId}", result);
    }

    private static async Task<IResult> GetSessionAsync(
        Guid id,
        ClaimsPrincipal user,
        ICharacterCreationSessionStore sessionStore,
        CancellationToken ct)
    {
        var ownerCheck = await VerifyOwnerAsync(id, GetAccountId(user), sessionStore);
        if (ownerCheck is not null) return ownerCheck;

        var session = await sessionStore.GetSessionAsync(id);
        return session is null
            ? Results.NotFound(new { error = $"Session {id} not found." })
            : Results.Ok(session);
    }

    private static async Task<IResult> CheckNameAsync(
        string? name,
        ICharacterRepository repo,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Results.Ok(new CheckNameAvailabilityResponse(false, "Name is required."));

        var formatError = ValidateNameFormat(name);
        if (formatError is not null)
            return Results.Ok(new CheckNameAvailabilityResponse(false, formatError));

        var taken = await repo.NameExistsAsync(name.Trim(), ct);
        return Results.Ok(new CheckNameAvailabilityResponse(
            Available: !taken,
            Error: taken ? "That name is already taken." : null));
    }

    private static async Task<IResult> SetNameAsync(
        Guid id,
        [FromBody] SetCreationNameRequest request,
        ClaimsPrincipal user,
        ICharacterCreationSessionStore sessionStore,
        IMediator mediator,
        ICharacterRepository repo,
        CancellationToken ct)
    {
        var ownerCheck = await VerifyOwnerAsync(id, GetAccountId(user), sessionStore);
        if (ownerCheck is not null) return ownerCheck;

        var formatError = ValidateNameFormat(request.CharacterName);
        if (formatError is not null)
            return Results.BadRequest(new { error = formatError });

        var taken = await repo.NameExistsAsync(request.CharacterName.Trim(), ct);
        if (taken)
            return Results.BadRequest(new { error = "That name is already taken." });

        var result = await mediator.Send(new SetCreationNameCommand(id, request.CharacterName), ct);
        return result.Success
            ? Results.Ok(result)
            : Results.BadRequest(new { error = result.Message });
    }

    private static async Task<IResult> SetClassAsync(
        Guid id,
        [FromBody] SetCreationClassRequest request,
        ClaimsPrincipal user,
        ICharacterCreationSessionStore sessionStore,
        IMediator mediator,
        CancellationToken ct)
    {
        var ownerCheck = await VerifyOwnerAsync(id, GetAccountId(user), sessionStore);
        if (ownerCheck is not null) return ownerCheck;

        var result = await mediator.Send(new SetCreationClassCommand(id, request.ClassName), ct);
        return result.Success
            ? Results.Ok(result)
            : Results.BadRequest(new { error = result.Message });
    }

    private static async Task<IResult> SetSpeciesAsync(
        Guid id,
        [FromBody] SetCreationSpeciesRequest request,
        ClaimsPrincipal user,
        ICharacterCreationSessionStore sessionStore,
        IMediator mediator,
        CancellationToken ct)
    {
        var ownerCheck = await VerifyOwnerAsync(id, GetAccountId(user), sessionStore);
        if (ownerCheck is not null) return ownerCheck;

        var result = await mediator.Send(new SetCreationSpeciesCommand(id, request.SpeciesSlug), ct);
        return result.Success
            ? Results.Ok(result)
            : Results.BadRequest(new { error = result.Message });
    }

    private static async Task<IResult> SetBackgroundAsync(
        Guid id,
        [FromBody] SetCreationBackgroundRequest request,
        ClaimsPrincipal user,
        ICharacterCreationSessionStore sessionStore,
        IMediator mediator,
        CancellationToken ct)
    {
        var ownerCheck = await VerifyOwnerAsync(id, GetAccountId(user), sessionStore);
        if (ownerCheck is not null) return ownerCheck;

        var result = await mediator.Send(new SetCreationBackgroundCommand(id, request.BackgroundId), ct);
        return result.Success
            ? Results.Ok(result)
            : Results.BadRequest(new { error = result.Message });
    }

    private static async Task<IResult> SetAttributesAsync(
        Guid id,
        [FromBody] SetCreationAttributesRequest request,
        ClaimsPrincipal user,
        ICharacterCreationSessionStore sessionStore,
        IMediator mediator,
        CancellationToken ct)
    {
        var ownerCheck = await VerifyOwnerAsync(id, GetAccountId(user), sessionStore);
        if (ownerCheck is not null) return ownerCheck;

        var result = await mediator.Send(new AllocateCreationAttributesCommand(id, request.Allocations), ct);
        return result.Success
            ? Results.Ok(result)
            : Results.BadRequest(new { error = result.Message });
    }

    private static async Task<IResult> SetEquipmentAsync(
        Guid id,
        [FromBody] SetCreationEquipmentPreferencesRequest request,
        ClaimsPrincipal user,
        ICharacterCreationSessionStore sessionStore,
        IMediator mediator,
        CancellationToken ct)
    {
        var ownerCheck = await VerifyOwnerAsync(id, GetAccountId(user), sessionStore);
        if (ownerCheck is not null) return ownerCheck;

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
        ClaimsPrincipal user,
        ICharacterCreationSessionStore sessionStore,
        IMediator mediator,
        CancellationToken ct)
    {
        var ownerCheck = await VerifyOwnerAsync(id, GetAccountId(user), sessionStore);
        if (ownerCheck is not null) return ownerCheck;

        var result = await mediator.Send(new SetCreationLocationCommand(id, request.LocationId), ct);
        return result.Success
            ? Results.Ok(result)
            : Results.BadRequest(new { error = result.Message });
    }

    private static async Task<IResult> GetPreviewAsync(
        Guid id,
        ClaimsPrincipal user,
        ICharacterCreationSessionStore sessionStore,
        IMediator mediator,
        CancellationToken ct)
    {
        var ownerCheck = await VerifyOwnerAsync(id, GetAccountId(user), sessionStore);
        if (ownerCheck is not null) return ownerCheck;

        var result = await mediator.Send(new GetCreationPreviewQuery(id), ct);
        if (!result.Success || result.Character is null)
            return Results.BadRequest(new { error = result.Message });

        var c = result.Character;
        return Results.Ok(new CharacterPreviewDto(
            ClassName: c.ClassName,
            SpeciesName: result.SpeciesDisplayName,
            BackgroundName: result.BackgroundDisplayName,
            Strength: c.Strength,
            Dexterity: c.Dexterity,
            Constitution: c.Constitution,
            Intelligence: c.Intelligence,
            Wisdom: c.Wisdom,
            Charisma: c.Charisma,
            Health: c.MaxHealth,
            Mana: c.MaxMana));
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

        var accountId = GetAccountId(user);
        if (session.AccountId is not null && session.AccountId != accountId)
            return Results.Forbid();

        if (session.Status == RealmEngine.Shared.Models.CreationSessionStatus.Finalized)
            return Results.BadRequest(new { error = "Session has already been finalized." });
        if (session.Status == RealmEngine.Shared.Models.CreationSessionStatus.Abandoned)
            return Results.BadRequest(new { error = "Cannot finalize an abandoned session." });

        if (session.SelectedClass is null)
            return Results.BadRequest(new { error = "A character class must be selected before finalizing." });
        if (session.SelectedSpecies is null)
            return Results.BadRequest(new { error = "A species must be selected before finalizing." });
        if (session.SelectedBackground is null)
            return Results.BadRequest(new { error = "A background must be selected before finalizing." });

        var resolvedName = request.CharacterName ?? session.CharacterName;
        if (string.IsNullOrWhiteSpace(resolvedName))
            return Results.BadRequest(new { error = "Character name is required. Provide it here or use the name step first." });

        var normalizedMode = request.DifficultyMode?.ToLowerInvariant() ?? "normal";
        if (normalizedMode is not "normal" and not "hardcore")
            return Results.BadRequest(new { error = "DifficultyMode must be \"normal\" or \"hardcore\"." });
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
                SessionId = id,
                CharacterName = resolvedName,
                DifficultyLevel = normalizedMode == "hardcore" ? "Hardcore" : "Normal",
            }, ct);

        if (!engineResult.Success || engineResult.Character is null)
            return Results.BadRequest(new { error = engineResult.Message });

        var engineChar = engineResult.Character;
        var attrsJson = JsonSerializer.Serialize(new Dictionary<string, int>
        {
            ["Strength"] = engineChar.Strength,
            ["Dexterity"] = engineChar.Dexterity,
            ["Constitution"] = engineChar.Constitution,
            ["Intelligence"] = engineChar.Intelligence,
            ["Wisdom"] = engineChar.Wisdom,
            ["Charisma"] = engineChar.Charisma,
            ["CurrentHealth"] = engineChar.Health,
            ["MaxHealth"] = engineChar.MaxHealth,
            ["CurrentMana"] = engineChar.Mana,
            ["MaxMana"] = engineChar.MaxMana,
            ["Gold"] = engineChar.Gold,
        });

        var equipmentDict = new Dictionary<string, string>();
        if (engineChar.EquippedMainHand is { Slug: { Length: > 0 } s1 }) equipmentDict["MainHand"] = s1;
        if (engineChar.EquippedOffHand is { Slug: { Length: > 0 } s2 }) equipmentDict["OffHand"] = s2;
        if (engineChar.EquippedHelmet is { Slug: { Length: > 0 } s3 }) equipmentDict["Helmet"] = s3;
        if (engineChar.EquippedShoulders is { Slug: { Length: > 0 } s4 }) equipmentDict["Shoulders"] = s4;
        if (engineChar.EquippedChest is { Slug: { Length: > 0 } s5 }) equipmentDict["Chest"] = s5;
        if (engineChar.EquippedBracers is { Slug: { Length: > 0 } s6 }) equipmentDict["Bracers"] = s6;
        if (engineChar.EquippedGloves is { Slug: { Length: > 0 } s7 }) equipmentDict["Gloves"] = s7;
        if (engineChar.EquippedBelt is { Slug: { Length: > 0 } s8 }) equipmentDict["Belt"] = s8;
        if (engineChar.EquippedLegs is { Slug: { Length: > 0 } s9 }) equipmentDict["Legs"] = s9;
        if (engineChar.EquippedBoots is { Slug: { Length: > 0 } s10 }) equipmentDict["Boots"] = s10;
        if (engineChar.EquippedNecklace is { Slug: { Length: > 0 } s11 }) equipmentDict["Necklace"] = s11;

        var inventoryItems = engineChar.Inventory
            .Where(i => !string.IsNullOrWhiteSpace(i.Slug))
            .Select(i => new { ItemRef = i.Slug, Quantity = 1 })
            .ToList();

        var abilitySlugs = engineChar.LearnedAbilities.Keys
            .Select(pid => pid.Contains(':') ? pid[(pid.LastIndexOf(':') + 1)..] : pid)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        var character = new Character
        {
            AccountId = accountId,
            SlotIndex = slotIndex,
            Name = resolvedName,
            ClassName = session.SelectedClass.Name,
            DifficultyMode = normalizedMode,
            Attributes = attrsJson,
            EquipmentBlob = JsonSerializer.Serialize(equipmentDict),
            InventoryBlob = JsonSerializer.Serialize(inventoryItems),
            AbilitiesBlob = JsonSerializer.Serialize(abilitySlugs),
            BackgroundId = session.SelectedBackground?.GetBackgroundId(),
            SpeciesSlug = session.SelectedSpecies?.Slug,
            CurrentZoneLocationSlug = DefaultStartingLocationSlug,
        };

        Character created;
        try
        {
            created = await repo.CreateAsync(character, ct);
        }
        catch (DbUpdateException)
        {
            return Results.Conflict(new { error = "Character name already taken." });
        }

        return Results.Created(
            $"/api/characters/{created.Id}",
            new CharacterDto(
                created.Id, created.SlotIndex, created.Name, created.ClassName,
                created.Level, created.Experience, created.LastPlayedAt, created.CurrentZoneId,
                DifficultyMode: created.DifficultyMode,
                IsOnline: false,
                IsHardcore: created.DifficultyMode == "hardcore",
                BackgroundId: created.BackgroundId,
                SpeciesSlug: created.SpeciesSlug,
                CurrentZoneLocationSlug: created.CurrentZoneLocationSlug));
    }

    private static async Task<IResult> AbandonAsync(
        Guid id,
        ClaimsPrincipal user,
        ICharacterCreationSessionStore sessionStore,
        IMediator mediator,
        CancellationToken ct)
    {
        var ownerCheck = await VerifyOwnerAsync(id, GetAccountId(user), sessionStore);
        if (ownerCheck is not null) return ownerCheck;

        var result = await mediator.Send(new AbandonCreationSessionCommand(id), ct);
        return result.Success
            ? Results.NoContent()
            : Results.NotFound(new { error = result.Message });
    }

    // Helpers

    /// <summary>
    /// Returns <see langword="null"/> when the caller owns the session (or it has no owner).
    /// Returns a 404 result when the session does not exist, or a 403 result when the caller is not the owner.
    /// </summary>
    private static async Task<IResult?> VerifyOwnerAsync(
        Guid sessionId,
        Guid callerId,
        ICharacterCreationSessionStore store)
    {
        var session = await store.GetSessionAsync(sessionId);
        if (session is null)
            return Results.NotFound(new { error = $"Session {sessionId} not found." });
        if (session.AccountId is not null && session.AccountId != callerId)
            return Results.Forbid();
        return null;
    }

    private static Guid GetAccountId(ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
                 ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
                 ?? throw new InvalidOperationException("Account ID claim missing from token.");
        return Guid.Parse(value);
    }
}
