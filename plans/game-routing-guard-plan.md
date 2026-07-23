# Game Routing Guard — Implementation Plan

## `/Game` Entry-Point Component with Session Discovery & Resume

**Status:** Draft  
**Date:** 2026-07-22  
**Phase:** 1 — Design (implementation to follow in separate phase)

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Routing State Machine](#routing-state-machine)
3. [Component Specification: `GameEntry.razor`](#component-specification-gameentryrazor)
4. [MudBlazor Resume Dialog Design](#mudblazor-resume-dialog-design)
5. [API Call Sequence & State Management](#api-call-sequence--state-management)
6. [NavMenu Update Specification](#navmenu-update-specification)
7. [Edge Cases & Error Handling](#edge-cases--error-handling)
8. [Testing Strategy](#testing-strategy)
9. [File Manifest](#file-manifest)

---

## Architecture Overview

### Problem

The current `NavMenu` "Play" button navigates directly to `/game/characterselect`, bypassing two opportunities:

1. **Session restoration** — If the player already has an active game session (e.g., after a page refresh), they should be offered to resume directly rather than re-selecting a character.
2. **Empty-account routing** — If the account has no characters, navigating to `/Game/CharacterSelect` shows an empty list with a "Create New Character" button. The routing guard can short-circuit this directly to `/Game/CreateCharacter`.

### Solution

Insert a new `GameEntry.razor` component at route `@page "/Game"` that acts as a **routing guard and session-discovery hub**. It calls two REST API endpoints (`GetCharactersAsync`, `GetLastSessionAsync`) and routes the user to the appropriate destination based on the results.

```
                      ┌──────────────┐
                      │   NavMenu    │
                      │ "Play" btn   │
                      └──────┬───────┘
                             │ Href="/game"
                             ▼
                      ┌──────────────┐
                      │  GameEntry   │  ← NEW: routing guard
                      │  /Game       │
                      └──────┬───────┘
                             │
              ┌──────────────┼──────────────────┐
              │              │                  │
              ▼              ▼                  ▼
     ┌──────────────┐ ┌──────────────┐ ┌──────────────┐
     │ CreateChar   │ │ CharSelect   │ │ Resume Modal │
     │ /Game/       │ │ /Game/       │ │ (MudDialog)  │
     │ CreateChar   │ │ CharSelect   │ │ → /Game/Play │
     └──────────────┘ └──────────────┘ └──────────────┘
```

The existing three game pages (`Game.razor`, `CharacterSelect.razor`, `CreateCharacter.razor`) retain their independent auth guards and continue to work when navigated to directly.

---

## Routing State Machine

```
                    ┌──────────────────┐
                    │   OnInitialized   │
                    │   Wait for Auth   │
                    └────────┬─────────┘
                             │
                    ┌────────▼─────────┐
                    │ Auth.IsAuthReady? │
                    └───┬──────────┬───┘
                        │ No       │ Yes
                        ▼          ▼
                 ┌──────────┐  ┌──────────────┐
                 │ Subscribe │  │ Auth.IsLogged│
                 │ OnChange  │  │ In?          │
                 │ Show      │  └──┬───────┬───┘
                 │ spinner   │     │ No    │ Yes
                 └──────────┘     ▼       ▼
                            ┌────────┐ ┌────────────────┐
                            │ Navigate│ │ TryRefreshAsync│
                            │ /login  │ └───────┬────────┘
                            └────────┘         │
                                    ┌──────────▼──────────┐
                                    │ Token valid?         │
                                    └──────┬──────────┬────┘
                                           │ No       │ Yes
                                           ▼          ▼
                                    ┌────────┐ ┌──────────────────┐
                                    │ Navigate│ │ Call GetCharacters│
                                    │ /login  │ │ Async()           │
                                    └────────┘ └────────┬─────────┘
                                                        │
                                               ┌────────▼──────────┐
                                               │ API call OK?       │
                                               └──┬────────────┬───┘
                                                  │ Yes        │ No (error)
                                                  ▼            ▼
                                          ┌──────────────┐ ┌──────────┐
                                          │ Characters   │ │ Show     │
                                          │ count?       │ │ error +  │
                                          └──┬──┬───┬────┘ │ retry UI │
                                             │  │   │      └──────────┘
                                   ┌─────────┘  │   └──────────┐
                                   │ 0 chars    │ >0 chars      │
                                   ▼            ▼               │
                            ┌────────────┐ ┌─────────────────┐  │
                            │ Navigate   │ │ Call GetLast    │  │
                            │ /Game/     │ │ SessionAsync()  │  │
                            │ CreateChar │ └───────┬─────────┘  │
                            └────────────┘         │            │
                                          ┌────────▼──────────┐ │
                                          │ API call OK?       │ │
                                          └──┬────────────┬───┘ │
                                             │ Yes        │ No  │
                                             ▼            ▼     │
                                     ┌──────────────┐ ┌──────────┐
                                     │ Session      │ │ Show     │
                                     │ present?     │ │ error +  │
                                     └──┬───────┬───┘ │ retry UI │
                                        │       │     └──────────┘
                                        │ Yes   │ No
                                        ▼       ▼
                               ┌────────────┐ ┌────────────────┐
                               │ Show Resume│ │ Navigate       │
                               │ MudDialog  │ │ /Game/         │
                               └──┬────┬────┘ │ CharacterSelect│
                                  │    │       └────────────────┘
                         ┌────────┘    └─────────┐
                         │ Resume                │ Choose Different
                         ▼                       ▼
                  ┌────────────┐          ┌────────────────┐
                  │ Navigate   │          │ Navigate       │
                  │ /Game/Play │          │ /Game/         │
                  └────────────┘          │ CharacterSelect│
                                          └────────────────┘
```

**Note on independent page guards:** The existing pages (`/Game/Play`, `/Game/CharacterSelect`, `/Game/CreateCharacter`) already have their own auth guards and continue to function independently. The `GameEntry` guard is additive — it improves the entry experience but does not replace the individual page guards.

---

## Component Specification: `GameEntry.razor`

### Location

```
Veldrath.GameClient.Components/Components/Pages/GameEntry.razor
```

This lives alongside the existing game pages in the RCL so it shares `@layout GameLayout` from `_Imports.razor`.

### Route

```razor
@page "/Game"
```

### Pattern

Uses the **code-behind partial class pattern** (per [`.github/instructions/blazor-component-development.md`](.github/instructions/blazor-component-development.md:25)):

- `GameEntry.razor` — Razor markup only
- `GameEntry.razor.cs` — C# logic: `[Inject]` services, parameters, lifecycle methods

### Dependencies (Injected)

| Service | Purpose |
|---------|---------|
| `AuthStateServiceBase` | Auth gate (`IsAuthReady`, `IsLoggedIn`, `OnChange`, `TryRefreshAsync`) |
| `IGameApiClient` | REST calls: `GetCharactersAsync()`, `GetLastSessionAsync()` |
| `NavigationManager` | Redirects (`NavigateTo`) |
| `IDialogService` (MudBlazor) | Show resume dialog |
| `ILogger<GameEntry>` | Structured logging |

### State Properties (Code-Behind)

```csharp
private enum EntryPageState { Loading, WaitingForAuth, Error, Ready }

private EntryPageState _state = EntryPageState.Loading;
private string? _errorMessage;
private bool _hasCharacters;
private LastSessionDto? _lastSession;
private CancellationTokenSource? _authTimeoutCts;
```

### Lifecycle

```csharp
// OnInitialized: subscribe to Auth.OnChange
protected override void OnInitialized()
{
    Auth.OnChange += OnAuthStateChanged;
}

// OnInitializedAsync: run routing logic
protected override async Task OnInitializedAsync()
{
    if (!Auth.IsAuthReady)
    {
        _state = EntryPageState.WaitingForAuth;
        StartAuthTimeout();
        return;
    }

    if (!Auth.IsLoggedIn)
    {
        Navigation.NavigateTo("/login");
        return;
    }

    await DiscoverAndRouteAsync();
}

// OnAuthStateChanged: re-evaluate when auth becomes ready
private void OnAuthStateChanged()
{
    if (!Auth.IsAuthReady) return;

    _authTimeoutCts?.Cancel();

    if (!Auth.IsLoggedIn)
    {
        Navigation.NavigateTo("/login");
        return;
    }

    if (_state == EntryPageState.WaitingForAuth)
    {
        _state = EntryPageState.Loading;
        InvokeAsync(async () =>
        {
            await DiscoverAndRouteAsync();
            StateHasChanged();
        });
    }
}

// IAsyncDisposable: unsubscribe Auth.OnChange, cancel timeout
public async ValueTask DisposeAsync()
{
    Auth.OnChange -= OnAuthStateChanged;
    _authTimeoutCts?.Cancel();
    _authTimeoutCts?.Dispose();
}
```

### `DiscoverAndRouteAsync` Core Logic

```csharp
private async Task DiscoverAndRouteAsync()
{
    _state = EntryPageState.Loading;
    _errorMessage = null;

    try
    {
        var tokenValid = await Auth.TryRefreshAsync();
        if (!tokenValid || Auth.AccessToken is null)
        {
            Navigation.NavigateTo("/login");
            return;
        }

        // Step 1: Check character list
        var characters = await Api.GetCharactersAsync();
        if (characters.Count == 0)
        {
            Navigation.NavigateTo("/Game/CreateCharacter");
            return;
        }

        _hasCharacters = true;

        // Step 2: Check last session
        var session = await Api.GetLastSessionAsync();
        if (session is null)
        {
            Navigation.NavigateTo("/Game/CharacterSelect");
            return;
        }

        _lastSession = session;
        _state = EntryPageState.Ready;

        // Step 3: Show resume dialog
        await ShowResumeDialogAsync(session);
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Failed to discover game session");
        _state = EntryPageState.Error;
        _errorMessage = "Failed to connect to the game server. Please try again.";
    }
}
```

### Razor Markup

```razor
@page "/Game"
@implements IAsyncDisposable
@using MudBlazor
@using Veldrath.Auth.Blazor
@using Veldrath.GameClient.Core.Abstractions
@using Veldrath.Contracts.Characters
@inject AuthStateServiceBase Auth
@inject IGameApiClient Api
@inject NavigationManager Navigation
@inject IDialogService DialogService
@inject ILogger<GameEntry> Logger

@switch (_state)
{
    case EntryPageState.WaitingForAuth:
        <MudStack Row="false" AlignItems="AlignItems.Center" Justify="Justify.Center" Style="min-height: 60vh;">
            <MudProgressCircular Color="Color.Primary" Size="Size.Large" Indeterminate="true" Class="mb-3" />
            <MudText Typo="Typo.body2" Color="Color.Dark">Waiting for authentication...</MudText>
        </MudStack>
        break;

    case EntryPageState.Loading:
        <MudStack Row="false" AlignItems="AlignItems.Center" Justify="Justify.Center" Style="min-height: 60vh;">
            <MudProgressCircular Color="Color.Primary" Size="Size.Large" Indeterminate="true" Class="mb-3" />
            <MudText Typo="Typo.body2" Color="Color.Dark">Preparing your adventure...</MudText>
        </MudStack>
        break;

    case EntryPageState.Error:
        <MudStack Row="false" AlignItems="AlignItems.Center" Justify="Justify.Center" Style="min-height: 60vh;" Spacing="4">
            <MudAlert Severity="Severity.Error" Class="mb-4">
                <MudText Typo="Typo.h6">Connection Error</MudText>
                <MudText>@_errorMessage</MudText>
            </MudAlert>
            <MudStack Row="true" Spacing="2">
                <MudButton Variant="Variant.Filled" Color="Color.Primary" OnClick="DiscoverAndRouteAsync">Retry</MudButton>
                <MudButton Variant="Variant.Outlined" Href="/">Back to Home</MudButton>
            </MudStack>
        </MudStack>
        break;

    case EntryPageState.Ready:
        @* Dialog is shown programmatically; render a minimal placeholder *@
        <MudStack Row="false" AlignItems="AlignItems.Center" Justify="Justify.Center" Style="min-height: 60vh;">
            <MudProgressCircular Color="Color.Primary" Size="Size.Large" Indeterminate="true" Class="mb-3" />
            <MudText Typo="Typo.body2" Color="Color.Dark">Loading...</MudText>
        </MudStack>
        break;
}
```

### Design Rationale

**Why a full component instead of modifying `CharacterSelect`?** The CharacterSelect page already has complex state (Hub connection, character subscriptions, selection flow). Adding session-discovery logic would bloat an already large component. A dedicated routing-guard component is:

1. **Single responsibility** — It only decides where to route; it doesn't manage Hub connections or character selection UI
2. **Testable in isolation** — Can be tested without the full Hub/GameState dependency graph
3. **Non-breaking** — Does not change existing page behavior; purely additive

**Why not use Blazor `[Authorize]` attribute?** The `[Authorize]` attribute only gates on `AuthenticationState` (claims-based). It cannot perform the post-auth routing logic (character discovery, session check, dialog). The auth gate in `GameEntry` uses the same `AuthStateServiceBase` pattern used by all existing game pages.

**Why call `GetCharactersAsync` before `GetLastSessionAsync`?** The `/api/characters/last-session` endpoint returns `null` both when there's no session AND when there are no characters. Calling `GetCharactersAsync` first gives us explicit knowledge of whether characters exist, allowing the "no characters → CreateCharacter" short-circuit without an ambiguous null.

---

## MudBlazor Resume Dialog Design

### Dialog Component: `ResumeSessionDialog.razor`

**Location:** `Veldrath.GameClient.Components/Components/Shared/ResumeSessionDialog.razor`

This is a reusable dialog component (used in at least one place; potentially reusable in other session-restore flows). Follows the code-behind pattern.

### Parameters

```csharp
// ResumeSessionDialog.razor.cs
[CascadingParameter]
private MudDialogInstance MudDialog { get; set; } = null!;

[Parameter]
public string CharacterName { get; set; } = string.Empty;

[Parameter]
public string ZoneName { get; set; } = string.Empty;

[Parameter]
public DateTimeOffset LastPlayedAt { get; set; }
```

### Dialog Content

```razor
<MudDialog>
    <DialogContent>
        <MudStack Spacing="4">
            <MudText Typo="Typo.h5">Active Game Session Found</MudText>

            <MudText Typo="Typo.body1">
                You have an active game session as
                <MudText Typo="Typo.body1" Color="Color.Primary" Inline="true">
                    <strong>@CharacterName</strong>
                </MudText>@(string.IsNullOrEmpty(ZoneName) ? "." : $" in {ZoneName}.")
            </MudText>

            @if (LastPlayedAt != default)
            {
                <MudText Typo="Typo.body2" Color="Color.Dark">
                    Last played: @LastPlayedAt.ToLocalTime().ToString("g")
                </MudText>
            }

            <MudText Typo="Typo.body2">
                Would you like to resume where you left off, or choose a different character?
            </MudText>
        </MudStack>
    </DialogContent>

    <DialogActions>
        <MudStack Row="true" Spacing="2" Justify="Justify.End">
            <MudButton Variant="Variant.Outlined"
                       OnClick="OnChooseDifferent">
                Choose Different Character
            </MudButton>
            <MudButton Variant="Variant.Filled"
                       Color="Color.Primary"
                       OnClick="OnResume">
                Resume
            </MudButton>
        </MudStack>
    </DialogActions>
</MudDialog>
```

### Code-Behind

```csharp
// ResumeSessionDialog.razor.cs
/// <summary>User chose to resume the existing session.</summary>
private void OnResume()
{
    MudDialog.Close(DialogResult.Ok(true));
}

/// <summary>User chose to pick a different character.</summary>
private void OnChooseDifferent()
{
    MudDialog.Close(DialogResult.Ok(false));
}
```

### Invocation from `GameEntry`

```csharp
private async Task ShowResumeDialogAsync(LastSessionDto session)
{
    var parameters = new DialogParameters
    {
        [nameof(ResumeSessionDialog.CharacterName)] = session.CharacterName,
        [nameof(ResumeSessionDialog.ZoneName)] = session.ZoneId ?? "the world map",
        [nameof(ResumeSessionDialog.LastPlayedAt)] = DateTimeOffset.UtcNow // or derive from session
    };

    var options = new DialogOptions
    {
        CloseOnEscapeKey = false,
        DisableBackdropClick = true,
        MaxWidth = MaxWidth.Small,
        FullWidth = true
    };

    var dialog = await DialogService.ShowAsync<ResumeSessionDialog>(
        "Resume Session", parameters, options);

    var result = await dialog.Result;

    if (result?.Data is bool shouldResume && shouldResume)
    {
        Navigation.NavigateTo("/Game/Play");
    }
    else
    {
        Navigation.NavigateTo("/Game/CharacterSelect");
    }
}
```

### Design Decisions

- **`CloseOnEscapeKey = false`** — The user must make an explicit choice; dismissing the dialog leaves the user on the `GameEntry` loading screen with no way forward
- **`DisableBackdropClick = true`** — Same reason; prevents accidental dismissal
- **`FullWidth = true`** — Ensures the dialog is usable on mobile viewports
- **`ZoneName` displays "the world map" when `ZoneId` is null** — Covers the case where the last session was on the region map (not inside a zone)
- **Dialog returns `bool`** — `true` = resume, `false` = choose different. This is simpler than an enum for a binary choice

---

## API Call Sequence & State Management

### Call Order (Critical Path)

```
1. IsAuthReady?                       → Block on OnChange if false
2. IsLoggedIn?                        → /login if false
3. TryRefreshAsync()                  → /login if fails
4. GetCharactersAsync()               → /Game/CreateCharacter if empty
5. GetLastSessionAsync()              → /Game/CharacterSelect if null
6. ShowResumeDialog(session)          → /Game/Play or /Game/CharacterSelect
```

**Why this order?** Steps 1-3 are the auth gate (identical pattern to all existing game pages). Step 4 must precede step 5 because `GetLastSessionAsync` returns null for both "no session" and "no characters" — we need step 4 to disambiguate.

### State Transitions

| State | Trigger | Next State |
|-------|---------|------------|
| `Loading` | Initial render | — |
| `Loading` | Auth not ready | `WaitingForAuth` |
| `Loading` | Not logged in | redirect → `/login` |
| `Loading` | API error | `Error` |
| `Loading` | No characters | redirect → `/Game/CreateCharacter` |
| `Loading` | Has characters, no session | redirect → `/Game/CharacterSelect` |
| `Loading` | Has session | `Ready` → show dialog |
| `WaitingForAuth` | `OnChange` fires, logged in | `Loading` → re-run discovery |
| `WaitingForAuth` | 10-second timeout + still not ready | `Error` (auth timeout) |
| `Error` | User clicks "Retry" | `Loading` → re-run discovery |

### Parallel vs Sequential

API calls are **sequential**, not parallel:

1. `GetCharactersAsync` must complete first (determines whether to even call `GetLastSessionAsync`)
2. `GetLastSessionAsync` is only called if characters exist

This avoids an unnecessary API call when the account has no characters.

---

## NavMenu Update Specification

### Current Code

File: [`Veldrath.Web/Components/Layout/NavMenu.razor`](Veldrath.Web/Components/Layout/NavMenu.razor:12)

```razor
<MudButton StartIcon="@Icons.Material.Filled.SportsEsports" Href="/game/characterselect">Play</MudButton>
```

### Required Change

Change `Href` from `/game/characterselect` to `/game`:

```razor
<MudButton StartIcon="@Icons.Material.Filled.SportsEsports" Href="/game">Play</MudButton>
```

### Impact Analysis

| What | Impact |
|------|--------|
| Existing `/Game/CharacterSelect` direct navigation | Unchanged — still works via direct URL or "Choose Different Character" in resume dialog |
| Existing `/Game/CreateCharacter` direct navigation | Unchanged — still works via direct URL |
| Existing `/Game/Play` direct navigation | Unchanged — still works via direct URL (e.g., page refresh mid-game) |
| User flow change | Users now hit `GameEntry` first, which may show a resume dialog or redirect |

---

## Edge Cases & Error Handling

### Auth Scenarios

| Scenario | Behavior |
|----------|----------|
| Auth not ready on initial render | Show `WaitingForAuth` spinner; subscribe to `OnChange`; 10-second timeout → error with retry |
| Auth state cleared mid-discovery (delegating handler 401) | `OnAuthStateChanged` fires; if `!IsLoggedIn`, redirect to `/login` |
| Token refresh fails (`TryRefreshAsync` → false) | Redirect to `/login` (same as all existing game pages) |

### API Scenarios

| Scenario | Behavior |
|----------|----------|
| `GetCharactersAsync` succeeds, empty list | Redirect to `/Game/CreateCharacter` |
| `GetCharactersAsync` succeeds, has characters | Continue to `GetLastSessionAsync` |
| `GetCharactersAsync` fails (network/500) | Show error state with Retry button |
| `GetLastSessionAsync` succeeds, returns null | Redirect to `/Game/CharacterSelect` |
| `GetLastSessionAsync` succeeds, returns session | Show resume dialog |
| `GetLastSessionAsync` fails (network/500) | Show error state with Retry button |
| Both API calls fail | Show error state with Retry button |

### Navigation Scenarios

| Scenario | Behavior |
|----------|----------|
| User navigates directly to `/Game/Play` | `Game.razor` handles its own auth guard + session restore; no change needed |
| User navigates directly to `/Game/CharacterSelect` | `CharacterSelect.razor` handles its own auth guard; no change needed |
| User navigates directly to `/Game/CreateCharacter` | `CreateCharacter.razor` handles its own auth guard; no change needed |
| User refreshes browser on `/Game/Play` | Blazor circuit lost; `Game.razor` calls `RestoreSessionFromServerAsync` — no change needed |
| User closes resume dialog via browser back | Dialog has `DisableBackdropClick=true` and `CloseOnEscapeKey=false`; user must choose |

### Session Edge Cases

| Scenario | Behavior |
|----------|----------|
| `LastSessionDto.ZoneId` is null (player was on region map) | Dialog shows "in the world map" instead of zone name |
| `LastSessionDto.CharacterName` is empty/missing | Dialog shows "as your character" (fallback) |
| Multiple browser tabs click "Play" simultaneously | Each tab has its own Blazor circuit; `GameEntry` handles independently. The `GameHub.SelectCharacter` claim mechanism (30s grace period) handles the double-claim on the server side |

### Race Conditions

| Condition | Mitigation |
|-----------|------------|
| `OnAuthStateChanged` fires during `DiscoverAndRouteAsync` | `DiscoverAndRouteAsync` checks `Auth.IsLoggedIn` at the top; if auth was cleared, the `OnAuthStateChanged` handler already navigated to `/login` |
| Double-click on "Retry" button | `DiscoverAndRouteAsync` sets `_state = Loading` and `_errorMessage = null` immediately; the second click sees the loading state and the button is not rendered |
| User clicks browser back from resume dialog | Dialog is modal; MudBlazor prevents back-navigation while dialog is open |

---

## Testing Strategy

### Test Project

```
Veldrath.GameClient.Components.Tests/
```

All tests use **bunit** v2.6.2 with the existing `BunitContext` base class pattern and `FakeXxx` stubs from `Infrastructure/`.

### Test File

```
Veldrath.GameClient.Components.Tests/GameEntryPageTests.cs
```

### Test Infrastructure Additions

#### `FakeGameApiClient` Changes

The existing [`FakeGameApiClient`](Veldrath.GameClient.Components.Tests/Infrastructure/FakeGameApiClient.cs) already has:
- `Characters` property (list)
- `LastSessionResult` property
- `GetLastSessionCallCount` tracking

**No changes needed** — the existing `FakeGameApiClient` is already sufficient for `GameEntry` testing.

#### `FakeAuthStateService` Changes

The existing [`FakeAuthStateService`](Veldrath.GameClient.Components.Tests/Infrastructure/FakeAuthStateService.cs) already has:
- `IsLoggedInOverride` (default `true`)
- `TryRefreshResult` (default `true`)

**Addition needed:** Ability to simulate `IsAuthReady = false` at construction time.

```csharp
// New property:
public bool IsAuthReadyOverride { get; set; } = true;

// Modify constructor to respect the override:
public FakeAuthStateService(IVeldrathAuthApiClient api) : base(api)
{
    _accessToken = "__test_mode__";
    _refreshToken = "__test_refresh__";
    IsAuthReady = IsAuthReadyOverride; // was: IsAuthReady = true;
}
```

Actually, since `IsAuthReady` is a `protected set` property on the base class, the test can set it directly after construction: `_fakeAuth.IsAuthReady = false;` — this is simpler and doesn't require changes to the existing constructor.

#### `FakeDialogService`

A test-only fake for `IDialogService` to verify dialog invocation and simulate user choices:

```csharp
// Veldrath.GameClient.Components.Tests/Infrastructure/FakeDialogService.cs
public sealed class FakeDialogService : IDialogService
{
    public IDialogReference? LastDialog { get; private set; }
    public DialogParameters? LastParameters { get; private set; }

    // Configurable: what the dialog returns
    public bool DialogResultValue { get; set; } = true;

    public IDialogReference Show<T>(string title, DialogParameters parameters, DialogOptions options)
        where T : ComponentBase
    {
        LastDialog = new FakeDialogReference(DialogResultValue);
        LastParameters = parameters;
        return LastDialog;
    }

    public Task<IDialogReference> ShowAsync<T>(string title, DialogParameters parameters, DialogOptions options)
        where T : ComponentBase
    {
        LastDialog = new FakeDialogReference(DialogResultValue);
        LastParameters = parameters;
        return Task.FromResult(LastDialog);
    }

    // Other IDialogService members — throw NotSupportedException (not used by GameEntry)
    public IDialogReference Show(Type componentType, string title, DialogParameters parameters, DialogOptions options)
        => throw new NotSupportedException();
    // ... etc.
}

internal sealed class FakeDialogReference(bool resultValue) : IDialogReference
{
    public Task<DialogResult> Result => Task.FromResult(DialogResult.Ok(resultValue));
    // Other members — throw NotSupportedException
}
```

**Note:** The `FakeDialogService` follows the project's convention of `FakeXxx` stubs (Rule 3: "Prefer FakeXxx stub classes over mocking frameworks").

### Test Cases

#### 1. Auth Gate Tests

| # | Test | Setup | Assertion |
|---|------|-------|-----------|
| 1.1 | `Redirects_To_Login_When_Not_Logged_In` | `IsLoggedInOverride = false`, `IsAuthReady = true` | Component renders; `NavigationManager.Uri` contains `/login` |
| 1.2 | `Shows_WaitingForAuth_When_Auth_Not_Ready` | `IsAuthReady = false` | Markup contains "Waiting for authentication" |
| 1.3 | `Transitions_From_WaitingForAuth_When_Auth_Ready` | Start `IsAuthReady = false`, then set `true` + fire `OnChange` | `DiscoverAndRouteAsync` is triggered |
| 1.4 | `Redirects_To_Login_When_Token_Refresh_Fails` | `TryRefreshResult = false` | `NavigationManager.Uri` contains `/login` |

#### 2. Character Discovery Tests

| # | Test | Setup | Assertion |
|---|------|-------|-----------|
| 2.1 | `Redirects_To_CreateCharacter_When_No_Characters` | `Characters = []` | `NavigationManager.Uri` contains `/Game/CreateCharacter` |
| 2.2 | `Redirects_To_CharacterSelect_When_Has_Characters_No_Session` | `Characters = [ch1]`, `LastSessionResult = null` | `NavigationManager.Uri` contains `/Game/CharacterSelect` |
| 2.3 | `Does_Not_Call_GetLastSession_When_No_Characters` | `Characters = []` | `GetLastSessionCallCount == 0` |

#### 3. Session Resume Dialog Tests

| # | Test | Setup | Assertion |
|---|------|-------|-----------|
| 3.1 | `Shows_Resume_Dialog_When_Session_Exists` | `Characters = [ch1]`, `LastSessionResult = valid` | `FakeDialogService.LastDialog` is not null; parameters contain `CharacterName` |
| 3.2 | `Navigates_To_Play_On_Resume` | Dialog returns `true` | `NavigationManager.Uri` contains `/Game/Play` |
| 3.3 | `Navigates_To_CharacterSelect_On_Choose_Different` | Dialog returns `false` | `NavigationManager.Uri` contains `/Game/CharacterSelect` |
| 3.4 | `Dialog_Uses_World_Map_When_ZoneId_Is_Null` | `LastSessionResult.ZoneId = null` | Dialog parameters: `ZoneName` = "the world map" |

#### 4. Error Handling Tests

| # | Test | Setup | Assertion |
|---|------|-------|-----------|
| 4.1 | `Shows_Error_When_GetCharacters_Fails` | `FakeGameApiClient` throws on `GetCharactersAsync` | Markup contains "Connection Error" + Retry button |
| 4.2 | `Shows_Error_When_GetLastSession_Fails` | `Characters = [ch1]`, `FakeGameApiClient` throws on `GetLastSessionAsync` | Markup contains "Connection Error" + Retry button |
| 4.3 | `Retry_Button_Reattempts_Discovery` | Error state, click Retry | `GetCharactersCallCount` increments; error clears |
| 4.4 | `Auth_Timeout_Shows_Error_After_10_Seconds` | `IsAuthReady = false`, wait 10s+ | Markup contains "longer than expected" error |

#### 5. NavMenu Integration Test

| # | Test | Setup | Assertion |
|---|------|-------|-----------|
| 5.1 | `Play_Button_Links_To_Game_Root` | Render `NavMenu` with logged-in state | Play button `Href` is `/game` (not `/game/characterselect`) |

#### 6. Loading State Tests

| # | Test | Setup | Assertion |
|---|------|-------|-----------|
| 6.1 | `Shows_Loading_Spinner_During_API_Calls` | Normal setup, slow API | Markup contains `MudProgressCircular` (use `WaitForState` or render-verify pattern) |
| 6.2 | `Loading_Spinner_Hides_After_Error` | API throws | `MudProgressCircular` no longer in markup; `MudAlert` present |

### Test Setup Pattern

Following the existing [`CharacterSelectPageTests`](Veldrath.GameClient.Components.Tests/CharacterSelectPageTests.cs:22-55) pattern:

```csharp
public class GameEntryPageTests : BunitContext
{
    private readonly FakeGameApiClient _fakeApi;
    private readonly FakeAuthStateService _fakeAuth;
    private readonly FakeVeldrathAuthApiClient _fakeAuthApi;
    private readonly FakeDialogService _fakeDialog;
    private readonly GameStateService _gameState;

    public GameEntryPageTests()
    {
        _fakeApi = new FakeGameApiClient();
        _fakeAuthApi = new FakeVeldrathAuthApiClient();
        _fakeAuth = new FakeAuthStateService(_fakeAuthApi);
        _fakeDialog = new FakeDialogService();
        _gameState = new GameStateService();

        Services.AddSingleton<IGameApiClient>(_fakeApi);
        Services.AddSingleton<AuthStateServiceBase>(_fakeAuth);
        Services.AddSingleton<IVeldrathAuthApiClient>(_fakeAuthApi);
        Services.AddSingleton<IDialogService>(_fakeDialog);
        Services.AddSingleton<IGameStateService>(_gameState);
        Services.AddSingleton(_gameState);
        Services.AddSingleton<ILogger<GameEntry>>(NullLogger<GameEntry>.Instance);
        Services.AddMudServices();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Veldrath:ServerUrl"] = "http://localhost:9000",
            })
            .Build();
        Services.AddSingleton<IConfiguration>(config);
    }
}
```

**Note:** `GameEntry` does NOT require `IGameHubConnectionService` — it only makes REST API calls. This keeps the test setup simpler than `CharacterSelectPageTests` (no hub connection dependency).

### Non-Goals (What NOT to Test)

- **MudBlazor dialog rendering** — MudBlazor's internal behavior is already tested by the library
- **`ResumeSessionDialog` rendering in isolation** — Only test that `GameEntry` invokes the dialog with correct parameters; test the dialog component itself only if it gains logic beyond simple parameter display
- **Timer precision** — Auth timeout test uses a reasonable delay (e.g., `Task.Delay(50)` with a short timeout set in the fake) rather than testing exact 10-second timing
- **`NavigationManager` URI parsing** — Test that `NavigateTo` was called with the expected relative URI, not absolute URI resolution

---

## File Manifest

### Files to Create

| File | Purpose |
|------|---------|
| `Veldrath.GameClient.Components/Components/Pages/GameEntry.razor` | Routing guard component — razor markup |
| `Veldrath.GameClient.Components/Components/Pages/GameEntry.razor.cs` | Routing guard component — code-behind logic |
| `Veldrath.GameClient.Components/Components/Shared/ResumeSessionDialog.razor` | Resume session MudDialog — razor markup |
| `Veldrath.GameClient.Components/Components/Shared/ResumeSessionDialog.razor.cs` | Resume session MudDialog — code-behind logic |
| `Veldrath.GameClient.Components.Tests/GameEntryPageTests.cs` | bunit tests for `GameEntry` |
| `Veldrath.GameClient.Components.Tests/Infrastructure/FakeDialogService.cs` | Test stub for `IDialogService` |

### Files to Modify

| File | Change | Purpose |
|------|--------|---------|
| [`Veldrath.Web/Components/Layout/NavMenu.razor`](Veldrath.Web/Components/Layout/NavMenu.razor:12) | Change `Href="/game/characterselect"` → `Href="/game"` | Route "Play" button through `GameEntry` guard |
| [`Veldrath.GameClient.Components.Tests/Infrastructure/FakeGameApiClient.cs`](Veldrath.GameClient.Components.Tests/Infrastructure/FakeGameApiClient.cs) | Add configurable exception throwing for `GetCharactersAsync` and `GetLastSessionAsync` | Enable error-path testing (e.g., `ExceptionToThrow` properties) |

### Files NOT Modified

These files remain unchanged — `GameEntry` is purely additive:

- [`Veldrath.GameClient.Components/Components/Pages/Game.razor`](Veldrath.GameClient.Components/Components/Pages/Game.razor) — `/Game/Play` — no changes needed
- [`Veldrath.GameClient.Components/Components/Pages/CharacterSelect.razor`](Veldrath.GameClient.Components/Components/Pages/CharacterSelect.razor) — `/Game/CharacterSelect` — no changes needed
- [`Veldrath.GameClient.Components/Components/Pages/CreateCharacter.razor`](Veldrath.GameClient.Components/Components/Pages/CreateCharacter.razor) — `/Game/CreateCharacter` — no changes needed
- [`Veldrath.GameClient.Components/Components/Pages/_Imports.razor`](Veldrath.GameClient.Components/Components/Pages/_Imports.razor) — `@layout GameLayout` applies to all pages including the new `GameEntry`
- [`Veldrath.GameClient.Components/Components/Layout/GameLayout.razor`](Veldrath.GameClient.Components/Components/Layout/GameLayout.razor) — no changes needed
- [`Veldrath.GameClient.Core/Abstractions/IGameApiClient.cs`](Veldrath.GameClient.Core/Abstractions/IGameApiClient.cs) — interface is already sufficient; no new methods needed
- [`Veldrath.Contracts/Characters/LastSessionDto.cs`](Veldrath.Contracts/Characters/LastSessionDto.cs) — DTO is already sufficient; no new fields needed

### Build Verification

After implementation, build the solution containing the changed components:

```powershell
dotnet build Veldrath.Web.slnx      # Web + GameClient.Components + engine + auth + assets
```

Or if only testing the RCL and its tests:

```powershell
dotnet build Veldrath.slnx          # Client + Server + GameClient.* + tests
```

Per the build strategy ([`.github/instructions/build-strategy.md`](.github/instructions/build-strategy.md)), changes to `Veldrath.GameClient.Components` and `Veldrath.Web` trigger `Veldrath.Web.slnx`.

---

## Appendix A: Existing Auth Guard Patterns (Reference)

The `GameEntry` guard follows the same patterns established by the three existing game pages:

| Pattern | Example in `Game.razor` | Used in `GameEntry` |
|---------|------------------------|---------------------|
| `OnInitialized` → `Auth.OnChange += handler` | [Line 67-69](Veldrath.GameClient.Components/Components/Pages/Game.razor:67) | ✓ Same pattern |
| `OnInitializedAsync` → `!Auth.IsAuthReady` guard | [Line 74-75](Veldrath.GameClient.Components/Components/Pages/Game.razor:74) | ✓ Same pattern |
| `!Auth.IsLoggedIn` → `/login` redirect | [Line 77-81](Veldrath.GameClient.Components/Components/Pages/Game.razor:77) | ✓ Same pattern |
| `Auth.TryRefreshAsync()` before API calls | [Line 132-137](Veldrath.GameClient.Components/Components/Pages/Game.razor:132) | ✓ Same pattern |
| `IAsyncDisposable` → unsubscribe `OnChange` | [Line 688-706](Veldrath.GameClient.Components/Components/Pages/Game.razor:688) | ✓ Same pattern |
| `try/catch` for API errors | [Line 239-244](Veldrath.GameClient.Components/Components/Pages/Game.razor:239) | ✓ Same pattern |

---

## Appendix B: Session Resume vs. Existing `Game.razor` Flow

The `GameEntry` resume dialog and the existing `Game.razor.RestoreSessionFromServerAsync` serve complementary purposes:

| Aspect | `GameEntry` Resume | `Game.razor` RestoreSessionFromServerAsync |
|--------|-------------------|---------------------------------------------|
| **When called** | First entry via `/Game` | After page refresh on `/Game/Play` |
| **Has Hub connection?** | No (REST only) | Yes (already connected) |
| **User prompt** | Yes (MudDialog) | No (auto-restore with spinner) |
| **User choice** | Resume or choose different | Auto-resume only |
| **On no session** | Redirects to `/Game/CharacterSelect` | Redirects to `/Game/CharacterSelect` |
| **On failure** | Shows error with Retry | Shows system message + redirects |
