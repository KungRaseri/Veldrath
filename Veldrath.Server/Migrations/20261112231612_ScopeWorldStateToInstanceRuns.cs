using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Veldrath.Server.Migrations
{
    /// <inheritdoc />
    public partial class ScopeWorldStateToInstanceRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ZoneInstanceId",
                table: "GroundItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ZoneInstanceId",
                table: "EnemyInstances",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroundItems_ZoneInstanceId_LocationSlug",
                table: "GroundItems",
                columns: new[] { "ZoneInstanceId", "LocationSlug" });

            migrationBuilder.CreateIndex(
                name: "IX_EnemyInstances_ZoneInstanceId_LocationSlug",
                table: "EnemyInstances",
                columns: new[] { "ZoneInstanceId", "LocationSlug" });

            migrationBuilder.AddForeignKey(
                name: "FK_EnemyInstances_ZoneInstances_ZoneInstanceId",
                table: "EnemyInstances",
                column: "ZoneInstanceId",
                principalTable: "ZoneInstances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GroundItems_ZoneInstances_ZoneInstanceId",
                table: "GroundItems",
                column: "ZoneInstanceId",
                principalTable: "ZoneInstances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EnemyInstances_ZoneInstances_ZoneInstanceId",
                table: "EnemyInstances");

            migrationBuilder.DropForeignKey(
                name: "FK_GroundItems_ZoneInstances_ZoneInstanceId",
                table: "GroundItems");

            migrationBuilder.DropIndex(
                name: "IX_GroundItems_ZoneInstanceId_LocationSlug",
                table: "GroundItems");

            migrationBuilder.DropIndex(
                name: "IX_EnemyInstances_ZoneInstanceId_LocationSlug",
                table: "EnemyInstances");

            migrationBuilder.DropColumn(
                name: "ZoneInstanceId",
                table: "GroundItems");

            migrationBuilder.DropColumn(
                name: "ZoneInstanceId",
                table: "EnemyInstances");
        }
    }
}
