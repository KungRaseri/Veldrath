---
kind: work-item
date: 2026-10-21
area: engine
title: "Linux workspace health audit"
---

### Session-44 (2026-10-21) — Linux workspace health audit

**Environment**: workspace is now at `/media/kungraseri/Projects/code/Veldrath` on Linux (earlier sessions used
`G:\code\Veldrath` on Windows). Verified: .NET SDK **10.0.402**, runtimes **10.0.13**, Docker **29.9.1**,
Compose **5.5.1**, PowerShell **7.6.7**. `dotnet-ef` is not installed (optional; migrations run at startup).

**Build/test baseline**:
- Purged 29 stale Windows `obj/` + `bin/` trees (they contained `C:\` NuGet paths) before the first Linux restore.
  The `<clear />` in `NuGet.config` remains essential.
- `dotnet build Realm.Full.slnx`: **29 projects, 0 warnings, 0 errors**.
- `dotnet test Realm.Full.slnx`: **4,848 tests across 13 test projects, 0 failures** (baseline was 4,739/11 at
  Session-41; `Veldrath.GameClient.Core.Tests` and `Veldrath.GameClient.Components.Tests` are now included).
- `dotnet list Realm.Full.slnx package --vulnerable --include-transitive`: **0 vulnerable packages**.

**Security/package bump (commit 99ab5c76)**: new advisories against `System.Security.Cryptography.Pkcs`/`Xml`
10.0.10 and `Microsoft.AspNetCore.SignalR.Common` 10.0.9. Pinned `System.Security.Cryptography.ProtectedData`/`Xml`,
all `Microsoft.Extensions.*`, and `Microsoft.AspNetCore.SignalR.Client` to **10.0.13** (matches the installed
runtime). Future package bumps require removing/recreating the `docker-compose.dev.yml` `nuget_cache` volume —
a stale cache caused `NETSDK1064` (MudBlazor missing) inside containers.

**Test-drift fixes (commit 99ab5c76)**:
- `GameCombat.razor` passed `Color="#e53935"` to `StatusBar`, which declares `BarColor` (`MudBlazor.Color`);
  fixed to `BarColor="@Color.Error"`.
- Added stable CSS hooks (`status-bar-label`, `action-btn-*`, `game-tile`, `tile-*`, `tile-indicator-player`,
  `game-tilemap-viewport`, `game-tilemap-empty`, `tile-status-bar`) so RCL tests target the current MudBlazor markup.
- bUnit RCL tests call `AddMudServices()`, set `JSInterop.Mode = JSRuntimeMode.Loose`, and register
  `NoopKeyInterceptorService`/`NoopResizeObserverFactory` (`Veldrath.GameClient.Components.Tests/Infrastructure/MudBlazorTestDoubles.cs`).
  MudBlazor 9.7 services are async-dispose-only while bUnit disposes synchronously.
- `GameLayoutRenderTests` updated to the current `MudLayout` structure (the old `in-game` class no longer exists).
- `RealmFoundry.Tests` excludes the intentionally public `RefreshToken` (used by `AuthDelegatingHandler`).
- `FakeCharacterCreationService` supplies an `EquipmentTypeCatalogDto`; the two `AvailableArmor/WeaponTypes`
  client tests await initialization.
- `ApplyStatusEffectHandler` now skips immunity/resistance rolls for beneficial effects (`Buff`, `HealOverTime`).
  Wisdom-derived resistance (1% per 10 Wisdom) previously gave every buff a ~1% chance to fail, which made
  `Should_Refresh_Duration_When_Enabled` intermittently fail (verified 5 consecutive clean runs after the fix).

**DataProtection native-run fix (commit e0fe2529)**: `Veldrath.Server`, `Veldrath.Web`, and `RealmFoundry` hardcoded
`/root/.aspnet/DataProtection-Keys`, breaking auth/register on native non-root Linux (500 in
`SendEmailConfirmationAsync`) and antiforgery. They now use the framework default
(`$HOME/.aspnet/DataProtection-Keys`, still `/root/...` in containers, preserving compose volumes) with an optional
`DataProtection:KeysPath` override.

**Docker dev fixes (uncommitted at audit end)**: `Veldrath.Web`, `RealmFoundry`, and `Veldrath.Discord`
`Dockerfile.dev` entrypoints now pass `--no-launch-profile`. Without it `dotnet watch` loaded `launchSettings.json`
and bound to `:5000`/HTTPS instead of `ASPNETCORE_URLS`, so the Foundry (:8081) and Web (:8082) containers were
unreachable. Server already had the flag.

**Runtime smoke results (native)**:
- Server `:9000`: `/health` 200; migrations + seed against Postgres; `/api/content/browse?type=item` 200
  (23 items, 4 archetypes in DB); `/api/announcements` 200; `POST /api/auth/register` 200 with JWT;
  authenticated `POST /hubs/game/negotiate` 200 (WebSockets).
- `Veldrath.Web` `:8082`: `/` and `/login` 200; SSR calls server editorial announcements 200.
- `Veldrath.Client` on X11: launches, holds two established connections to the server, server logs `/health` +
  `/api/announcements` 200 from the client.
- `RealmFoundry` `:8081`: 200 with correct title. `RealmForge` launches (content load blocked only by the port
  conflict below).

**Docker dev stack**: verified `postgres`, `seq`, `server`, `foundry`, `web` — server healthy on 9000, Foundry 200 on
8081, Web 200 on 8080, `dotnet watch` hot reload enabled. Services were started sequentially after clearing
`nuget_cache`.

**Host-specific conflict (not a repo defect)**: host port **5433** is occupied by an unrelated
`kungharness-postgres-1` container. Compose/native defaults (5433) cannot reach Veldrath's DB while that project
runs. Smoke tests used a temporary Postgres on **5434** and a compose override
`postgres.ports: !override ["5434:5432"]`. `RealmForge` reads `ConnectionStrings:ContentDb` (5433) from persistent
settings; override it or change its settings when using 5434. The Discord bot was not started (live external
connection/token side effects).

**Git note**: an external auto-commit process committed the package/test fixes as `99ab5c76` and `e0fe2529` during
the session; the three `Dockerfile.dev` fixes remained uncommitted at audit end.

---
