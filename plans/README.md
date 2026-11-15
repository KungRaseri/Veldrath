# Plans

Only **live** work lives here. Design history is not archived as plans — when a plan is
finished or superseded, the still-useful decisions are folded into `docs/` and the plan is
deleted (git history keeps it).

| Plan | Status | Purpose |
|---|---|---|
| [`dotnet-coverage-gate.md`](dotnet-coverage-gate.md) | 🔧 **Live** — decided 2026-11-03, implementation carded | The .NET half of the 90% coverage goal: which assemblies are gated, the enforcement mechanism (a script over ReportGenerator's merged Cobertura, in a new `ci-coverage.yml`), and the per-assembly floors measured on 10.0.402. Target 90% line; floors are a no-regression ratchet at today's measured baseline. |
| [`web-feature-parity-and-coverage.md`](web-feature-parity-and-coverage.md) | 🔧 **Live** | Web feature parity matrix plus the 90% Vitest gate with per-glob floors. |
| [`ui-depth.md`](ui-depth.md) | 🔧 **Live** — milestone E; E1/E2/E3/E5 shipped, E4 moved to the content milestone, E6 moved to `dungeons.md` | Turns wired game systems into usable surfaces: powers and abilities, the skill sheet, party-aware combat, NPC dialogue, crafting on the real engine, dungeons. Separates stub surfaces from placeholder bridges from absent features, and gives each phase a definition of done plus seven milestone-wide rules (no identifier on screen, cost and consequence shown before commit, snapshot-first state, no new deferral). |
| [`dungeons.md`](dungeons.md) | 🔧 **Live** — model decided 2026-11-12, phases F1–F5 outlined, no cards yet | How dungeons relate to maps, regions and zones: five are already authored zone maps; a `ZoneLocation` may open into an interior map of rooms; locations are public or instanced per location with progress scoped to match; one entry path for map travel and the party action; a lobby per dungeon and a soft level gate. Includes the four content defects that make three dungeons inescapable. The map editor outline moved to `foundry-map-editor.md`. |
| [`foundry-map-editor.md`](foundry-map-editor.md) | 🔧 **Live** — milestone G; G0–G1 in progress | The remaining Foundry map authoring surface: navigation and tests, validate-only with per-node issues, the canvas and node movement, node and edge CRUD, exits and metadata, map create/delete, and the documentation and coverage gates. Location traits and actor pools are a separate plan. |
| [`server-first-world-state.md`](server-first-world-state.md) | ✅ Complete — milestone D (epic #32) | Move live enemies, respawn timers and combat sessions out of process memory into Postgres so a restart is lossless. First of D → C → A → B. |
| [`party-play.md`](party-play.md) | ✅ Complete — milestone C (epic #37) | Multiplayer party: persisted parties and invites, shared combat with contribution-based rewards, party chat. Builds directly on D. |
| [`death-and-first-hour.md`](death-and-first-hour.md) | ✅ Complete — milestone A (epic #38) | Gold-first death cost, safe respawn plus death sickness, tutorial handoff, quest HUD tracker, new objective kinds, hardcore memorial and leaderboard. |
| [`content-pipeline.md`](content-pipeline.md) | ✅ Complete — milestone B (epic #39) | Authoring-time validators for every content family, a RBAC-gated Foundry write path (RealmForge was removed), content validation in CI, and coverage/Codecov honesty. |
| [`github-issue-refresh.md`](github-issue-refresh.md) | ✅ Complete — roadmap + issue bodies | Issue bodies for milestones C, A and B, plus the roadmap refresh. Superseded: every phase it described has shipped. |
| [`blazor-decommission-and-cleanup.md`](blazor-decommission-and-cleanup.md) | ✅ Complete (W1–W12) | Blazor residue removed, desktop client retired, Identity auth surface removed (tables kept for RBAC). One open question left: whether internal `Veldrath.Contracts` DTOs move next to their feature slices. |
| [`sveltekit-migration.md`](sveltekit-migration.md) | ✅ Complete (phases 0–6) | Canonical record of the Blazor → SvelteKit migration and the post-migration log. Its three unfinished items (authenticated game-loop coverage, WebView2 smoke, payload drift) now live in the decommission plan as W7–W9. |

> **All four roadmap milestones (D → C → A → B) are closed**, and `plans/` is now older than the repository
> around it: these five plans are complete but still present. The convention at the top of this file says a
> finished plan should have its durable decisions folded into `docs/` and then be deleted. That fold has not
> happened yet — the four plans each carry an "Explicitly deferred" section (guild/trade/PvP from C, resurrection
> and corpse runs from A, bulk content import and content versioning from B) that needs to become either a docs
> note or a tracked issue before the files go. Until then, read them as history, not as live work.

## Where the deleted plans' knowledge went

| Topic | Now documented in |
|---|---|
| Panel-driven client UI (no tilemap/sprite rendering) and the DB map graph | [`docs/client-ui-architecture.md`](../docs/client-ui-architecture.md) |
| Design tokens, typography, retired Blazor surfaces | [`docs/design-system.md`](../docs/design-system.md) |
| Deployment, Coolify resources, env vars, troubleshooting | [`docs/deployment.md`](../docs/deployment.md), [`docs/coolify-deployment.md`](../docs/coolify-deployment.md) |
| Web app conventions and skill references | [`AGENTS.md`](../AGENTS.md), [`web/README.md`](../web/README.md), [`.roo/skills/`](../.roo/skills/) |
| Session-by-session context (including why each pivot happened) | [`.github/agent-memory/`](../.github/agent-memory/) |

## Adding a plan

Write it here only if the work is not yet done. Once it ships, move the durable decisions into
`docs/` and delete the file.
