---
kind: work-item
date: 2026-11-27
area: auth
card: t_b8eb9773
title: "The self-escalation guard's permission set is declared and fully classified"
---

### Session 2026-11-27 — the guard's policy leaves the handler

Follow-up to PR #334. PR #334 gated the four role/permission mutations behind `admin.roles` and refused a
self-directed grant of `admin.roles`, `admin.users`, `players.ban` and `players.view`, but the four lived in
a private `string[]` inside `AdminEndpoints.cs` and the `AssignRoleAsync` self guard had no test.

What the code looks like now:

- **`Permissions.AuthorityWidening`** — the four, as a `HashSet<string>(StringComparer.OrdinalIgnoreCase)`
  next to `Permissions.All`, with the XML doc saying it is a policy list rather than a derivation and that
  widening it is a product decision. `GrantPermissionAsync` reads it; the handler's private array is gone.
- **`Permissions.NonEscalating`** — the remaining twelve, so the classification is exhaustive rather than a
  hand-picked subset. `All` must equal the union of the two sets, which is what the new test asserts.
- **`AdminUserManagementEndpointTests.Permissions_EveryDeclaredPermission_IsClassifiedForSelfGranting`**
  is the drift alarm. A declared permission in neither set fails with `…but it misses {"world.driftprobe"}`;
  dropping or reclassifying a floor member fails the explicit `Contain` on the four.
- **`AssignRole_RefusesSelfAssignment`** pins the role guard: an `admin.roles` client POSTing
  `{"role":"Curator"}` to its own id gets 403, no role is added and no `assign_role` audit row is written.

Negative controls actually run (each restored afterwards):

- Comment out the `AssignRoleAsync` guard → `AssignRole_RefusesSelfAssignment` fails 403 vs 200.
- Short-circuit the escalation-set condition → `GrantPermission_RefusesSelfEscalation` fails 403 vs 200.
- Append an unclassified 17th permission to `All` → the classification test fails and names it.
- Move `players.ban` from `AuthorityWidening` to `NonEscalating` → the floor assertion fails and names it.

Open policy question, deliberately not decided here: `content.manage`, `world.give` and `admin.debug` are
still self-grantable, because blocking them would be a policy change beyond the reviewed floor. Whether a
role manager should be able to hand themselves content-authoring, item-granting or debug authority belongs
to an architecture decision; when it is made, moving the entry between the two sets is the whole change.

Note for anyone reading the card that spawned this: its line numbers (`AdminEndpoints.cs:229-230`,
`627-633`) are PR-head coordinates. On `development` the guards are at `AdminEndpoints.cs:292` and the
former field's call site at `:345`.
