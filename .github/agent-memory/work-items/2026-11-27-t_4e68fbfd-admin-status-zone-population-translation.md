---
kind: work-item
date: 2026-11-27
area: engine
card: t_4e68fbfd
title: "The admin status snapshot answers 200 again: EF Core cannot order a DTO constructor projection"
---

### Session 2026-11-27 — `GET /api/admin/status` was 500 on every database

The zone-population aggregate in `AdminEndpoints.GetServerStatusAsync` projected into a record and *then*
sorted:

```csharp
.GroupBy(s => new { s.ZoneId, s.Zone!.Name })
.Select(g => new ZonePopulationDto(g.Key.ZoneId!, g.Key.Name, g.Count()))
.OrderByDescending(z => z.PlayerCount)          // ← over the DTO, not over SQL
```

EF Core cannot translate an ordering over a member of a constructor projection: it refuses the whole
query with `The LINQ expression … could not be translated`, so the endpoint answered 500 on every call
against Postgres and the `/admin` counters never appeared. The test file claimed the opposite — that
Npgsql translates it and only SQLite refuses — which is why the shape survived: the tests only ever
asserted the 401/403 boundary and never made a request that reached the handler.

**The failure is provider-independent.** Reproduced with `ToQueryString()` on both `UseNpgsql` and
`UseSqlite` before touching anything: `Include`, a missing `Include`, and an anonymous intermediate
projection all fail identically while the last step is a `ZonePopulationDto` constructor. Two shapes
translate on both providers:

- aggregate and order over an anonymous projection, then construct the DTO last (used);
- drop the ordering from SQL and sort the materialised list in memory.

The fix keeps the aggregate and the ordering in SQL:

```csharp
internal static IQueryable<ZonePopulationDto> ZonePopulationQuery(ApplicationDbContext db) =>
    db.PlayerSessions.AsNoTracking()
        .Where(s => s.ZoneId != null)
        .GroupBy(s => new { s.ZoneId, s.Zone!.Name })
        .Select(g => new { ZoneId = g.Key.ZoneId!, ZoneName = g.Key.Name, PlayerCount = g.Count() })
        .OrderByDescending(row => row.PlayerCount)
        .Select(row => new ZonePopulationDto(row.ZoneId, row.ZoneName, row.PlayerCount));
```

Npgsql emits `SELECT p."ZoneId", z."Name", count(*)::int … LEFT JOIN "Zones" … GROUP BY p."ZoneId",
z."Name" ORDER BY count(*)::int DESC`. The `Include(s => s.Zone)` was dropped with it: the projection
through the navigation already produces that LEFT JOIN, and an `Include` beside a `GroupBy` reads as if
it mattered when it is ignored.

**Two guards, because the old ones could not see this.** `Status_Admin_ReportsOccupiedZonesOrderedByPopulation`
drives the endpoint as an Admin over the SQLite test host and asserts the zone rows, their counts, their
order and that a region-map session creates no row — it fails with 500 against the old query.
`AdminStatusQueryTests` compiles the exact production query with the Npgsql provider via `ToQueryString()`
(no live database) and asserts `GROUP BY`, `count(`, `LEFT JOIN`, `ORDER BY` survive: a future rewrite
that only works in memory reds the unit run instead of the dashboard.

**Verified end to end on the real stack.** `./scripts/e2e-stack.sh start --fresh`, an admin JWT minted
with the default dev key, two sessions seeded in `crestfall` and one in `aldenmere`, one on the region
map: the old query returns `500` with the reported `InvalidOperationException`, the fixed one returns
`200` with `zonePopulations: [{crestfall,2},{aldenmere,1}]`, ordered by population and excluding the
map session. The same A/B was run with `scripts/e2e-stack.sh api-stop && api-start` between the two
builds, so the 500 in the log is this query and not a stale build.

A note on the test host: the endpoint *is* exercisable on SQLite once the query translates — the earlier
comment ("covering the snapshot itself needs a Postgres-backed host") was describing this bug, not an
infrastructure limit.
