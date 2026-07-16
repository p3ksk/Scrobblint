using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scrobblint.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddScrobbleIgnoreRegex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AlbumIgnoreRegex",
                table: "UserSettings",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArtistIgnoreRegex",
                table: "UserSettings",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrackIgnoreRegex",
                table: "UserSettings",
                type: "TEXT",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AlbumIgnoreRegex",
                table: "UserSettings");

            migrationBuilder.DropColumn(
                name: "ArtistIgnoreRegex",
                table: "UserSettings");

            migrationBuilder.DropColumn(
                name: "TrackIgnoreRegex",
                table: "UserSettings");
        }
    }
}
