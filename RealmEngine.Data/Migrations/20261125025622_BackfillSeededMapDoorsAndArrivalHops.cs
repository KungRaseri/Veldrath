using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealmEngine.Data.Migrations
{
    /// <summary>
    /// Patches the twelve doorless seeded zone maps (#270) and the two region-board Arrival hops (#282) into a
    /// content database that was populated by an earlier release, where <c>MapGraphSeeder</c> has already run and
    /// therefore never writes again.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>MapGraphSeeder</c> is bootstrap-only: it returns as soon as <c>Maps</c> holds a row
    /// (<c>RealmEngine.Data/Seeders/MapGraphSeeder.cs:21-22</c>), and the server migrates the content database
    /// before it seeds (<c>Veldrath.Server/Program.cs:406-422</c>). A database seeded before #270/#282 therefore
    /// keeps its old topology forever. This migration carries the same rows the seeder now writes, frozen as
    /// literals — it never calls the seeder — and applies them to an already-seeded database.
    /// </para>
    /// <para>
    /// It is add-only and guarded. On a populated database it patches only the named seed-owned source maps
    /// (active, no creator, expected id/type/owner); absent, inactive and user-owned maps are reported and left
    /// alone, never recreated or reactivated. Before writing it verifies every required endpoint, every door's
    /// target map/node and each region marker's <c>TargetMapId</c>. An existing node id with the same topology
    /// (map/type/ref/exit/target/visibility/unlock semantics) is left untouched, including its text and layout.
    /// An existing edge that already fills the natural slot — <c>(MapId, FromNodeId, Direction)</c>, the unique
    /// index from <c>ContentModelConfiguration</c> — satisfies the patch even with another uuid; its uuid and
    /// label are preserved. An incompatible node id, an incompatible uuid, or an occupied slot holding a different
    /// edge aborts the whole migration with the exact identity: there is no <c>ON CONFLICT DO NOTHING</c> here, so
    /// a conflict can never leave a half-doored map.
    /// </para>
    /// <para>
    /// Everything runs in the transaction Entity Framework opens for the migration, as one PL/pgSQL block, so a
    /// <c>RAISE EXCEPTION</c> rolls the whole patch back and no migration-history row is written. Each changed map
    /// has <c>Version</c>/<c>UpdatedAt</c> bumped exactly once; activation, authorship and review fields are not
    /// touched. Re-running the block (or restarting the server) is a no-op once the rows are present.
    /// </para>
    /// <para>
    /// One declared re-home is part of the patch. The seeder derives the zone walk after the authored edges and
    /// treats a walk's direction as a hint, so a database seeded before #270 has Aldenmere's marketplace walking
    /// East to the Grey Cup — the slot the new moor-road door takes — while a fresh seed walks it North. The
    /// migration moves that one seed-time walk from East to North when it finds it there, preserving its uuid and
    /// label, because otherwise the door cannot be added and the old database could never converge on the seeded
    /// topology. The move is verified as strictly as the inserts: the source slot must hold exactly that walk, the
    /// destination must be free (or already hold the moved walk on a replay), and anything else aborts.
    /// </para>
    /// <para>
    /// <c>Down</c> deliberately deletes nothing: the rows this migration adds may since have been edited in
    /// Foundry, and there is no way to tell an inserted door from an authored one. Rolling data back means
    /// restoring the pre-migration backup, or shipping a reviewed compensating migration that removes exactly the
    /// twelve ids and twenty-six slots named below. Do not apply this to production without those steps: preflight
    /// on a restored copy, and quiesce other writers and instances while it runs.
    /// </para>
    /// </remarks>
    public partial class BackfillSeededMapDoorsAndArrivalHops : Migration
    {
        /// <summary>
        /// The guarded patch, in one PL/pgSQL block. Kept in one literal so the verification script can extract and
        /// replay the exact body it documents; see <c>scripts/verify-map-graph-upgrade.sh</c>.
        /// </summary>
        private const string PatchSql = """
            DO $backfill$
            DECLARE
                v_eligible text;
                v_excluded text;
                v_conflicts text;
                v_expected_nodes integer;
                v_expected_edges integer;
                v_inserted_nodes integer := 0;
                v_inserted_edges integer := 0;
                v_bumped_maps integer := 0;
                v_moved_edges integer := 0;
                v_missing integer;
            BEGIN
                -- A fresh database is the seeder's job: it returns here and seeds the current graph, doors included.
                IF NOT EXISTS (SELECT 1 FROM "Maps") THEN
                    RAISE NOTICE 'BackfillSeededMapDoorsAndArrivalHops: "Maps" is empty, so this migration returns unchanged; the bootstrap seeder creates the graph with these rows on a fresh database.';
                    RETURN;
                END IF;

                -- Temporary spec tables. They are created inside the migration transaction and dropped with it; the
                -- drops make the block replayable inside one transaction (the verification script does exactly that).
                DROP TABLE IF EXISTS pg_temp._backfill_maps;
                DROP TABLE IF EXISTS pg_temp._backfill_nodes;
                DROP TABLE IF EXISTS pg_temp._backfill_edges;
                DROP TABLE IF EXISTS pg_temp._backfill_markers;
                DROP TABLE IF EXISTS pg_temp._backfill_eligible;
                DROP TABLE IF EXISTS pg_temp._backfill_changed;

                CREATE TEMP TABLE _backfill_maps (
                    "MapId" text PRIMARY KEY,
                    "MapType" text NOT NULL,
                    "OwnerId" text NOT NULL
                ) ON COMMIT DROP;

                INSERT INTO _backfill_maps ("MapId", "MapType", "OwnerId") VALUES
                    ('zone:aldenmere', 'Zone', 'aldenmere'),
                    ('zone:ashfields', 'Zone', 'ashfields'),
                    ('zone:pale-moor', 'Zone', 'pale-moor'),
                    ('zone:saltcliff-heights', 'Zone', 'saltcliff-heights'),
                    ('zone:skarhold', 'Zone', 'skarhold'),
                    ('zone:smoldering-reach', 'Zone', 'smoldering-reach'),
                    ('zone:tolvaren', 'Zone', 'tolvaren'),
                    ('region:cinderplain', 'Region', 'cinderplain'),
                    ('region:saltcliff', 'Region', 'saltcliff');

                CREATE TEMP TABLE _backfill_nodes (
                    "Id" text PRIMARY KEY,
                    "MapId" text NOT NULL,
                    "NodeType" text NOT NULL,
                    "RefId" text,
                    "DisplayName" text NOT NULL,
                    "Description" text NOT NULL,
                    "IconKey" text,
                    "LayoutX" integer,
                    "LayoutY" integer,
                    "IsEntryPoint" boolean NOT NULL,
                    "IsExitPoint" boolean NOT NULL,
                    "TargetMapId" text,
                    "TargetNodeId" text,
                    "IsHidden" boolean NOT NULL,
                    "UnlockType" text,
                    "UnlockKey" text,
                    "DiscoverThreshold" integer,
                    "SortOrder" integer NOT NULL
                ) ON COMMIT DROP;

                INSERT INTO _backfill_nodes (
                    "Id", "MapId", "NodeType", "RefId", "DisplayName", "Description", "IconKey", "LayoutX", "LayoutY",
                    "IsEntryPoint", "IsExitPoint", "TargetMapId", "TargetNodeId", "IsHidden", "UnlockType", "UnlockKey",
                    "DiscoverThreshold", "SortOrder") VALUES
                    ('zone:aldenmere:exit:zone:barrow-deeps', 'zone:aldenmere', 'ZoneExit', 'barrow-deeps', 'Descent into the Barrow Deeps', 'A sprawling underground burial network carved through the moorland, where ancestor-beasts guard relics older than the moor above them.', 'exit_to_app', 10, 15, false, true, 'zone:barrow-deeps', 'zone:barrow-deeps:entry', false, NULL, NULL, NULL, 50),
                    ('zone:aldenmere:exit:zone:pale-moor', 'zone:aldenmere', 'ZoneExit', 'pale-moor', 'Road to the Pale Moor', 'Endless fog-blanketed moors where waymarks shift overnight and travellers learn quickly to trust local guides.', 'exit_to_app', 48, 10, false, true, 'zone:pale-moor', 'zone:pale-moor:entry', false, NULL, NULL, NULL, 60),
                    ('zone:ashfields:exit:zone:skarhold', 'zone:ashfields', 'ZoneExit', 'skarhold', 'Road to Skarhold', 'A forge-city built inside a dormant caldera, where the best smiths in Veldrath ply their trade under sulphur skies.', 'exit_to_app', 20, 24, false, true, 'zone:skarhold', 'zone:skarhold:entry', false, NULL, NULL, NULL, 50),
                    ('zone:ashfields:exit:zone:smoldering-reach', 'zone:ashfields', 'ZoneExit', 'smoldering-reach', 'Track to the Smoldering Reach', 'A cracked lava field where vents of superheated gas erupt without warning and heat-adapted predators hunt at dusk.', 'exit_to_app', 45, 47, false, true, 'zone:smoldering-reach', 'zone:smoldering-reach:entry', false, NULL, NULL, NULL, 60),
                    ('zone:pale-moor:exit:zone:aldenmere', 'zone:pale-moor', 'ZoneExit', 'aldenmere', 'Road to Aldenmere', 'A grey-stoned waytown built atop old foundations, known for its mead, its mercenaries, and its many secrets.', 'exit_to_app', 20, 24, false, true, 'zone:aldenmere', 'zone:aldenmere:entry', false, NULL, NULL, NULL, 50),
                    ('zone:pale-moor:exit:zone:soddenfen', 'zone:pale-moor', 'ZoneExit', 'soddenfen', 'Track to Soddenfen', 'A waterlogged fenland buzzing with plague insects and half-submerged ruins, avoided by all with good sense.', 'exit_to_app', 45, 47, false, true, 'zone:soddenfen', 'zone:soddenfen:entry', false, NULL, NULL, NULL, 60),
                    ('zone:saltcliff-heights:exit:zone:tolvaren', 'zone:saltcliff-heights', 'ZoneExit', 'tolvaren', 'Road to Tolvaren', 'A salt-caked port city clinging to the cliff face, its harbour full of storm-worn ships and bold-faced traders.', 'exit_to_app', 24, 32, false, true, 'zone:tolvaren', 'zone:tolvaren:entry', false, NULL, NULL, NULL, 50),
                    ('zone:skarhold:exit:zone:ashfields', 'zone:skarhold', 'ZoneExit', 'ashfields', 'Road to the Ashfields', 'Grey ash plains stretching to the horizon, dotted with fused obsidian trees and the scorched skulls of fallen armies.', 'exit_to_app', 24, 8, false, true, 'zone:ashfields', 'zone:ashfields:entry', false, NULL, NULL, NULL, 50),
                    ('zone:smoldering-reach:exit:zone:kaldrek-maw', 'zone:smoldering-reach', 'ZoneExit', 'kaldrek-maw', 'Descent into the Maw', 'A vast volcanic vent complex, lair of a fire-bound ancient and the final test of the worthy.', 'exit_to_app', 38, 10, false, true, 'zone:kaldrek-maw', 'zone:kaldrek-maw:entry', false, NULL, NULL, NULL, 50),
                    ('zone:tolvaren:exit:zone:saltcliff-heights', 'zone:tolvaren', 'ZoneExit', 'saltcliff-heights', 'Climb to the Saltcliff Heights', 'Wind-blasted clifftops above a sea that never settles, dotted with lighthouse ruins and contested by rival gull-rider clans.', 'exit_to_app', 24, 8, false, true, 'zone:saltcliff-heights', 'zone:saltcliff-heights:entry', false, NULL, NULL, NULL, 50),
                    ('zone:tolvaren:exit:zone:sunken-name', 'zone:tolvaren', 'ZoneExit', 'sunken-name', 'Descent into the Sunken Name', 'The drowned heart of a city no surviving record names, now a flooded ruin accessible only to the brave and the waterproofed.', 'exit_to_app', 10, 19, false, true, 'zone:sunken-name', 'zone:sunken-name:entry', false, NULL, NULL, NULL, 60),
                    ('zone:tolvaren:exit:zone:tidewrack-flats', 'zone:tolvaren', 'ZoneExit', 'tidewrack-flats', 'Track to the Tidewrack Flats', 'Tide-scoured mudflats littered with shipwreck timber and kelp-draped bones, hunted by scavengers and wading predators.', 'exit_to_app', 35, 22, false, true, 'zone:tidewrack-flats', 'zone:tidewrack-flats:entry', false, NULL, NULL, NULL, 70);

                CREATE TEMP TABLE _backfill_edges (
                    "Id" uuid PRIMARY KEY,
                    "MapId" text NOT NULL,
                    "FromNodeId" text NOT NULL,
                    "ToNodeId" text NOT NULL,
                    "Direction" text NOT NULL,
                    "EdgeType" text NOT NULL,
                    "Label" text,
                    "IsTraversable" boolean NOT NULL,
                    "IsHidden" boolean NOT NULL
                ) ON COMMIT DROP;

                INSERT INTO _backfill_edges (
                    "Id", "MapId", "FromNodeId", "ToNodeId", "Direction", "EdgeType", "Label", "IsTraversable", "IsHidden") VALUES
                    ('27b8b645-fbf3-46dc-b7f0-7bbde33cd1b3', 'zone:aldenmere', 'zone:aldenmere:location:ironhollow-keep', 'zone:aldenmere:exit:zone:barrow-deeps', 'South', 'ExitToZone', 'barrow-stair', true, false),
                    ('2d43bf13-803c-43e2-8c95-82e967ee47a8', 'zone:aldenmere', 'zone:aldenmere:exit:zone:barrow-deeps', 'zone:aldenmere:location:ironhollow-keep', 'North', 'ExitToZone', 'barrow-stair', true, false),
                    ('ccfc4ff2-6fe6-42f1-bb30-9024de679f51', 'zone:aldenmere', 'zone:aldenmere:location:aldenmere-marketplace', 'zone:aldenmere:exit:zone:pale-moor', 'East', 'ExitToZone', 'moor-road', true, false),
                    ('fa16fa6d-c1de-4da4-a137-1e270e1ba8d4', 'zone:aldenmere', 'zone:aldenmere:exit:zone:pale-moor', 'zone:aldenmere:location:aldenmere-marketplace', 'West', 'ExitToZone', 'moor-road', true, false),
                    ('5a7d1e2e-6a01-4a21-a9ee-2081b888bd17', 'zone:ashfields', 'zone:ashfields:entry', 'zone:ashfields:exit:zone:skarhold', 'West', 'ExitToZone', 'ash-road', true, false),
                    ('c8c9455d-8565-4075-a2e1-2c18c783b37a', 'zone:ashfields', 'zone:ashfields:exit:zone:skarhold', 'zone:ashfields:entry', 'East', 'ExitToZone', 'ash-road', true, false),
                    ('c549a9ed-de23-4b88-bfc3-c201f213b106', 'zone:ashfields', 'zone:ashfields:location:scorched-battlefield', 'zone:ashfields:exit:zone:smoldering-reach', 'South', 'ExitToZone', 'lava-track', true, false),
                    ('3d97744b-383a-45a1-bb9b-dfc6629df4aa', 'zone:ashfields', 'zone:ashfields:exit:zone:smoldering-reach', 'zone:ashfields:location:scorched-battlefield', 'North', 'ExitToZone', 'lava-track', true, false),
                    ('0044c3f3-4efa-4af6-b173-4f0ed103c0bd', 'zone:pale-moor', 'zone:pale-moor:entry', 'zone:pale-moor:exit:zone:aldenmere', 'West', 'ExitToZone', 'waytown-road', true, false),
                    ('6d93b407-2edd-4d16-9976-02e3938690bf', 'zone:pale-moor', 'zone:pale-moor:exit:zone:aldenmere', 'zone:pale-moor:entry', 'East', 'ExitToZone', 'waytown-road', true, false),
                    ('e9151578-f5b8-44cc-99c1-15d6e7e011ee', 'zone:pale-moor', 'zone:pale-moor:location:moorstone-cairns', 'zone:pale-moor:exit:zone:soddenfen', 'South', 'ExitToZone', 'fen-track', true, false),
                    ('7f07a552-17d9-4971-aed2-50593c8036d9', 'zone:pale-moor', 'zone:pale-moor:exit:zone:soddenfen', 'zone:pale-moor:location:moorstone-cairns', 'North', 'ExitToZone', 'fen-track', true, false),
                    ('8d69e45a-18f8-42ca-8f73-8631f94842a8', 'zone:saltcliff-heights', 'zone:saltcliff-heights:entry', 'zone:saltcliff-heights:exit:zone:tolvaren', 'South', 'ExitToZone', 'cliff-road', true, false),
                    ('a7cb64b6-1572-47a7-a671-373fa2098fb6', 'zone:saltcliff-heights', 'zone:saltcliff-heights:exit:zone:tolvaren', 'zone:saltcliff-heights:entry', 'North', 'ExitToZone', 'cliff-road', true, false),
                    ('6a4f967b-60f6-4830-9b31-f7f0a06665a6', 'zone:skarhold', 'zone:skarhold:entry', 'zone:skarhold:exit:zone:ashfields', 'North', 'ExitToZone', 'caldera-gate', true, false),
                    ('8173294a-e2b7-4557-b193-2cc472845bf4', 'zone:skarhold', 'zone:skarhold:exit:zone:ashfields', 'zone:skarhold:entry', 'South', 'ExitToZone', 'caldera-gate', true, false),
                    ('c8e866e2-3144-49d0-9eb7-0567d06be0d8', 'zone:smoldering-reach', 'zone:smoldering-reach:location:vent-fields', 'zone:smoldering-reach:exit:zone:kaldrek-maw', 'East', 'ExitToZone', 'vent-descent', true, false),
                    ('b62af2e4-fddd-4aa0-ae35-e887233b810a', 'zone:smoldering-reach', 'zone:smoldering-reach:exit:zone:kaldrek-maw', 'zone:smoldering-reach:location:vent-fields', 'West', 'ExitToZone', 'vent-descent', true, false),
                    ('cf9516dd-7c7a-4da5-b430-c3d1ee67de99', 'zone:tolvaren', 'zone:tolvaren:entry', 'zone:tolvaren:exit:zone:saltcliff-heights', 'North', 'ExitToZone', 'cliff-stair', true, false),
                    ('3e1a9d26-74ce-4546-a8d6-fdd5ae8cae94', 'zone:tolvaren', 'zone:tolvaren:exit:zone:saltcliff-heights', 'zone:tolvaren:entry', 'South', 'ExitToZone', 'cliff-stair', true, false),
                    ('27d355b2-5092-45a5-9452-4efa6ee45d31', 'zone:tolvaren', 'zone:tolvaren:location:tolvaren-harbour', 'zone:tolvaren:exit:zone:sunken-name', 'South', 'ExitToZone', 'harbour-stair', true, false),
                    ('437d5d3a-2d52-42e1-a8eb-1d765456183b', 'zone:tolvaren', 'zone:tolvaren:exit:zone:sunken-name', 'zone:tolvaren:location:tolvaren-harbour', 'North', 'ExitToZone', 'harbour-stair', true, false),
                    ('c5f24ce4-3ad2-4e1d-89aa-b522d5505f58', 'zone:tolvaren', 'zone:tolvaren:location:cliff-road-market', 'zone:tolvaren:exit:zone:tidewrack-flats', 'South', 'ExitToZone', 'shore-track', true, false),
                    ('77a606ba-c181-405a-9db8-a139b4eab06e', 'zone:tolvaren', 'zone:tolvaren:exit:zone:tidewrack-flats', 'zone:tolvaren:location:cliff-road-market', 'North', 'ExitToZone', 'shore-track', true, false),
                    ('9fe48038-d744-4052-959e-91fb32adc5c9', 'region:cinderplain', 'region:cinderplain:entry', 'region:cinderplain:zone:skarhold', 'East', 'Walk', NULL, true, false),
                    ('66e2b0f4-f9c7-4697-9430-046667d651cb', 'region:saltcliff', 'region:saltcliff:entry', 'region:saltcliff:zone:saltcliff-heights', 'South', 'Walk', NULL, true, false);

                -- Declared re-homes: seed-time walks whose direction the new authored door takes over. The seeder
                -- derives those walks after the authored edges, so on a database seeded before #270 the market's walk
                -- to the Grey Cup sits East and the new moor-road door needs that slot. A fresh seed walks it North
                -- instead. Moving it keeps the site's connectivity identical to a fresh seed and preserves the row
                -- (same uuid, label, type); nothing is deleted, and a database where it has already moved is accepted.
                CREATE TEMP TABLE _backfill_rehomes (
                    "MapId" text NOT NULL,
                    "FromNodeId" text NOT NULL,
                    "ToNodeId" text NOT NULL,
                    "FromDirection" text NOT NULL,
                    "ToDirection" text NOT NULL,
                    "EdgeType" text NOT NULL,
                    PRIMARY KEY ("MapId", "FromNodeId", "FromDirection")
                ) ON COMMIT DROP;

                INSERT INTO _backfill_rehomes ("MapId", "FromNodeId", "ToNodeId", "FromDirection", "ToDirection", "EdgeType") VALUES
                    ('zone:aldenmere', 'zone:aldenmere:location:aldenmere-marketplace', 'zone:aldenmere:location:grey-cup', 'East', 'North', 'Walk');

                CREATE TEMP TABLE _backfill_markers (
                    "MapId" text NOT NULL,
                    "NodeId" text NOT NULL,
                    "ExpectedTargetMapId" text NOT NULL,
                    PRIMARY KEY ("MapId", "NodeId")
                ) ON COMMIT DROP;

                INSERT INTO _backfill_markers ("MapId", "NodeId", "ExpectedTargetMapId") VALUES
                    ('region:cinderplain', 'region:cinderplain:zone:skarhold', 'zone:skarhold'),
                    ('region:saltcliff', 'region:saltcliff:zone:saltcliff-heights', 'zone:saltcliff-heights');

                -- Internal consistency: a typo in the literals above must fail loudly rather than quietly drop a door.
                SELECT string_agg(s."Id", ', ' ORDER BY s."Id") INTO v_conflicts
                FROM _backfill_nodes s
                WHERE NOT EXISTS (SELECT 1 FROM _backfill_maps m WHERE m."MapId" = s."MapId");
                IF v_conflicts IS NOT NULL THEN
                    RAISE EXCEPTION 'BackfillSeededMapDoorsAndArrivalHops: internal spec error, node(s) % name a map the migration does not declare.', v_conflicts;
                END IF;

                SELECT string_agg(s."Id"::text, ', ' ORDER BY s."Id"::text) INTO v_conflicts
                FROM _backfill_edges s
                WHERE NOT EXISTS (SELECT 1 FROM _backfill_maps m WHERE m."MapId" = s."MapId");
                IF v_conflicts IS NOT NULL THEN
                    RAISE EXCEPTION 'BackfillSeededMapDoorsAndArrivalHops: internal spec error, edge(s) % name a map the migration does not declare.', v_conflicts;
                END IF;

                SELECT string_agg(format('%s/%s %s->%s', r."MapId", r."FromNodeId", r."FromDirection", r."ToDirection"), ', ' ORDER BY r."MapId", r."FromNodeId") INTO v_conflicts
                FROM _backfill_rehomes r
                WHERE r."FromDirection" = r."ToDirection"
                   OR NOT EXISTS (SELECT 1 FROM _backfill_maps m WHERE m."MapId" = r."MapId")
                   OR EXISTS (SELECT 1 FROM _backfill_edges s
                              WHERE s."MapId" = r."MapId" AND s."FromNodeId" = r."FromNodeId" AND s."Direction" = r."ToDirection");
                IF v_conflicts IS NOT NULL THEN
                    RAISE EXCEPTION 'BackfillSeededMapDoorsAndArrivalHops: internal spec error, re-home(s) % are malformed or claim a direction an inserted edge also claims.', v_conflicts;
                END IF;

                -- A named map that exists, is live and seed-owned but carries another type or owner is not something
                -- this migration may silently skip: it is a different map wearing the same id.
                SELECT string_agg(format('%s (MapType=%s, OwnerId=%s)', m."MapId", target."MapType", target."OwnerId"), ', ' ORDER BY m."MapId") INTO v_conflicts
                FROM _backfill_maps m
                JOIN "Maps" target ON target."Id" = m."MapId"
                WHERE target."IsActive"
                  AND target."CreatedByAccountId" IS NULL
                  AND (target."MapType" <> m."MapType" OR target."OwnerId" <> m."OwnerId");
                IF v_conflicts IS NOT NULL THEN
                    RAISE EXCEPTION 'BackfillSeededMapDoorsAndArrivalHops: named map(s) exist with a different identity: %. Refusing to patch a map that is not the seeded one.', v_conflicts;
                END IF;

                CREATE TEMP TABLE _backfill_eligible ON COMMIT DROP AS
                SELECT m."MapId", m."MapType", m."OwnerId"
                FROM _backfill_maps m
                JOIN "Maps" target ON target."Id" = m."MapId"
                WHERE target."IsActive"
                  AND target."CreatedByAccountId" IS NULL
                  AND target."MapType" = m."MapType"
                  AND target."OwnerId" = m."OwnerId";

                SELECT string_agg(e."MapId", ', ' ORDER BY e."MapId") INTO v_eligible FROM _backfill_eligible e;

                -- Preflight report: what is being skipped, and why. Nothing has been written yet beyond temp tables.
                SELECT string_agg(format('%s [%s]', skipped."MapId", skipped."Reason"), ', ' ORDER BY skipped."MapId") INTO v_excluded
                FROM (
                    SELECT m."MapId",
                           CASE
                               WHEN target."Id" IS NULL THEN 'absent'
                               WHEN NOT target."IsActive" THEN 'inactive'
                               ELSE 'user-owned'
                           END AS "Reason"
                    FROM _backfill_maps m
                    LEFT JOIN "Maps" target ON target."Id" = m."MapId"
                    WHERE target."Id" IS NULL
                       OR NOT target."IsActive"
                       OR target."CreatedByAccountId" IS NOT NULL
                ) skipped;

                SELECT count(*) INTO v_expected_nodes
                FROM _backfill_nodes s JOIN _backfill_eligible e ON e."MapId" = s."MapId";
                SELECT count(*) INTO v_expected_edges
                FROM _backfill_edges s JOIN _backfill_eligible e ON e."MapId" = s."MapId";

                RAISE NOTICE 'BackfillSeededMapDoorsAndArrivalHops preflight: eligible maps [%]; excluded absent/inactive/user-owned maps [%].',
                    COALESCE(v_eligible, 'none'), COALESCE(v_excluded, 'none');

                -- A declared re-home is checked and applied before any slot is judged, because it is what frees the
                -- doorless destination slot. Its two endpoints must still be the seeded nodes.
                SELECT string_agg(format('%s/%s -> %s', r."MapId", r."FromNodeId", r."ToNodeId"), ', ' ORDER BY r."MapId", r."FromNodeId") INTO v_conflicts
                FROM _backfill_rehomes r
                JOIN _backfill_eligible e ON e."MapId" = r."MapId"
                WHERE NOT EXISTS (SELECT 1 FROM "MapNodes" n WHERE n."Id" = r."FromNodeId" AND n."MapId" = r."MapId")
                   OR NOT EXISTS (SELECT 1 FROM "MapNodes" n WHERE n."Id" = r."ToNodeId" AND n."MapId" = r."MapId");
                IF v_conflicts IS NOT NULL THEN
                    RAISE EXCEPTION 'BackfillSeededMapDoorsAndArrivalHops: a re-homed walk lost an endpoint: %. Refusing to move an edge whose nodes are gone.', v_conflicts;
                END IF;

                CREATE TEMP TABLE _backfill_rehome_state ON COMMIT DROP AS
                SELECT r."MapId", r."FromNodeId", r."ToNodeId", r."FromDirection", r."ToDirection", r."EdgeType",
                       EXISTS (SELECT 1 FROM "MapEdges" x
                               WHERE x."MapId" = r."MapId" AND x."FromNodeId" = r."FromNodeId"
                                 AND x."Direction" = r."FromDirection" AND x."ToNodeId" = r."ToNodeId"
                                 AND x."EdgeType" = r."EdgeType" AND x."IsTraversable" AND NOT x."IsHidden") AS source_matches,
                       EXISTS (SELECT 1 FROM "MapEdges" x
                               WHERE x."MapId" = r."MapId" AND x."FromNodeId" = r."FromNodeId"
                                 AND x."Direction" = r."ToDirection" AND x."ToNodeId" = r."ToNodeId"
                                 AND x."EdgeType" = r."EdgeType" AND x."IsTraversable" AND NOT x."IsHidden") AS destination_matches,
                       EXISTS (SELECT 1 FROM "MapEdges" x
                               WHERE x."MapId" = r."MapId" AND x."FromNodeId" = r."FromNodeId"
                                 AND x."Direction" = r."ToDirection") AS destination_occupied
                FROM _backfill_rehomes r
                JOIN _backfill_eligible e ON e."MapId" = r."MapId";

                SELECT string_agg(format('%s/%s %s->%s', s."MapId", s."FromNodeId", s."FromDirection", s."ToDirection"), ', ' ORDER BY s."MapId", s."FromNodeId") INTO v_conflicts
                FROM _backfill_rehome_state s
                WHERE (s.source_matches AND s.destination_matches)
                   OR (NOT s.source_matches AND NOT s.destination_matches)
                   OR (s.source_matches AND s.destination_occupied AND NOT s.destination_matches);
                IF v_conflicts IS NOT NULL THEN
                    RAISE EXCEPTION 'BackfillSeededMapDoorsAndArrivalHops: a seed-time walk cannot be re-homed as declared (duplicate, missing, or destination occupied): %. Refusing to guess which walk the door should replace.', v_conflicts;
                END IF;

                CREATE TEMP TABLE _backfill_moved ("MapId" text PRIMARY KEY) ON COMMIT DROP;
                SELECT count(*) INTO v_moved_edges
                FROM _backfill_rehome_state s
                WHERE s.source_matches AND NOT s.destination_occupied;

                WITH moved AS (
                    UPDATE "MapEdges" x
                       SET "Direction" = s."ToDirection"
                      FROM _backfill_rehome_state s
                     WHERE s.source_matches
                       AND NOT s.destination_occupied
                       AND x."MapId" = s."MapId"
                       AND x."FromNodeId" = s."FromNodeId"
                       AND x."Direction" = s."FromDirection"
                       AND x."ToNodeId" = s."ToNodeId"
                       AND x."EdgeType" = s."EdgeType"
                    RETURNING x."MapId"
                )
                INSERT INTO _backfill_moved ("MapId") SELECT DISTINCT "MapId" FROM moved;

                -- An existing node id must describe the same topology. Text, icon, layout, entry flag and sort order
                -- are the author's; they are not compared and never rewritten.
                SELECT string_agg(
                           format('%s (existing: map=%s type=%s ref=%s exit=%s targetMap=%s targetNode=%s hidden=%s unlockType=%s unlockKey=%s threshold=%s)',
                                  s."Id", n."MapId", n."NodeType", COALESCE(n."RefId", '<null>'), n."IsExitPoint",
                                  COALESCE(n."TargetMapId", '<null>'), COALESCE(n."TargetNodeId", '<null>'), n."IsHidden",
                                  COALESCE(n."UnlockType", '<null>'), COALESCE(n."UnlockKey", '<null>'),
                                  COALESCE(n."DiscoverThreshold"::text, '<null>')),
                           ', ' ORDER BY s."Id") INTO v_conflicts
                FROM _backfill_nodes s
                JOIN _backfill_eligible e ON e."MapId" = s."MapId"
                JOIN "MapNodes" n ON n."Id" = s."Id"
                WHERE n."MapId" IS DISTINCT FROM s."MapId"
                   OR n."NodeType" IS DISTINCT FROM s."NodeType"
                   OR n."RefId" IS DISTINCT FROM s."RefId"
                   OR n."IsExitPoint" IS DISTINCT FROM s."IsExitPoint"
                   OR n."TargetMapId" IS DISTINCT FROM s."TargetMapId"
                   OR n."TargetNodeId" IS DISTINCT FROM s."TargetNodeId"
                   OR n."IsHidden" IS DISTINCT FROM s."IsHidden"
                   OR n."UnlockType" IS DISTINCT FROM s."UnlockType"
                   OR n."UnlockKey" IS DISTINCT FROM s."UnlockKey"
                   OR n."DiscoverThreshold" IS DISTINCT FROM s."DiscoverThreshold";
                IF v_conflicts IS NOT NULL THEN
                    RAISE EXCEPTION 'BackfillSeededMapDoorsAndArrivalHops: existing node(s) with a backfill id carry different topology: %. Refusing to overwrite authoring.', v_conflicts;
                END IF;

                -- Every endpoint must exist already, or be one of the door nodes this migration inserts.
                SELECT string_agg(format('%s/%s -> %s (%s)', s."MapId", s."FromNodeId", s."ToNodeId", s."Direction"), ', ' ORDER BY s."MapId", s."FromNodeId", s."Direction") INTO v_conflicts
                FROM _backfill_edges s
                JOIN _backfill_eligible e ON e."MapId" = s."MapId"
                WHERE (NOT EXISTS (
                          SELECT 1 FROM "MapNodes" n
                          WHERE n."Id" = s."FromNodeId" AND n."MapId" = s."MapId")
                  AND NOT EXISTS (
                          SELECT 1 FROM _backfill_nodes sn
                          WHERE sn."Id" = s."FromNodeId" AND sn."MapId" = s."MapId"))
                   OR (NOT EXISTS (
                          SELECT 1 FROM "MapNodes" n
                          WHERE n."Id" = s."ToNodeId" AND n."MapId" = s."MapId")
                  AND NOT EXISTS (
                          SELECT 1 FROM _backfill_nodes sn
                          WHERE sn."Id" = s."ToNodeId" AND sn."MapId" = s."MapId"));
                IF v_conflicts IS NOT NULL THEN
                    RAISE EXCEPTION 'BackfillSeededMapDoorsAndArrivalHops: an endpoint is missing on an eligible map: %. Refusing to add a door onto a node that no longer exists.', v_conflicts;
                END IF;

                -- Every door must still lead somewhere live.
                SELECT string_agg(format('%s targets %s/%s', s."Id", COALESCE(s."TargetMapId", '<null>'), COALESCE(s."TargetNodeId", '<null>')), ', ' ORDER BY s."Id") INTO v_conflicts
                FROM _backfill_nodes s
                JOIN _backfill_eligible e ON e."MapId" = s."MapId"
                WHERE s."TargetMapId" IS NULL
                   OR NOT EXISTS (SELECT 1 FROM "Maps" tm WHERE tm."Id" = s."TargetMapId" AND tm."IsActive")
                   OR (s."TargetNodeId" IS NOT NULL AND NOT EXISTS (
                           SELECT 1 FROM "MapNodes" tn
                           WHERE tn."Id" = s."TargetNodeId" AND tn."MapId" = s."TargetMapId"));
                IF v_conflicts IS NOT NULL THEN
                    RAISE EXCEPTION 'BackfillSeededMapDoorsAndArrivalHops: a door target is missing or inactive: %. Refusing to add a door that leads nowhere.', v_conflicts;
                END IF;

                -- Each region board's marker must still point at the zone map the new Arrival hop is meant to reach.
                SELECT string_agg(format('%s/%s (TargetMapId=%s)', mk."MapId", mk."NodeId", COALESCE(n."TargetMapId", '<null>')), ', ' ORDER BY mk."MapId") INTO v_conflicts
                FROM _backfill_markers mk
                JOIN _backfill_eligible e ON e."MapId" = mk."MapId"
                LEFT JOIN "MapNodes" n ON n."Id" = mk."NodeId" AND n."MapId" = mk."MapId"
                WHERE n."Id" IS NULL
                   OR n."NodeType" <> 'Zone'
                   OR n."TargetMapId" IS DISTINCT FROM mk."ExpectedTargetMapId"
                   OR NOT EXISTS (SELECT 1 FROM "Maps" tm WHERE tm."Id" = mk."ExpectedTargetMapId" AND tm."IsActive");
                IF v_conflicts IS NOT NULL THEN
                    RAISE EXCEPTION 'BackfillSeededMapDoorsAndArrivalHops: region marker(s) no longer lead to the expected zone map: %. Refusing to add an Arrival hop that would strand the board.', v_conflicts;
                END IF;

                -- A frozen uuid may already exist, but only if it is the very edge this migration describes.
                SELECT string_agg(format('%s (existing slot %s/%s/%s -> %s)', s."Id"::text, x."MapId", x."FromNodeId", x."Direction", x."ToNodeId"), ', ' ORDER BY s."Id"::text) INTO v_conflicts
                FROM _backfill_edges s
                JOIN _backfill_eligible e ON e."MapId" = s."MapId"
                JOIN "MapEdges" x ON x."Id" = s."Id"
                WHERE x."MapId" IS DISTINCT FROM s."MapId"
                   OR x."FromNodeId" IS DISTINCT FROM s."FromNodeId"
                   OR x."Direction" IS DISTINCT FROM s."Direction"
                   OR x."ToNodeId" IS DISTINCT FROM s."ToNodeId"
                   OR x."EdgeType" IS DISTINCT FROM s."EdgeType"
                   OR x."IsTraversable" IS DISTINCT FROM s."IsTraversable"
                   OR x."IsHidden" IS DISTINCT FROM s."IsHidden";
                IF v_conflicts IS NOT NULL THEN
                    RAISE EXCEPTION 'BackfillSeededMapDoorsAndArrivalHops: a frozen edge uuid already exists as a different edge: %. Refusing to reuse another edge identity.', v_conflicts;
                END IF;

                -- The natural slot is the unique key. An edge already there satisfies the patch only when it connects
                -- the same nodes the same way; its own uuid and label are kept. Anything else is a conflict.
                SELECT string_agg(format('%s/%s/%s holds edge %s -> %s (%s, traversable=%s, hidden=%s)',
                                         x."MapId", x."FromNodeId", x."Direction", x."Id", x."ToNodeId", x."EdgeType",
                                         x."IsTraversable", x."IsHidden"), ', ' ORDER BY x."MapId", x."FromNodeId", x."Direction") INTO v_conflicts
                FROM _backfill_edges s
                JOIN _backfill_eligible e ON e."MapId" = s."MapId"
                JOIN "MapEdges" x
                  ON x."MapId" = s."MapId" AND x."FromNodeId" = s."FromNodeId" AND x."Direction" = s."Direction"
                WHERE x."Id" <> s."Id"
                  AND (x."ToNodeId" <> s."ToNodeId"
                    OR x."EdgeType" <> s."EdgeType"
                    OR x."IsTraversable" <> s."IsTraversable"
                    OR x."IsHidden" <> s."IsHidden");
                IF v_conflicts IS NOT NULL THEN
                    RAISE EXCEPTION 'BackfillSeededMapDoorsAndArrivalHops: an occupied direction holds a different edge: %. Refusing to overwrite it or pick another direction.', v_conflicts;
                END IF;

                -- Which eligible maps actually need a write, so each is version-bumped once and only once.
                CREATE TEMP TABLE _backfill_changed ("MapId" text PRIMARY KEY) ON COMMIT DROP;
                INSERT INTO _backfill_changed ("MapId") SELECT "MapId" FROM _backfill_moved
                ON CONFLICT ("MapId") DO NOTHING;
                INSERT INTO _backfill_changed ("MapId")
                SELECT DISTINCT s."MapId"
                FROM _backfill_nodes s
                JOIN _backfill_eligible e ON e."MapId" = s."MapId"
                WHERE NOT EXISTS (SELECT 1 FROM "MapNodes" n WHERE n."Id" = s."Id")
                ON CONFLICT ("MapId") DO NOTHING;
                INSERT INTO _backfill_changed ("MapId")
                SELECT DISTINCT s."MapId"
                FROM _backfill_edges s
                JOIN _backfill_eligible e ON e."MapId" = s."MapId"
                WHERE NOT EXISTS (SELECT 1 FROM "MapEdges" x WHERE x."Id" = s."Id")
                  AND NOT EXISTS (
                          SELECT 1 FROM "MapEdges" x
                          WHERE x."MapId" = s."MapId" AND x."FromNodeId" = s."FromNodeId" AND x."Direction" = s."Direction")
                ON CONFLICT ("MapId") DO NOTHING;

                -- Nodes first: edges reference them.
                INSERT INTO "MapNodes" (
                    "Id", "MapId", "NodeType", "RefId", "DisplayName", "Description", "IconKey", "LayoutX", "LayoutY",
                    "IsEntryPoint", "IsExitPoint", "TargetMapId", "TargetNodeId", "IsHidden", "UnlockType", "UnlockKey",
                    "DiscoverThreshold", "SortOrder")
                SELECT s."Id", s."MapId", s."NodeType", s."RefId", s."DisplayName", s."Description", s."IconKey",
                       s."LayoutX", s."LayoutY", s."IsEntryPoint", s."IsExitPoint", s."TargetMapId", s."TargetNodeId",
                       s."IsHidden", s."UnlockType", s."UnlockKey", s."DiscoverThreshold", s."SortOrder"
                FROM _backfill_nodes s
                JOIN _backfill_eligible e ON e."MapId" = s."MapId"
                WHERE NOT EXISTS (SELECT 1 FROM "MapNodes" n WHERE n."Id" = s."Id");
                GET DIAGNOSTICS v_inserted_nodes = ROW_COUNT;

                INSERT INTO "MapEdges" (
                    "Id", "MapId", "FromNodeId", "ToNodeId", "Direction", "EdgeType", "Label", "IsTraversable", "IsHidden")
                SELECT s."Id", s."MapId", s."FromNodeId", s."ToNodeId", s."Direction", s."EdgeType", s."Label",
                       s."IsTraversable", s."IsHidden"
                FROM _backfill_edges s
                JOIN _backfill_eligible e ON e."MapId" = s."MapId"
                WHERE NOT EXISTS (SELECT 1 FROM "MapEdges" x WHERE x."Id" = s."Id")
                  AND NOT EXISTS (
                          SELECT 1 FROM "MapEdges" x
                          WHERE x."MapId" = s."MapId" AND x."FromNodeId" = s."FromNodeId" AND x."Direction" = s."Direction");
                GET DIAGNOSTICS v_inserted_edges = ROW_COUNT;

                UPDATE "Maps" m
                   SET "Version" = m."Version" + 1,
                       "UpdatedAt" = now()
                 WHERE m."Id" IN (SELECT "MapId" FROM _backfill_changed);
                GET DIAGNOSTICS v_bumped_maps = ROW_COUNT;

                SELECT count(*) INTO v_missing
                FROM _backfill_nodes s
                JOIN _backfill_eligible e ON e."MapId" = s."MapId"
                WHERE NOT EXISTS (SELECT 1 FROM "MapNodes" n WHERE n."Id" = s."Id");
                IF v_missing > 0 THEN
                    RAISE EXCEPTION 'BackfillSeededMapDoorsAndArrivalHops: % node(s) are still missing after the insert; refusing to record a half-applied patch.', v_missing;
                END IF;

                SELECT count(*) INTO v_missing
                FROM _backfill_edges s
                JOIN _backfill_eligible e ON e."MapId" = s."MapId"
                WHERE NOT EXISTS (SELECT 1 FROM "MapEdges" x WHERE x."Id" = s."Id")
                  AND NOT EXISTS (
                          SELECT 1 FROM "MapEdges" x
                          WHERE x."MapId" = s."MapId" AND x."FromNodeId" = s."FromNodeId" AND x."Direction" = s."Direction");
                IF v_missing > 0 THEN
                    RAISE EXCEPTION 'BackfillSeededMapDoorsAndArrivalHops: % edge slot(s) are still missing after the insert; refusing to record a half-applied patch.', v_missing;
                END IF;

                RAISE NOTICE 'BackfillSeededMapDoorsAndArrivalHops: inserted % node(s) and % edge(s); re-homed % seed-time walk(s); %/% node(s) and %/% edge slot(s) already satisfied; bumped % map version(s).',
                    v_inserted_nodes, v_inserted_edges, v_moved_edges,
                    v_expected_nodes - v_inserted_nodes, v_expected_nodes,
                    v_expected_edges - v_inserted_edges, v_expected_edges,
                    v_bumped_maps;
            END
            $backfill$;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(PatchSql);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Intentionally empty: this backfill deletes no data. The added rows may have been edited in Foundry since,
        /// and an inserted door cannot be told apart from an authored one, so rolling data back means restoring the
        /// pre-migration backup or shipping a reviewed compensating migration (see the class remarks).
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
