# Veldrath Server + Web Client — Memory Notes

> **Status**: this file is the **index** of the memory store, not a log. Every note lives in
> `work-items/` (one file per work item) or `areas/` (one file per stable topic). The table at
> the foot is generated from their YAML front matter, and this whole file is generated from
> `scripts/memory/veldrath-memory.template.md` — a hand edit is a failed build.

The Avalonia desktop client is gone — Veldrath is web-first, and the SvelteKit app in `web/` is the
only client. Notes written before that pivot mention `Veldrath.Client`, `GameClient.Core`,
`Razor`/`MudBlazor` or `/api/foundry-bridge/*`; they are historical session logs, not current
guidance.

## How to add a note

One file per work item, and only your own file — a new path cannot conflict with another branch:

1. Create `.github/agent-memory/work-items/<yyyy-mm-dd>-<card>-<slug>.md`. `<card>` is the
   `t_xxxxxxxx` id and is omitted when the work has none; the date is the day the work landed.
2. Start it with the front matter below and write the note underneath. The date in the file name
   and `date:` agree, or both say `undated`.
3. Regenerate the index with `node scripts/memory/memory-index.mjs --write` and commit both files.

```yaml
---
kind: work-item
date: 2026-11-23
area: content
card: t_d97538ea
pr: 244
title: "Progression is server-side; a level-up moves the health bar"
---
```

`area` is one of `content`, `engine`, `web`, `map`, `auth` or `infra`; `card` and `pr` are optional.
A stable topic that outgrows a session log becomes `areas/<topic>.md` with `kind: area`.

## Rules

- **One file per work item.** Never append to another card's file, and never append a
  `### Session-*` block to this one — the generator would erase it.
- **This file is generated.** `node scripts/memory/memory-index.mjs --check` compares the whole
  file byte-for-byte with what the template and the notes produce, so any hand edit — a preamble
  change, a table cell, or a note appended below the table — fails.
- **Counters have one source.** Where a number is asserted by a test, the note points at the test
  and does not restate the number — the seeded map graph is the worked example, and
  `RealmEngine.Data.Tests/Seeders/MapGraphSeederTests.cs` owns both the map/node/edge totals and the
  "every live zone is enterable through a door" list.
- **The other seven reference files are not split.** `engine-codebase.md`,
  `json-migration-status.md`, `foundry-content-schema.md`, `gap-analysis-process.md`,
  `auth-and-character-creation-plan.md`, `combat-loop-plan.md` and `world-lore-plan.md` are edited in
  place; only this index is generated.

## Index

Newest first. `—` in the Date column means the note carries no date of its own.

<!-- memory-index:begin -->
| Date | Area | Card | PR | Title | File |
| --- | --- | --- | --- | --- | --- |
| 2026-11-23 | map | `t_990e7186` |  | Respawn moves the map node, so a reload wakes where the player did | [work-items/2026-11-23-t_990e7186-respawn-moves-the-map-node-so-a-reload-wakes-where-the-player-did.md](work-items/2026-11-23-t_990e7186-respawn-moves-the-map-node-so-a-reload-wakes-where-the-player-did.md) |
| 2026-11-23 | content | `t_d97538ea` | #244 | Progression is server-side; a level-up moves the health bar | [work-items/2026-11-23-t_d97538ea-progression-is-server-side-a-level-up-moves-the-health-bar.md](work-items/2026-11-23-t_d97538ea-progression-is-server-side-a-level-up-moves-the-health-bar.md) |
| 2026-11-23 | web | `t_dad908f7` |  | ExperienceGained carries the banked attribute points, so the panel follows a level-up | [work-items/2026-11-23-t_dad908f7-experience-gained-carries-the-banked-attribute-points.md](work-items/2026-11-23-t_dad908f7-experience-gained-carries-the-banked-attribute-points.md) |
| 2026-11-23 | content |  |  | The Halrow Import Pack and Its Way In (2026-11-23) | [areas/the-halrow-import-pack-and-its-way-in-2026-11-23.md](areas/the-halrow-import-pack-and-its-way-in-2026-11-23.md) |
| 2026-11-21 | content | `t_d65b853d` |  | The people arrive: D3's roster is seeded, and the three towns can trade again | [work-items/2026-11-21-t_d65b853d-the-people-arrive-d3-s-roster-is-seeded-and-the-three-towns-can-trade.md](work-items/2026-11-21-t_d65b853d-the-people-arrive-d3-s-roster-is-seeded-and-the-three-towns-can-trade.md) |
| 2026-11-20 | web |  |  | A script the web job runs now triggers the web job | [work-items/2026-11-20-a-script-the-web-job-runs-now-triggers-the-web-job.md](work-items/2026-11-20-a-script-the-web-job-runs-now-triggers-the-web-job.md) |
| 2026-11-20 | content |  |  | Dialogue depth: E4 is shipped | [work-items/2026-11-20-dialogue-depth-e4-is-shipped.md](work-items/2026-11-20-dialogue-depth-e4-is-shipped.md) |
| 2026-11-20 | web |  |  | E2E reporting, and the spec that had stopped running | [work-items/2026-11-20-e2e-reporting-and-the-spec-that-had-stopped-running.md](work-items/2026-11-20-e2e-reporting-and-the-spec-that-had-stopped-running.md) |
| 2026-11-20 | web |  |  | The authenticated e2e job was red: one failure, three traps | [work-items/2026-11-20-the-authenticated-e2e-job-was-red-one-failure-three-traps.md](work-items/2026-11-20-the-authenticated-e2e-job-was-red-one-failure-three-traps.md) |
| 2026-11-20 | web |  |  | The coverage comment now quotes the gate, not the reporter | [work-items/2026-11-20-the-coverage-comment-now-quotes-the-gate-not-the-reporter.md](work-items/2026-11-20-the-coverage-comment-now-quotes-the-gate-not-the-reporter.md) |
| 2026-11-20 | web |  |  | The coverage reporter now reads the root counters (v1.0.4) | [work-items/2026-11-20-the-coverage-reporter-now-reads-the-root-counters-v1-0-4.md](work-items/2026-11-20-the-coverage-reporter-now-reads-the-root-counters-v1-0-4.md) |
| 2026-11-20 | content |  |  | The shelves were re-based onto the approved band, and a guard now pins the arithmetic | [work-items/2026-11-20-the-shelves-were-re-based-onto-the-approved-band-and-a-guard-now-pins.md](work-items/2026-11-20-the-shelves-were-re-based-onto-the-approved-band-and-a-guard-now-pins.md) |
| 2026-11-20 | content |  |  | The shop became a person: T1 lands the merchant band and the per-zone shelf | [work-items/2026-11-20-the-shop-became-a-person-t1-lands-the-merchant-band-and-the-per-zone.md](work-items/2026-11-20-the-shop-became-a-person-t1-lands-the-merchant-band-and-the-per-zone.md) |
| 2026-11-20 | content |  |  | Enemy Instance Rows Carry Their Spawn-Projection Version (2026-11-20) | [areas/enemy-instance-rows-carry-their-spawn-projection-version-2026-11-20.md](areas/enemy-instance-rows-carry-their-spawn-projection-version-2026-11-20.md) |
| 2026-11-20 | web |  |  | Web gates in a worktree (standing recipe, 2026-11-20) | [areas/web-gates-in-a-worktree-standing-recipe-2026-11-20.md](areas/web-gates-in-a-worktree-standing-recipe-2026-11-20.md) |
| 2026-11-19 | web |  |  | Design documentation restructured for a creative team | [work-items/2026-11-19-design-documentation-restructured-for-a-creative-team.md](work-items/2026-11-19-design-documentation-restructured-for-a-creative-team.md) |
| 2026-11-04 | engine |  | #53 | Kill rewards and the killing cast both persist (PR #53 round 3) | [work-items/2026-11-04-kill-rewards-and-the-killing-cast-both-persist-pr-53-round-3.md](work-items/2026-11-04-kill-rewards-and-the-killing-cast-both-persist-pr-53-round-3.md) |
| 2026-11-04 | web |  |  | .NET coverage 75.4% → 95.1%, and the floors move with it | [work-items/2026-11-04-net-coverage-75-4-95-1-and-the-floors-move-with-it.md](work-items/2026-11-04-net-coverage-75-4-95-1-and-the-floors-move-with-it.md) |
| 2026-11-04 | web |  | #53 | Review corrections on PR #53: determinism, culture, real assertions | [work-items/2026-11-04-review-corrections-on-pr-53-determinism-culture-real-assertions.md](work-items/2026-11-04-review-corrections-on-pr-53-determinism-culture-real-assertions.md) |
| 2026-11-04 | web |  |  | The wiki pin conflicts a second time, and why git cannot resolve it | [work-items/2026-11-04-the-wiki-pin-conflicts-a-second-time-and-why-git-cannot-resolve-it.md](work-items/2026-11-04-the-wiki-pin-conflicts-a-second-time-and-why-git-cannot-resolve-it.md) |
| 2026-11-04 | web |  |  | `Veldrath.Assets` deleted, and the tracker shrinks by half | [work-items/2026-11-04-veldrath-assets-deleted-and-the-tracker-shrinks-by-half.md](work-items/2026-11-04-veldrath-assets-deleted-and-the-tracker-shrinks-by-half.md) |
| 2026-11-03 | web |  |  | A root-config pull request reports no status, and `["*"]` then blocks it | [work-items/2026-11-03-a-root-config-pull-request-reports-no-status-and-then-blocks-it.md](work-items/2026-11-03-a-root-config-pull-request-reports-no-status-and-then-blocks-it.md) |
| 2026-11-03 | engine |  |  | `main`'s wiki pin was dangling, and a sync merge is not a content merge | [work-items/2026-11-03-main-s-wiki-pin-was-dangling-and-a-sync-merge-is-not-a-content-merge.md](work-items/2026-11-03-main-s-wiki-pin-was-dangling-and-a-sync-merge-is-not-a-content-merge.md) |
| 2026-11-03 | web |  |  | Merge gate: `development` and `main` are branch-protected | [work-items/2026-11-03-merge-gate-development-and-main-are-branch-protected.md](work-items/2026-11-03-merge-gate-development-and-main-are-branch-protected.md) |
| 2026-11-03 | web |  |  | The .NET coverage gate landed, and settling it moved no floor | [work-items/2026-11-03-the-net-coverage-gate-landed-and-settling-it-moved-no-floor.md](work-items/2026-11-03-the-net-coverage-gate-landed-and-settling-it-moved-no-floor.md) |
| 2026-11-03 | web |  |  | Web coverage gate green: branch tail closed | [work-items/2026-11-03-web-coverage-gate-green-branch-tail-closed.md](work-items/2026-11-03-web-coverage-gate-green-branch-tail-closed.md) |
| 2026-11-02 | infra |  |  | Forgejo CI actually green: the four real blockers | [work-items/2026-11-02-forgejo-ci-actually-green-the-four-real-blockers.md](work-items/2026-11-02-forgejo-ci-actually-green-the-four-real-blockers.md) |
| 2026-11-02 | engine |  |  | Forgejo CI migration: mirrored actions, registry, releases | [work-items/2026-11-02-forgejo-ci-migration-mirrored-actions-registry-releases.md](work-items/2026-11-02-forgejo-ci-migration-mirrored-actions-registry-releases.md) |
| 2026-11-02 | infra |  |  | No dependency bot: `.github/dependabot.yml` deleted | [work-items/2026-11-02-no-dependency-bot-github-dependabot-yml-deleted.md](work-items/2026-11-02-no-dependency-bot-github-dependabot-yml-deleted.md) |
| 2026-11-02 | web |  |  | web parity matrix + coverage gate (PR 1 of the web pass) | [work-items/2026-11-02-web-parity-matrix-coverage-gate-pr-1-of-the-web-pass.md](work-items/2026-11-02-web-parity-matrix-coverage-gate-pr-1-of-the-web-pass.md) |
| 2026-10-26 | infra |  |  | B1: one content rule set, shared by audit / write path / CI | [work-items/2026-10-26-b1-one-content-rule-set-shared-by-audit-write-path-ci.md](work-items/2026-10-26-b1-one-content-rule-set-shared-by-audit-write-path-ci.md) |
| 2026-10-26 | web |  |  | B2: the Foundry content editor (write path + edit form) | [work-items/2026-10-26-b2-the-foundry-content-editor-write-path-edit-form.md](work-items/2026-10-26-b2-the-foundry-content-editor-write-path-edit-form.md) |
| 2026-10-26 | content |  |  | B3: content guard, and the three defects it made non-negotiable | [work-items/2026-10-26-b3-content-guard-and-the-three-defects-it-made-non-negotiable.md](work-items/2026-10-26-b3-content-guard-and-the-three-defects-it-made-non-negotiable.md) |
| 2026-10-26 | content |  |  | Milestone B closed (B1-B5) | [work-items/2026-10-26-milestone-b-closed-b1-b5.md](work-items/2026-10-26-milestone-b-closed-b1-b5.md) |
| 2026-10-25 | content |  |  | A2: safe respawn location, death sickness, loud failsafes | [work-items/2026-10-25-a2-safe-respawn-location-death-sickness-loud-failsafes.md](work-items/2026-10-25-a2-safe-respawn-location-death-sickness-loud-failsafes.md) |
| 2026-10-25 | content |  |  | A3: tutorial handoff | [work-items/2026-10-25-a3-tutorial-handoff.md](work-items/2026-10-25-a3-tutorial-handoff.md) |
| 2026-10-25 | content |  |  | A4: quest HUD tracker, completion diffing, turn-in toast (client only) | [work-items/2026-10-25-a4-quest-hud-tracker-completion-diffing-turn-in-toast-client-only.md](work-items/2026-10-25-a4-quest-hud-tracker-completion-diffing-turn-in-toast-client-only.md) |
| 2026-10-25 | web |  |  | A5: objective kinds, kind constant, audit coverage | [work-items/2026-10-25-a5-objective-kinds-kind-constant-audit-coverage.md](work-items/2026-10-25-a5-objective-kinds-kind-constant-audit-coverage.md) |
| 2026-10-25 | engine |  |  | A6: hardcore memorial and leaderboard (Milestone A complete) | [work-items/2026-10-25-a6-hardcore-memorial-and-leaderboard-milestone-a-complete.md](work-items/2026-10-25-a6-hardcore-memorial-and-leaderboard-milestone-a-complete.md) |
| 2026-10-25 | web |  |  | Bag rows and the item tooltip | [work-items/2026-10-25-bag-rows-and-the-item-tooltip.md](work-items/2026-10-25-bag-rows-and-the-item-tooltip.md) |
| 2026-10-25 | engine |  | #37 | C1: party schema and hub commands (#37) | [work-items/2026-10-25-c1-party-schema-and-hub-commands-37.md](work-items/2026-10-25-c1-party-schema-and-hub-commands-37.md) |
| 2026-10-25 | engine |  |  | C2 client half: party panel, invite toast, snapshot handlers | [work-items/2026-10-25-c2-client-half-party-panel-invite-toast-snapshot-handlers.md](work-items/2026-10-25-c2-client-half-party-panel-invite-toast-snapshot-handlers.md) |
| 2026-10-25 | engine |  |  | C2 server half: invite flow | [work-items/2026-10-25-c2-server-half-invite-flow.md](work-items/2026-10-25-c2-server-half-invite-flow.md) |
| 2026-10-25 | content |  |  | C3: shared combat participation and contribution rewards | [work-items/2026-10-25-c3-shared-combat-participation-and-contribution-rewards.md](work-items/2026-10-25-c3-shared-combat-participation-and-contribution-rewards.md) |
| 2026-10-25 | content |  |  | C4: party chat, and Milestone C closed | [work-items/2026-10-25-c4-party-chat-and-milestone-c-closed.md](work-items/2026-10-25-c4-party-chat-and-milestone-c-closed.md) |
| 2026-10-25 | engine |  | #33 | D2: the live world moved into Postgres (#33) | [work-items/2026-10-25-d2-the-live-world-moved-into-postgres-33.md](work-items/2026-10-25-d2-the-live-world-moved-into-postgres-33.md) |
| 2026-10-25 | content |  | #34 | D3: the respawn sweeper replaces the old timer (#34) | [work-items/2026-10-25-d3-the-respawn-sweeper-replaces-the-old-timer-34.md](work-items/2026-10-25-d3-the-respawn-sweeper-replaces-the-old-timer-34.md) |
| 2026-10-25 | content |  | #35 | D4: reconnect grace, AFK service, claim refusal (#35) | [work-items/2026-10-25-d4-reconnect-grace-afk-service-claim-refusal-35.md](work-items/2026-10-25-d4-reconnect-grace-afk-service-claim-refusal-35.md) |
| 2026-10-25 | content |  | #36 | D5: content audit and failsafe-not-fallback (#36, closes #31) | [work-items/2026-10-25-d5-content-audit-and-failsafe-not-fallback-36-closes-31.md](work-items/2026-10-25-d5-content-audit-and-failsafe-not-fallback-36-closes-31.md) |
| 2026-10-25 | engine |  |  | Dead-service sweep, StatusEffectParser hardening, tracker emptied | [work-items/2026-10-25-dead-service-sweep-statuseffectparser-hardening-tracker-emptied.md](work-items/2026-10-25-dead-service-sweep-statuseffectparser-hardening-tracker-emptied.md) |
| 2026-10-25 | web |  |  | Desktop client deleted, Veldrath is web-first only | [work-items/2026-10-25-desktop-client-deleted-veldrath-is-web-first-only.md](work-items/2026-10-25-desktop-client-deleted-veldrath-is-web-first-only.md) |
| 2026-10-25 | content |  |  | Gameplay gaps closed: items, ground drops, quests, attributes, abilities | [work-items/2026-10-25-gameplay-gaps-closed-items-ground-drops-quests-attributes-abilities.md](work-items/2026-10-25-gameplay-gaps-closed-items-ground-drops-quests-attributes-abilities.md) |
| 2026-10-25 | content |  |  | HUD rework: character sheet, world column, dialog overlays | [work-items/2026-10-25-hud-rework-character-sheet-world-column-dialog-overlays.md](work-items/2026-10-25-hud-rework-character-sheet-world-column-dialog-overlays.md) |
| 2026-10-25 | auth |  |  | Identity auth surface removed; RBAC stays on the server | [work-items/2026-10-25-identity-auth-surface-removed-rbac-stays-on-the-server.md](work-items/2026-10-25-identity-auth-surface-removed-rbac-stays-on-the-server.md) |
| 2026-10-25 | map |  |  | Login resumes the last known node | [work-items/2026-10-25-login-resumes-the-last-known-node.md](work-items/2026-10-25-login-resumes-the-last-known-node.md) |
| 2026-10-25 | engine |  |  | Milestone A kickoff + A1: gold-first death cost | [work-items/2026-10-25-milestone-a-kickoff-a1-gold-first-death-cost.md](work-items/2026-10-25-milestone-a-kickoff-a1-gold-first-death-cost.md) |
| 2026-10-25 | web |  |  | Milestone B scope shift: RealmForge removed, Foundry is the editor | [work-items/2026-10-25-milestone-b-scope-shift-realmforge-removed-foundry-is-the-editor.md](work-items/2026-10-25-milestone-b-scope-shift-realmforge-removed-foundry-is-the-editor.md) |
| 2026-10-25 | content |  |  | Milestone D review: runtime smoke and two content findings | [work-items/2026-10-25-milestone-d-review-runtime-smoke-and-two-content-findings.md](work-items/2026-10-25-milestone-d-review-runtime-smoke-and-two-content-findings.md) |
| 2026-10-24 | map |  |  | Payload contract guard, bundle smoke, decommission tail | [work-items/2026-10-24-payload-contract-guard-bundle-smoke-decommission-tail.md](work-items/2026-10-24-payload-contract-guard-bundle-smoke-decommission-tail.md) |
| 2026-10-24 | content |  |  | Repository Config Consolidation (Session-64, 2026-10-24) | [areas/repository-config-consolidation-session-64-2026-10-24.md](areas/repository-config-consolidation-session-64-2026-10-24.md) |
| 2026-10-24 | web |  |  | SvelteKit Account + Character Lifecycle Completion (Session-62, 2026-10-24) | [areas/sveltekit-account-character-lifecycle-completion-session-62-2026-10-24.md](areas/sveltekit-account-character-lifecycle-completion-session-62-2026-10-24.md) |
| 2026-10-24 | web |  |  | SvelteKit Theme + Asset Parity (Session-63, 2026-10-24) | [areas/sveltekit-theme-asset-parity-session-63-2026-10-24.md](areas/sveltekit-theme-asset-parity-session-63-2026-10-24.md) |
| 2026-10-23 | web |  |  | Cardinal map layout refresh for the post-tile world | [work-items/2026-10-23-cardinal-map-layout-refresh-for-the-post-tile-world.md](work-items/2026-10-23-cardinal-map-layout-refresh-for-the-post-tile-world.md) |
| 2026-10-23 | auth |  |  | Delete confirmation, shields, and inventory/equipment sync | [work-items/2026-10-23-delete-confirmation-shields-and-inventory-equipment-sync.md](work-items/2026-10-23-delete-confirmation-shields-and-inventory-equipment-sync.md) |
| 2026-10-23 | auth |  |  | First-login map, starting equipment, and map button fixed | [work-items/2026-10-23-first-login-map-starting-equipment-and-map-button-fixed.md](work-items/2026-10-23-first-login-map-starting-equipment-and-map-button-fixed.md) |
| 2026-10-23 | content |  |  | Web-first pivot and RCL action correctness | [work-items/2026-10-23-web-first-pivot-and-rcl-action-correctness.md](work-items/2026-10-23-web-first-pivot-and-rcl-action-correctness.md) |
| 2026-10-23 | engine |  |  | Web inventory/shop transaction correctness (web-first P0-3) | [work-items/2026-10-23-web-inventory-shop-transaction-correctness-web-first-p0-3.md](work-items/2026-10-23-web-inventory-shop-transaction-correctness-web-first-p0-3.md) |
| 2026-10-23 | engine |  |  | SvelteKit Migration — Phase 0 + .NET Auth Bridge (Session-57, 2026-10-23) | [areas/sveltekit-migration-phase-0-net-auth-bridge-session-57-2026-10-23.md](areas/sveltekit-migration-phase-0-net-auth-bridge-session-57-2026-10-23.md) |
| 2026-10-23 | engine |  |  | SvelteKit Migration — Phase 3 Public Site + Account (Session-58, 2026-10-23) | [areas/sveltekit-migration-phase-3-public-site-account-session-58-2026-10-23.md](areas/sveltekit-migration-phase-3-public-site-account-session-58-2026-10-23.md) |
| 2026-10-23 | engine |  |  | SvelteKit Migration — Phase 4 Staff Portals (Session-59, 2026-10-23) | [areas/sveltekit-migration-phase-4-staff-portals-session-59-2026-10-23.md](areas/sveltekit-migration-phase-4-staff-portals-session-59-2026-10-23.md) |
| 2026-10-23 | engine |  |  | SvelteKit Migration — Phase 5 Desktop WebView (Session-60, 2026-10-23) | [areas/sveltekit-migration-phase-5-desktop-webview-session-60-2026-10-23.md](areas/sveltekit-migration-phase-5-desktop-webview-session-60-2026-10-23.md) |
| 2026-10-23 | engine |  |  | SvelteKit Migration — Phase 6 Decommission (Session-61, 2026-10-23) | [areas/sveltekit-migration-phase-6-decommission-session-61-2026-10-23.md](areas/sveltekit-migration-phase-6-decommission-session-61-2026-10-23.md) |
| 2026-10-22 | engine |  |  | Character creation equipment dropdowns fixed | [work-items/2026-10-22-character-creation-equipment-dropdowns-fixed.md](work-items/2026-10-22-character-creation-equipment-dropdowns-fixed.md) |
| 2026-10-22 | engine |  |  | OAuth "No authentication handler" fix | [work-items/2026-10-22-oauth-no-authentication-handler-fix.md](work-items/2026-10-22-oauth-no-authentication-handler-fix.md) |
| 2026-10-22 | content |  |  | P1 documentation re-audit + P2 gap fixes | [work-items/2026-10-22-p1-documentation-re-audit-p2-gap-fixes.md](work-items/2026-10-22-p1-documentation-re-audit-p2-gap-fixes.md) |
| 2026-10-22 | map |  |  | Phase 25 complete: RealmForge map graph editor + JSON authoring | [work-items/2026-10-22-phase-25-complete-realmforge-map-graph-editor-json-authoring.md](work-items/2026-10-22-phase-25-complete-realmforge-map-graph-editor-json-authoring.md) |
| 2026-10-22 | content |  |  | Phase 7b complete: TMX layer fully removed | [work-items/2026-10-22-phase-7b-complete-tmx-layer-fully-removed.md](work-items/2026-10-22-phase-7b-complete-tmx-layer-fully-removed.md) |
| 2026-10-21 | engine |  |  | Linux workspace health audit | [work-items/2026-10-21-linux-workspace-health-audit.md](work-items/2026-10-21-linux-workspace-health-audit.md) |
| 2026-10-21 | content |  |  | Text dashboard, cardinal map, and tile removal (Phases 3–7a) | [work-items/2026-10-21-text-dashboard-cardinal-map-and-tile-removal-phases-3-7a.md](work-items/2026-10-21-text-dashboard-cardinal-map-and-tile-removal-phases-3-7a.md) |
| 2026-10-21 | map |  |  | Text dashboard map graph: Phases 0–2 complete | [work-items/2026-10-21-text-dashboard-map-graph-phases-0-2-complete.md](work-items/2026-10-21-text-dashboard-map-graph-phases-0-2-complete.md) |
| 2026-09-11 | web |  |  | Dev-container `obj/`/`bin/` isolation (fixes NETSDK1064 + dotnet-watch watcher crash) | [work-items/2026-09-11-dev-container-obj-bin-isolation-fixes-netsdk1064-dotnet-watch-watcher.md](work-items/2026-09-11-dev-container-obj-bin-isolation-fixes-netsdk1064-dotnet-watch-watcher.md) |
| 2026-09-11 | infra |  |  | NuGet Fallback Folder / Linux Docker `dotnet watch` Fix | [work-items/2026-09-11-nuget-fallback-folder-linux-docker-dotnet-watch-fix.md](work-items/2026-09-11-nuget-fallback-folder-linux-docker-dotnet-watch-fix.md) |
| 2026-06-30 | content |  |  | Reactive UI Pivot + Test Fixes | [work-items/2026-06-30-reactive-ui-pivot-test-fixes.md](work-items/2026-06-30-reactive-ui-pivot-test-fixes.md) |
| 2026-06-27 | content |  |  | Expansion, Polish & Cleanup | [work-items/2026-06-27-expansion-polish-cleanup.md](work-items/2026-06-27-expansion-polish-cleanup.md) |
| 2026-06-26 | infra |  |  | Agent Infrastructure Reset | [work-items/2026-06-26-agent-infrastructure-reset.md](work-items/2026-06-26-agent-infrastructure-reset.md) |
| 2026-04-27 | content |  |  | Stability, Chat, Moderation, Dev Tools | [work-items/2026-04-27-stability-chat-moderation-dev-tools.md](work-items/2026-04-27-stability-chat-moderation-dev-tools.md) |
| 2026-04-16 | auth |  |  | Auth Overhaul & Account Management | [work-items/2026-04-16-auth-overhaul-account-management.md](work-items/2026-04-16-auth-overhaul-account-management.md) |
| 2026-04-16 | engine |  |  | Design System, DB Consolidation, Language Catalog | [work-items/2026-04-16-design-system-db-consolidation-language-catalog.md](work-items/2026-04-16-design-system-db-consolidation-language-catalog.md) |
| 2026-04-15 | engine |  |  | Veldrath.Web, Editorial System, Cryptography | [work-items/2026-04-15-veldrath-web-editorial-system-cryptography.md](work-items/2026-04-15-veldrath-web-editorial-system-cryptography.md) |
| 2026-04-14 | content |  |  | World Lore & Calethic Language | [work-items/2026-04-14-world-lore-calethic-language.md](work-items/2026-04-14-world-lore-calethic-language.md) |
| 2026-04-13 | infra |  |  | Deployment Infrastructure & Tilemap Updates | [work-items/2026-04-13-deployment-infrastructure-tilemap-updates.md](work-items/2026-04-13-deployment-infrastructure-tilemap-updates.md) |
| 2026-04-12 | infra |  |  | Monitoring Infrastructure | [work-items/2026-04-12-monitoring-infrastructure.md](work-items/2026-04-12-monitoring-infrastructure.md) |
| 2026-04-11 | web |  |  | Auth flow hardening (pass 1) | [work-items/2026-04-11-auth-flow-hardening-pass-1.md](work-items/2026-04-11-auth-flow-hardening-pass-1.md) |
| 2026-04-10 | auth |  |  | OAuth Provider-Link Confirmation Flow (session-30, 2026-04-10) — DELETED in W12 | [areas/oauth-provider-link-confirmation-flow-session-30-2026-04-10-deleted-in.md](areas/oauth-provider-link-confirmation-flow-session-30-2026-04-10-deleted-in.md) |
| 2026-04-08 | engine |  |  | Session-29 (2026-04-08) — Region Map Architecture + PlayerSession Migration | [areas/session-29-2026-04-08-region-map-architecture-playersession-migration.md](areas/session-29-2026-04-08-region-map-architecture-playersession-migration.md) |
| 2026-04-02 | content |  |  | Connection dot, settings flyout, full chat system | [work-items/2026-04-02-connection-dot-settings-flyout-full-chat-system.md](work-items/2026-04-02-connection-dot-settings-flyout-full-chat-system.md) |
| 2026-03-30 | engine |  |  | Combat system tests + GameView combat UI | [work-items/2026-03-30-combat-system-tests-gameview-combat-ui.md](work-items/2026-03-30-combat-system-tests-gameview-combat-ui.md) |
| 2026-03-30 | engine |  |  | Phase 0b — Server Schema Migrations (2026-03-30) | [areas/phase-0b-server-schema-migrations-2026-03-30.md](areas/phase-0b-server-schema-migrations-2026-03-30.md) |
| 2026-03-26 | content |  |  | Map redesign + ZoneLocations panel + cross-zone traversal reinit | [work-items/2026-03-26-map-redesign-zonelocations-panel-cross-zone-traversal-reinit.md](work-items/2026-03-26-map-redesign-zonelocations-panel-cross-zone-traversal-reinit.md) |
| 2026-03-21 | content |  |  | World / Region / Zone Schema (session-14 — 2026-03-21; seed facts re-derived 2026-11-20) | [areas/world-region-zone-schema-session-14-2026-03-21-seed-facts-re-derived.md](areas/world-region-zone-schema-session-14-2026-03-21-seed-facts-re-derived.md) |
| 2026-03-19 | engine |  |  | DbContext Separation (established 2026-03-19 session-6) | [areas/dbcontext-separation-established-2026-03-19-session-6.md](areas/dbcontext-separation-established-2026-03-19-session-6.md) |
| — | engine |  |  | AddGold Handler Details (session-9) | [areas/addgold-handler-details-session-9.md](areas/addgold-handler-details-session-9.md) |
| — | engine |  |  | Architecture: Hub → MediatR Bridge Pattern | [areas/architecture-hub-mediatr-bridge-pattern.md](areas/architecture-hub-mediatr-bridge-pattern.md) |
| — | engine |  |  | AwardSkillXp Handler Details (session-5) | [areas/awardskillxp-handler-details-session-5.md](areas/awardskillxp-handler-details-session-5.md) |
| — | engine |  |  | Character Attributes JSON Blob Schema | [areas/character-attributes-json-blob-schema.md](areas/character-attributes-json-blob-schema.md) |
| — | auth |  |  | CharacterSelected Payload Details (session-12) | [areas/characterselected-payload-details-session-12.md](areas/characterselected-payload-details-session-12.md) |
| — | engine |  |  | ClientSettings (NEW session-5) | [areas/clientsettings-new-session-5.md](areas/clientsettings-new-session-5.md) |
| — | engine |  |  | CraftItem Handler Details (session-13) | [areas/craftitem-handler-details-session-13.md](areas/craftitem-handler-details-session-13.md) |
| — | web |  |  | CRITICAL — CSS/Styling Rule | [areas/critical-css-styling-rule.md](areas/critical-css-styling-rule.md) |
| — | engine |  |  | DI Registration — UseExternal Pattern | [areas/di-registration-useexternal-pattern.md](areas/di-registration-useexternal-pattern.md) |
| — | engine |  |  | EnterDungeon Handler Details (session-13) | [areas/enterdungeon-handler-details-session-13.md](areas/enterdungeon-handler-details-session-13.md) |
| — | engine |  |  | Equipment Blob Schema (session-8) | [areas/equipment-blob-schema-session-8.md](areas/equipment-blob-schema-session-8.md) |
| — | engine |  |  | GainExperience Client Wiring Details (session-11) | [areas/gainexperience-client-wiring-details-session-11.md](areas/gainexperience-client-wiring-details-session-11.md) |
| — | content |  |  | IContentService Extension (session-13) | [areas/icontentservice-extension-session-13.md](areas/icontentservice-extension-session-13.md) |
| — | web |  |  | Milestone H — Foundry portal overhaul | [areas/milestone-h-foundry-portal-overhaul.md](areas/milestone-h-foundry-portal-overhaul.md) |
| — | engine |  |  | P3 Stubs Status | [areas/p3-stubs-status.md](areas/p3-stubs-status.md) |
| — | engine |  |  | P4 XML Doc Gaps | [areas/p4-xml-doc-gaps.md](areas/p4-xml-doc-gaps.md) |
| — | engine |  |  | Server Reconnect Polling (session-20) | [areas/server-reconnect-polling-session-20.md](areas/server-reconnect-polling-session-20.md) |
| — | content |  |  | Session Log | [areas/session-log.md](areas/session-log.md) |
| — | engine |  |  | SignalR Parameter Binding — Critical Rule | [areas/signalr-parameter-binding-critical-rule.md](areas/signalr-parameter-binding-critical-rule.md) |
| — | engine |  |  | TakeDamage Handler Details (session-10) | [areas/takedamage-handler-details-session-10.md](areas/takedamage-handler-details-session-10.md) |
| — | content |  |  | Test Counts | [areas/test-counts.md](areas/test-counts.md) |
| — | engine |  |  | UseAbility Handler Details (session-4) | [areas/useability-handler-details-session-4.md](areas/useability-handler-details-session-4.md) |
| — | engine |  |  | VisitShop Handler Details (session-17) | [areas/visitshop-handler-details-session-17.md](areas/visitshop-handler-details-session-17.md) |
| — | engine |  |  | Wired Hub→MediatR Bridges | [areas/wired-hub-mediatr-bridges.md](areas/wired-hub-mediatr-bridges.md) |
<!-- memory-index:end -->
