---
kind: work-item
date: 2026-11-24
area: map
card: t_b298457d
title: "map_unreachable measures a door, not reachability — and now says so"
---

### Session-t_b298457d (2026-11-24) — map_unreachable says what it measures

Follow-up to PR #241 (`t_53311432`). The rule it added is kept; only its wording and the reasoning behind it
changed. Files: `RealmEngine.Data/Validation/MapGraphValidator.cs`, the issue-code tests, and the seeded pin.

- **Decision: option 1, "has a door", kept deliberately.** A live zone or interior is flagged only when no
  non-marker node on another map targets it. PR #241's exclusion stands: a region board's `Zone` marker is not a
  door. It is a way in *from beside the marker* — `TravelToNodeHubCommand` only needs a traversable one-hop edge,
  and the board is not a through-route, so a character arriving in a region can only stand next to a neighbour of
  the entry.
- **Why not option 2 (transitive reachability).** A walk needs a root set the validator does not have.
  `MapGraph` carries no liveness or starter flag and `ValidateWorld` is handed only `liveMapIds`; characters are
  placed on a map by character creation (`CharacterCreationSessionEndpoints.DefaultStartingLocationSlug =
  "crestfall-square"`), by staff teleport and summon (`OnTeleported`/`OnSummoned` → `EnterZone`, no graph edge),
  and by resume. A walk rooted at region entries would report maps players do reach — a new false-warning class.
  The authored shape the rule asks for, a `ZoneExit` on a neighbouring zone map, is what `t_58ccac38` satisfied
  for 12 zones.
- **Wording now matches the measurement.** The message is "Map 'X' is live but no exit on another map leads to it:
  a character can only walk in through its marker on a region board, and only if that board has one beside a node
  they can stand on." The old "so nobody can reach it" is gone. Two tests pin it, including the marker-beside-the-
  entry arrangement the old sentence overstated.
- **The sentence is branched by map kind** (review round 1): the same rule fires for `MapType.Interior`, which
  never has a region-board marker — `IsRegionBoardMarker` requires a region map — so an interior's message says
  what is true instead: "nothing on another map leads into it: an interior is entered from a Location node on the
  zone map that places its owner, so that location needs a node pointing here." One test pins that variant; the
  live-doorless-interior case had no coverage before.
- **The seeded debt list keeps its one row** (`zone:playground`, the dev sandbox) and its doc now says what
  entering one depends on: a marker beside a node a character can stand on.
- **Known limitation, not fixed here.** A zone reachable only from a marker beside a region entry is still
  flagged; that is content debt (no door), not a reachability proof. Revisit option 2 only if the validator is
  ever given a root set — the starting location, and the fact that teleport and summon place a character anywhere.
- **One stale quote left outside this card's scope:** `web/client/tests/a1-drowning-pits.e2e.ts:66` quotes the old
  message verbatim in a spec comment.
