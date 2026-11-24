---
kind: work-item
date: 2026-11-24
area: engine
card: t_57d2edd8
title: "The retired desktop client leaves the handler notes"
---

### Session t_57d2edd8 (2026-11-24) — The retired desktop client leaves the handler notes

Avalonia was retired and `web/` is the only client, but seven `areas/` notes and `engine-codebase.md` still named
its view models as current wiring. Each file was judged on its own content, and every replacement claim was read
out of the source at `development@58d560fa` (the merge of #290) rather than inferred.

- `areas/craftitem-handler-details-session-13.md` was fiction end to end: there is no `DefaultCraftingCost`
  (the grep is empty), no gold read or write in `CraftItemHubCommandHandler`, and no `ItemCraftedPayload` record.
  Rewritten to the real shape — level / skill-rank / material checks, refund-on-failure material consumption,
  the `AwardSkillXpHubCommand` dispatch that shares one blob write with the craft, the anonymous `ItemCrafted`
  and `SkillXpGained` payloads, and the web `craftedSummary` + `openInventory` subscriber.
- `areas/takedamage-handler-details-session-10.md` got a retirement banner: the hub method and the command are
  both gone (`GameHubTests` pins it). The note now describes the real path — the combat handlers clamp
  `CurrentHealth` at zero and announce a whole `CombatTurn` to the caller — and records that `DamageTaken`
  survives only on the GM `/sethealth` sender with its camelCase payload.
- `areas/useability-handler-details-session-4.md`, `areas/visitshop-handler-details-session-17.md` and
  `areas/characterselected-payload-details-session-12.md` kept their handler facts and lost the view-model lines:
  the payloads are anonymous objects, and the client half is `types.ts` plus `session.svelte.ts`. The shop note
  also stopped claiming a `HasMerchant` check on its own — `ShopMerchantResolver` needs the character's stored
  location, the zone flag and a shopkeeper NPC before it serves anyone.
- `areas/enterdungeon-handler-details-session-13.md` gained the entrance-location resolution and the real
  `DungeonEntered` payload, and records that no web consumer exists yet (the parity suite defers it).
- `areas/gainexperience-client-wiring-details-session-11.md` was rewritten around the retired hub method: the
  command is dispatched by `GameHub.TurnInQuest` (announcing `ExperienceGained` to the caller) and by
  `POST /api/dev/experience`, with the web subscriber in `session.svelte.ts`.
- `engine-codebase.md` lost the P3 item's false `Server:BaseUrl` claim (no such key exists), the Avalonia
  test-affinity and `AvailableClasses` sections, and its two remaining "Avalonia follows web" claims; the
  `ActorClassDto` entry now states the current six positional plus optional fields. A stray `======= REPLACE`
  merge marker that sat in the file was removed, and the `ShopCatalog` entry no longer claims a second
  Avalonia-only `Items` payload that the hub does not send.

**Verified:** `node scripts/memory/memory-index.mjs --check` exits 0 (the hub table is generated, not
hand-edited), `node scripts/check-markdown-links.mjs` exits 0, and
`grep -rn "CharacterSelectViewModel\|GameViewModel\|Avalonia" .github/agent-memory/engine-codebase.md` now returns
only lines that say the client is deleted.

- **Still stale, filed as a follow-up rather than folded in:** `areas/p3-stubs-status.md`,
  `areas/clientsettings-new-session-5.md`, `areas/equipment-blob-schema-session-8.md`,
  `areas/dbcontext-separation-established-2026-03-19-session-6.md`,
  `areas/session-29-2026-04-08-region-map-architecture-playersession-migration.md`,
  `auth-and-character-creation-plan.md` and `combat-loop-plan.md` still describe the deleted client's view models
  and subscriptions as current. They are the same class of defect and outside this card's scope.
