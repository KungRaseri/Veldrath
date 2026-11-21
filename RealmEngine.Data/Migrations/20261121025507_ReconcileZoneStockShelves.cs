using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealmEngine.Data.Migrations
{
    /// <summary>
    /// Removes the two authored shelf rows that did not respect the approved L0/L10 purse bands, so a content
    /// database seeded before the fix converges on the same shelf a fresh database gets.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>ZoneStockSeeder</c> is insert-only by contract, so it never rewrites a row it already wrote: a database
    /// seeded by <c>AddZoneStockItems</c>' release keeps <c>crestfall/hunters-bow</c> (35 gold, over the 30-gold
    /// ceiling the L0 band is sized from) and <c>tolvaren/orb-of-force</c> (300 gold, which prices the L10 shelf at
    /// 1650 against an approved 1600 purse). Deleting them here lets the next start insert <c>tolvaren/fire-ruby</c>
    /// in the freed slot, which is what the approved table expects.
    /// </para>
    /// <para>
    /// Both statements are filtered deletes, so this is a no-op on a database that was created after the fix.
    /// </para>
    /// </remarks>
    public partial class ReconcileZoneStockShelves : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM "ZoneStockItems" WHERE "ZoneId" = 'crestfall' AND "ItemSlug" = 'hunters-bow';
                DELETE FROM "ZoneStockItems" WHERE "ZoneId" = 'tolvaren' AND "ItemSlug" = 'orb-of-force';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restores exactly the rows Up removed, at the sort position the seeder had given them (both were the
            // tenth row of their list). The identifiers have to be supplied because the column has no default and
            // the original values were generated in the application.
            migrationBuilder.Sql(
                """
                INSERT INTO "ZoneStockItems" ("Id", "ZoneId", "ItemSlug", "SortOrder")
                VALUES ('a1b2c3d4-0000-4000-8000-000000000001', 'crestfall', 'hunters-bow', 100)
                ON CONFLICT ("ZoneId", "ItemSlug") DO NOTHING;
                INSERT INTO "ZoneStockItems" ("Id", "ZoneId", "ItemSlug", "SortOrder")
                VALUES ('a1b2c3d4-0000-4000-8000-000000000002', 'tolvaren', 'orb-of-force', 100)
                ON CONFLICT ("ZoneId", "ItemSlug") DO NOTHING;
                """);
        }
    }
}
