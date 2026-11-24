---
kind: area
date: 2026-10-23
area: engine
title: "SvelteKit Migration — Phase 6 Decommission (Session-61, 2026-10-23)"
---

## SvelteKit Migration — Phase 6 Decommission (Session-61, 2026-10-23)

**Deleted**
- Projects: `Veldrath.Web`, `Veldrath.Web.Tests`, `RealmFoundry`, `RealmFoundry.Tests`,
  `Veldrath.Auth.Blazor`, `Veldrath.GameClient.Components`,
  `Veldrath.GameClient.Components.Tests`.
- Solutions: `Veldrath.Web.slnx`, `RealmFoundry.slnx`.
- Version props: `versions/web.props`, `versions/foundry.props`.
- CI: `.github/workflows/ci-foundry.yml`.
- Docs/assets: `llms.txt`, `.github/instructions/blazor-component-development.md`,
  `.github/instructions/styling-and-css.md`, `.roo/skills/run-realmfoundry/`.
- Package versions: MudBlazor, bunit, Markdig, HtmlSanitizer, AngleSharp, AngleSharp.Css.
- `Veldrath.Auth.Tests/AuthStateServiceBaseTests.cs` and its `Veldrath.Auth.Blazor` reference.

**Kept deliberately**
- .NET auth endpoints (`/api/auth/*`) and the internal SvelteKit → Identity bridge
  (`InternalBridge:Token`, `/api/internal/accounts/sync`, SvelteKit
  `foundry-bridge.ts`): the desktop client's native fallback still signs in through
  ASP.NET Core Identity. The bridge is now documented as desktop compatibility, not
  a Foundry transition.
- `Foundry:BaseUrl` config is optional; `ExternalAuthEndpoints` falls back to `Web:BaseUrl`.

**Updated**
- `Realm.Full.slnx`, `Veldrath.slnx`, `Veldrath.Client.slnx`, `Veldrath.Libraries.slnx`,
  and `RealmForge.slnx` no longer list the deleted projects. `.NET` solution now has
  22 projects (13 source + 9 test).
- Compose: removed the `foundry` service, `foundry_dataprotection_keys` volumes, and
  `Foundry__BaseUrl` env from base/dev/prod/coolify files. Kept the `web` service.
- CI: `ci-web.yml` also runs `npm run build:desktop` and uploads both builds;
  `ci-client.yml` installs Node and builds the desktop bundle before the .NET build so
  `SvelteApp/` is included. `release.yml` and `deploy.yml` no longer reference Foundry,
  and the Caddy reload step was removed from deploy.
- VS Code: removed Veldrath.Web/RealmFoundry launch/tasks; added `web:dev`, `web:build`,
  `web:desktop`, `web:test`, a `Web (SvelteKit)` launch config, and an updated full-stack
  compound.
- `AGENTS.md`, `README.md`, build-strategy/testing/xml-doc/ef-core instructions,
  deployment docs, design-system banner, and test/run skills updated for the SvelteKit
  layout.

**Verification**
- `dotnet build Realm.Full.slnx`: 0 warnings, 0 errors.
- Full `dotnet test Realm.Full.slnx`: **4,617 passing** across 9 test projects.
  `ApplyStatusEffectTests.Should_Not_Apply_Same_Effect_Twice_Without_Stacking` failed once
  in the first post-deletion run and passed in isolation and in the final full rerun — a
  pre-existing flake, not related to Phase 6.
- SvelteKit: typecheck, lint, 31 Vitest, 15 Playwright, `npm run build`, and
  `npm run build:desktop` pass.
- Compose files remain valid; `.vscode` JSONC files validated.
