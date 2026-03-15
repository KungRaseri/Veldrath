using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealmUnbound.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddFoundryEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FoundrySubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmitterId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ReviewNotes = table.Column<string>(type: "text", nullable: true),
                    ReviewerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FoundrySubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FoundrySubmissions_AspNetUsers_ReviewerId",
                        column: x => x.ReviewerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_FoundrySubmissions_AspNetUsers_SubmitterId",
                        column: x => x.SubmitterId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FoundryVotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    VoterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FoundryVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FoundryVotes_AspNetUsers_VoterId",
                        column: x => x.VoterId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FoundryVotes_FoundrySubmissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "FoundrySubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FoundrySubmissions_ContentType",
                table: "FoundrySubmissions",
                column: "ContentType");

            migrationBuilder.CreateIndex(
                name: "IX_FoundrySubmissions_CreatedAt",
                table: "FoundrySubmissions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_FoundrySubmissions_ReviewerId",
                table: "FoundrySubmissions",
                column: "ReviewerId");

            migrationBuilder.CreateIndex(
                name: "IX_FoundrySubmissions_Status",
                table: "FoundrySubmissions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_FoundrySubmissions_SubmitterId",
                table: "FoundrySubmissions",
                column: "SubmitterId");

            migrationBuilder.CreateIndex(
                name: "IX_FoundryVotes_SubmissionId_VoterId",
                table: "FoundryVotes",
                columns: new[] { "SubmissionId", "VoterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FoundryVotes_VoterId",
                table: "FoundryVotes",
                column: "VoterId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FoundryVotes");

            migrationBuilder.DropTable(
                name: "FoundrySubmissions");
        }
    }
}
