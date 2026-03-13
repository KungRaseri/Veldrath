using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealmUnbound.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddContentItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContentItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Domain = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    TypeKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Slug = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    RarityWeight = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    Data = table.Column<string>(type: "jsonb", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentItems_Domain",
                table: "ContentItems",
                column: "Domain");

            migrationBuilder.CreateIndex(
                name: "IX_ContentItems_Domain_TypeKey_Slug",
                table: "ContentItems",
                columns: new[] { "Domain", "TypeKey", "Slug" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContentItems");
        }
    }
}
