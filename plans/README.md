# Plans index

Small index of every plan in this directory so nobody (human or agent) follows a
document that describes a design the repository no longer has.

**How to use this folder:** the plans are design records, not instructions. A plan marked
`historical` or `superseded` explains *why* a thing was built the way it was — it must not be
implemented as written.

## Active

| Plan | Status | Purpose |
|---|---|---|
| [`blazor-decommission-and-cleanup.md`](blazor-decommission-and-cleanup.md) | 🟢 Active | Remove the last Blazor residue, decide the Identity question, and run the W1–W6 cleanup sweep. |
| [`reactive-ui-pivot-plan.md`](reactive-ui-pivot-plan.md) | 🟢 Active reference | The Avalonia client's panel-driven reactive UI (replaced tilemap rendering). Tokens live in [`docs/design-system.md`](../docs/design-system.md). |

## Reference records (completed)

| Plan | Status | Notes |
|---|---|---|
| [`sveltekit-migration.md`](sveltekit-migration.md) | ✅ Complete (phases 0–6) | Canonical record of the Blazor → SvelteKit migration, including the post-migration log for account/character lifecycle, theme parity, and deployment. |
| [`sveltekit-phase0.md`](sveltekit-phase0.md) | ✅ Executed runbook | Phase 0 workspace move and verification steps. Kept for disaster-recovery context. |

## Superseded (pre-SvelteKit, Avalonia rendering)

| Plan | Superseded by |
|---|---|
| [`ascii-tile-entity-system.md`](ascii-tile-entity-system.md) | [`reactive-ui-pivot-plan.md`](reactive-ui-pivot-plan.md) |
| [`rendering-extraction-plan.md`](rendering-extraction-plan.md) | [`reactive-ui-pivot-plan.md`](reactive-ui-pivot-plan.md) |

## Historical — Blazor era

These documents describe applications that no longer exist (`Veldrath.Web`, `RealmFoundry`,
`Veldrath.Auth.Blazor`, `Veldrath.GameClient.Components`). They are retained purely as design
history; every one carries a historical banner at the top. Current web guidance lives in
[`web/`](../web/) and [`docs/design-system.md`](../docs/design-system.md).

| Plan | What it documented |
|---|---|
| [`web-client-architecture.md`](web-client-architecture.md) | Browser game client inside Blazor Server |
| [`web-first-roadmap.md`](web-first-roadmap.md) | RCL-first feature sequencing |
| [`game-client-unification-plan.md`](game-client-unification-plan.md) | Extracting the shared Razor Class Library |
| [`game-client-unification-gaps.md`](game-client-unification-gaps.md) | RCL ↔ Avalonia parity gaps |
| [`game-client-parity-checklist.md`](game-client-parity-checklist.md) | Hub event/command parity checklist (G1–G133) |
| [`gameplay-view-refactor-plan.md`](gameplay-view-refactor-plan.md) | Gameplay layout refactor in the RCL |
| [`game-routing-guard-plan.md`](game-routing-guard-plan.md) | `GameEntry` routing guard + resume dialog |
| [`map-view-refactor-plan.md`](map-view-refactor-plan.md) | Tilemap viewport/camera rewrite |
| [`page-refresh-resilience-plan.md`](page-refresh-resilience-plan.md) | Restoring game state after a circuit refresh |
| [`text-dashboard-map-graph-plan.md`](text-dashboard-map-graph-plan.md) | Text-first dashboard + unified map graph in the RCL |
| [`media-breakpoint-strategy.md`](media-breakpoint-strategy.md) | Responsive breakpoints for the Blazor surfaces |
| [`css-audit-game-client-components.md`](css-audit-game-client-components.md) | MudBlazor utility migration audit |
| [`realmfoundry-web-audit.md`](realmfoundry-web-audit.md) | RealmFoundry CSS/Razor audit |
| [`realmfoundry-migration-status.md`](realmfoundry-migration-status.md) | RealmFoundry MudBlazor migration status |
| [`auth-delegating-handler-design.md`](auth-delegating-handler-design.md) | Blazor `AuthDelegatingHandler` refresh design |
| [`web-character-creation-sync-plan.md`](web-character-creation-sync-plan.md) | Character-creation parity for the Blazor wizard |
| [`infrastructure-coolify-extraction-plan.md`](infrastructure-coolify-extraction-plan.md) | First Coolify extraction pass (foundry/web services) |
| [`coolify-compatibility-analysis.md`](coolify-compatibility-analysis.md) | Coolify compatibility audit for the Compose stack |
| [`docker-compose-coolify-spec.md`](docker-compose-coolify-spec.md) | Compose spec with Caddy → Traefik migration |
