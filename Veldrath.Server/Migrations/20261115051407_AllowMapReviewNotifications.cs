using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Veldrath.Server.Migrations
{
    /// <inheritdoc />
    public partial class AllowMapReviewNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "SubmissionId",
                table: "FoundryNotifications",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "FoundryNotifications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MapId",
                table: "FoundryNotifications",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "FoundryNotifications",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Kind",
                table: "FoundryNotifications");

            migrationBuilder.DropColumn(
                name: "MapId",
                table: "FoundryNotifications");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "FoundryNotifications");

            migrationBuilder.AlterColumn<Guid>(
                name: "SubmissionId",
                table: "FoundryNotifications",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
