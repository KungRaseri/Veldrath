---
kind: work-item
date: 2026-11-24
area: engine
card: t_87a63cd7
title: "The run pointer resumes on reconnect, by looking up the run rather than reading the pointer"
---

### Session t_87a63cd7 (2026-11-24) — The run pointer resumes on reconnect

`PlayerSession.ZoneInstanceId` and `ApplyArrivalGroupAsync` both promised a reconnect that lands the character back
inside their instance run. The bodies did not do it, and the card asked me to either build it or correct the
comments. I built it, because the comments were not the source of the promise — the design was:

- [`plans/dungeons.md`](../../../plans/dungeons.md) F2.4: "Disconnect and reconnect | `PlayerSession.ZoneInstanceId`
  restores the run; the group is recomputed from the row"; F2.5 rule 6: "Emptiness respects the disconnect grace, so
  a relog rejoins the run instead of finding a fresh one"; and its F2.6 order of work lists step 3 as shipped with a
  test named "group restored on reconnect" — which did not exist.
- `ZoneInstance.LastOccupiedAt`'s own doc says "a relog rejoins the run instead of finding a new one".
- `IPlayerSessionRepository.SetZoneInstanceAsync`'s doc said the pointer "is also what a reconnect resumes into".

Correcting the two summaries would have written a bug down as the design. Verified at `development@dee6b0ad`
(one commit past the card's `a92ff353`); nothing in the claim had moved.

**The pointer cannot be read back, and that is the whole reason the old comments were wrong.** An unexpected
disconnect reaches `GameHub.OnDisconnectedAsync` → `LeaveCurrentZoneAsync`, which *deletes* the session row and
closes participation; `SelectCharacter` then inserts a fresh row with no pointer, and `EnterZone` replaces it with
another one. So the run is not remembered anywhere — but it is still discoverable, because `ZoneInstances` is keyed
by `(zoneId, OwnerPartyId)` for a party and `(zoneId, CreatedByCharacterId)` for a solo character, and it stays
`Open` while anyone is inside it. That is the same lookup a late joiner already used.

**What I changed**

- `IZoneInstanceService.FindResumableAsync(zoneId, locationSlug, characterId)` (`EfCoreZoneInstanceService`): returns
  the character's open run for a location, and **never opens one**. Policy in the service: the location must exist,
  must be `IsInstanced`, and must belong to the zone asked for (a slug matching in another zone must not hand over
  someone else's run). `FindOpenAsync` already filters `State == Open`, which is what keeps an emptied run out of
  reach. The party lookup was extracted to `FindPartyIdAsync` so `FindOrCreateAsync` and this share it.
- `GameHub.EnterZone`: on a resume into the zone the character was already inside, at a saved location node, it asks
  for that run and — if `JoinAsync` succeeds (the run can be swept between the two calls) — makes it the new
  session's `ZoneInstanceId`, sets `Context.Items["CurrentZoneInstanceId"]`, and joins the run's `ZoneGroup` instead
  of the composed shared group. `JoinAsync` clears the participant's `LeftAt`, so credit and the emptiness check see
  a live member again.
- `IPlayerSessionRepository.GetByZoneInstanceAsync`: the `ZoneEntered` occupant list is the run's sessions when a run
  is resumed. Before this, `EnterZone` answered with every session in the zone, which inside a run would leak the
  other parties standing in the same zone. That leak only became reachable with this change, which is why the fix
  ships with it.
- Comments rewired to the truth: `PlayerSession.ZoneInstanceId`, `ApplyArrivalGroupAsync`'s summary,
  `SetZoneInstanceAsync`'s summary, `EnterZone`'s summary (which also said it sends `ZoneState`; it sends
  `ZoneEntered`).

**Decisions the card asked for**

- *Why `EnterZone` and not `SelectCharacter`.* `SelectCharacter`'s session is a region-map row (`ZoneId = null`) that
  `EnterZone` replaces moments later, so a pointer written there would be discarded. `EnterZone` is the call that
  builds the session the player actually plays in.
- *Party or solo.* Whatever the run is: a party member resumes their party's open run, a solo character their own.
  Neither can resume a run belonging to another party — covered by `FindResumable_IgnoresAnotherPartysRun`.
- *Cleared runs.* Resuming is the same run, so cleared rooms stay cleared (`GetClearedLocationsAsync` still answers
  for it) and the attempt is not reset. A run only resets by ending.
- *Swept runs.* Not resumable: `State` leaves `Open`, so the character stays in the shared zone. Leaving them in a
  group nobody else will join would be worse than the shared zone.
- *The card's consequence.* "Re-entering the instance through `TravelToNode` may put them in a different run than
  their party" is **not** what happens, and it never was: `TravelToNode` → `ResolveScopeAsync` →
  `FindOrCreateAsync` is keyed by the party, so a walk back in rejoins the party's open run and only opens a new one
  once the old one has emptied. The real defect was narrower and worse: the resumed player stood on a room node in
  the shared scope, seeing the shared chests, loot and chat, until they moved.

**Tests** — `ZoneInstanceServiceTests`: party run found, solo run found, another party's run refused, emptied run
refused, shared location refused, other-zone slug refused, missing location refused, and no run opened by a resume
(8). `GameHubZoneTests`: the resumed session carries the pointer, joins the run group (not `zone:crestfall`), has a
live participation row and a run-scoped occupant list that excludes a stranger in the same zone; an emptied run
leaves the pointer null and the shared group (2). `HubHarness.Create` grew an optional location catalog so a test
can make a location instanced.

**Still open, and deliberately not this card** (a follow-up card is filed): `plans/dungeons.md` also says a
character whose run was swept "wakes in the dungeon's lobby with a message". Today they resume at the room node in
the shared scope. And the client asks for ground items, chests, NPCs and nodes on resume but never for the location
roster, so a resumed player — in a run or not — sees the room's enemies only after their next move.

Correction to [`work-items/2026-11-24-t_d3eef21a-the-retired-client-leaves-the-last-seven-notes.md`](2026-11-24-t_d3eef21a-the-retired-client-leaves-the-last-seven-notes.md):
its statement that the pointer does not survive a reconnect was true when it was written and is still true as
written — the pointer really does not survive. What changed is that the run is re-derived on resume, so the
behaviour that note describes (a dropped player returns in the zone's shared scope) is now the swept-run case only.
