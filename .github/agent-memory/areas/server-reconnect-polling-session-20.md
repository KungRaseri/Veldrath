---
kind: area
date: undated
area: engine
title: "Server Reconnect Polling (session-20)"
---

## Server Reconnect Polling (session-20)

**`IServerStatusService.StartPollingAsync(Func<string> getServerUrl, CancellationToken ct)`** — background loop that calls `CheckAsync` periodically:
- Waits `OfflinePollInterval` (5 s default) between checks when offline; `OnlinePollInterval` (30 s default) when online
- Kicked off in `App.axaml.cs` `OnFrameworkInitializationCompleted` after DI is built: `_ = serverStatus.StartPollingAsync(() => clientSettings.ServerBaseUrl)`
- `OperationCanceledException` from `CheckAsync` is re-thrown (not treated as server failure) — prevents a shutdown cancellation from flipping Status to Offline
- Both intervals are `internal` fields on `ServerStatusService` for test override (set to 10 ms in tests)
- `FakeServerStatusService` has a no-op `StartPollingAsync` implementation
- `ServerStatusServiceTests` uses event-based (`TaskCompletionSource`) assertions, not wall-clock timeouts, to avoid flakiness under parallel test runs


**Problem fixed**: When server was offline at client launch, SplashViewModel ignored RefreshAsync failure, leaving stale tokens in memory. This caused a redirect loop (always landing on CharacterSelect) and logout not clearing auth buttons properly.

**New components**:
- `Veldrath.Contracts/Announcements/AnnouncementContracts.cs` — `AnnouncementDto(int Id, string Title, string Body, string Category, bool IsPinned, DateTimeOffset PublishedAt)` shared between server and client
- `Veldrath.Server/Data/Entities/Announcement.cs` — EF entity with `IsActive`, `ExpiresAt?`, `IsPinned`, `PublishedAt`
- `Veldrath.Server/Data/Repositories/AnnouncementRepository.cs` — `IAnnouncementRepository` + impl; `GetActiveAsync()` filters `IsActive && (ExpiresAt == null || ExpiresAt > now)`, orders `IsPinned DESC, PublishedAt DESC`. **Must use `var now = DateTimeOffset.UtcNow` local variable** — SQLite cannot translate `DateTimeOffset.UtcNow` directly in LINQ expressions.
- `Veldrath.Server/Features/Announcements/AnnouncementEndpoints.cs` — `GET /api/announcements`, `AllowAnonymous`, returns `AnnouncementDto[]`
- `Veldrath.Client/Services/ServerStatusService.cs` — `IServerStatusService : INotifyPropertyChanged` singleton; pings `/health`; `IsOnline`, `StatusMessage`, `Status` (enum); `CheckAsync(serverUrl)`.  **Interface must extend `System.ComponentModel.INotifyPropertyChanged`** so `WhenAnyValue(s => s.Status)` works on the interface type.
- `Veldrath.Client/Services/AnnouncementService.cs` — `IAnnouncementService` + `HttpAnnouncementService`; uses typed `HttpClient` registered via `AddHttpClient<IAnnouncementService, HttpAnnouncementService>` with base address set in DI

**Key architectural note — `LoadAnnouncementsAsync` guard**: Only guard on `announcementService is not null`. Do NOT also guard on `settings is not null` — the HttpClient base address is already set in DI; no need to pass `serverUrl` to the method.

**SplashViewModel soft-logout fix**: In `RunSplashAsync` Phase 3, check server health first. If server offline and tokens present → call `LogoutAsync()` to clear local state. If server online and tokens present → `await RefreshAsync()`; if that returns `false` → call `LogoutAsync()`. This prevents redirect loops and stale auth state.

**MainWindowViewModel**: Added `IServerStatusService` (required parameter). Exposes `IsServerOnline` and `ServerStatusMessage` reactive properties; subscribes to `serverStatus.WhenAnyValue(s => s.Status)`.

**MainWindow.axaml**: Wraps content in 2-row `Grid`. Row 0 = dark-red `Border` banner (`IsVisible="{Binding !IsServerOnline}"`). Row 1 = `ContentControl` for page navigation.

**MainMenuViewModel disabled commands**: `RegisterCommand` and `LoginCommand` gated on `serverOnline` observable; `SelectCharacterCommand` gated on `canEnterGame = IsLoggedIn && IsServerOnline`. Commands are auto-disabled (greyed out) when server is offline.

**MainMenuView.axaml news panel**: 3-column grid (`280,*,280`). Column 0 = news panel with `ScrollViewer > StackPanel > [ItemsControl | placeholder TextBlock]`. **ScrollViewer is a ContentControl (single child only)** — wrap multiple children in a `StackPanel` inside the `ScrollViewer`.

**EF migration**: `AddAnnouncements` — in `Veldrath.Server/Data/Migrations/Application/`

**Test structure — announcement integration tests**: Follow the `IAsyncLifetime` fixture pattern (same as other server integration tests). Use a `AnnouncementsFixture` that seeds all data in `InitializeAsync`. Tests needing a clean/empty DB must be in a **separate class with their own `WebAppFactory` instance** (`AnnouncementEmptyEndpointTests : IAsyncLifetime`). Using `IClassFixture<WebAppFactory>` directly with per-test seeding causes the "empty list" test to fail when it runs after seeding tests.
