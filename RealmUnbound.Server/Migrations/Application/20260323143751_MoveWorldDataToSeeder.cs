using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RealmUnbound.Server.Migrations.Application
{
    /// <inheritdoc />
    public partial class MoveWorldDataToSeeder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "RegionConnections",
                keyColumns: new[] { "FromRegionId", "ToRegionId" },
                keyValues: new object[] { "cinderplain", "greymoor" });

            migrationBuilder.DeleteData(
                table: "RegionConnections",
                keyColumns: new[] { "FromRegionId", "ToRegionId" },
                keyValues: new object[] { "greymoor", "cinderplain" });

            migrationBuilder.DeleteData(
                table: "RegionConnections",
                keyColumns: new[] { "FromRegionId", "ToRegionId" },
                keyValues: new object[] { "greymoor", "saltcliff" });

            migrationBuilder.DeleteData(
                table: "RegionConnections",
                keyColumns: new[] { "FromRegionId", "ToRegionId" },
                keyValues: new object[] { "greymoor", "thornveil" });

            migrationBuilder.DeleteData(
                table: "RegionConnections",
                keyColumns: new[] { "FromRegionId", "ToRegionId" },
                keyValues: new object[] { "saltcliff", "greymoor" });

            migrationBuilder.DeleteData(
                table: "RegionConnections",
                keyColumns: new[] { "FromRegionId", "ToRegionId" },
                keyValues: new object[] { "thornveil", "greymoor" });

            migrationBuilder.DeleteData(
                table: "ZoneConnections",
                keyColumns: new[] { "FromZoneId", "ToZoneId" },
                keyValues: new object[] { "aldenmere", "pale-moor" });

            migrationBuilder.DeleteData(
                table: "ZoneConnections",
                keyColumns: new[] { "FromZoneId", "ToZoneId" },
                keyValues: new object[] { "aldenmere", "thornveil-hollow" });

            migrationBuilder.DeleteData(
                table: "ZoneConnections",
                keyColumns: new[] { "FromZoneId", "ToZoneId" },
                keyValues: new object[] { "ashfields", "skarhold" });

            migrationBuilder.DeleteData(
                table: "ZoneConnections",
                keyColumns: new[] { "FromZoneId", "ToZoneId" },
                keyValues: new object[] { "ashfields", "smoldering-reach" });

            migrationBuilder.DeleteData(
                table: "ZoneConnections",
                keyColumns: new[] { "FromZoneId", "ToZoneId" },
                keyValues: new object[] { "barrow-deeps", "skarhold" });

            migrationBuilder.DeleteData(
                table: "ZoneConnections",
                keyColumns: new[] { "FromZoneId", "ToZoneId" },
                keyValues: new object[] { "barrow-deeps", "soddenfen" });

            migrationBuilder.DeleteData(
                table: "ZoneConnections",
                keyColumns: new[] { "FromZoneId", "ToZoneId" },
                keyValues: new object[] { "fenwick-crossing", "greenveil-paths" });

            migrationBuilder.DeleteData(
                table: "ZoneConnections",
                keyColumns: new[] { "FromZoneId", "ToZoneId" },
                keyValues: new object[] { "greenveil-paths", "fenwick-crossing" });

            migrationBuilder.DeleteData(
                table: "ZoneConnections",
                keyColumns: new[] { "FromZoneId", "ToZoneId" },
                keyValues: new object[] { "greenveil-paths", "thornveil-hollow" });

            migrationBuilder.DeleteData(
                table: "ZoneConnections",
                keyColumns: new[] { "FromZoneId", "ToZoneId" },
                keyValues: new object[] { "kaldrek-maw", "smoldering-reach" });

            migrationBuilder.DeleteData(
                table: "ZoneConnections",
                keyColumns: new[] { "FromZoneId", "ToZoneId" },
                keyValues: new object[] { "pale-moor", "aldenmere" });

            migrationBuilder.DeleteData(
                table: "ZoneConnections",
                keyColumns: new[] { "FromZoneId", "ToZoneId" },
                keyValues: new object[] { "pale-moor", "soddenfen" });

            migrationBuilder.DeleteData(
                table: "ZoneConnections",
                keyColumns: new[] { "FromZoneId", "ToZoneId" },
                keyValues: new object[] { "saltcliff-heights", "sunken-name" });

            migrationBuilder.DeleteData(
                table: "ZoneConnections",
                keyColumns: new[] { "FromZoneId", "ToZoneId" },
                keyValues: new object[] { "saltcliff-heights", "tidewrack-flats" });

            migrationBuilder.DeleteData(
                table: "ZoneConnections",
                keyColumns: new[] { "FromZoneId", "ToZoneId" },
                keyValues: new object[] { "skarhold", "ashfields" });

            migrationBuilder.DeleteData(
                table: "ZoneConnections",
                keyColumns: new[] { "FromZoneId", "ToZoneId" },
                keyValues: new object[] { "skarhold", "barrow-deeps" });

            migrationBuilder.DeleteData(
                table: "ZoneConnections",
                keyColumns: new[] { "FromZoneId", "ToZoneId" },
                keyValues: new object[] { "smoldering-reach", "ashfields" });

            migrationBuilder.DeleteData(
                table: "ZoneConnections",
                keyColumns: new[] { "FromZoneId", "ToZoneId" },
                keyValues: new object[] { "smoldering-reach", "kaldrek-maw" });

            migrationBuilder.DeleteData(
                table: "ZoneConnections",
                keyColumns: new[] { "FromZoneId", "ToZoneId" },
                keyValues: new object[] { "soddenfen", "barrow-deeps" });

            migrationBuilder.DeleteData(
                table: "ZoneConnections",
                keyColumns: new[] { "FromZoneId", "ToZoneId" },
                keyValues: new object[] { "soddenfen", "pale-moor" });

            migrationBuilder.DeleteData(
                table: "ZoneConnections",
                keyColumns: new[] { "FromZoneId", "ToZoneId" },
                keyValues: new object[] { "soddenfen", "tolvaren" });

            migrationBuilder.DeleteData(
                table: "ZoneConnections",
                keyColumns: new[] { "FromZoneId", "ToZoneId" },
                keyValues: new object[] { "sunken-name", "saltcliff-heights" });

            migrationBuilder.DeleteData(
                table: "ZoneConnections",
                keyColumns: new[] { "FromZoneId", "ToZoneId" },
                keyValues: new object[] { "thornveil-hollow", "aldenmere" });

            migrationBuilder.DeleteData(
                table: "ZoneConnections",
                keyColumns: new[] { "FromZoneId", "ToZoneId" },
                keyValues: new object[] { "thornveil-hollow", "greenveil-paths" });

            migrationBuilder.DeleteData(
                table: "ZoneConnections",
                keyColumns: new[] { "FromZoneId", "ToZoneId" },
                keyValues: new object[] { "thornveil-hollow", "verdant-barrow" });

            migrationBuilder.DeleteData(
                table: "ZoneConnections",
                keyColumns: new[] { "FromZoneId", "ToZoneId" },
                keyValues: new object[] { "tidewrack-flats", "saltcliff-heights" });

            migrationBuilder.DeleteData(
                table: "ZoneConnections",
                keyColumns: new[] { "FromZoneId", "ToZoneId" },
                keyValues: new object[] { "tidewrack-flats", "tolvaren" });

            migrationBuilder.DeleteData(
                table: "ZoneConnections",
                keyColumns: new[] { "FromZoneId", "ToZoneId" },
                keyValues: new object[] { "tolvaren", "soddenfen" });

            migrationBuilder.DeleteData(
                table: "ZoneConnections",
                keyColumns: new[] { "FromZoneId", "ToZoneId" },
                keyValues: new object[] { "tolvaren", "tidewrack-flats" });

            migrationBuilder.DeleteData(
                table: "ZoneConnections",
                keyColumns: new[] { "FromZoneId", "ToZoneId" },
                keyValues: new object[] { "verdant-barrow", "thornveil-hollow" });

            migrationBuilder.DeleteData(
                table: "Zones",
                keyColumn: "Id",
                keyValue: "aldenmere");

            migrationBuilder.DeleteData(
                table: "Zones",
                keyColumn: "Id",
                keyValue: "ashfields");

            migrationBuilder.DeleteData(
                table: "Zones",
                keyColumn: "Id",
                keyValue: "barrow-deeps");

            migrationBuilder.DeleteData(
                table: "Zones",
                keyColumn: "Id",
                keyValue: "fenwick-crossing");

            migrationBuilder.DeleteData(
                table: "Zones",
                keyColumn: "Id",
                keyValue: "greenveil-paths");

            migrationBuilder.DeleteData(
                table: "Zones",
                keyColumn: "Id",
                keyValue: "kaldrek-maw");

            migrationBuilder.DeleteData(
                table: "Zones",
                keyColumn: "Id",
                keyValue: "pale-moor");

            migrationBuilder.DeleteData(
                table: "Zones",
                keyColumn: "Id",
                keyValue: "saltcliff-heights");

            migrationBuilder.DeleteData(
                table: "Zones",
                keyColumn: "Id",
                keyValue: "skarhold");

            migrationBuilder.DeleteData(
                table: "Zones",
                keyColumn: "Id",
                keyValue: "smoldering-reach");

            migrationBuilder.DeleteData(
                table: "Zones",
                keyColumn: "Id",
                keyValue: "soddenfen");

            migrationBuilder.DeleteData(
                table: "Zones",
                keyColumn: "Id",
                keyValue: "sunken-name");

            migrationBuilder.DeleteData(
                table: "Zones",
                keyColumn: "Id",
                keyValue: "thornveil-hollow");

            migrationBuilder.DeleteData(
                table: "Zones",
                keyColumn: "Id",
                keyValue: "tidewrack-flats");

            migrationBuilder.DeleteData(
                table: "Zones",
                keyColumn: "Id",
                keyValue: "tolvaren");

            migrationBuilder.DeleteData(
                table: "Zones",
                keyColumn: "Id",
                keyValue: "verdant-barrow");

            migrationBuilder.DeleteData(
                table: "Regions",
                keyColumn: "Id",
                keyValue: "cinderplain");

            migrationBuilder.DeleteData(
                table: "Regions",
                keyColumn: "Id",
                keyValue: "greymoor");

            migrationBuilder.DeleteData(
                table: "Regions",
                keyColumn: "Id",
                keyValue: "saltcliff");

            migrationBuilder.DeleteData(
                table: "Regions",
                keyColumn: "Id",
                keyValue: "thornveil");

            migrationBuilder.DeleteData(
                table: "Worlds",
                keyColumn: "Id",
                keyValue: "draveth");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Worlds",
                columns: new[] { "Id", "Description", "Era", "Name" },
                values: new object[] { "draveth", "A world of scattered kingdoms, ancient ruins, and contested wilds, shaped by centuries of war and forgotten magic.", "The Age of Embers", "Draveth" });

            migrationBuilder.InsertData(
                table: "Regions",
                columns: new[] { "Id", "Description", "IsDiscoverable", "IsStarter", "MaxLevel", "MinLevel", "Name", "Type", "WorldId" },
                values: new object[,]
                {
                    { "cinderplain", "Kaldrek's scorched expanse of ash and cooled lava, where forge-tempered warbands and fire-touched creatures stake their claim.", true, false, 30, 18, "Cinderplain", "Volcanic", "draveth" },
                    { "greymoor", "Fog-wreathed Dravan highlands where ancient burial mounds dot the heather and wandering spirits trouble the living.", true, false, 14, 5, "Greymoor", "Highland", "draveth" },
                    { "saltcliff", "Thysmara sea cliffs battered by storm winds, home to sailors, smugglers, and the drowned remnants of a sunken empire.", true, false, 20, 10, "Saltcliff", "Coastal", "draveth" },
                    { "thornveil", "A dense Eiraveth forest shrouding ruins of the first kingdoms, where moss-covered paths lead beginners into adventure.", true, true, 6, 0, "Thornveil", "Forest", "draveth" }
                });

            migrationBuilder.InsertData(
                table: "RegionConnections",
                columns: new[] { "FromRegionId", "ToRegionId" },
                values: new object[,]
                {
                    { "cinderplain", "greymoor" },
                    { "greymoor", "cinderplain" },
                    { "greymoor", "saltcliff" },
                    { "greymoor", "thornveil" },
                    { "saltcliff", "greymoor" },
                    { "thornveil", "greymoor" }
                });

            migrationBuilder.InsertData(
                table: "Zones",
                columns: new[] { "Id", "Description", "HasInn", "HasMerchant", "IsDiscoverable", "IsPvpEnabled", "IsStarter", "MaxPlayers", "MinLevel", "Name", "RegionId", "Type" },
                values: new object[,]
                {
                    { "aldenmere", "A grey-stoned Dravan waytown built atop old foundations, known for its mead, its mercenaries, and its many secrets.", true, true, true, false, false, 0, 5, "Aldenmere", "greymoor", "Town" },
                    { "ashfields", "Grey ash plains stretching to the horizon, dotted with fused obsidian trees and the scorched skulls of fallen armies.", false, false, true, false, false, 0, 20, "The Ashfields", "cinderplain", "Wilderness" },
                    { "barrow-deeps", "A sprawling underground burial network carved through the moorland, where ancestor-beasts guard ancient Dravan relics.", false, false, true, false, false, 0, 11, "The Barrow Deeps", "greymoor", "Dungeon" },
                    { "fenwick-crossing", "A well-worn crossroads inn and market at the forest's edge, where new adventurers take their first uncertain steps.", true, true, true, false, true, 0, 0, "Fenwick's Crossing", "thornveil", "Town" },
                    { "greenveil-paths", "Winding root-bound trails beneath a canopy of ancient oaks, alive with sprites and overly curious wildlife.", false, false, true, false, false, 0, 1, "The Greenveil Paths", "thornveil", "Wilderness" },
                    { "kaldrek-maw", "A vast volcanic vent complex known as the Maw, lair of Kaldrek's fire-bound ancient and final test of the worthy.", false, false, true, false, false, 0, 26, "Kaldrek's Maw", "cinderplain", "Dungeon" },
                    { "pale-moor", "Endless fog-blanketed moors where waymarks shift overnight and travellers learn quickly to trust local guides.", false, false, true, false, false, 0, 7, "The Pale Moor", "greymoor", "Wilderness" },
                    { "saltcliff-heights", "Wind-blasted clifftops above the Thysmara sea, dotted with lighthouse ruins and contested by rival gull-rider clans.", false, false, true, false, false, 0, 14, "The Saltcliff Heights", "saltcliff", "Wilderness" },
                    { "skarhold", "A Kaldrek forge-city built inside a dormant caldera, where the best smiths in Draveth ply their trade under sulphur skies.", true, true, true, false, false, 0, 18, "Skarhold", "cinderplain", "Town" },
                    { "smoldering-reach", "A cracked lava field where vents of superheated gas erupt without warning and heat-adapted predators hunt at dusk.", false, false, true, false, false, 0, 23, "The Smoldering Reach", "cinderplain", "Wilderness" },
                    { "soddenfen", "A waterlogged fenland buzzing with plague insects and half-submerged ruins, avoided by all with good sense.", false, false, true, false, false, 0, 9, "Soddenfen", "greymoor", "Wilderness" },
                    { "sunken-name", "The drowned heart of a once-great Thysmara empire, now a flooded ruin accessible only to the brave and the waterproofed.", false, false, true, false, false, 0, 16, "The Sunken Name", "saltcliff", "Dungeon" },
                    { "thornveil-hollow", "A shaded hollow deep in the forest where old Eiraveth wards have begun to fail and darker things stir.", false, false, true, false, false, 0, 3, "Thornveil Hollow", "thornveil", "Wilderness" },
                    { "tidewrack-flats", "Tide-scoured mudflats littered with shipwreck timber and kelp-draped bones, hunted by scavengers and wading predators.", false, false, true, false, false, 0, 12, "The Tidewrack Flats", "saltcliff", "Wilderness" },
                    { "tolvaren", "A salt-caked Thysmara port city clinging to the cliff face, its harbour full of storm-worn ships and bold-faced traders.", true, true, true, false, false, 0, 10, "Tolvaren", "saltcliff", "Town" },
                    { "verdant-barrow", "A barrow complex reclaimed by roots and bioluminescent fungi, prowled by the restless dead of a forgotten village.", false, false, true, false, false, 0, 4, "The Verdant Barrow", "thornveil", "Dungeon" }
                });

            migrationBuilder.InsertData(
                table: "ZoneConnections",
                columns: new[] { "FromZoneId", "ToZoneId" },
                values: new object[,]
                {
                    { "aldenmere", "pale-moor" },
                    { "aldenmere", "thornveil-hollow" },
                    { "ashfields", "skarhold" },
                    { "ashfields", "smoldering-reach" },
                    { "barrow-deeps", "skarhold" },
                    { "barrow-deeps", "soddenfen" },
                    { "fenwick-crossing", "greenveil-paths" },
                    { "greenveil-paths", "fenwick-crossing" },
                    { "greenveil-paths", "thornveil-hollow" },
                    { "kaldrek-maw", "smoldering-reach" },
                    { "pale-moor", "aldenmere" },
                    { "pale-moor", "soddenfen" },
                    { "saltcliff-heights", "sunken-name" },
                    { "saltcliff-heights", "tidewrack-flats" },
                    { "skarhold", "ashfields" },
                    { "skarhold", "barrow-deeps" },
                    { "smoldering-reach", "ashfields" },
                    { "smoldering-reach", "kaldrek-maw" },
                    { "soddenfen", "barrow-deeps" },
                    { "soddenfen", "pale-moor" },
                    { "soddenfen", "tolvaren" },
                    { "sunken-name", "saltcliff-heights" },
                    { "thornveil-hollow", "aldenmere" },
                    { "thornveil-hollow", "greenveil-paths" },
                    { "thornveil-hollow", "verdant-barrow" },
                    { "tidewrack-flats", "saltcliff-heights" },
                    { "tidewrack-flats", "tolvaren" },
                    { "tolvaren", "soddenfen" },
                    { "tolvaren", "tidewrack-flats" },
                    { "verdant-barrow", "thornveil-hollow" }
                });
        }
    }
}
