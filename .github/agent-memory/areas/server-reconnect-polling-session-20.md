---
kind: area
date: undated
area: engine
title: "Server Reconnect Polling (session-20)"
---

## Server Reconnect Polling (session-20)

> **The poller half of this note is a closed record, not current wiring.** `IServerStatusService`,
> `ServerStatusService`, `FakeServerStatusService`, `HttpAnnouncementService`, `SplashViewModel`,
> `MainWindowViewModel`, `MainMenuViewModel` and the `MainWindow` / `MainMenuView` XAML were Avalonia code and went
> with that client in session-68 (2026-10-25) — see
> [`work-items/2026-10-25-desktop-client-deleted-veldrath-is-web-first-only.md`](../work-items/2026-10-25-desktop-client-deleted-veldrath-is-web-first-only.md).
> **No web counterpart was written for the poller, and none should be invented.** The shell learns that the
> connection is dead from the hub it is already on: `session.phase` moves through `connecting`, `reconnecting`,
> `failed` and `disconnected` in
> [`session.svelte.ts`](../../../web/client/src/lib/game/session.svelte.ts). `Program.cs` still maps
> `MapHealthChecks("/health")` and `/health/detail` for probes (`Program.cs:536`, `:546`) — not for a client-side
> loop.
>
> **The announcements half survives and is current**; its pointers are in the first section below. The
> `Announcements` table and its index ship in `Veldrath.Server/Migrations/20260416172040_InitialCreate.cs` — there
> is no `AddAnnouncements` migration and no `Migrations/Application/` directory.

### Announcements — the surviving server half

- `Veldrath.Contracts/Announcements/AnnouncementContracts.cs` — `AnnouncementDto(int Id, string Title, string Body, string Category, bool IsPinned, DateTimeOffset PublishedAt)`
- `Veldrath.Server/Data/Entities/Announcement.cs` — EF entity with `IsActive`, `ExpiresAt?`, `IsPinned`, `PublishedAt`
- `Veldrath.Server/Data/Repositories/AnnouncementRepository.cs` — `IAnnouncementRepository.GetActiveAsync(CancellationToken)` filters `IsActive && (ExpiresAt == null || ExpiresAt > now)` and orders `IsPinned` first, then `PublishedAt` newest first. **Keep the `var now = DateTimeOffset.UtcNow` local variable** — SQLite cannot translate `DateTimeOffset.UtcNow` directly in the LINQ expression. Registered `AddScoped` at `Program.cs:162`.
- `Veldrath.Server/Features/Announcements/AnnouncementEndpoints.cs` — `MapGroup("/api/announcements").WithTags("Announcements").AllowAnonymous()`, one `GET /` returning `AnnouncementDto[]`; mapped at `Program.cs:515`.
- **Nothing in the repo calls it.** The news panel that read the feed was the desktop menu. The web site reads the editorial feed instead — `/api/editorial/announcements` in [`editorial-api.ts`](../../../web/client/src/lib/server/editorial-api.ts) — and the legacy route is allow-listed by hand as a removal candidate in `web/client/src/lib/parity/rest-routes.test.ts`, with the record in `plans/web-feature-parity-and-coverage.md`.
- **Test structure still holds** — `Veldrath.Server.Tests/Features/AnnouncementEndpointTests.cs` is the `IAsyncLifetime` pattern: `AnnouncementsFixture` seeds in `InitializeAsync` and is shared through `IClassFixture`, and the tests that need an empty database are a **separate class with their own `WebAppFactory`** (`AnnouncementEmptyEndpointTests`), because per-test seeding under `IClassFixture` makes the "empty list" test fail when it runs after the seeded ones.

### Client half — deleted with the Avalonia client (session-68)

~~`IServerStatusService.StartPollingAsync(Func<string> getServerUrl, CancellationToken ct)` — a background loop calling `CheckAsync`, waiting `OfflinePollInterval` (5 s default) when offline and `OnlinePollInterval` (30 s) when online, kicked off from `App.axaml.cs` `OnFrameworkInitializationCompleted` after DI was built. `OperationCanceledException` was re-thrown rather than treated as a server failure, so a shutdown cancellation could not flip `Status` to Offline. Both intervals were `internal` fields on `ServerStatusService` so tests could shrink them to 10 ms, and `ServerStatusServiceTests` asserted through `TaskCompletionSource` rather than wall-clock waits.~~

~~`FakeServerStatusService` had a no-op `StartPollingAsync`.~~

~~Splash soft-logout: `RunSplashAsync` phase 3 checked server health first — offline with tokens present, or an `RefreshAsync` that returned `false`, meant `LogoutAsync()`. That is what stopped the CharacterSelect redirect loop and the auth buttons that survived logout.~~

~~`MainWindowViewModel` took `IServerStatusService` as a required parameter, exposed `IsServerOnline` and `ServerStatusMessage` from `WhenAnyValue(s => s.Status)`, and `MainWindow.axaml` wrapped the shell in a two-row grid whose row 0 was the dark-red offline banner. `MainMenuViewModel` gated `RegisterCommand` and `LoginCommand` on the `serverOnline` observable and `SelectCharacterCommand` on `canEnterGame = IsLoggedIn && IsServerOnline`.~~

~~`MainMenuView.axaml` news panel: 3-column grid `280,*,280`, column 0 a `ScrollViewer` over an `ItemsControl` and a placeholder — the `ScrollViewer` is a `ContentControl` (single child only), which is why the children needed the inner `StackPanel`.~~

~~`HttpAnnouncementService` and `AddHttpClient<IAnnouncementService, HttpAnnouncementService>` with the base address set in DI, plus the `LoadAnnouncementsAsync` guard rule: guard on `announcementService is not null` only, never also on `settings`.~~
