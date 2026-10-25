# Plans

Only **live** work lives here. Design history is not archived as plans — when a plan is
finished or superseded, the still-useful decisions are folded into `docs/` and the plan is
deleted (git history keeps it).

| Plan | Status | Purpose |
|---|---|---|
| [`server-first-world-state.md`](server-first-world-state.md) | 🟢 Active — milestone D | Move live enemies, respawn timers and combat sessions out of process memory into Postgres so a restart is lossless. First of D → C → A → B. |
| [`party-play.md`](party-play.md) | 🟢 Planned — milestone C | Multiplayer party: persisted parties and invites, shared combat with contribution-based rewards, party chat. Builds directly on D. |
| [`death-and-first-hour.md`](death-and-first-hour.md) | 🟢 Planned — milestone A | Gold-first death cost, safe respawn plus death sickness, tutorial handoff, quest HUD tracker, new objective kinds, hardcore memorial and leaderboard. |
| [`content-pipeline.md`](content-pipeline.md) | 🟢 Planned — milestone B | Authoring-time validators for every content family, write-path enforcement in RealmForge, a CI content guard, and coverage/Codecov honesty. |
| [`github-issue-refresh.md`](github-issue-refresh.md) | 🟢 Active — roadmap + issue bodies | Issue bodies for milestones C (party), A (death consequences + first hour) and B (content pipeline); the tracker itself stays empty until each milestone starts. |
| [`blazor-decommission-and-cleanup.md`](blazor-decommission-and-cleanup.md) | ✅ Complete (W1–W12) | Blazor residue removed, desktop client retired, Identity auth surface removed (tables kept for RBAC). One open question left: whether internal `Veldrath.Contracts` DTOs move next to their feature slices. |
| [`sveltekit-migration.md`](sveltekit-migration.md) | ✅ Complete (phases 0–6) | Canonical record of the Blazor → SvelteKit migration and the post-migration log. Its three unfinished items (authenticated game-loop coverage, WebView2 smoke, payload drift) now live in the decommission plan as W7–W9. |

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
