# Plans

Only **live** work lives here. Design history is not archived as plans — when a plan is
finished or superseded, the still-useful decisions are folded into `docs/` and the plan is
deleted (git history keeps it).

| Plan | Status | Purpose |
|---|---|---|
| [`blazor-decommission-and-cleanup.md`](blazor-decommission-and-cleanup.md) | 🟢 Active | Finish the Blazor residue, decide the ASP.NET Core Identity question, run the W1–W6 cleanup sweep. |
| [`sveltekit-migration.md`](sveltekit-migration.md) | ✅ Complete (phases 0–6) | Canonical record of the Blazor → SvelteKit migration and the post-migration log for account/character lifecycle, theme parity, and deployment. |

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
