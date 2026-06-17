using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scrobblint.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddScrobbleCompositeIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Scrobbles_UserId_Artist",
                table: "Scrobbles");

            migrationBuilder.CreateIndex(
                name: "IX_Scrobbles_UserId_Artist_Album",
                table: "Scrobbles",
                columns: new[] { "UserId", "Artist", "Album" });

            migrationBuilder.CreateIndex(
                name: "IX_Scrobbles_UserId_Artist_Track",
                table: "Scrobbles",
                columns: new[] { "UserId", "Artist", "Track" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Scrobbles_UserId_Artist_Album",
                table: "Scrobbles");

            migrationBuilder.DropIndex(
                name: "IX_Scrobbles_UserId_Artist_Track",
                table: "Scrobbles");

            migrationBuilder.CreateIndex(
                name: "IX_Scrobbles_UserId_Artist",
                table: "Scrobbles",
                columns: new[] { "UserId", "Artist" });
        }
    }
}
