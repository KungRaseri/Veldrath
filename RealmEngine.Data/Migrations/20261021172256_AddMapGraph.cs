using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealmEngine.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMapGraph : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Maps",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    MapType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    EntryNodeId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Maps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MapNodes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MapId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    NodeType = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    RefId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    IconKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LayoutX = table.Column<int>(type: "integer", nullable: true),
                    LayoutY = table.Column<int>(type: "integer", nullable: true),
                    IsEntryPoint = table.Column<bool>(type: "boolean", nullable: false),
                    IsExitPoint = table.Column<bool>(type: "boolean", nullable: false),
                    TargetMapId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    TargetNodeId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsHidden = table.Column<bool>(type: "boolean", nullable: false),
                    UnlockType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UnlockKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    DiscoverThreshold = table.Column<int>(type: "integer", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MapNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MapNodes_Maps_MapId",
                        column: x => x.MapId,
                        principalTable: "Maps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MapEdges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MapId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    FromNodeId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ToNodeId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Direction = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    EdgeType = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsTraversable = table.Column<bool>(type: "boolean", nullable: false),
                    IsHidden = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MapEdges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MapEdges_MapNodes_FromNodeId",
                        column: x => x.FromNodeId,
                        principalTable: "MapNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MapEdges_MapNodes_ToNodeId",
                        column: x => x.ToNodeId,
                        principalTable: "MapNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MapEdges_Maps_MapId",
                        column: x => x.MapId,
                        principalTable: "Maps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MapEdges_FromNodeId",
                table: "MapEdges",
                column: "FromNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_MapEdges_MapId_FromNodeId_Direction",
                table: "MapEdges",
                columns: new[] { "MapId", "FromNodeId", "Direction" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MapEdges_MapId_ToNodeId",
                table: "MapEdges",
                columns: new[] { "MapId", "ToNodeId" });

            migrationBuilder.CreateIndex(
                name: "IX_MapEdges_ToNodeId",
                table: "MapEdges",
                column: "ToNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_MapNodes_MapId_NodeType",
                table: "MapNodes",
                columns: new[] { "MapId", "NodeType" });

            migrationBuilder.CreateIndex(
                name: "IX_Maps_MapType_OwnerId",
                table: "Maps",
                columns: new[] { "MapType", "OwnerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MapEdges");

            migrationBuilder.DropTable(
                name: "MapNodes");

            migrationBuilder.DropTable(
                name: "Maps");
        }
    }
}
