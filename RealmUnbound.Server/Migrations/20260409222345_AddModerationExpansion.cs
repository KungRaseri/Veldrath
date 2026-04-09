using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealmUnbound.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddModerationExpansion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsMuted",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MuteReason",
                table: "AspNetUsers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MutedUntil",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WarnCount",
                table: "AspNetUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AdminAuditEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUsername = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    TargetAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetUsername = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Details = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminAuditEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlayerReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReporterCharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReporterName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TargetCharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolvedByAccountId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerReports", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditEntries_ActorAccountId",
                table: "AdminAuditEntries",
                column: "ActorAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditEntries_OccurredAt",
                table: "AdminAuditEntries",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditEntries_TargetAccountId",
                table: "AdminAuditEntries",
                column: "TargetAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerReports_IsResolved",
                table: "PlayerReports",
                column: "IsResolved");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerReports_SubmittedAt",
                table: "PlayerReports",
                column: "SubmittedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerReports_TargetCharacterId",
                table: "PlayerReports",
                column: "TargetCharacterId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminAuditEntries");

            migrationBuilder.DropTable(
                name: "PlayerReports");

            migrationBuilder.DropColumn(
                name: "IsMuted",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "MuteReason",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "MutedUntil",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "WarnCount",
                table: "AspNetUsers");
        }
    }
}
