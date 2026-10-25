# GitHub Issue Refresh — triage, roadmap, and the issue set to land

> **Status:** Proposed — waiting for approval
> **Date:** 2026-10-25
> **Scope:** re-triage the five open issues (#3–#7), close what the codebase has outgrown, rewrite what is
> still wanted for the web-first architecture, and add the backlog that the last three sessions surfaced.
> **Related:** [`blazor-decommission-and-cleanup.md`](blazor-decommission-and-cleanup.md),
> [`../docs/client-ui-architecture.md`](../docs/client-ui-architecture.md),
> [`../.github/agent-memory/veldrath-memory.md`](../.github/agent-memory/veldrath-memory.md)

---

## 1. What the repository actually is now (the lens for triage)

Everything below was checked in the working tree, not assumed:

| Fact | Evidence |
|---|---|
| The SvelteKit app is the only game client; Avalonia survives only in RealmForge | `AGENTS.md`, `plans/blazor-decommission-and-cleanup.md` §W11 |
| Movement is a **DB map graph** (nodes + edges), rendered as panels — **no tilemap, no sprites, no TMX** | [`docs/client-ui-architecture.md`](../docs/client-ui-architecture.md): "panel-driven client UI (no tilemap/sprite rendering)"; `GetMapGraph`, `TravelToNode`, `EnterZone`, `LeaveZone`, `NavigateToLocation` in `GameHub` |
| Login resumes the saved node (Session-72), and that seam is the intended hook for onboarding | `veldrath-memory.md` §Session-72 |
| Quests are real: journal blob, objectives, accept/abandon/turn-in, and a client quest dialog | `QuestJournal`, `AcceptQuest`/`AbandonQuest`/`TurnInQuest`; [`GameOverlays.svelte`](../web/client/src/lib/game/GameOverlays.svelte:92) |
| No guild, trade, duel or PvP code exists anywhere | repo-wide search: zero `Guild`/`TradeSession`/`Duel`/`Pvp` types |
| Auth is Better Auth → JWT → JIT Identity provisioning; Identity owns RBAC only | `JwtClaimsEnrichment`, `AccountProvisioningService`, plan §W12 |

The five open issues were all written on **2026-05-27**, before the web pivot. Every one of them
specifies Avalonia ViewModels (`GameViewModel`, `CharacterSelectViewModel`, `GuildViewModel`,
`TradeViewModel`), which no longer exist.

---

## 2. Triage of the open issues

| # | Title | Verdict | Reasoning |
|---|---|---|---|
| **#3** | Region Map — Client-Side Rendering, Movement, Zone Entry/Exit (Phases 3–8) | **Close — superseded by design** | The plan is built on TMX tile layers, `RegionMapDto`, `ITileMapRepository` and `RegionTilemapViewModel`. The shipped architecture deliberately has none of that: the map is `MapGraph` data rendered as a node panel, and travel/zone-entry is live (`GetMapGraph`, `TravelToNode`, `EnterZone`, `LeaveZone`) with resume-at-last-node. Phases 3–8 are either done by a different design or explicitly out of scope. |
| **#4** | Guild/Clan System — Persistent Player Organizations | **Keep — rewrite for web-first** | Still a good retention feature and nothing about it is blocked by the old client. The body must drop Avalonia ViewModels, use the current migration/command/hub conventions, and decide blob-vs-table for bank state. |
| **#5** | PvP Combat — Duel Requests and Open-World PvP | **Keep — rewrite, and sequence last** | Combat is now turn-based against a single enemy; PvP needs a second combatant model, so it is the most expensive of the three social epics. Narrow the first slice to duels only; keep open-world PvP behind a flag. |
| **#6** | Player Trading — Secure Trade Window | **Keep — rewrite for web-first** | Ground drops now exist (`DropItemToGround` / `PickUpGroundItem`), which covers casual item transfer, but a two-phase secure trade is still missing. Rewrite as a SvelteKit dialog + hub commands; server-side sessions stay in memory. |
| **#7** | Quest Tracker / Journal UI — Client-Side Quest Tracking | **Close — mostly shipped** | The journal shipped: active quests with objective progress, abandon and turn-in, plus an available-quest list with accept, in the quest dialog. The data layer (`QuestJournal`, hub commands, broadcasts) is done too. The two genuine leftovers become new, smaller issues: HUD tracker + completion toast, and objective types beyond `kill`/`collect`/`visit`. |

Net effect: **2 closes, 3 rewrites, and a set of new issues so the backlog reflects the shipped game.**

---

## 3. What to plan next

Ordered by "unblocks the most with the least risk". Milestone names are suggestions for labels, not
GitHub milestones that exist today.

### Milestone A — Pipeline and hygiene (finish what is already in flight)
1. Make the coverage gates honest: `@veldrath/shared` currently cannot pass its own `coverage` script.
2. Decide the Codecov status policy (they are `informational: true` today, deliberately).
3. Close the documentation drift that survived the desktop retirement (`build:desktop` references,
   stale plan statuses).

### Milestone B — First-hour experience
4. First-login tutorial handoff (the Session-72 seam): a new character starts on the tutorial node, not
   the region entry node, with a guided first quest.
5. Quest HUD tracker + completion toast (the journal exists; the *during play* surface does not).
6. Quest objective types beyond `kill`/`collect`/`visit` (`talk`, `deliver`, `craft`, `reach_level`).

### Milestone C — Play together (the three social epics, in dependency order)
7. Party panel + invite flow — `Party`/`PartyMember` exist in `RealmEngine.Shared` with no hub or UI.
8. Guilds (#4, rewritten).
9. Trading (#6, rewritten).
10. PvP (#5, rewritten; duels first).

### Cross-cutting small work (can be picked up any time)
- Delete or wire `RespawnService`, `CharacterInitializationService`, `EquipmentPowerService`.
- `StatusEffectParser`: non-positive `statusDuration`, `JsonElement` `statModifiers`.
- A `CorsSetup` test that pins the dev defaults, including the preview origin.
- Dev mail catcher (Mailpit) so `NullEmailSender` no longer needs to log bodies at all.

---

## 4. Operations to apply

### 4.1 Close (with a comment)

**#3** → comment:

> Superseded by the shipped map-graph design. The client is panel-driven with a DB map graph
> (`docs/client-ui-architecture.md`) — there is no tilemap/TMX rendering, by decision. Travel, zone
> entry/exit and resume-at-last-node are live on `GameHub` (`GetMapGraph`, `TravelToNode`, `EnterZone`,
> `LeaveZone`, `NavigateToLocation`). Closing this; visual map polish would be a different, smaller
> issue if we want it.

**#7** → comment:

> The quest journal shipped in `GameOverlays.svelte`: active quests with objective progress, abandon and
> turn-in, plus available quests with accept. The remaining work is split into "quest HUD tracker +
> completion toast" and "objective types beyond kill/collect/visit", which are now tracked separately.

### 4.2 Rewrite (keep the number, replace the body)

Body templates below. They keep the user story, drop the deleted client, and use the current
conventions (vertical-slice hub command → MediatR, SvelteKit runes stores, `FakeXxx` tests).

---

#### #4 — Guild/Clan System — Persistent Player Organizations

```markdown
## User story
As a player I want a persistent guild with a roster, ranks, MOTD and a shared bank, so that
cooperation outlives a single play session.

## Design constraints (web-first)
- The only client is the SvelteKit app (`web/client`); there are no ViewModels.
- Server shape follows the hub → MediatR bridge: `GameHub.X(...)` → `XHubCommand` → handler → DTO/broadcast.
- Guild state is shared, mutable, and must survive restart: use ApplicationDbContext tables, not the
  character blob.
- Permissions go through the existing RBAC primitives, not a parallel role system.

## Scope (first slice)
### Data
- [ ] `Guild` (Id, Name unique, Tag, Description, Motd, Level, Experience, CreatedAt)
- [ ] `GuildMember` (GuildId, CharacterId unique, Rank enum Leader/Officer/Member/Recruit, JoinedAt)
- [ ] Migration `AddGuilds` + model snapshot in `Veldrath.Server/Migrations`
- [ ] `IGuildRepository` + `EfCoreGuildRepository`; registered in `ServiceCollectionExtensions`

### Server
- [ ] `CreateGuild` (creator becomes Leader; validates name/tag uniqueness)
- [ ] `InviteToGuild`, `AcceptGuildInvite`, `DeclineGuildInvite` (invites expire after 48 h)
- [ ] `KickFromGuild`, `LeaveGuild`, `PromoteGuildMember`, `TransferGuildLeadership`, `DisbandGuild`
- [ ] `GetGuildInfo` (roster with online flags from the session store, MOTD, bank summary)
- [ ] Rank rules enforced in handlers, not in the hub: Officer+ invites/kicks Recruit/Member;
  Leader only promotes/transfers/disbands; a Leader must transfer before leaving

### Hub + wire
- [ ] Hub methods for the nine operations above, one DTO parameter each
- [ ] Broadcasts: `GuildUpdated`, `GuildInviteReceived`, `GuildMemberChanged`
- [ ] `payload-contract.test.ts` passes for every new payload

### Client
- [ ] `GuildSession` runes store or `GameSession` fields for guild, roster, invites
- [ ] Guild panel in the left/right column: roster, ranks, MOTD, leave/disband
- [ ] Invite notification with accept/decline
- [ ] `#guild` chat channel wired through the existing chat channel switch

### Tests
- [ ] Repository tests (uniqueness, rank transitions, disband cascade)
- [ ] Handler tests for every command/query, including the rank-denial paths
- [ ] Hub tests for each method; web store tests with a `FakeHub`

## Out of scope for the first slice
Guild bank items (gold-only bank at first), guild levels/perks, guild vs guild competition.

## Technical notes
- Reuse the moderation/role patterns from `RolePermissionResolver`.
- Prefer a single `GuildHubCommand.cs`-style feature file per operation group, matching the
  `Features/Characters/*HubCommand.cs` layout.
```

---

#### #5 — PvP Combat — Duels (and later open-world PvP)

```markdown
## User story
As a player I want to challenge another character to a duel in the zone we share, so that I can test
my build against a real opponent.

## Design constraints
- Combat is turn-based against one enemy today (`AttackEnemy`, `DefendAction`, `UseAbilityInCombat`,
  `FleeFromCombat`). PvP needs a second combatant model rather than a second code path bolted on.
- Web-first: no ViewModels; the client is the SvelteKit combat panel.
- `Zone.IsPvpEnabled` already exists; open-world PvP stays behind that flag and behind `IsPvpEnabled`
  on the character.

## Scope (duel-only first slice)
### Server
- [ ] Duel session (in-memory, like the dungeon sessions): challenger, target, zone, snapshotted HP/MP,
  whose turn, 30 s accept window
- [ ] `RequestDuel`, `AcceptDuel`, `DeclineDuel`, `CancelDuel`
- [ ] `AttackPlayer` dispatch through the same turn resolution used for PvE (extract the shared step
  instead of copying `AttackEnemyHubCommand`)
- [ ] Auto-cancel on zone change, disconnect, or engaging an enemy
- [ ] Loss = knocked out, respawn at 50 % HP, no item loss

### Hub + wire
- [ ] `RequestDuel` / `AcceptDuel` / `DeclineDuel` / `AttackPlayer` / `TogglePvpFlag`
- [ ] `DuelRequested`, `DuelStarted`, `DuelEnded`, `PvpTurn` broadcasts to both combatants only

### Client
- [ ] Duel invite toast with accept/decline + countdown
- [ ] Combat panel renders the opponent health bar and reuses the existing combat log
- [ ] Character sheet shows PvP wins/losses

### Tests
- [ ] Turn resolution shared-step tests (PvE and PvP), duel expiry, cancel paths, flag enforcement
- [ ] Hub tests per method; web store tests via `FakeHub`

## Later (separate issue when duels are live)
Open-world PvP, looting, bounty/reputation consequences, arenas.

## Technical notes
Key on `ICombatSettings.PlayerDamageMultiplier` for the PvP multiplier; do not fork the damage formula.
```

---

#### #6 — Player Trading — Secure Two-Phase Trade

```markdown
## User story
As a player I want to trade items and gold with someone in my zone, with both sides confirming before
anything moves.

## Design constraints
- Ground drops (`DropItemToGround` / `PickUpGroundItem`) already cover casual transfer; trade must add
  guarantees, not a second inventory.
- In-memory trade sessions (like dungeon sessions) — cancelled on disconnect, zone change or combat.
- Web-first: the trade window is a SvelteKit dialog driven by the `GameSession` store.

## Scope
### Server
- [ ] `TradeSession` in memory: both character ids, both offers (item refs + gold), both confirm flags,
  60 s idle timeout
- [ ] `RequestTrade`, `AcceptTrade`, `DeclineTrade`, `CancelTrade`
- [ ] `AddItemToTrade`, `RemoveItemFromTrade`, `SetTradeGold`, `ConfirmTrade`
- [ ] Atomic execution: re-validate both inventories and gold at confirm time; fail the whole trade
  with a clear reason if anything moved
- [ ] Guard rails: both level 3+, same zone, not in combat, item not equipped, max 8 stacks and
  1,000,000 gold per side

### Hub + wire
- [ ] One hub method per operation, single DTO parameter
- [ ] `TradeRequested`, `TradeUpdated`, `TradeCompleted`, `TradeCancelled` broadcasts to the two players

### Client
- [ ] Trade dialog: both offers, both confirm states, gold input, cancel
- [ ] Inventory drag/click to add an item to the offer; blocked items explain why
- [ ] Invite notification with accept/decline

### Tests
- [ ] Handler tests: happy path, re-validation failure, timeout, disconnect, double-confirm race
- [ ] Inventory-service stubs that mutate between confirm and execute
- [ ] Web store tests with `FakeHub`

## Technical notes
Validation must use `IInventoryService` inside the handler, not the cached client view.
```

---

### 4.3 Create (new issues, in priority order)

#### NEW-1 — `fix(web/shared): the @veldrath/shared coverage gate cannot pass`

```markdown
`web/shared/package.json` has `coverage: vitest run --coverage` and `web/shared/vitest.config.ts` sets a
90 % per-file threshold, but the package has no test files. `npm run coverage` (the root script that
`ci-web.yml` deliberately avoids) therefore fails on `src/db/auth.schema.ts`.

Options:
- add a smoke test for the shared exports and exclude the generated `auth.schema.ts`, or
- lower `coverage.all`/thresholds for generated files, or
- drop the package-level coverage script if it is not meant to be gated.

Acceptance: `cd web && npm run coverage` passes from the repo root, and `ci-web.yml` can use the root
script instead of the `--workspace client` workaround.
```

#### NEW-2 — `chore(engine): delete or wire RespawnService, CharacterInitializationService, EquipmentPowerService`

```markdown
Three services are registered or shipped with no production consumer:
- `RespawnService` (`Features/Death/Services`) — registered in `ServiceCollectionExtensions`, nothing injects it.
- `CharacterInitializationService` (`Features/CharacterCreation/Services`) — registered, nothing injects it.
- `EquipmentPowerService` (`RealmEngine.Shared/Services/EquipmentAbilityService.cs`) — has tests, no callers.

Acceptance: each service is either deleted (with its registration/tests) or consumed by a real handler,
and `dotnet build Realm.Full.slnx` plus the full test suite stay green.
```

#### NEW-3 — `fix(engine): StatusEffectParser edge cases from the coverage pass`

```markdown
Two behaviours are pinned by the new tests but should probably change:
- a zero or negative `statusDuration` trait is accepted as-is, so a content typo creates an effect that
  expires instantly or never;
- custom `statModifiers` only apply when the trait value is `Dictionary<string, object>`, so a
  `JsonElement` from deserialized content is silently ignored.

Acceptance: decide the intended semantics, clamp/reject invalid durations, accept `JsonElement` maps
(case-insensitive keys), and update `StatusEffectParserTests` to assert the new behaviour.
```

#### NEW-4 — `test(server): cover the dev CORS defaults (preview origin included)`

```markdown
`CorsSetup` has no tests. The default dev origins now include `http://localhost:4173` for the Playwright
preview, and a regression there breaks the game-loop e2e with an opaque "Failed to complete negotiation".

Acceptance: a test asserts the default dev origins contain 5173 / 4173 / 8081, that
`Cors:AllowedOrigins` overrides them, and that the Production guard throws for a localhost-only list.
```

#### NEW-5 — `feat(server,web): first-login tutorial handoff`

```markdown
Session-72 made login resume the saved node, and left the explicit seam: a brand-new character writes
`CurrentNodeId = null`, so first login falls back to the entry node. Use that to start the tutorial:

- server: flag "tutorial not completed" on the character, choose the tutorial start node on first
  `SelectCharacter`, skip the resume path until it is completed;
- content: a starter quest chain accepted automatically, with `talk`/`visit` objectives;
- client: onboarding hint in the Local panel, "skip tutorial" affordance;
- tests: first login picks the tutorial node, second login resumes the saved node, completion is persisted.

Acceptance: a new character starts in the tutorial and a returning character resumes exactly where they
logged out (both covered by server tests, plus one e2e assertion in `game-loop.e2e.ts`).
```

#### NEW-6 — `feat(web): quest HUD tracker and completion toast`

```markdown
The quest journal dialog exists; the during-play surface does not.

Acceptance:
- the world column shows the top 3 active quests with `current/required` per objective;
- objective progress updates live from `QuestLogReceived` / completion broadcasts without reopening the log;
- turning a quest in shows a toast with the gold/XP granted (`QuestTurnedIn` already carries them);
- Vitest coverage for the tracker selector and the toast trigger.
```

#### NEW-7 — `feat(engine,server): quest objective types beyond kill/collect/visit`

```markdown
`QuestJournal.Advance` currently understands `kill:<enemy>`, `collect:<item>` and `visit:<location>`.
Add the remaining GDD objective kinds:

- `talk:<npc>`, `deliver:<item>@<npc>`, `craft:<recipe>` (N times), `reach_level:<n>`.
- Wire each to the existing hub command that can observe it (dialogue, trade/quest turn-in, crafting,
  `ExperienceGained`).
- Seed at least one quest per new kind; keep unknown keys inert rather than throwing.
- Tests: each kind advances only on its event and is idempotent on replays.
```

#### NEW-8 — `feat(web,server): party panel and invite flow`

```markdown
`Party` / `PartyMember` exist in `RealmEngine.Shared` but there is no hub surface and no UI.

Scope: invite/accept/decline/leave, party roster with online flags in the right column, party chat
channel, shared XP visibility (not shared XP mechanics yet), auto-disband on empty.

Acceptance: server handlers + hub methods with tests, `payload-contract.test.ts` green for new payloads,
and a `GameSession` store test suite driven by `FakeHub`.
```

#### NEW-9 — `docs: fix stale plan status and retired-command references`

```markdown
- `plans/README.md` still lists `blazor-decommission-and-cleanup.md` as 🟢 Active although the plan is
  Done; it should be removed from the live table (or marked Done and kept as history).
- `.vscode/tasks.json:622` still runs `npm run build:desktop`.
- `.roo/skills/web-development/SKILL.md` still tells agents to run `npm run build:desktop` and mentions
  `adapter-static`.

Acceptance: `grep -rn "build:desktop" -- . ':!web/llms'` returns nothing outside agent-memory history,
`repo-hygiene` passes, and the plans index matches reality.
```

#### NEW-10 — `ci: flip Codecov statuses from informational to blocking`

```markdown
`codecov.yml` intentionally sets project/patch statuses `informational: true` until the numbers are
stable. With the flag wiring fixed in #30 and the coverage uploads green, decide a floor per component
(engine/server/web) and enforce it.

Acceptance: statuses are non-informational with targets chosen from the last 2–4 weeks of data; a
deliberate coverage drop fails the PR.
```

#### NEW-11 — `ops(dev): Mailpit for local mail, and a NullEmailSender that logs nothing`

```markdown
`NullEmailSender` still echoes the full message body in Development so reset links are reachable. With a
catcher container, devs can read the real message through a UI instead of the log.

Acceptance: `docker-compose.dev.yml` gains Mailpit (`8025` UI / `1025` SMTP) and documents the
`Email:Smtp*` values; `NullEmailSender` logs the envelope only in every environment; the game-loop e2e
and `email.ts` dev path are re-verified.
```

---

## 5. Labels to use (create if missing)

`epic`, `server`, `web`, `engine`, `ci`, `docs`, `security`, `small` — descriptions captured in the
apply step. The existing repo labels are only Dependabot's (`dependencies`, `javascript`, `.NET`,
`github_actions`), so the human labels are new.

---

## 6. How to apply

**Blocked on credentials, confirmed:** the MCP GitHub token used in this session is read-only for the
repository. `merge_pull_request`, `create_or_update_file` and `add_issue_comment` all return
*"Resource not accessible by personal access token"*, so this document is the deliverable until the
token is granted `issues: write` (and `contents: write` for the PR follow-ups).

### 6a. Manual apply checklist (GitHub UI)

Ordered so nothing is lost if the session stops halfway. Every body is copy-paste ready above.

| Order | Action | Where |
|---|---|---|
| 1 | Comment + close with the §4.1 text | <https://github.com/KungRaseri/Veldrath/issues/3> |
| 2 | Comment + close with the §4.1 text | <https://github.com/KungRaseri/Veldrath/issues/7> |
| 3 | Replace body with the §4.2 #4 template (title unchanged) | <https://github.com/KungRaseri/Veldrath/issues/4> |
| 4 | Replace body with the §4.2 #5 template (title unchanged) | <https://github.com/KungRaseri/Veldrath/issues/5> |
| 5 | Replace body with the §4.2 #6 template (title unchanged) | <https://github.com/KungRaseri/Veldrath/issues/6> |
| 6 | Create NEW-1 … NEW-5 | <https://github.com/KungRaseri/Veldrath/issues/new> |
| 7 | Create NEW-6 … NEW-11 | <https://github.com/KungRaseri/Veldrath/issues/new> |

Titles for the new issues, in the order they should be created:

1. `fix(web/shared): the @veldrath/shared coverage gate cannot pass`
2. `chore(engine): delete or wire RespawnService, CharacterInitializationService, EquipmentPowerService`
3. `fix(engine): StatusEffectParser edge cases from the coverage pass`
4. `test(server): cover the dev CORS defaults (preview origin included)`
5. `feat(server,web): first-login tutorial handoff`
6. `feat(web): quest HUD tracker and completion toast`
7. `feat(engine,server): quest objective types beyond kill/collect/visit`
8. `feat(web,server): party panel and invite flow`
9. `docs: fix stale plan status and retired-command references`
10. `ci: flip Codecov statuses from informational to blocking`
11. `ops(dev): Mailpit for local mail, and a NullEmailSender that logs nothing`

Labels and milestones have to be created in the UI as well — the MCP server exposes no label or
milestone endpoints, and the repo currently only has the Dependabot labels (`dependencies`,
`javascript`, `.NET`, `github_actions`). Suggested labels: `epic`, `server`, `web`, `engine`, `ci`,
`docs`, `security`, `small`.

### 6b. If the token is granted `issues: write`

Switch to Code mode and the whole set can be applied through the API — `add_issue_comment` +
`update_issue(state=closed)` for #3/#7, `update_issue(body=…)` for #4/#5/#6, and eleven
`create_issue` calls, checking each returned number against the list above.

- [ ] Close #3 with the superseded-design comment
- [ ] Close #7 with the journal-shipped comment and pointers to NEW-6 / NEW-7
- [ ] Replace #4 / #5 / #6 bodies with the web-first rewrites
- [ ] Create NEW-1 … NEW-11 (titles above)
- [ ] Create the human labels and assign them per §5
