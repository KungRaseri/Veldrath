# Blazor Decommission + Whole-Codebase Cleanup

> **Status:** Plan / For Review
> **Date:** 2026-10-24
> **Scope:** finish the Blazor → SvelteKit transition and audit the repository for cleanup.
> **Related:** [`sveltekit-migration.md`](sveltekit-migration.md), [`game-client-unification-plan.md`](game-client-unification-plan.md), [`web-client-architecture.md`](web-client-architecture.md)

---

## 1. Where the migration actually stands

Phase 6 already deleted the Blazor applications. Verified in the current tree:

- gone: `Veldrath.Web`, `RealmFoundry`, `Veldrath.Auth.Blazor`, `Veldrath.GameClient.Components` (and their test projects, `.slnx` files, CI workflow, MudBlazor/bunit packages);
- the desktop host is now [`DesktopUiServer`](../Veldrath.Client/HostedWeb/DesktopUiServer.cs), which serves the static Svelte build and proxies `/api` + `/hubs` with an injected JWT;
- the only surviving `Blazor`/`MudBlazor` code in the repo lives in an orphan folder: `scripts/dev/blazor/`.

So the remaining work is **residue removal (no behaviour risk)** plus **one real architectural decision** (ASP.NET Core Identity), not a migration.

---

## 2. Part 1 — Blazor residue removal

| Artifact | Detail | Action |
|---|---|---|
| `scripts/dev/blazor/` | 19 orphan files: 18 `.razor` snapshots of deleted pages plus `VeldrathTheme.cs` (MudBlazor theme) and 5 CSS copies. Nothing references the folder. | Delete. Git history keeps it. |
| [`Veldrath.Client.csproj:47`](../Veldrath.Client/Veldrath.Client.csproj:47) | Comment `Embedded WebView2 for Blazor game UI hosting` | Reword to "hosts the Svelte desktop build". |
| [`RealmForge/README.md`](../RealmForge/README.md) | Claims `.NET MAUI Blazor Hybrid` / "Why Blazor Hybrid?" — the app is Avalonia + ReactiveUI. | Rewrite (tech stack, run commands, what it edits). |
| [`docs/design-system.md`](../docs/design-system.md) | MudBlazor-only content: §5.5 spacing delegation, MudBlazor token map in §8.2, RealmFoundry tables (§3.1, §7.8), §8.3, §8.4, §8.5 migration notes. | Move to a single appendix "Retired surfaces (Blazor/MudBlazor)" at the end; keep Avalonia + SvelteKit sections as the live reference. |
| [`web/shared/db/README.md`](../web/shared/db/README.md:41) | Section "Why this is safe on the existing Blazor database" | Reword to "the existing Postgres database". |
| `wiki/` (submodule) | `Foundry-Overview.md` describes a Blazor Server app; `Home.md`, `_Sidebar.md`, `Engine-Implementation-Status.md` list RealmFoundry / `Veldrath.Auth.Blazor` / Caddy. | Rewrite `Foundry-Overview.md` as the `/foundry` route group doc, fix Home/Sidebar/Implementation-Status. Separate commit inside the submodule. |
| `plans/` (19 files) | Historical Blazor designs: `web-client-architecture`, `game-client-unification-plan/_gaps`, `game-client-parity-checklist`, `realmfoundry-web-audit`, `realmfoundry-migration-status`, `css-audit-game-client-components`, `media-breakpoint-strategy`, `page-refresh-resilience-plan`, `game-routing-guard-plan`, `gameplay-view-refactor-plan`, `map-view-refactor-plan`, `text-dashboard-map-graph-plan`, `auth-delegating-handler-design`, `web-character-creation-sync-plan`, `infrastructure-coolify-extraction-plan`, `coolify-compatibility-analysis`, `web-first-roadmap`, `docker-compose-coolify-spec`. | Add a one-line historical banner (`> **Status:** historical — Blazor era, superseded by …`) and index them under `plans/README.md` as archived. Do **not** delete: they document why the current design looks the way it does. |
| [`docs/deployment.md`](../docs/deployment.md:180) | "The old Blazor `Veldrath.Web` and `RealmFoundry` containers … are retired." | Keep — accurate history. |

Acceptance: `rg -i "blazor|mudblazor" --glob '!plans/**' --glob '!web/llms/**'` returns only intended mentions (design-system appendix, archived plans, deployment history note).

---

## 3. Part 2 — The one real decision: ASP.NET Core Identity

Identity (not Blazor) is what still ties the old world to the new one:

- `PlayerAccount : IdentityUser<Guid>` is the game account row (characters, RBAC, moderation, bans).
- `/api/auth/{register,login,refresh,renew-jwt,logout,exchange,external/*,session}` exists for the **desktop client's native sign-in**.
- [`InternalAccountBridgeEndpoints`](../Veldrath.Server/Features/Auth/InternalAccountBridgeEndpoints.cs) + [`foundry-bridge.ts`](../web/client/src/lib/server/foundry-bridge.ts) + [`hooks.server.ts`](../web/client/src/hooks.server.ts) mirror Better Auth accounts into Identity (name/email/verification/password, provider links).

| Option | What changes | Cost / risk | When it makes sense |
|---|---|---|---|
| **1. Keep Identity as the account store (recommended)** | No deletion. Optionally shrink the bridge to one-way JIT provisioning and drop the password mirror once the desktop signs in through the web. | None now | Immediately — unblocks shipping, keeps desktop native sign-in working |
| **2. Retire Identity *auth* endpoints, keep Identity *tables* | Delete `/api/auth/{login,register,refresh,renew-jwt,external/*,session}` and `AuthService` token issuance; desktop signs in via Better Auth (browser/deep-link + exchange code). Identity tables stay for RBAC/characters. | Medium: needs desktop auth UX + deep-link handling; touches `AuthEndpoints`, `ExternalAuthEndpoints`, `SessionEndpoints`, `Veldrath.Auth` | After the desktop shell decision (full-window Svelte or native chrome) |
| **3. Remove Identity entirely** | `PlayerAccount` becomes a plain entity; roles/permissions move to first-class tables; password migration for remaining Identity-only accounts; drop cookies, DataProtection keys, `AspNetUserLogins`, `Identity` stores. | Large: schema + migration + every `UserManager`/`RoleManager` call site | Only after 2 has been live for a while |

Recommendation: **Option 1 now, decide 2 together with the desktop finalization**, and treat 3 as a separate future project. Nothing in Part 1 depends on it.

---

## 4. Part 3 — Whole-codebase cleanup workstreams

Ordered by risk/return. Each item is a single reviewable PR.

### W1 — Documentation truth pass (low risk, high value)
- Reconcile [`AGENTS.md`](../AGENTS.md), [`README.md`](../README.md), [`web/README.md`](../web/README.md), [`web/client/README.md`](../web/client/README.md), `wiki/`, `docs/design-system.md` with the current tree (ports 8080/8081/8083, `#lib`, `$app/env`, `#lib/server/env.ts`, no Caddy, no compose in Coolify).
- Add `plans/README.md`: index every plan with `active` / `superseded` / `archived` + one-line purpose. This alone stops future agents from following dead designs.
- Make `docs/coolify-deployment.md` the single deployment reference; `docs/deployment.md` becomes the overview that links to it.

### W2 — Dead code and unused references
- `Veldrath.Contracts`: enumerate consumers (client/server/tests) and delete DTOs no endpoint returns.
- `Veldrath.GameClient.Core`: now desktop-only — confirm no server/web references, then decide whether `IGameApiClient` (unimplemented in .NET now?) is still needed.
- `Veldrath.Auth`: confirm the desktop-only surface.
- Duplicate payload definitions: `Veldrath.GameClient.Core/Payloads/*` vs `web/client/src/lib/game/types.ts` — document the two-runner split or generate the TS types from the C# records with a small script.
- `[Obsolete] StartingSpellIds` in [`CharacterClass`](../RealmEngine.Shared/Models/CharacterClass.cs:137): remove or migrate usages.
- Web server duplication: every module that hand-rolls `fetch` should go through `#lib/server/game-request.ts` (`game-api.ts` already does; check `editorial-api.ts`, `content-api.ts`, `admin-api.ts`, `foundry-api.ts`, `character-api.ts`).

### W3 — Engine/server hygiene
- XML doc gaps flagged as P4 in [`.github/agent-memory/engine-codebase.md`](../.github/agent-memory/engine-codebase.md) — close them or move to "won't fix".
- Review `RealmEngine.Core` handler quirks listed in the memory file and either fix or document at the call site.
- `Program.cs` is ~730 lines: extract auth, CORS, email, SignalR, and startup seeding into extension methods (`AddVeldrathAuth`, `AddVeldrathCors`, …). Pure refactor, no behaviour change.

### W4 — Testing and CI
- Consolidate `FakeXxx` stubs; delete stubs with no consumers.
- Add a coverage floor per component in CI (engine/server/client/web) so regressions are visible.
- Add a markdown link check and `dotnet format --verify-no-changes` to CI.
- Web: extend Playwright to cover the authenticated `/characters` → `/play` path with a seeded account (currently only guards are covered).

### W5 — Configuration and deploy consolidation
- One source of truth for public URLs/ports: `docker-compose*.yml`, `.env.example`, `web/client/.env.example`, and the two deploy docs (now consistent after the env audit — verify with a scripted cross-check).
- Decide the fate of `docker-compose.api.yml` and `docker-compose.debugging.yml` (both still reference the old API-only/dev-debug flows) and of `deploy.yml`/GHCR jobs that publish images Coolify does not consume.
- Add a `scripts/check-env-docs.mjs`-style guard that fails CI when a documented env var is not read by code (or vice versa).

### W6 — Repository hygiene
- Remove `scripts/dev/blazor/` (Part 1) and any other orphan scratch folders.
- Re-check `.gitignore` / `.dockerignore` after the file moves (`client/build-desktop`, `SvelteApp/`, `.env`).
- Review `web/llms/` size (~5 MB of vendored docs) and whether it should stay in-repo.

---

## 5. Sequencing

```mermaid
graph LR
    A[Part 1 residue removal] --> B[W1 docs truth pass]
    B --> C[W2 dead code]
    C --> D[W3 engine/server hygiene]
    D --> E[W4 testing and CI]
    E --> F[W5 config consolidation]
    F --> G[W6 repo hygiene]
    H[Identity decision with desktop finalization] -.-> D
```

**Acceptance for the whole effort**
1. `rg -i blazor` outside `plans/`, `web/llms/`, and the design-system appendix finds nothing.
2. `dotnet build Realm.Full.slnx`, the 10 .NET test projects, and `cd web && npm run typecheck && npm run lint && npm run test && npm run build` all pass after every PR.
3. `plans/README.md` marks every plan with a status; no active plan references a deleted project.
4. No documentation tells a reader to run a command that no longer exists.

---

## 6. Open questions (need answers before implementation)

1. **`scripts/dev/blazor/`** — delete outright, or move to `docs/archive/blazor/` first?
2. **Plans** — banner + index (recommended), or move the Blazor-era ones into `plans/archive/`?
3. **Identity** — confirm Option 1 (keep) until the desktop shell decision, or start Option 2 now?
4. **`docker-compose.api.yml` / `docker-compose.debugging.yml`** — keep for local API debugging, or delete as unused?
5. **Cleanup scope** — do you want the whole W1–W6 sweep, or only W1 + W2 (docs + dead code) first?
