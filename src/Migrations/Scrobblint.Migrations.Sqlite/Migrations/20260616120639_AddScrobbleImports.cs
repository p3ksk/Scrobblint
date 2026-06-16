using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scrobblint.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddScrobbleImports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScrobbleImports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Provider = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceAccount = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ToTimestamp = table.Column<long>(type: "INTEGER", nullable: false),
                    NextPage = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalPages = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalAvailable = table.Column<int>(type: "INTEGER", nullable: false),
                    ImportedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    DuplicateCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Error = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScrobbleImports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScrobbleImports_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScrobbleImports_UserId_Status",
                table: "ScrobbleImports",
                columns: new[] { "UserId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScrobbleImports");
        }
    }
}
