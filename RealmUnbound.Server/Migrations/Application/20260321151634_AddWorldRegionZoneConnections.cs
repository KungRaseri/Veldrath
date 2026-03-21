using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RealmUnbound.Server.Migrations.Application
{
    /// <inheritdoc />
    public partial class AddWorldRegionZoneConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Zones",
                keyColumn: "Id",
                keyValue: "dungeon-grotto");

            migrationBuilder.DeleteData(
                table: "Zones",
                keyColumn: "Id",
                keyValue: "starting-zone");

            migrationBuilder.DeleteData(
                table: "Zones",
                keyColumn: "Id",
                keyValue: "town-ironhold");

            migrationBuilder.DeleteData(
                table: "Zones",
                keyColumn: "Id",
                keyValue: "town-millhaven");

            migrationBuilder.DeleteData(
                table: "Zones",
                keyColumn: "Id",
                keyValue: "wild-ashenveil");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Zones",
                type: "text",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<bool>(
                name: "HasInn",
                table: "Zones",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasMerchant",
                table: "Zones",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDiscoverable",
                table: "Zones",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPvpEnabled",
                table: "Zones",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RegionId",
                table: "Zones",
                type: "character varying(64)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Worlds",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Era = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Worlds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ZoneConnections",
                columns: table => new
                {
                    FromZoneId = table.Column<string>(type: "character varying(64)", nullable: false),
                    ToZoneId = table.Column<string>(type: "character varying(64)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZoneConnections", x => new { x.FromZoneId, x.ToZoneId });
                    table.ForeignKey(
                        name: "FK_ZoneConnections_Zones_FromZoneId",
                        column: x => x.FromZoneId,
                        principalTable: "Zones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ZoneConnections_Zones_ToZoneId",
                        column: x => x.ToZoneId,
                        principalTable: "Zones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Regions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    MinLevel = table.Column<int>(type: "integer", nullable: false),
                    MaxLevel = table.Column<int>(type: "integer", nullable: false),
                    IsStarter = table.Column<bool>(type: "boolean", nullable: false),
                    IsDiscoverable = table.Column<bool>(type: "boolean", nullable: false),
                    WorldId = table.Column<string>(type: "character varying(64)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Regions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Regions_Worlds_WorldId",
                        column: x => x.WorldId,
                        principalTable: "Worlds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RegionConnections",
                columns: table => new
                {
                    FromRegionId = table.Column<string>(type: "character varying(64)", nullable: false),
                    ToRegionId = table.Column<string>(type: "character varying(64)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegionConnections", x => new { x.FromRegionId, x.ToRegionId });
                    table.ForeignKey(
                        name: "FK_RegionConnections_Regions_FromRegionId",
                        column: x => x.FromRegionId,
                        principalTable: "Regions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RegionConnections_Regions_ToRegionId",
                        column: x => x.ToRegionId,
                        principalTable: "Regions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_Zones_RegionId",
                table: "Zones",
                column: "RegionId");

            migrationBuilder.CreateIndex(
                name: "IX_RegionConnections_ToRegionId",
                table: "RegionConnections",
                column: "ToRegionId");

            migrationBuilder.CreateIndex(
                name: "IX_Regions_WorldId",
                table: "Regions",
                column: "WorldId");

            migrationBuilder.CreateIndex(
                name: "IX_ZoneConnections_ToZoneId",
                table: "ZoneConnections",
                column: "ToZoneId");

            migrationBuilder.AddForeignKey(
                name: "FK_Zones_Regions_RegionId",
                table: "Zones",
                column: "RegionId",
                principalTable: "Regions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Zones_Regions_RegionId",
                table: "Zones");

            migrationBuilder.DropTable(
                name: "RegionConnections");

            migrationBuilder.DropTable(
                name: "ZoneConnections");

            migrationBuilder.DropTable(
                name: "Regions");

            migrationBuilder.DropTable(
                name: "Worlds");

            migrationBuilder.DropIndex(
                name: "IX_Zones_RegionId",
                table: "Zones");

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

            migrationBuilder.DropColumn(
                name: "HasInn",
                table: "Zones");

            migrationBuilder.DropColumn(
                name: "HasMerchant",
                table: "Zones");

            migrationBuilder.DropColumn(
                name: "IsDiscoverable",
                table: "Zones");

            migrationBuilder.DropColumn(
                name: "IsPvpEnabled",
                table: "Zones");

            migrationBuilder.DropColumn(
                name: "RegionId",
                table: "Zones");

            migrationBuilder.AlterColumn<int>(
                name: "Type",
                table: "Zones",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.InsertData(
                table: "Zones",
                columns: new[] { "Id", "Description", "IsStarter", "MaxPlayers", "MinLevel", "Name", "Type" },
                values: new object[,]
                {
                    { "dungeon-grotto", "A shallow cave network overrun with kobolds and giant insects — ideal for beginners.", false, 0, 1, "Mossglow Grotto", 2 },
                    { "starting-zone", "A small crossroads at the edge of the Ashenveil Forest, where new adventurers gather.", true, 0, 0, "Ashenveil Crossroads", 0 },
                    { "town-ironhold", "A fortified dwarven outpost in the foothills, renowned for its smiths and ales.", false, 0, 5, "Ironhold", 1 },
                    { "town-millhaven", "A prosperous market town built along the Silver River, hub of trade and gossip.", false, 0, 0, "Millhaven", 1 },
                    { "wild-ashenveil", "Dense woodland alive with wolves, bandits, and rumours of something darker within.", false, 0, 3, "Ashenveil Forest", 3 }
                });
        }
    }
}
