---
kind: work-item
date: 2026-10-25
area: map
title: "Login resumes the last known node"
---

### Session-72 (2026-10-25) — Login resumes the last known node

- **Bug:** `SelectCharacter` stamped `CurrentNodeId = regionGraph?.EntryNodeId ?? character.CurrentNodeId`,
  so the arrival node always won over the persisted position. `EnterZone` had the same problem twice
  over: it set the session/payload node to `zoneGraph?.EntryNodeId` **and** persisted that value (with
  `locationSlug: null`), wiping the saved node + location on every login — because the client re-enters
  the current zone right after `CharacterSelected`.
- **Fix:** `GameHub.ResolveResumeNodeId(nodeIds, savedNodeId, fallbackNodeId)` — the saved node wins when
  it exists on the graph being sent (or when there is no graph to check against), else the entry node.
  `SelectCharacter` uses it for the session row and the `CharacterSelected` payload; `EnterZone` uses it
  only when the character's `CurrentZoneId` matches the zone being entered, and then persists the
  resumed node **and** `CurrentZoneLocationSlug` instead of the entry node/null.
- `GetMapGraphHubCommand` already prefers `character.CurrentNodeId` when `CurrentMapId` matches, so the
  map payload, `CharacterSelected` and `ZoneEntered` now all agree.
- Brand-new characters keep the entry node (creation writes `CurrentNodeId` as null → fallback), which
  is the seam a tutorial/onboarding flow will hook into: first login should override the resume with
  the tutorial start node.
- Tests: `SelectCharacter_Should_Resume_At_Last_Known_Node`,
  `EnterZone_Should_Keep_The_Saved_Node_When_Reentering_The_Same_Zone`,
  `EnterZone_Should_Use_The_Arrival_Node_For_A_New_Zone` (696 server tests green).
