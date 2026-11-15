using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealmEngine.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMapReviewState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ApprovedAt",
                table: "Maps",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedByAccountId",
                table: "Maps",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovedVersion",
                table: "Maps",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByAccountId",
                table: "Maps",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SubmittedAt",
                table: "Maps",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubmittedByAccountId",
                table: "Maps",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "Maps");

            migrationBuilder.DropColumn(
                name: "ApprovedByAccountId",
                table: "Maps");

            migrationBuilder.DropColumn(
                name: "ApprovedVersion",
                table: "Maps");

            migrationBuilder.DropColumn(
                name: "CreatedByAccountId",
                table: "Maps");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "Maps");

            migrationBuilder.DropColumn(
                name: "SubmittedByAccountId",
                table: "Maps");
        }
    }
}
