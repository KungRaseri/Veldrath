---
kind: area
date: 2026-10-23
area: engine
title: "SvelteKit Migration — Phase 0 + .NET Auth Bridge (Session-57, 2026-10-23)"
---

## SvelteKit Migration — Phase 0 + .NET Auth Bridge (Session-57, 2026-10-23)

**Phase 0 (complete)**
- SvelteKit app lives in `web/` (`shared` + `client` workspaces). Colyseus, the TypeScript engine,
  and the Caddyfile are gone.
- Better Auth 1.7.1 owns the `auth` Postgres schema (`user`, `session`, `account`, `verification`,
  `__drizzle_migrations`). `pgSchema("auth")` keeps FKs out of `public`.
- SvelteKit mints the .NET JWT with `jose` (issuer `Veldrath.Server`, audience `Veldrath.Client`,
  HS256, 10-minute TTL). The `JWT_KEY` in `web/client/.env` must match the root `.env` value.
- Social providers configured: Discord, Google, Microsoft (Discord confirmed working).
- Docker: web entrypoint waits for Postgres, applies Drizzle migrations, then starts adapter-node.
  `config/postgres/init-auth-schema.sh` creates the `veldrath_auth` role/schema on first boot.
  Deployment is Coolify + Traefik; no Caddy.
- Gotcha: Better Auth scopes the session cookie to the login host. Use `localhost`, not `127.0.0.1`.

**.NET auth bridge (complete)**
- `JwtClaimsEnrichment.EnrichAsync` is wired to `JwtBearerEvents.OnTokenValidated`. It validates the
  `sub` GUID, provisions unknown accounts, and appends `ClaimTypes.Role` + `permission` claims.
- `AccountProvisioningService` creates a `PlayerAccount` with the token GUID as its primary key.
  Identity is configured with `RequireUniqueEmail`, so accounts without an email claim (or whose
  email is already owned) get a deterministic placeholder: `{accountId:N}@external.veldrath.local`.
  Access snapshots are cached for 60 seconds (`ClearCache()` exists for tests/admin changes).
- `RolePermissionResolver` is shared by `AuthService` token issuance and inbound enrichment so RBAC
  derivation cannot drift.
- `game-token.ts` now includes `email` and `email_verified`.
- Verified live: Better Auth user with `0` matching `AspNetUsers` rows → `/api/characters` `200` →
  row created with matching GUID/username/email → repeat request keeps one row →
  `/hubs/game/negotiate` `200`. `Veldrath.Server.Tests`: **771 passing** (8 new).

**Temporary Blazor Foundry bridge**
- `POST /api/internal/accounts/sync` (`InternalAccountBridgeEndpoints`) accepts a Better Auth
  account (GUID, username, email, optional plaintext password, optional provider logins) and
  writes the Identity password hash directly via `PasswordHasher.HashPassword` (bypasses the
  12-char policy on purpose). Guarded by `X-Internal-Bridge-Token` == `InternalBridge:Token`;
  returns `503` when unconfigured.
- `web/client/src/lib/server/foundry-bridge.ts` calls it server-side. Registration posts the
  plaintext password once to `/api/foundry-bridge/register`; `hooks.server.ts` does a once-per-user
  account/provider-login sync and retries on failure. `INTERNAL_BRIDGE_URL=http://server:8080`
  inside Docker.
- Provider IDs are normalized `discord|google|microsoft` → `Discord|Google|Microsoft` for
  `AspNetUserLogins`.
- Concurrency gotcha: registration and the hooks sync race to provision the same account.
  `CreateAccountAsync` catches `DbUpdateException` (duplicate `PK_AspNetUsers`), detaches the
  failed entity, and re-queries the winning row.
- Identity requires a unique email (`RequireUniqueEmail`), so accounts without one get
  `{accountId:N}@external.veldrath.local`.

**Existing-account migration**
- `web/scripts/migrate-identity-accounts.mjs` (`npm run db:migrate-identity --workspace client`)
  copies `AspNetUsers` → `auth.user` (same GUID), `PasswordHash` → `auth.account`
  (`provider_id='credential'`), and `AspNetUserLogins` → `auth.account`. Idempotent; `--dry-run`
  rolls back.
- Better Auth 1.7.1 matches credential accounts by `issuer` too. Insert
  `issuer='local:credential'` for password accounts and `issuer='local:oauth:{provider}'` for
  social logins, or sign-in reports `INVALID_EMAIL_OR_PASSWORD` even with a correct hash.
- Migration needs `MIGRATION_DATABASE_URL` with access to `public."AspNetUsers"`; the restricted
  `veldrath_auth` role cannot read public tables (and its `search_path=auth` resolves unqualified
  `"AspNetUsers"` to `auth."AspNetUsers"`).
- Identity PBKDF2 v3 hashes stay valid: `auth.ts` uses a custom `emailAndPassword.password.verify`
  that handles Better Auth scrypt (`salt:key`) and Identity v3
  (`0x01 | prf | iterations | saltLen | salt | subkey`). New password changes write scrypt.
- Verified live: migrated Identity-only account signed in through Better Auth with its legacy
  password, minted a game token, and `/api/characters` returned `200`.
- Client Vitest: **4 passing** (`auth-password.test.ts`). Server tests: **776 passing**.

**Phase 1 vertical slice (complete)**
- `/characters` is an SSR page: `game-api.ts` mints a token and calls `/api/characters` using
  `INTERNAL_BRIDGE_URL` (or `PUBLIC_API_URL` as fallback).
- `/play/[id]` uses `GameSession` (`$lib/game/session.svelte.ts`), a runes store that owns the
  SignalR connection. Hub methods: `SelectCharacter`, `EnterZone`, `GetMapGraph`, `TravelToNode`,
  `GetInventory`. Events: `CharacterSelected`, `ZoneEntered`, `MapGraphReceived`, `NodeTraveled`,
  `InventoryLoaded`, `SystemMessage`, `Error`, `OnKicked`.
- SelectCharacter always resets the character to the region-map entry. If `currentZoneId` is set,
  the client calls `EnterZone` first, then `GetMapGraph`; `EnterZone` moves the session to the zone
  map, so the subsequent graph request returns the zone map.
- Travel into a zone node returns `NodeTraveled.mapChanged=true` with `zoneId`; request the new
  graph with `mapId`.
- `/game` now redirects to `/characters`; Playwright covers unauthenticated guards.
- Live smoke (local DB, migrated legacy user + seeded character `Thornwarden`):
  CharacterSelected → ZoneEntered (Aldenmere, 1 occupant) → MapGraphReceived (4 nodes) →
  TravelToNode (The Grey Cup) → InventoryLoaded (overlay true), no hub errors.
- Local smoke scripts live in `scripts/dev/` (gitignored). The SignalR package must be loaded via
  `createRequire` from `web/node_modules/@microsoft/signalr/dist/cjs/index.js` because the ESM
  build uses extensionless imports Node cannot resolve.

**Phase 2 start**
- `CardinalGraphLayout` is ported to `web/client/src/lib/game/cardinal-layout.ts`; all 13 C#
  layout tests were mirrored in `cardinal-layout.test.ts` and pass unchanged. `CardinalMap.svelte`
  renders the computed grid (current/reachable/known, direction hints, entry/exit badges).
- Browser SignalR CORS gotcha: the negotiate preflight includes `x-signalr-user-agent`. The
  `AllowLocalClient` policy now allows it, and origins come from `Cors:AllowedOrigins` with
  fallback local dev origins + `Web:BaseUrl`. Regression tests live in `CorsTests`.
- Playwright UI smoke with a real session confirmed the play page renders the map, enters the
  zone, opens the inventory drawer, and produces no page errors.

**Phase 2 shell (complete)**
- Game UI components under `web/client/src/lib/game/`: `GameHeader`, `GameFooter`, `LeftSidebar`,
  `RightSidebar` (Zone/Global/Whisper chat), `CombatPanel`, `GameOverlays` (inventory/shop/quests/
  settings), `CardinalMap`.
- `GameSession` now tracks stats, occupants, location/enemies, chat buffer, shop catalog, quests,
  combat log, death, and overlays. Pure math lives in `reducers.ts` (`appendChat`,
  `describeCombatTurn`, stat reducers, `barPercent`).
- Hub methods wired: SelectCharacter, EnterZone, GetMapGraph, TravelToNode, GetInventory,
  EquipItem, GetQuestLog, SearchArea, RestAtLocation, LeaveZone, VisitShop, GetShopCatalog,
  BuyItem, SellItem, DropItem, EngageEnemy, AttackEnemy, DefendAction, FleeFromCombat,
  UseAbilityInCombat, Respawn, SendZoneMessage, SendGlobalMessage, SendWhisper.
- Live hub smoke: chat round-trip, inventory, quest log (empty), search, rest (CharacterRested),
  shop catalog (23 items), no errors. Seeded Aldenmere spawns no enemies, so combat is covered by
  reducer/component tests rather than a live fight.
- Browser smoke: map, inventory, quests, settings, chat send — zero page/console errors.
- Vitest now has 31 tests (layout 13, reducers 10, components 4, password 4).
  Playwright still covers unauthenticated guards; authenticated flows need the seeded local stack.
